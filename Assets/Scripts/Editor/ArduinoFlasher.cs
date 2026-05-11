using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Tools → Arduino → Flash Level N
/// Kompiliert und uploaded den passenden .ino-Sketch via arduino-cli.
///
/// Auto-Flash:
///   • Beim Öffnen einer Level-Szene  → asynchron im Hintergrund flashen.
///   • Beim Play-Drücken              → synchron flashen + Play bei Fehler abbrechen.
///   • Nur wenn .ino seit letztem Flash geändert wurde (mtime-Cache).
/// </summary>
[InitializeOnLoad]
public static class ArduinoFlasher
{
    private const string PrefPort         = "ArduinoFlasher.Port";
    private const string PrefFqbn         = "ArduinoFlasher.Fqbn";
    private const string PrefCli          = "ArduinoFlasher.CliPath";
    private const string PrefAutoFlash    = "ArduinoFlasher.AutoFlash";
    private const string PrefLastFlashFmt = "ArduinoFlasher.LastFlash.{0}";
    private const string PrefCurrentSketch = "ArduinoFlasher.CurrentSketch"; // welcher Sketch liegt aktuell auf dem Board

    private const string DefaultPort = "COM3";
    private const string DefaultFqbn = "arduino:renesas_uno:unor4wifi";
    private const string DefaultCli  = @"C:\Program Files\Arduino CLI\arduino-cli.exe";

    private static string Port      => EditorPrefs.GetString(PrefPort, DefaultPort);
    private static string Fqbn      => EditorPrefs.GetString(PrefFqbn, DefaultFqbn);
    private static string Cli       => EditorPrefs.GetString(PrefCli,  DefaultCli);
    private static bool   AutoFlash => EditorPrefs.GetBool(PrefAutoFlash, true);

    // Szenenname → Sketch-Ordner (relativ zu <project>/Arduino)
    private static readonly Dictionary<string, string> SceneToSketch = new()
    {
        { "Level1", "Level1_Keypad" },
        { "Level2", "Level2_Humidity" },
    };

    // Async-Flash-Zustand
    private static Process       _bgProc;
    private static string        _bgSketch;
    private static string        _bgPrefKey;
    private static long          _bgMtime;
    private static System.Text.StringBuilder _bgStdout;
    private static System.Text.StringBuilder _bgStderr;

    static ArduinoFlasher()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneOpened         += OnSceneOpened;
        SceneManager.sceneLoaded               += OnRuntimeSceneLoaded;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Runtime: Szenenwechsel im Play-Mode → COM-Port freigeben → flash → reconnect
    // ─────────────────────────────────────────────────────────────────────────

