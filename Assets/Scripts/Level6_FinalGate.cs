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

    [Header("Arduino (Temperatursensor / Föhn)")]
    [Tooltip("Befehls-ID des Temperatursensors (Standard 0x60).")]
    [SerializeField] private byte arduinoCmdId = 0x60;
    [Tooltip("Schwelle in Grad Celsius, ab der der Föhn als 'aktiv' zählt.")]
    [SerializeField] private float heatThresholdC = 32f;
    [Tooltip("Sekunden Karenz nach letztem Sensor-Wert, bevor wieder abgekühlt wird.")]
    [SerializeField] private float arduinoTimeoutSec = 0.6f;

    private enum State { Idle, WaitingApproach, Heating, Done }
    private State state = State.Idle;

    private bool  holding;
    private bool  won;
    private bool  arduinoConnected;
    private float arduinoLastTempC;
    private float arduinoLastUpdateTime;
    private Action<string> tempHandler;

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

        // Arduino-Temperatursensor (Befehl 0x60) – physikalischer Föhn vor Sensor
        // halten erhitzt das Tor. Wenn kein Arduino vorhanden, greift der
        // Maus-Fallback ("BRENNER HALTEN").
        if (ArduinoBridge.Instance != null)
        {
            tempHandler = OnTemperatureFromArduino;
            ArduinoBridge.Instance.RegisterHandler(arduinoCmdId, tempHandler);
        }

        if (BigYahuDialogSystem.Instance)
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Big Yahu: Das ist es – das letzte Tor. Dahinter liegt die Freiheit!",
                "Big Yahu: Ich hab den Bunsenbrenner aus der Werkstatt dabei …",
                "Big Yahu: Halt den Föhn vor den Sensor – oder den Brenner-Button gedrückt!"
            });
    }

    void OnDisable()
    {
        if (ArduinoBridge.Instance != null && tempHandler != null)
            ArduinoBridge.Instance.UnregisterHandler(arduinoCmdId, tempHandler);
    }

    // -------------------------------------------------------------------------
    // Arduino-Eingang: empfängt "TEMP:42" oder rohen Wert "42"
    // -------------------------------------------------------------------------

    void OnTemperatureFromArduino(string payload)
    {
        string val = payload;
        if (val.StartsWith("TEMP:", StringComparison.Ordinal)) val = val.Substring(5);

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
        bool near = gateSpot != null && gateSpot.PlayerNearby;
        if (interactionPrompt) interactionPrompt.SetActive(near);
        if (near && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
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
            // Eingangsquelle anzeigen, damit klar ist welche Eingabe gerade zählt
            string src = arduinoHeating ? $"[FÖHN  {arduinoLastTempC:F0}°C]"
                       : holding        ? "[BRENNER]"
                       : string.Empty;

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
