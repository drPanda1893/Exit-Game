using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Level 6 – Das finale Gefängnistor.
///
/// Spieler hält den Bunsenbrenner-Button gedrückt → Schloss erhitzt sich.
/// Temperatur-Bar füllt sich (0 → 100 %).
/// Loslassen → kühlt langsam ab.
/// Bei 100 % → Schloss bricht → Big Yahu ist frei!
/// Win-Screen wird direkt in dieser Szene gezeigt (kein CompleteCurrentLevel).
/// </summary>
public class Level6_FinalGate : MonoBehaviour
{
    [Header("Puzzle UI")]
    [SerializeField] private Slider    temperatureBar;
    [SerializeField] private TextMeshProUGUI temperatureLabel;   // "73 %"
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button    heatButton;
    [SerializeField] private Image     lockImage;
    [SerializeField] private Sprite    lockOpenSprite;           // Aufgebrochenes Schloss

    [Header("Gate Visual")]
    [SerializeField] private GameObject gateClosedVisual;        // Torgitter (optional)
    [SerializeField] private GameObject gateOpenVisual;          // Offenes Tor (optional)

    [Header("Win Screen")]
    [SerializeField] private GameObject winOverlay;
    [SerializeField] private Button     restartButton;

    [Header("Einstellungen")]
    [SerializeField] private float heatSpeed = 0.10f;   // Bar-Füllung / Sekunde beim Halten
    [SerializeField] private float coolSpeed = 0.04f;   // Abkühlung / Sekunde beim Loslassen

    private bool holding;
    private bool won;

    // -------------------------------------------------------------------------

    void OnEnable()
    {
        won     = false;
        holding = false;

        if (temperatureBar)   temperatureBar.value = 0f;
        if (statusText)       statusText.text       = string.Empty;
        if (winOverlay)       winOverlay.SetActive(false);
        if (gateOpenVisual)   gateOpenVisual.SetActive(false);
        if (gateClosedVisual) gateClosedVisual.SetActive(true);

        if (BigYahuDialogSystem.Instance)
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Big Yahu: Das ist es – das letzte Tor. Dahinter liegt die Freiheit!",
                "Big Yahu: Ich hab hier einen Bunsenbrenner gefunden...",
                "Big Yahu: Halte ihn ans Schloss bis die Temperatur 100 % erreicht!"
            });
    }

    void Start()
    {
        SetupHeatButton();
        if (restartButton) restartButton.onClick.AddListener(OnRestartClicked);
    }

    // -------------------------------------------------------------------------

    void SetupHeatButton()
    {
        if (!heatButton) return;

        EventTrigger trigger = heatButton.gameObject.GetComponent<EventTrigger>()
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

    void Update()
    {
        if (won) return;

        float delta = holding ? heatSpeed : -coolSpeed;
        temperatureBar.value = Mathf.Clamp01(temperatureBar.value + delta * Time.deltaTime);

        float pct = temperatureBar.value * 100f;
        if (temperatureLabel) temperatureLabel.text = $"{Mathf.RoundToInt(pct)} %";

        // Bar-Farbe: blau (kalt) → orange/rot (heiß)
        if (temperatureBar.fillRect)
        {
            Image fill = temperatureBar.fillRect.GetComponent<Image>();
            if (fill) fill.color = Color.Lerp(
                new Color(0.25f, 0.55f, 1.00f),
                new Color(1.00f, 0.30f, 0.00f),
                temperatureBar.value);
        }

        // Stufenweise Statusmeldungen
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

    // -------------------------------------------------------------------------

    IEnumerator Win()
    {
        won     = true;
        holding = false;

        if (statusText)  statusText.text  = "SCHLOSS GEKNACKT!";
        if (heatButton)  heatButton.interactable = false;

        // Schloss öffnen
        if (lockImage && lockOpenSprite) lockImage.sprite = lockOpenSprite;
        if (gateClosedVisual) gateClosedVisual.SetActive(false);
        if (gateOpenVisual)   gateOpenVisual.SetActive(true);

        yield return new WaitForSeconds(1.5f);

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
        if (winOverlay) winOverlay.SetActive(true);
    }

    public void OnRestartClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RestartGame();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Level1");
    }
}
