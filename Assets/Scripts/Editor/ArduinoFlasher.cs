using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Tools → Arduino → Flash Level N
/// Kompiliert und uploaded den passenden .ino-Sketch via arduino-cli.
/// Trennt vorher die laufende Serial-Verbindung (Port-Lock vermeiden).
/// Konfigurierbar via Tools → Arduino → Settings… (EditorPrefs).
/// </summary>
public static class ArduinoFlasher
{
    private const string PrefPort = "ArduinoFlasher.Port";
    private const string PrefFqbn = "ArduinoFlasher.Fqbn";
    private const string PrefCli  = "ArduinoFlasher.CliPath";

    private const string DefaultPort = "COM3";
    private const string DefaultFqbn = "arduino:avr:uno";
    private const string DefaultCli  = "arduino-cli";

    private static string Port => EditorPrefs.GetString(PrefPort, DefaultPort);
    private static string Fqbn => EditorPrefs.GetString(PrefFqbn, DefaultFqbn);
    private static string Cli  => EditorPrefs.GetString(PrefCli,  DefaultCli);

    [MenuItem("Tools/Arduino/Flash Level 1 (Keypad)", priority = 100)]
    public static void FlashLevel1() => Flash("Level1_Keypad");

    [MenuItem("Tools/Arduino/Flash Level 2 (Humidity)", priority = 101)]
    public static void FlashLevel2() => Flash("Level2_Humidity");

    [MenuItem("Tools/Arduino/Settings…", priority = 200)]
    public static void OpenSettings() => ArduinoFlasherSettings.ShowWindow();

    private static void Flash(string sketchFolder)
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[ArduinoFlasher] Erst Play-Mode beenden – Unity blockiert sonst den COM-Port.");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string sketchPath  = Path.Combine(projectRoot, "Arduino", sketchFolder);

        if (!Directory.Exists(sketchPath))
        {
            Debug.LogError($"[ArduinoFlasher] Sketch-Ordner fehlt: {sketchPath}");
            return;
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
            using var proc = Process.Start(psi);
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stdout)) Debug.Log(stdout);
            if (proc.ExitCode == 0)
                Debug.Log($"[ArduinoFlasher] ✓ {sketchFolder} → {Port} geflasht.");
            else
                Debug.LogError($"[ArduinoFlasher] ✗ Exit {proc.ExitCode}\n{stderr}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ArduinoFlasher] arduino-cli nicht gefunden ({Cli}): {ex.Message}\n" +
                           "Installiere mit: winget install ArduinoSA.CLI");
        }
    }

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

            if (GUI.changed)
            {
                EditorPrefs.SetString(PrefPort, port);
                EditorPrefs.SetString(PrefFqbn, fqbn);
                EditorPrefs.SetString(PrefCli,  cli);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Port: typisch COM3 (Windows).\n" +
                "FQBN: Freenove FNK0059 = arduino:avr:uno.\n" +
                "CLI: 'arduino-cli' wenn im PATH, sonst voller Pfad.",
                MessageType.Info);
        }
    }
}
