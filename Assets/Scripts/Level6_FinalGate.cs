using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
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

    private enum State { Idle, WaitingApproach, Heating, Done }
    private State state = State.Idle;

    private bool holding;
    private bool won;

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

        if (BigYahuDialogSystem.Instance)
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Big Yahu: Das ist es – das letzte Tor. Dahinter liegt die Freiheit!",
                "Big Yahu: Ich hab den Bunsenbrenner dabei...",
                "Big Yahu: Geh zum Schloss und halt ihn ans Metall!"
            });
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

        float delta = holding ? heatSpeed : -coolSpeed;
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
            if      (pct <  1f)  statusText.text = string.Empty;
            else if (pct < 35f)  statusText.text = "Das Metall wird warm...";
            else if (pct < 65f)  statusText.text = "Das Schloss glüht!";
            else if (pct < 90f)  statusText.text = "FAST! Nicht aufhören!";
            else if (pct < 100f) statusText.text = "JETZT! KURZ VOR DEM DURCHBRUCH!";
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

        if (gateBarsGO) gateBarsGO.SetActive(false);

        yield return new WaitForSeconds(0.7f);

        if (BigYahuDialogSystem.Instance)
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Big Yahu: WIR SIND FREI!!!",
                "Big Yahu: Ich kann's kaum glauben – nach all dem haben wir es wirklich geschafft!",
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
