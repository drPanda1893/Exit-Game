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
/// Protokoll:  "XX:payload\n"  (XX = 2-stelliger Hex-Befehlscode)
///
/// Verwendung:
///   ArduinoBridge.Instance.RegisterHandler(0x05, payload => { ... });
///   ArduinoBridge.Instance.Send(0xFF, "START");
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

    [Header("TCP Emulator (kein Hardware nötig)")]
    [SerializeField] private bool useTcpEmulator = false;
    [SerializeField] private int  tcpPort        = 12345;

    [Header("Reconnect")]
    [SerializeField] private float reconnectDelay = 3f;

    [Header("Debug")]
    [SerializeField] private bool logIncoming = true;
    [SerializeField] private bool logOutgoing = false;

    // ── Interne Zustand ───────────────────────────────────────────────────────

    private SerialPort   _port;
    private TcpListener  _tcpListener;
    private TcpClient    _tcpClient;
    private StreamReader _tcpReader;

    private Thread        _thread;
    private volatile bool _running;
    private float         _retryTimer;

    private readonly ConcurrentQueue<(byte cmd, string payload)> _incoming = new();
    private readonly Dictionary<byte, List<Action<string>>>      _handlers = new();

    public bool   IsConnected => useTcpEmulator
        ? (_tcpClient != null && _tcpClient.Connected && (_thread?.IsAlive ?? false))
        : (_port != null && _port.IsOpen && (_thread?.IsAlive ?? false));

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
        while (_incoming.TryDequeue(out var msg))
        {
            if (_handlers.TryGetValue(msg.cmd, out var list))
                foreach (var h in list) h?.Invoke(msg.payload);
        }

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
        try
        {
            _port = new SerialPort(portName, baudRate)
            {
                ReadTimeout  = 1000,
                WriteTimeout = 500,
                NewLine      = "\n",
                DtrEnable    = true,
                RtsEnable    = true
            };
            _port.Open();

            _running = true;
            _thread  = new Thread(SerialReadLoop) { IsBackground = true, Name = "ArduinoSerial" };
            _thread.Start();

            Debug.Log($"[ArduinoBridge] Verbunden: {portName} @ {baudRate} Baud");
            OnConnectionChanged?.Invoke(true);
            StartCoroutine(AutoPingRoutine());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ArduinoBridge] Verbindungsfehler ({portName}): {ex.Message}");
            _port = null;
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
        if (_running) { _running = false; _incoming.Enqueue((0xFE, "disconnected")); }
    }

    void TcpAcceptLoop()
    {
        try
        {
            _tcpClient = _tcpListener.AcceptTcpClient();
            _tcpReader = new StreamReader(_tcpClient.GetStream());
            _incoming.Enqueue((0xFD, "tcp-connected"));

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
                    if (_running) _incoming.Enqueue((0xFE, $"tcp-error:{ex.Message}"));
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            if (_running) _incoming.Enqueue((0xFE, $"tcp-accept:{ex.Message}"));
        }
    }

    // ── Protokoll-Parser ──────────────────────────────────────────────────────

    void ParseAndEnqueue(string line)
    {
        int colon = line.IndexOf(':');
        if (colon < 1) return;

        string hexPart = line[..colon];
        string payload = line[(colon + 1)..];

        if (byte.TryParse(hexPart,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out byte cmd))
        {
            if (logIncoming) Debug.Log($"[ArduinoBridge] ← {line}");
            _incoming.Enqueue((cmd, payload));
        }
        else
        {
            Debug.LogWarning($"[ArduinoBridge] Unbekanntes Format: '{line}'");
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

    public void UnregisterHandler(byte cmdId, Action<string> handler)
    {
        if (_handlers.TryGetValue(cmdId, out var list))
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

    [ContextMenu("Ping senden")] void PingFromInspector()       => Ping();
    [ContextMenu("Trennen")]     void DisconnectFromInspector() => CloseConnections();
    [ContextMenu("Neu verbinden")] void ReconnectFromInspector() => TryConnect();
}
