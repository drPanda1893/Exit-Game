using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections;
using System.Globalization;
using UnityEngine.InputSystem;

/// <summary>
/// Level 6 – Das finale Gefängnistor (3D-Szene).
///
/// Spieler läuft zum Tor → [E] → Bunsenbrenner-Panel erscheint.
/// Button gedrückt halten → Temperatur steigt.
/// Bei 100 % → Tor öffnet sich → Sieger-Bildschirm.
/// </summary>
public class Level6_FinalGate : MonoBehaviour
{
    public static Level6_FinalGate Instance { get; private set; }

    [Header("Näherungs-Trigger")]
    [SerializeField] private DustyWallSpot gateSpot;
    [SerializeField] private GameObject    interactionPrompt;

    [Header("3D Gate Visual")]
    [SerializeField] private GameObject gateBarsGO;

    [Header("Heat Panel")]
    [SerializeField] private GameObject        heatPanel;
    [SerializeField] private Slider            temperatureBar;
    [SerializeField] private TextMeshProUGUI   temperatureLabel;
    [SerializeField] private TextMeshProUGUI   statusText;
    [SerializeField] private Button            heatButton;

    [Header("Win Screen")]
    [SerializeField] private GameObject        winOverlay;
    [SerializeField] private TextMeshProUGUI   timerText;
    [SerializeField] private Button            restartButton;

    [Header("Einstellungen")]
    [SerializeField] private float heatSpeed = 0.10f;
    [SerializeField] private float coolSpeed = 0.04f;

    [Header("Arduino (Thermistor – wie Level 2)")]
    [Tooltip("Befehls-ID des Thermistors (gleich wie Level 2 → 0x10).")]
    [SerializeField] private byte arduinoCmdId = 0x10;
    [Tooltip("Schwelle in Grad Celsius, ab der der Bunsenbrenner als 'aktiv' zählt – Balken faengt an zu wandern.")]
    [SerializeField] private float heatThresholdC = 40f;
    [Tooltip("Sekunden Karenz nach letztem Sensor-Wert, bevor wieder abgekühlt wird.")]
    [SerializeField] private float arduinoTimeoutSec = 0.6f;

    private enum State { Idle, WaitingApproach, Heating, Done }
    private State state = State.Idle;

    private bool  holding;
    private bool  won;
    private bool  arduinoConnected;
    private bool  arduinoSubscribed;
    private float arduinoLastTempC;
    private float arduinoLastUpdateTime;
    private Action<string> tempHandler;
    private Coroutine arduinoStartRoutine;

    void Awake() => Instance = this;

    void OnEnable()
    {
        won     = false;
        holding = false;
        state   = State.WaitingApproach;

        if (interactionPrompt) interactionPrompt.SetActive(false);
        if (heatPanel)         heatPanel.SetActive(false);
        if (winOverlay)        winOverlay.SetActive(false);
        if (gateBarsGO)        gateBarsGO.SetActive(true);
        if (temperatureBar)    temperatureBar.value = 0f;
        if (statusText)        statusText.text = string.Empty;
    }