    private static void OnRuntimeSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying) return;
        if (!AutoFlash) return;
        if (!SceneToSketch.TryGetValue(scene.name, out string sketchFolder)) return;

        // Skip wenn der korrekte Sketch schon auf dem Board ist UND .ino unverändert
        string current = EditorPrefs.GetString(PrefCurrentSketch, "");
        bool needsFlash = NeedsFlash(sketchFolder, out long inoMtime, out string prefKey, out _);
        if (current == sketchFolder && !needsFlash)
        {
            Debug.Log($"[ArduinoFlasher] {sketchFolder} ist bereits auf dem Board → kein Flash.");
            return;
        }

        Debug.Log($"[ArduinoFlasher] Runtime-Szenenwechsel → '{scene.name}' → flashe {sketchFolder}.");

        var bridge = ArduinoBridge.Instance;
        if (bridge != null) bridge.Disconnect();

        // Synchroner Flash mit Pause + Progress-Bar (Spielzeit eingefroren)
        float prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        bool ok = FlashSync(sketchFolder, withProgress: true);

        Time.timeScale = prevTimeScale;

        if (ok)
        {
            EditorPrefs.SetString(PrefCurrentSketch, sketchFolder);
            EditorPrefs.SetString(prefKey, inoMtime.ToString());
        }

        // UNO R4 braucht ~1 Sek nach Reset bevor der Port wieder bereit ist
        if (bridge != null)
        {
            EditorApplication.delayCall += () =>
            {
                if (bridge != null) bridge.Reconnect();
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Menüs
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Arduino/Flash Level 1 (Keypad)", priority = 100)]
    public static void FlashLevel1() => FlashSync("Level1_Keypad");

    [MenuItem("Tools/Arduino/Flash Level 2 (Humidity)", priority = 101)]
    public static void FlashLevel2() => FlashSync("Level2_Humidity");

    [MenuItem("Tools/Arduino/Settings…", priority = 200)]
    public static void OpenSettings() => ArduinoFlasherSettings.ShowWindow();

    // ─────────────────────────────────────────────────────────────────────────
    // Trigger: Szene geöffnet → Hintergrund-Flash
    // ─────────────────────────────────────────────────────────────────────────

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!AutoFlash) return;
        if (mode != OpenSceneMode.Single) return;
        if (!SceneToSketch.TryGetValue(scene.name, out string sketchFolder)) return;

        if (!NeedsFlash(sketchFolder, out long inoMtime, out string prefKey, out string inoPath))
        {
            Debug.Log($"[ArduinoFlasher] {sketchFolder}.ino unverändert → kein Auto-Flash.");
            return;
        }

        Debug.Log($"[ArduinoFlasher] Szene '{scene.name}' geöffnet → Hintergrund-Flash {sketchFolder}.");
        StartBackgroundFlash(sketchFolder, prefKey, inoMtime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Trigger: Play-Drücken → synchroner Flash (Abbruch bei Fehler)
    // ─────────────────────────────────────────────────────────────────────────

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        if (!AutoFlash) return;

        // Falls Hintergrund-Flash noch läuft → blockierend abwarten
        if (_bgProc != null && !_bgProc.HasExited)
        {
            EditorUtility.DisplayProgressBar("Arduino Flash",
                $"Warte auf Hintergrund-Flash {_bgSketch}…", 0.7f);
            _bgProc.WaitForExit();
            EditorUtility.ClearProgressBar();
            FinishBackgroundFlash();
        }

        string sceneName = EditorSceneManager.GetActiveScene().name;
        if (!SceneToSketch.TryGetValue(sceneName, out string sketchFolder)) return;

        // Skip wenn der korrekte Sketch schon auf dem Board ist UND .ino unverändert
        string current = EditorPrefs.GetString(PrefCurrentSketch, "");
        bool needsFlash = NeedsFlash(sketchFolder, out long inoMtime, out string prefKey, out _);
        if (current == sketchFolder && !needsFlash)
        {
            Debug.Log($"[ArduinoFlasher] {sketchFolder} ist bereits auf dem Board → kein Flash.");
            return;
        }

        // Falls eine alte Bridge noch existiert (Stop→Play ohne Domain Reload), Port freigeben
        var bridge = ArduinoBridge.Instance;
        if (bridge != null) bridge.Disconnect();

        Debug.Log($"[ArduinoFlasher] Auto-Flash {sketchFolder} vor Play…");
        if (FlashSync(sketchFolder, withProgress: true))
        {
            EditorPrefs.SetString(prefKey, inoMtime.ToString());
            EditorPrefs.SetString(PrefCurrentSketch, sketchFolder);
        }
        else
        {
            EditorApplication.isPlaying = false;
            Debug.LogError("[ArduinoFlasher] Auto-Flash fehlgeschlagen → Play abgebrochen. " +
                           "Siehe Zeile darüber für arduino-cli Exit-Code/stderr.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // mtime-Cache
    // ─────────────────────────────────────────────────────────────────────────

    private static bool NeedsFlash(string sketchFolder, out long inoMtime,
                                   out string prefKey,  out string inoPath)
    {
        inoMtime = 0;
        prefKey  = string.Format(PrefLastFlashFmt, sketchFolder);
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        inoPath  = Path.Combine(projectRoot, "Arduino", sketchFolder, sketchFolder + ".ino");

        if (!File.Exists(inoPath))
        {
            Debug.LogWarning($"[ArduinoFlasher] {inoPath} fehlt – Auto-Flash übersprungen.");
            return false;
        }

        inoMtime = File.GetLastWriteTimeUtc(inoPath).ToFileTimeUtc();
        long last = long.TryParse(EditorPrefs.GetString(prefKey, "0"), out var p) ? p : 0;
        return inoMtime != last;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Async-Flash (Hintergrund)
    // ─────────────────────────────────────────────────────────────────────────

    private static void StartBackgroundFlash(string sketchFolder, string prefKey, long inoMtime)
    {
        if (_bgProc != null && !_bgProc.HasExited)
        {
            Debug.LogWarning("[ArduinoFlasher] Hintergrund-Flash läuft bereits – ignoriere.");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string sketchPath  = Path.Combine(projectRoot, "Arduino", sketchFolder);
        string args        = $"compile --upload -b {Fqbn} -p {Port} \"{sketchPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName               = Cli,
            Arguments              = args,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        try
        {
            _bgStdout = new System.Text.StringBuilder();
            _bgStderr = new System.Text.StringBuilder();
            _bgProc   = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _bgProc.OutputDataReceived += (_, e) => { if (e.Data != null) _bgStdout.AppendLine(e.Data); };
            _bgProc.ErrorDataReceived  += (_, e) => { if (e.Data != null) _bgStderr.AppendLine(e.Data); };
            _bgProc.Start();
            _bgProc.BeginOutputReadLine();
            _bgProc.BeginErrorReadLine();

            _bgSketch  = sketchFolder;
            _bgPrefKey = prefKey;
            _bgMtime   = inoMtime;
            EditorApplication.update += PollBackgroundFlash;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ArduinoFlasher] arduino-cli nicht gefunden ({Cli}): {ex.Message}");
            _bgProc = null;
        }
    }

    private static void PollBackgroundFlash()
    {
        if (_bgProc == null) { EditorApplication.update -= PollBackgroundFlash; return; }
        if (!_bgProc.HasExited) return;

        EditorApplication.update -= PollBackgroundFlash;
        FinishBackgroundFlash();
    }

    private static void FinishBackgroundFlash()
    {
        if (_bgProc == null) return;

        int exit = _bgProc.ExitCode;
        string sketch = _bgSketch;

        if (_bgStdout.Length > 0) Debug.Log(_bgStdout.ToString());

        if (exit == 0)
        {
            Debug.Log($"[ArduinoFlasher] ✓ Hintergrund: {sketch} → {Port} geflasht.");
            EditorPrefs.SetString(_bgPrefKey, _bgMtime.ToString());
            EditorPrefs.SetString(PrefCurrentSketch, sketch);
        }
        else
        {
            Debug.LogError($"[ArduinoFlasher] ✗ Hintergrund Exit {exit}\n{_bgStderr}");
        }

        _bgProc.Dispose();
        _bgProc    = null;
        _bgSketch  = null;
        _bgPrefKey = null;
        _bgStdout  = null;
        _bgStderr  = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sync-Flash (Menü + Play-Gate)
    // ─────────────────────────────────────────────────────────────────────────

    private static bool FlashSync(string sketchFolder, bool withProgress = false)
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[ArduinoFlasher] Erst Play-Mode beenden – Unity blockiert sonst den COM-Port.");
            return false;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string sketchPath  = Path.Combine(projectRoot, "Arduino", sketchFolder);

        if (!Directory.Exists(sketchPath))
        {
            Debug.LogError($"[ArduinoFlasher] Sketch-Ordner fehlt: {sketchPath}");
            return false;
        }

        string args = $"compile --upload -b {Fqbn} -p {Port} \"{sketchPath}\"";
        Debug.Log($"[ArduinoFlasher] {Cli} {args}");

        var psi = new ProcessStartInfo
        {
            FileName               = Cli,
            Arguments              = args,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        try
        {
            // Windows .NET SerialPort.Close() gibt den Port verzögert frei → kurz warten,
            // damit arduino-cli ihn öffnen kann. Bei "Serial port busy" einmal wiederholen.
            const int maxAttempts = 3;
            int proc_exit = -1;
            string stdout = "", stderr = "";

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (withProgress) EditorUtility.DisplayProgressBar(
                    "Arduino Flash",
                    attempt == 1
                        ? $"Flashe {sketchFolder} → {Port}…"
                        : $"Versuch {attempt}/{maxAttempts} – Port {Port} noch belegt…",
                    0.5f);

                System.Threading.Thread.Sleep(attempt == 1 ? 800 : 1500);

                using var proc = Process.Start(psi);
                stdout = proc.StandardOutput.ReadToEnd();
                stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                proc_exit = proc.ExitCode;

                if (proc_exit == 0) break;
                if (!stderr.Contains("Serial port busy") && !stderr.Contains("opening port")) break;
                Debug.LogWarning($"[ArduinoFlasher] Port {Port} busy (Versuch {attempt}/{maxAttempts}) – warte…");
            }

            if (!string.IsNullOrWhiteSpace(stdout)) Debug.Log(stdout);
            if (proc_exit == 0)
            {
                Debug.Log($"[ArduinoFlasher] ✓ {sketchFolder} → {Port} geflasht.");

                // mtime + aktuellen Sketch cachen (vermeidet doppeltes Auto-Flash)
                string inoPath = Path.Combine(sketchPath, sketchFolder + ".ino");
                if (File.Exists(inoPath))
                {
                    string key = string.Format(PrefLastFlashFmt, sketchFolder);
                    EditorPrefs.SetString(key, File.GetLastWriteTimeUtc(inoPath).ToFileTimeUtc().ToString());
                }
                EditorPrefs.SetString(PrefCurrentSketch, sketchFolder);
                return true;
            }

            Debug.LogError($"[ArduinoFlasher] ✗ Exit {proc_exit}\n{stderr}");
            return false;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ArduinoFlasher] arduino-cli nicht gefunden ({Cli}): {ex.Message}\n" +
                           "Installiere mit: winget install ArduinoSA.CLI");
            return false;
        }
        finally
        {
            if (withProgress) EditorUtility.ClearProgressBar();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Settings-Fenster
    // ─────────────────────────────────────────────────────────────────────────

    private class ArduinoFlasherSettings : EditorWindow
    {
        public static void ShowWindow() => GetWindow<ArduinoFlasherSettings>("Arduino Flasher");

        void OnGUI()
        {
            EditorGUILayout.LabelField("arduino-cli Konfiguration", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            string port = EditorGUILayout.TextField("Port", Port);
            string fqbn = EditorGUILayout.TextField("Board FQBN", Fqbn);
            string cli  = EditorGUILayout.TextField("CLI Pfad", Cli);
            bool   auto = EditorGUILayout.Toggle("Auto-Flash aktiv", AutoFlash);

            if (GUI.changed)
            {
                EditorPrefs.SetString(PrefPort, port);
                EditorPrefs.SetString(PrefFqbn, fqbn);
                EditorPrefs.SetString(PrefCli,  cli);
                EditorPrefs.SetBool(PrefAutoFlash, auto);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Auto-Flash:\n" +
                "  • Beim Öffnen einer Level-Szene → Hintergrund-Flash (Editor bleibt nutzbar).\n" +
                "  • Beim Play-Drücken → Flash blockierend, Play bricht bei Fehler ab.\n" +
                "  • Nur wenn .ino seit letztem Flash geändert wurde.\n\n" +
                "FQBN UNO R4 WiFi: arduino:renesas_uno:unor4wifi\n" +
                "FQBN UNO R3:      arduino:avr:uno",
                MessageType.Info);

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Flash-Cache zurücksetzen (zwingt nächstes Mal Re-Flash)"))
            {
                foreach (var kv in SceneToSketch)
                    EditorPrefs.DeleteKey(string.Format(PrefLastFlashFmt, kv.Value));
                EditorPrefs.DeleteKey(PrefCurrentSketch);
                Debug.Log("[ArduinoFlasher] Flash-Cache geleert.");
            }

            string current = EditorPrefs.GetString(PrefCurrentSketch, "(unbekannt)");
            EditorGUILayout.LabelField($"Aktuell auf Board: {current}");
        }
    }
}
