using UnityEngine;
using System;
using System.IO.Ports;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Singleton – vermittelt zwischen Unity und dem Arduino über serielle USB-Verbindung.
///
/// Protokoll (text-basiert, zeilengetrennt):
///   Arduino → Unity : "XX:payload\n"
///   Unity   → Arduino: "XX:payload\n"
///   XX = 2-stelliger Hex-Befehlscode (z.B. "20" für Farbsensor)
///
/// Befehls-IDs (aus ProjectPlan):
///   0x10  Humidity-Sensor  (Level 2a)
///   0x11  Joystick         (Level 2b)
///   0x20  Farbsensor       (Level 3)
///   0x50  Breadboard-State (Level 5)
///   0x60  Wärmesensor      (Level 6)
///   0xFF  Ping/Pong        (Verbindungstest)
///
/// Verwendung:
///   ArduinoBridge.Instance.RegisterHandler(0x20, payload => { ... });
///   ArduinoBridge.Instance.Send(0xFF, "ping");
/// </summary>
public class ArduinoBridge : MonoBehaviour
{
    public static ArduinoBridge Instance { get; private set; }

    [Header("Verbindung")]
    [SerializeField] private string portName  = "COM3";
    [SerializeField] private int    baudRate  = 115200;
    [SerializeField] private bool   autoConnect = true;

    [Header("Debug")]
    [SerializeField] private bool logIncoming = true;
    [SerializeField] private bool logOutgoing = false;

    // ── Interne Zustand ───────────────────────────────────────────────────────

    private SerialPort port;
    private Thread     readThread;
    private bool       running;

    // Vom Hintergrund-Thread in die Main-Thread-Queue geschriebene Nachrichten
    private readonly ConcurrentQueue<(byte cmd, string payload)> incoming = new();

    // Pro Befehls-ID registrierte Callbacks (nur auf Main Thread schreiben/lesen)
    private readonly Dictionary<byte, List<Action<string>>> handlers = new();

    // ── Status ────────────────────────────────────────────────────────────────

    public bool   IsConnected => port != null && port.IsOpen;
    public string PortName    => portName;

    // Wird auf dem Main Thread aufgerufen, sobald die Verbindung steht/bricht
    public event Action<bool> OnConnectionChanged;

    // =========================================================================
    // Lifecycle
    // =========================================================================

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (autoConnect)
            Connect(portName, baudRate);
    }

    void Update()
    {
        // Alle eingehenden Nachrichten auf dem Main Thread dispatchen
        while (incoming.TryDequeue(out var msg))
        {
            if (handlers.TryGetValue(msg.cmd, out var list))
                foreach (var h in list)
                    h?.Invoke(msg.payload);
        }
    }

    void OnApplicationQuit() => Disconnect();
    void OnDestroy()         => Disconnect();

    // =========================================================================
    // Verbindung
    // =========================================================================

    public void Connect(string comPort, int baud)
    {
        Disconnect();
        portName = comPort;
        baudRate = baud;

        try
        {
            port = new SerialPort(portName, baudRate)
            {
                ReadTimeout  = 1000,
                WriteTimeout = 500,
                NewLine      = "\n",
                DtrEnable    = true,    // Reset bei Verbindungsaufbau (wie Arduino IDE)
                RtsEnable    = true
            };
            port.Open();

            running    = true;
            readThread = new Thread(ReadLoop) { IsBackground = true, Name = "ArduinoRead" };
            readThread.Start();

            Debug.Log($"[ArduinoBridge] Verbunden: {portName} @ {baudRate} Baud");
            OnConnectionChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ArduinoBridge] Verbindungsfehler ({portName}): {ex.Message}");
            port = null;
        }
    }

    public void Disconnect()
    {
        running = false;
        readThread?.Join(600);
        readThread = null;

        if (port != null)
        {
            if (port.IsOpen)
            {
                try { port.Close(); }
                catch { /* ignorieren */ }
                Debug.Log("[ArduinoBridge] Verbindung getrennt.");
                OnConnectionChanged?.Invoke(false);
            }
            port.Dispose();
            port = null;
        }
    }

    // =========================================================================
    // Handler-Registrierung
    // =========================================================================

    /// <summary>Registriert einen Callback für einen bestimmten Befehlscode.</summary>
    public void RegisterHandler(byte cmdId, Action<string> handler)
    {
        if (!handlers.ContainsKey(cmdId))
            handlers[cmdId] = new List<Action<string>>();
        if (!handlers[cmdId].Contains(handler))
            handlers[cmdId].Add(handler);
    }

    /// <summary>Entfernt einen zuvor registrierten Callback.</summary>
    public void UnregisterHandler(byte cmdId, Action<string> handler)
    {
        if (handlers.TryGetValue(cmdId, out var list))
            list.Remove(handler);
    }

    // =========================================================================
    // Senden
    // =========================================================================

    /// <summary>
    /// Sendet einen Befehl an den Arduino.
    /// Format: "XX:data\n"
    /// </summary>
    public void Send(byte cmdId, string data = "")
    {
        if (!IsConnected)
        {
            Debug.LogWarning("[ArduinoBridge] Senden fehlgeschlagen – nicht verbunden.");
            return;
        }

        string line = $"{cmdId:X2}:{data}\n";
        try
        {
            port.Write(line);
            if (logOutgoing) Debug.Log($"[ArduinoBridge] → {line.TrimEnd()}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ArduinoBridge] Sende-Fehler: {ex.Message}");
        }
    }

    // =========================================================================
    // Hintergrund-Lese-Thread
    // =========================================================================

    void ReadLoop()
    {
        while (running)
        {
            if (port == null || !port.IsOpen) break;

            try
            {
                string raw = port.ReadLine();
                if (string.IsNullOrWhiteSpace(raw)) continue;

                string line = raw.Trim();
                if (logIncoming) Debug.Log($"[ArduinoBridge] ← {line}");

                ParseAndEnqueue(line);
            }
            catch (TimeoutException)
            {
                // Kein Zeichen innerhalb ReadTimeout – normal, weiter warten
            }
            catch (Exception ex)
            {
                if (running)
                    Debug.LogWarning($"[ArduinoBridge] Lese-Fehler: {ex.Message}");
                break;
            }
        }

        // Verbindung unerwartet verloren → auf Main Thread melden
        if (running)
        {
            running = false;
            incoming.Enqueue((0xFE, "disconnected"));   // 0xFE = internes Disconnect-Event
        }
    }

    void ParseAndEnqueue(string line)
    {
        // Erwartet: "XX:payload"
        int colon = line.IndexOf(':');
        if (colon < 1) return;

        string hexPart = line[..colon];
        string payload = line[(colon + 1)..];

        if (byte.TryParse(hexPart,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out byte cmd))
        {
            incoming.Enqueue((cmd, payload));
        }
        else
        {
            Debug.LogWarning($"[ArduinoBridge] Unbekanntes Format: '{line}'");
        }
    }

    // =========================================================================
    // Hilfsmethoden für Level-Scripts
    // =========================================================================

    /// <summary>Shortcut: sendet einen Ping und gibt true zurück wenn verbunden.</summary>
    public bool Ping()
    {
        if (!IsConnected) return false;
        Send(0xFF, "ping");
        return true;
    }
}
