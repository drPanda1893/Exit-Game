using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Verbindungstest-Komponente für den ArduinoBridge.
/// Sendet Ping → erwartet Pong. Startet optional Temperatur-Simulation.
///
/// Benötigt ein ArduinoBridge-Objekt in der Szene.
/// Kann per Editor-Button (BuildArduinoTestScene) oder manuell in jede Szene gelegt werden.
/// </summary>
public class ArduinoConnectionTest : MonoBehaviour
{
    [Header("UI-Referenzen (optional)")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private Button          pingButton;
    [SerializeField] private Button          tempStartButton;
    [SerializeField] private Button          tempStopButton;
    [SerializeField] private Slider          tempBar;

    private string logBuffer = string.Empty;
    private float  lastTemp  = 0f;

    // =========================================================================

    void Start()
    {
        if (ArduinoBridge.Instance == null)
        {
            Log("FEHLER: Kein ArduinoBridge in der Szene!");
            return;
        }

        // Callbacks registrieren
        ArduinoBridge.Instance.RegisterHandler(0xFF, OnPong);
        ArduinoBridge.Instance.RegisterHandler(0x60, OnTempData);
        ArduinoBridge.Instance.RegisterHandler(0xFE, OnDisconnected);   // internes Event
        ArduinoBridge.Instance.OnConnectionChanged += OnConnectionChanged;

        // Buttons verdrahten
        if (pingButton)      pingButton.onClick.AddListener(SendPing);
        if (tempStartButton) tempStartButton.onClick.AddListener(StartTempSim);
        if (tempStopButton)  tempStopButton.onClick.AddListener(StopTempSim);

        Log("ArduinoConnectionTest gestartet.");
        Log($"Port: {ArduinoBridge.Instance.PortName} – verbunden: {ArduinoBridge.Instance.IsConnected}");
    }

    void OnDestroy()
    {
        if (ArduinoBridge.Instance == null) return;
        ArduinoBridge.Instance.UnregisterHandler(0xFF, OnPong);
        ArduinoBridge.Instance.UnregisterHandler(0x60, OnTempData);
        ArduinoBridge.Instance.UnregisterHandler(0xFE, OnDisconnected);
        ArduinoBridge.Instance.OnConnectionChanged -= OnConnectionChanged;
    }

    void Update()
    {
        if (statusText == null || ArduinoBridge.Instance == null) return;
        statusText.text = ArduinoBridge.Instance.IsConnected
            ? $"VERBUNDEN  ({ArduinoBridge.Instance.PortName})"
            : "GETRENNT";
        statusText.color = ArduinoBridge.Instance.IsConnected
            ? new Color(0.3f, 1f, 0.4f)
            : new Color(1f, 0.3f, 0.3f);
    }

    // =========================================================================
    // Button-Aktionen
    // =========================================================================

    void SendPing()
    {
        if (ArduinoBridge.Instance.Ping())
            Log("→ FF:ping gesendet (warte auf FF:pong ...)");
        else
            Log("Nicht verbunden – Ping nicht möglich.");
    }

    void StartTempSim()
    {
        ArduinoBridge.Instance.Send(0x60, "start");
        Log("→ 60:start  (Temperatur-Simulation gestartet)");
    }

    void StopTempSim()
    {
        ArduinoBridge.Instance.Send(0x60, "stop");
        Log("→ 60:stop");
    }

    // =========================================================================
    // Callbacks vom Arduino
    // =========================================================================

    void OnPong(string payload)
    {
        Log($"← FF:{payload}  ✓ Verbindung OK!");
    }

    void OnTempData(string payload)
    {
        // Erwartet: "TEMP:72.5" oder "ok" / "stopped"
        Log($"← 60:{payload}");

        if (payload.StartsWith("TEMP:") &&
            float.TryParse(payload[5..],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float val))
        {
            lastTemp = val;
            if (tempBar) tempBar.value = val / 100f;
        }
    }

    void OnDisconnected(string _)
    {
        Log("⚠ Verbindung verloren!");
    }

    void OnConnectionChanged(bool connected)
    {
        Log(connected ? "✓ Arduino verbunden." : "✗ Arduino getrennt.");
    }

    // =========================================================================
    // Log-Hilfsmethode
    // =========================================================================

    void Log(string msg)
    {
        string entry = $"[{Time.timeSinceLevelLoad:F1}s] {msg}";
        Debug.Log($"[ArduinoTest] {msg}");

        logBuffer = entry + "\n" + logBuffer;
        if (logBuffer.Length > 1200)
            logBuffer = logBuffer[..1200];

        if (logText) logText.text = logBuffer;
    }
}
