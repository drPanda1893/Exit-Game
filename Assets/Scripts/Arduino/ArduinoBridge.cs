using UnityEngine;
using System;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Singleton – verwaltet Serial- oder TCP-Kommunikation mit dem Arduino.
/// Liest auf einem Hintergrund-Thread; dispatcht Events auf den Main Thread.
///
/// Protokoll:  Text ("KEY:x" | "HUMIDITY:x" | "COLOR:x" | "TEMP:x")
///             oder Hex  "XX:payload\n"  (XX = 2-stelliger Hex-Befehlscode)
///
/// Zwei komplementäre APIs:
///   1) Events  – OnKeypadKey, OnHumidity, OnColor (Level1/2/3)
///   2) Handler-Registry – RegisterHandler(byte, Action<string>) (Level6+)
///
/// TCP-Emulator-Modus:
///   "Use Tcp Emulator" im Inspector aktivieren, dann Arduino/emulator.py starten.
///   Kein COM-Port oder Hardware nötig.
/// </summary>
public class ArduinoBridge : MonoBehaviour
{
    public static ArduinoBridge Instance { get; private set; }

    [Header("Serial Port")]
    [SerializeField] private string portName    = "COM3";
    [SerializeField] private int    baudRate    = 115200;
    [SerializeField] private bool   autoConnect = true;
    [SerializeField] private bool   autoDetectSerialPort = true;

    [Header("TCP Emulator (kein Hardware nötig)")]
    [SerializeField] private bool useTcpEmulator = false;
    [SerializeField] private int  tcpPort        = 12345;

    [Header("Reconnect")]
    [SerializeField] private float reconnectDelay = 3f;

    [Header("Debug")]
    [SerializeField] private bool logIncoming = true;
    [SerializeField] private bool logOutgoing = false;

    // ── Events (auf Main Thread ausgelöst) ────────────────────────────────────
    /// <summary>L1 Keypad – Werte: "0"–"9", "DEL", "ENT".</summary>
    public event Action<string> OnKeypadKey;
    /// <summary>L2 Humidity – Wert: 0–100.</summary>
    public event Action<int>    OnHumidity;
    /// <summary>L3 Color – Wert: Hex-String oder Farbname.</summary>
    public event Action<string> OnColor;

    // ── Interne Zustände ──────────────────────────────────────────────────────
    private SerialPort   _port;
    private TcpListener  _tcpListener;
    private TcpClient    _tcpClient;
    private StreamReader _tcpReader;

    private Thread        _thread;
    private volatile bool _running;
    private float         _retryTimer;

    private readonly ConcurrentQueue<Action>                _incoming = new();
    private readonly Dictionary<byte, List<Action<string>>> _handlers = new();

    public bool   IsConnected => useTcpEmulator
        ? (_tcpClient != null && _tcpClient.Connected && (_thread?.IsAlive ?? false))
        : (_port != null && _port.IsOpen && (_thread?.IsAlive ?? false));

    /// <summary>Alias für Skripte die .Connected direkt nutzen.</summary>
    public bool   Connected => IsConnected;

    public string PortName => portName;

