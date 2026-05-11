using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Singleton – manages a background serial (or TCP emulator) thread for Arduino communication.
/// Dispatches parsed events to the Unity main thread via a thread-safe queue.
/// Per CLAUDE.md §4: reads on background thread only; no blocking in Update().
///
/// TCP Emulator Mode:
///   Enable "Use Tcp Emulator" in Inspector, then run Arduino/emulator.py in VSCode.
///   No virtual COM port or hardware required.
/// </summary>
public class ArduinoBridge : MonoBehaviour
{
    public static ArduinoBridge Instance { get; private set; }

    [Header("Serial Port")]
    [SerializeField] private string portName       = "COM3";
    [SerializeField] private int    baudRate       = 9600;
    [SerializeField] private float  reconnectDelay = 3f;

    [Header("TCP Emulator (no hardware needed)")]
    [SerializeField] private bool useTcpEmulator = false;
    [SerializeField] private int  tcpPort        = 12345;

    [Header("Fallback")]
    [SerializeField] private bool arduinoFallback = true;

    // ── Events (fired on main thread) ─────────────────────────────────────
    /// <summary>L1 Keypad – values: "0"–"9", "DEL", "ENT".</summary>
    public event Action<string> OnKeypadKey;

    /// <summary>L2 Humidity – value: 0–100.</summary>
    public event Action<int> OnHumidity;

    /// <summary>L3 Color – value: hex string or color name.</summary>
    public event Action<string> OnColor;

    // ── Internal ──────────────────────────────────────────────────────────
    private SerialPort  _port;
    private TcpListener _tcpListener;
    private TcpClient   _tcpClient;
    private StreamReader _tcpReader;

    private Thread        _thread;
    private volatile bool _running;
    private float         _retryTimer;

    private readonly Queue<Action> _dispatch     = new Queue<Action>();
    private readonly object        _dispatchLock = new object();

    // ═════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        TryConnect();
    }

    void Update()
    {
        FlushDispatch();

        if (!IsConnected())
        {
            _retryTimer -= Time.deltaTime;
            if (_retryTimer <= 0f) TryConnect();
        }
    }

    // ── Connection ────────────────────────────────────────────────────────

    bool IsConnected()
    {
        if (useTcpEmulator)
            return _tcpClient != null && _tcpClient.Connected && (_thread?.IsAlive ?? false);
        return _port != null && _port.IsOpen && (_thread?.IsAlive ?? false);
    }

    /// <summary>Public accessor so Inspector buttons / other scripts can check.</summary>
    public bool Connected => IsConnected();

    void TryConnect()
    {
        _retryTimer = reconnectDelay;
        CloseConnections();

        if (useTcpEmulator)
            StartTcpListener();
        else
            OpenSerialPort();
    }

    // ── Serial ────────────────────────────────────────────────────────────

    void OpenSerialPort()
    {
        try
        {
            _port = new SerialPort(portName, baudRate)
            {
                ReadTimeout  = 500,
                WriteTimeout = 500,
                NewLine      = "\n"
            };
            _port.Open();

            _running = true;
            _thread  = new Thread(SerialReadLoop) { IsBackground = true, Name = "ArduinoSerial" };
            _thread.Start();

            Debug.Log($"[ArduinoBridge] Serial connected → {portName} @ {baudRate}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ArduinoBridge] Cannot open {portName}: {ex.Message}");
        }
    }

    void SerialReadLoop()
    {
        while (_running && _port != null && _port.IsOpen)
        {
            try
            {
                string line = _port.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                    ParseLine(line.Trim());
            }
            catch (TimeoutException) { /* normal – keep looping */ }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ArduinoBridge] Serial read error: {ex.Message}");
                break;
            }
        }
    }

    // ── TCP Emulator ──────────────────────────────────────────────────────

    void StartTcpListener()
    {
        try
        {
            _tcpListener = new TcpListener(IPAddress.Loopback, tcpPort);
            _tcpListener.Start();
            Debug.Log($"[ArduinoBridge] TCP emulator waiting on port {tcpPort} — run Arduino/emulator.py");

            _running = true;
            _thread  = new Thread(TcpAcceptLoop) { IsBackground = true, Name = "ArduinoTCP" };
            _thread.Start();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ArduinoBridge] TCP listen failed on port {tcpPort}: {ex.Message}");
        }
    }

    void TcpAcceptLoop()
    {
        try
        {
            // Block until emulator.py connects
            _tcpClient = _tcpListener.AcceptTcpClient();
            _tcpReader = new StreamReader(_tcpClient.GetStream());
            Enqueue(() => Debug.Log("[ArduinoBridge] Emulator connected via TCP"));

            while (_running && _tcpClient.Connected)
            {
                try
                {
                    string line = _tcpReader.ReadLine();
                    if (line == null) break; // connection closed
                    if (!string.IsNullOrWhiteSpace(line))
                        ParseLine(line.Trim());
                }
                catch (Exception ex)
                {
                    Enqueue(() => Debug.LogWarning($"[ArduinoBridge] TCP read error: {ex.Message}"));
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            if (_running)
                Enqueue(() => Debug.LogWarning($"[ArduinoBridge] TCP accept error: {ex.Message}"));
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────

    void CloseConnections()
    {
        _running = false;
        try { _thread?.Join(300); } catch { /* ignored */ }

        try { _tcpReader?.Close(); }   catch { /* ignored */ }
        try { _tcpClient?.Close(); }   catch { /* ignored */ }
        try { _tcpListener?.Stop(); }  catch { /* ignored */ }
        try { if (_port?.IsOpen == true) _port.Close(); } catch { /* ignored */ }

        _port        = null;
        _tcpReader   = null;
        _tcpClient   = null;
        _tcpListener = null;
        _thread      = null;
    }

    void OnApplicationQuit() => CloseConnections();
    void OnDestroy()         => CloseConnections();

    // ── Protocol Parser ───────────────────────────────────────────────────
    // Format:  KEY:<key>  |  HUMIDITY:<0-100>  |  COLOR:<name>

    void ParseLine(string line)
    {
        if (line.StartsWith("KEY:", StringComparison.Ordinal))
        {
            string key = line.Substring(4);
            Enqueue(() => OnKeypadKey?.Invoke(key));
            return;
        }

        if (line.StartsWith("HUMIDITY:", StringComparison.Ordinal))
        {
            if (int.TryParse(line.Substring(9), out int h))
                Enqueue(() => OnHumidity?.Invoke(h));
            return;
        }

        if (line.StartsWith("COLOR:", StringComparison.Ordinal))
        {
            string col = line.Substring(6);
            Enqueue(() => OnColor?.Invoke(col));
            return;
        }
    }

    // ── Main-thread Dispatch ──────────────────────────────────────────────

    void Enqueue(Action action)
    {
        lock (_dispatchLock) _dispatch.Enqueue(action);
    }

    void FlushDispatch()
    {
        lock (_dispatchLock)
        {
            while (_dispatch.Count > 0)
                _dispatch.Dequeue()?.Invoke();
        }
    }
}
