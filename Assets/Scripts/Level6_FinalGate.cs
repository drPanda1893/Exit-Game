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

    [Header("Card Reader (RFID, vorgeschaltet)")]
    [SerializeField] private GameObject        cardPanel;
    [SerializeField] private TextMeshProUGUI   cardStatusText;
    [Tooltip("Befehls-ID des RFID-Lesers – muss mit Arduino-Sketch (0x80) uebereinstimmen.")]
    [SerializeField] private byte arduinoRfidCmdId = 0x80;

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

    private enum State { Idle, WaitingApproach, CardCheck, Heating, Done }
    private State state = State.Idle;

    private bool  holding;
    private bool  won;
    private bool  arduinoConnected;
    private bool  arduinoSubscribed;
    private float arduinoLastTempC;
    private float arduinoLastUpdateTime;
    private Action<string> tempHandler;
    private Action<string> rfidHandler;
    private Coroutine arduinoStartRoutine;
    private float cardDeniedHideAt;

    void Awake() => Instance = this;

    void OnEnable()
    {
        won     = false;
        holding = false;
        state   = State.WaitingApproach;

        if (interactionPrompt) interactionPrompt.SetActive(false);
        if (cardPanel)         cardPanel.SetActive(false);
        if (heatPanel)         heatPanel.SetActive(false);
        if (winOverlay)        winOverlay.SetActive(false);
        if (gateBarsGO)        gateBarsGO.SetActive(true);
        if (temperatureBar)    temperatureBar.value = 0f;
        if (statusText)        statusText.text = string.Empty;
        if (cardStatusText)    cardStatusText.text = string.Empty;
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
            rfidHandler = OnRfidFromArduino;
            ArduinoBridge.Instance.RegisterHandler(arduinoRfidCmdId, rfidHandler);
            ArduinoBridge.Instance.OnConnectionChanged += HandleArduinoConnectionChanged;
            arduinoSubscribed = true;
        }

        // Intro-Dialog mit Hinweisen zum Bunsenbrenner/Schloss-Erhitzen entfernt –
        // der Spieler soll die Mechanik selbst entdecken.
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
            if (rfidHandler != null)
                ArduinoBridge.Instance.UnregisterHandler(arduinoRfidCmdId, rfidHandler);
            if (arduinoSubscribed)
                ArduinoBridge.Instance.OnConnectionChanged -= HandleArduinoConnectionChanged;
            arduinoSubscribed = false;
            // Thermistor + RFID abschalten, damit andere Level den Sensor wieder
            // sauber initialisieren koennen.
            ArduinoBridge.Instance.Send(arduinoCmdId, "STOP");
            ArduinoBridge.Instance.Send(arduinoRfidCmdId, "STOP");
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

            case State.CardCheck:
                HandleCardCheck();
                break;

            case State.Heating:
                HandleHeat();
                break;
        }
    }

    // -------------------------------------------------------------------------

    void HandleApproach()
    {
        // Sobald der Spieler am Tor ist, oeffnet sich ZUERST der Karten-Check.
        bool near = gateSpot != null && gateSpot.PlayerNearby;
        if (interactionPrompt) interactionPrompt.SetActive(near);
        if (near) OpenCardCheck();
    }

    void OpenCardCheck()
    {
        state = State.CardCheck;
        if (interactionPrompt) interactionPrompt.SetActive(false);
        if (cardPanel)         cardPanel.SetActive(true);
        if (cardStatusText)    cardStatusText.text = "Zugangskarte am Leser scannen…";
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
        EventSystem.current?.SetSelectedGameObject(null);

        // RFID-Leser aktivieren – erst hier, damit andere Level vorher nicht
        // ungewollt SPI-Pins greifen.
        ArduinoBridge.Instance?.Send(arduinoRfidCmdId, "START");
    }

    void HandleCardCheck()
    {
        // Verzoegerung fuer das Ausblenden des "Falsche Karte"-Hinweises:
        // nach kurzer Zeit zurueck auf "Zugangskarte am Leser scannen".
        if (cardDeniedHideAt > 0f && Time.time >= cardDeniedHideAt)
        {
            cardDeniedHideAt = 0f;
            if (cardStatusText) cardStatusText.text = "Zugangskarte am Leser scannen…";
        }
    }

    void OnRfidFromArduino(string payload)
    {
        if (state != State.CardCheck) return;

        if (payload.Equals("OK", StringComparison.OrdinalIgnoreCase))
        {
            if (cardStatusText) cardStatusText.text = "Zugang gewährt.";
            ArduinoBridge.Instance?.Send(arduinoRfidCmdId, "STOP");
            StartCoroutine(CardAcceptedThenOpenHeat());
        }
        else if (payload.StartsWith("DENIED", StringComparison.OrdinalIgnoreCase))
        {
            if (cardStatusText) cardStatusText.text = "FALSCHE KARTE – Zugang verweigert.";
            cardDeniedHideAt = Time.time + 1.6f;
        }
        else if (payload.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            if (cardStatusText) cardStatusText.text = "Leser-Fehler – Karte erneut auflegen.";
            cardDeniedHideAt = Time.time + 1.6f;
        }
    }

    IEnumerator CardAcceptedThenOpenHeat()
    {
        yield return new WaitForSeconds(1.0f);
        if (cardPanel) cardPanel.SetActive(false);
        OpenHeatPanel();
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

        if (temperatureBar.fillRect)
        {
            var fill = temperatureBar.fillRect.GetComponent<Image>();
            if (fill) fill.color = Color.Lerp(
                new Color(0.25f, 0.55f, 1.00f),
                new Color(1.00f, 0.30f, 0.00f),
                temperatureBar.value);
        }

        // Temperatur-Anzeige (Prozent + Sensor-Wert) komplett entfernt –
        // nur noch der reine Balken zeigt den Fortschritt.
        if (temperatureLabel) temperatureLabel.text = string.Empty;
        if (statusText)       statusText.text       = string.Empty;

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

        // Win-Screen erst NACH dem Schluss-Dialog mit Rene Redo zeigen.
        // Hier nur den Freiheits-Dialog von Big Yahu spielen – ShowWinScreen()
        // wird von ReneRedoInteraction aufgerufen, sobald Rene fertig gesprochen hat.
        if (BigYahuDialogSystem.Instance)
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Big Yahu: Das Tor ist offen … ich kann die Wiese riechen!",
                "Big Yahu: WIR SIND FREI!!!",
                "Big Yahu: Da vorne steht jemand … sprich mit ihm."
            });
    }

    /// <summary>
    /// Vom Schluss-NPC (Rene Redo) aufgerufen, nachdem sein Dialog beendet ist.
    /// Stoppt den Timer und blendet den Win-Screen ein.
    /// </summary>
    public void ShowWinScreen()
    {
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
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