    public event Action<bool> OnConnectionChanged;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (autoConnect) TryConnect();
    }

    void Update()
    {
        while (_incoming.TryDequeue(out var action))
            action?.Invoke();

        if (!IsConnected)
        {
            _retryTimer -= Time.deltaTime;
            if (_retryTimer <= 0f) TryConnect();
        }
    }

    void OnApplicationQuit() => CloseConnections();
    void OnDestroy()         => CloseConnections();

    // ── Verbindung ────────────────────────────────────────────────────────────

    void TryConnect()
    {
        _retryTimer = reconnectDelay;
        CloseConnections();

        if (useTcpEmulator) StartTcpListener();
        else                OpenSerialPort();
    }

    void OpenSerialPort()
    {
        var candidates = SerialPortCandidates();
        var errors = new List<string>();

        foreach (string candidate in candidates)
            if (TryOpenSerialPort(candidate, errors))
                return;

        string tried = candidates.Count == 0 ? "keine" : string.Join(", ", candidates);
        string detail = errors.Count == 0 ? "" : "\n" + string.Join("\n", errors);
        Debug.LogWarning(
            $"[ArduinoBridge] Kein serieller Arduino verbunden. Versucht: {tried}\n" +
            $"Verfuegbare Ports: {AvailableSerialPorts()}{detail}");
    }

    List<string> SerialPortCandidates()
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(portName))
            candidates.Add(portName);

        if (!autoDetectSerialPort) return candidates;

        try
        {
            foreach (string detected in SerialPort.GetPortNames())
                if (!candidates.Contains(detected))
                    candidates.Add(detected);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ArduinoBridge] COM-Port-Erkennung fehlgeschlagen: {ex.Message}");
        }

        return candidates;
    }

    bool TryOpenSerialPort(string candidate, List<string> errors)
    {
        try
        {
            _port = new SerialPort(candidate, baudRate)
            {
                ReadTimeout  = 1000,
                WriteTimeout = 500,
                NewLine      = "\n",
                DtrEnable    = true,
                RtsEnable    = true
            };
            _port.Open();

            portName = candidate;
            _running = true;
            _thread  = new Thread(SerialReadLoop) { IsBackground = true, Name = "ArduinoSerial" };
            _thread.Start();

            Debug.Log($"[ArduinoBridge] Verbunden: {portName} @ {baudRate} Baud");
            OnConnectionChanged?.Invoke(true);
            StartCoroutine(AutoPingRoutine());
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            errors.Add($"  {candidate}: belegt ({ex.Message})");
        }
        catch (Exception ex)
        {
            errors.Add($"  {candidate}: {ex.Message}");
        }

        CleanupFailedSerialPort();
        return false;
    }

    void CleanupFailedSerialPort()
    {
        try { if (_port?.IsOpen == true) _port.Close(); _port?.Dispose(); } catch { }
        _port = null;
    }

    string AvailableSerialPorts()
    {
        try
        {
            string[] ports = SerialPort.GetPortNames();
            return ports.Length == 0 ? "keine" : string.Join(", ", ports);
        }
        catch (Exception ex)
        {
            return $"unbekannt ({ex.Message})";
        }
    }

    void StartTcpListener()
    {
        try
        {
            _tcpListener = new TcpListener(IPAddress.Loopback, tcpPort);
            _tcpListener.Start();
            Debug.Log($"[ArduinoBridge] TCP-Emulator wartet auf Port {tcpPort} — starte Arduino/emulator.py");

            _running = true;
            _thread  = new Thread(TcpAcceptLoop) { IsBackground = true, Name = "ArduinoTCP" };
            _thread.Start();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ArduinoBridge] TCP-Listener fehlgeschlagen (Port {tcpPort}): {ex.Message}");
        }
    }

    void CloseConnections()
    {
        _running = false;
        try { _thread?.Join(300); } catch { }

        try { _tcpReader?.Close(); }  catch { }
        try { _tcpClient?.Close(); }  catch { }
        try { _tcpListener?.Stop(); } catch { }
        try { if (_port?.IsOpen == true) _port.Close(); _port?.Dispose(); } catch { }

        _port        = null;
        _tcpReader   = null;
        _tcpClient   = null;
        _tcpListener = null;
        _thread      = null;
    }

    // ── Read-Loops ────────────────────────────────────────────────────────────

    void SerialReadLoop()
    {
        while (_running && _port != null && _port.IsOpen)
        {
            try
            {
                string line = _port.ReadLine();
                if (!string.IsNullOrWhiteSpace(line)) ParseAndEnqueue(line.Trim());
            }
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                if (_running) Debug.LogWarning($"[ArduinoBridge] Serial-Fehler: {ex.Message}");
                break;
            }
        }
        if (_running) _incoming.Enqueue(() => OnConnectionChanged?.Invoke(false));
    }

    void TcpAcceptLoop()
    {
        try
        {
            _tcpClient = _tcpListener.AcceptTcpClient();
            _tcpReader = new StreamReader(_tcpClient.GetStream());
            _incoming.Enqueue(() => Debug.Log("[ArduinoBridge] TCP verbunden."));

            while (_running && _tcpClient.Connected)
            {
                try
                {
                    string line = _tcpReader.ReadLine();
                    if (line == null) break;
                    if (!string.IsNullOrWhiteSpace(line)) ParseAndEnqueue(line.Trim());
                }
                catch (Exception ex)
                {
                    if (_running) _incoming.Enqueue(() => Debug.LogWarning($"[ArduinoBridge] TCP-Lesefehler: {ex.Message}"));
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            if (_running) _incoming.Enqueue(() => Debug.LogWarning($"[ArduinoBridge] TCP-Accept-Fehler: {ex.Message}"));
        }
    }

    // ── Handler-Registrierung ─────────────────────────────────────────────────

    public void RegisterHandler(byte cmdId, Action<string> handler)
    {
        if (!_handlers.ContainsKey(cmdId))
            _handlers[cmdId] = new List<Action<string>>();
        if (!_handlers[cmdId].Contains(handler))
            _handlers[cmdId].Add(handler);
    }

    public void UnregisterHandler(byte cmdId, Action<string> handler = null)
    {
        if (handler == null)
            _handlers.Remove(cmdId);
        else if (_handlers.TryGetValue(cmdId, out var list))
            list.Remove(handler);
    }

    // ── Senden ────────────────────────────────────────────────────────────────

    public void Send(byte cmdId, string data = "")
    {
        string line = $"{cmdId:X2}:{data}\n";

        if (useTcpEmulator)
        {
            if (_tcpClient == null || !_tcpClient.Connected)
            {
                Debug.LogWarning("[ArduinoBridge] TCP-Emulator nicht verbunden.");
                return;
            }
            try
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(line);
                _tcpClient.GetStream().Write(bytes, 0, bytes.Length);
                if (logOutgoing) Debug.Log($"[ArduinoBridge] → {line.TrimEnd()}");
            }
            catch (Exception ex) { Debug.LogWarning($"[ArduinoBridge] TCP-Sende-Fehler: {ex.Message}"); }
            return;
        }

        if (_port == null || !_port.IsOpen)
        {
            Debug.LogWarning("[ArduinoBridge] Senden fehlgeschlagen – nicht verbunden.");
            return;
        }
        try
        {
            _port.Write(line);
            if (logOutgoing) Debug.Log($"[ArduinoBridge] → {line.TrimEnd()}");
        }
        catch (Exception ex) { Debug.LogWarning($"[ArduinoBridge] Sende-Fehler: {ex.Message}"); }
    }

    // ── Protokoll-Parser ──────────────────────────────────────────────────────

    void ParseAndEnqueue(string line)
    {
        if (logIncoming && !IsNoisyTelemetry(line)) Debug.Log($"[ArduinoBridge] ← {line}");

        // Format 1: Text-Kommandos (Level1/2/3)
        if (line.StartsWith("KEY:", StringComparison.Ordinal))
        {
            string key = line.Substring(4);
            _incoming.Enqueue(() => { OnKeypadKey?.Invoke(key); DispatchHandlers(0x01, key); });
            return;
        }
        if (line.StartsWith("HUMIDITY:", StringComparison.Ordinal))
        {
            string val = line.Substring(9);
            _incoming.Enqueue(() => { if (int.TryParse(val, out int h)) OnHumidity?.Invoke(h); DispatchHandlers(0x10, val); });
            return;
        }
        if (line.StartsWith("COLOR:", StringComparison.Ordinal))
        {
            string col = line.Substring(6);
            _incoming.Enqueue(() => { OnColor?.Invoke(col); DispatchHandlers(0x20, col); });
            return;
        }
        if (line.StartsWith("TEMP:", StringComparison.Ordinal))
        {
            string val = line.Substring(5);
            _incoming.Enqueue(() => DispatchHandlers(0x60, "TEMP:" + val));
            return;
        }

        // Format 2: Hex "XX:payload"
        int colon = line.IndexOf(':');
        if (colon < 1) { Debug.LogWarning($"[ArduinoBridge] Unbekanntes Format: '{line}'"); return; }

        string hexPart = line[..colon];
        string payload = line[(colon + 1)..];

        if (byte.TryParse(hexPart,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out byte cmd))
        {
            _incoming.Enqueue(() => DispatchHandlers(cmd, payload));
        }
        else
        {
            Debug.LogWarning($"[ArduinoBridge] Unbekanntes Format: '{line}'");
        }
    }

    void DispatchHandlers(byte cmd, string payload)
    {
        if (_handlers.TryGetValue(cmd, out var list))
            foreach (var h in list) h?.Invoke(payload);
    }

    // Sensor-Telemetrie, die mit >1 Hz reinkommt, fluten sonst die Konsole.
    // Auslöser-Events (BLOW, RESET, named COLOR) sollen weiterhin sichtbar bleiben.
    static bool IsNoisyTelemetry(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;
        if (line.StartsWith("TEMP:",      StringComparison.Ordinal)) return true;
        if (line.StartsWith("10:TEMP:",   StringComparison.Ordinal)) return true;
        if (line.StartsWith("HUMIDITY:",  StringComparison.Ordinal)) return true;
        if (line.StartsWith("COLOR:RGB:", StringComparison.Ordinal)) return true;
        if (line.StartsWith("30:JOY:",    StringComparison.Ordinal)) return true;
        return false;
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    System.Collections.IEnumerator AutoPingRoutine()
    {
        yield return new WaitForSeconds(1.2f);
        if (IsConnected) Send(0xFF, "ping");
    }

    public bool Ping()
    {
        if (!IsConnected) return false;
        Send(0xFF, "ping");
        return true;
    }

    [ContextMenu("Ping senden")]    void PingFromInspector()       => Ping();
    [ContextMenu("Trennen")]        void DisconnectFromInspector() => CloseConnections();
    [ContextMenu("Neu verbinden")]  void ReconnectFromInspector()  => TryConnect();

    // Public API für Auto-Flash (Editor-Tool gibt den COM-Port frei und reconnectet)
    public void Disconnect() => CloseConnections();
    public void Reconnect()  => TryConnect();
}