    void Start()
    {
        SetupHeatButton();
        if (restartButton) restartButton.onClick.AddListener(OnRestartClicked);

        EnsureArduinoBridge();

        // Thermistor NICHT sofort starten – erst wenn der Spieler am Tor steht
        // und das Heat-Panel oeffnet (siehe OpenHeatPanel). Handler/Listener
        // werden trotzdem hier registriert, damit der erste Wert sofort ankommt.
        if (ArduinoBridge.Instance != null)
        {
            tempHandler = OnTemperatureFromArduino;
            ArduinoBridge.Instance.RegisterHandler(arduinoCmdId, tempHandler);
            ArduinoBridge.Instance.OnConnectionChanged += HandleArduinoConnectionChanged;
            arduinoSubscribed = true;
        }

        if (BigYahuDialogSystem.Instance)
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Big Yahu: Das ist es – das letzte Tor. Dahinter liegt die Freiheit!",
                "Big Yahu: Ich hab den Bunsenbrenner aus der Werkstatt dabei …",
                "Big Yahu: Halt die Flamme an den Sensor – ab 40 °C schmilzt das Schloss!"
            });
    }

    void OnDisable()
    {
        if (ArduinoBridge.Instance != null)
        {
            if (arduinoStartRoutine != null)
            {
                StopCoroutine(arduinoStartRoutine);
                arduinoStartRoutine = null;
            }
            if (tempHandler != null)
                ArduinoBridge.Instance.UnregisterHandler(arduinoCmdId, tempHandler);
            if (arduinoSubscribed)
                ArduinoBridge.Instance.OnConnectionChanged -= HandleArduinoConnectionChanged;
            arduinoSubscribed = false;
            // Thermistor abschalten, damit andere Level (z.B. spaeter Level 2)
            // den Sensor wieder sauber initialisieren koennen.
            ArduinoBridge.Instance.Send(arduinoCmdId, "STOP");
        }
    }

    // -------------------------------------------------------------------------
    // Arduino-Eingang: empfängt "TEMP:42" oder rohen Wert "42"
    // -------------------------------------------------------------------------

    void EnsureArduinoBridge()
    {
        if (ArduinoBridge.Instance != null) return;

        var go = new GameObject("ArduinoBridge");
        go.AddComponent<ArduinoBridge>();
        Debug.Log("[Level6] ArduinoBridge fehlte in der Szene und wurde automatisch erstellt.");
    }

    void HandleArduinoConnectionChanged(bool connected)
    {
        arduinoConnected = connected;
        if (!connected)
        {
            arduinoLastUpdateTime = 0f;
            return;
        }

        // Nach (Re-)Connect nur neu starten, wenn der Spieler bereits im Heat-Panel ist.
        if (state == State.Heating) RequestArduinoStart();
    }

    void RequestArduinoStart()
    {
        if (!isActiveAndEnabled) return;
        if (arduinoStartRoutine != null)
            StopCoroutine(arduinoStartRoutine);
        arduinoStartRoutine = StartCoroutine(StartArduinoThermistorRoutine());
    }

    IEnumerator StartArduinoThermistorRoutine()
    {
        const int maxAttempts = 6;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var bridge = ArduinoBridge.Instance;
            if (bridge == null) break;

            if (bridge.IsConnected)
            {
                bridge.Send(arduinoCmdId, "START");
                yield return new WaitForSeconds(attempt == 1 ? 1.2f : 0.75f);

                if (arduinoLastUpdateTime > 0f &&
                    Time.time - arduinoLastUpdateTime <= arduinoTimeoutSec)
                    break;
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
        }

        arduinoStartRoutine = null;
    }

    void OnTemperatureFromArduino(string payload)
    {
        string val = payload;
        if (val.StartsWith("TEMP:", StringComparison.OrdinalIgnoreCase)) val = val.Substring(5);
        else if (val.StartsWith("BLOW:", StringComparison.OrdinalIgnoreCase)) val = val.Substring(5);
        else if (val.StartsWith("HUM:", StringComparison.OrdinalIgnoreCase)) val = val.Substring(4);

        if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float tempC))
        {
            arduinoConnected      = true;
            arduinoLastTempC      = tempC;
            arduinoLastUpdateTime = Time.time;
        }
    }

    /// <summary>Wahr wenn ein frischer Föhn-Wert über der Schwelle vorliegt.</summary>
    bool IsArduinoHeating()
    {
        if (!arduinoConnected) return false;
        if (Time.time - arduinoLastUpdateTime > arduinoTimeoutSec) return false;
        return arduinoLastTempC >= heatThresholdC;
    }

    void Update()
    {
        switch (state)
        {
            case State.WaitingApproach:
                HandleApproach();
                break;

            case State.Heating:
                HandleHeat();
                break;
        }
    }

    // -------------------------------------------------------------------------

    void HandleApproach()
    {
        // Kein [E] mehr – sobald der Spieler am Tor steht, geht das Heat-Panel
        // automatisch auf und der Thermistor wird aktiviert.
        bool near = gateSpot != null && gateSpot.PlayerNearby;
        if (interactionPrompt) interactionPrompt.SetActive(near);
        if (near) OpenHeatPanel();
    }

    void OpenHeatPanel()
    {
        state = State.Heating;
        if (interactionPrompt) interactionPrompt.SetActive(false);
        if (heatPanel)         heatPanel.SetActive(true);
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
        EventSystem.current?.SetSelectedGameObject(null);
        // Thermistor erst hier starten – der Spieler ist jetzt am Tor.
        RequestArduinoStart();
    }

    void HandleHeat()
    {
        if (won) return;

        bool arduinoHeating = IsArduinoHeating();
        bool isHeating      = holding || arduinoHeating;

        float delta = isHeating ? heatSpeed : -coolSpeed;
        temperatureBar.value = Mathf.Clamp01(temperatureBar.value + delta * Time.deltaTime);

        float pct = temperatureBar.value * 100f;
        if (temperatureLabel) temperatureLabel.text = $"{Mathf.RoundToInt(pct)} %";

        if (temperatureBar.fillRect)
        {
            var fill = temperatureBar.fillRect.GetComponent<Image>();
            if (fill) fill.color = Color.Lerp(
                new Color(0.25f, 0.55f, 1.00f),
                new Color(1.00f, 0.30f, 0.00f),
                temperatureBar.value);
        }

        if (statusText)
        {
            // Live-Temperatur immer anzeigen, sobald ein frischer Sensor-Wert vorliegt –
            // auch unterhalb der 40-Grad-Schwelle. Brennt der Brenner aktiv, wird der
            // Wert hervorgehoben.
            bool freshTemp = arduinoConnected
                          && (Time.time - arduinoLastUpdateTime) <= arduinoTimeoutSec;
            string src = freshTemp
                       ? (arduinoHeating
                            ? $"[BRENNER  {arduinoLastTempC:F1}°C]"
                            : $"[SENSOR  {arduinoLastTempC:F1}°C]")
                       : holding ? "[FALLBACK]" : string.Empty;

            string phase =
                  pct <  1f  ? string.Empty
                : pct < 35f  ? "Das Metall wird warm…"
                : pct < 65f  ? "Das Schloss glüht!"
                : pct < 90f  ? "FAST! Nicht aufhören!"
                : pct < 100f ? "JETZT! KURZ VOR DEM DURCHBRUCH!"
                : string.Empty;

            statusText.text = (src.Length > 0 && phase.Length > 0)
                ? $"{src}  {phase}"
                : phase + src;
        }

        if (temperatureBar.value >= 1f && !won)
            StartCoroutine(Win());
    }

    void SetupHeatButton()
    {
        if (!heatButton) return;

        var trigger = heatButton.gameObject.GetComponent<EventTrigger>()
                   ?? heatButton.gameObject.AddComponent<EventTrigger>();

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => { if (!won) holding = true; });
        trigger.triggers.Add(down);

        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => holding = false);
        trigger.triggers.Add(up);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => holding = false);
        trigger.triggers.Add(exit);
    }

    // -------------------------------------------------------------------------

    IEnumerator Win()
    {
        won     = true;
        holding = false;
        state   = State.Done;

        if (statusText)  statusText.text = "SCHLOSS GEKNACKT!";
        if (heatButton)  heatButton.interactable = false;

        yield return new WaitForSeconds(0.8f);
        if (heatPanel) heatPanel.SetActive(false);
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Tor klappt animiert nach oben weg (Stäbe sind am Sturz aufgehängt)
        if (gateBarsGO != null)
        {
            // Kollider sofort aus, damit der Spieler gleich durchlaufen könnte
            foreach (var c in gateBarsGO.GetComponentsInChildren<Collider>())
                c.enabled = false;

            Quaternion start = gateBarsGO.transform.localRotation;
            Quaternion end   = start * Quaternion.Euler(-95f, 0f, 0f);  // hoch klappen
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 0.9f;
                gateBarsGO.transform.localRotation =
                    Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        // Kurzer Moment, den Blick in die Freiheit genießen lassen
        yield return new WaitForSeconds(1.2f);

        if (BigYahuDialogSystem.Instance)
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Big Yahu: Das Tor ist offen … ich kann die Wiese riechen!",
                "Big Yahu: WIR SIND FREI!!!",
                "Big Yahu: Du warst der beste Komplize den man sich wünschen kann. Danke!"
            }, ShowWinScreen);
        else
            ShowWinScreen();
    }

    void ShowWinScreen()
    {
        GameManager.Instance?.StopTimer();
        if (timerText && GameManager.Instance != null)
        {
            float t   = GameManager.Instance.ElapsedSeconds;
            int   min = Mathf.FloorToInt(t / 60f);
            int   sec = Mathf.FloorToInt(t % 60f);
            timerText.text = $"Zeit: {min:00}:{sec:00}";
        }
        if (winOverlay) winOverlay.SetActive(true);
    }

    public void OnRestartClicked()
    {
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (GameManager.Instance != null)
            GameManager.Instance.RestartGame();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Level1");
    }
}
