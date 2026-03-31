using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Level 6 – Das Tor.
/// Spieler hält den "Erhitzen"-Button gedrückt.
/// Progressbar füllt sich (Temperatur steigt).
/// Bei 100% → Schloss bricht auf → Spiel gewonnen.
/// Loslassen → Temperatur kühlt langsam ab.
/// </summary>
public class Level6_FinalGate : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider temperatureBar;
    [SerializeField] private TextMeshProUGUI temperatureLabel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button heatButton;
    [SerializeField] private Image lockImage;          // Schloss-Bild
    [SerializeField] private Sprite lockOpenSprite;    // Offenes Schloss (optional)

    [Header("Einstellungen")]
    [SerializeField] private float heatSpeed = 0.12f;  // Füllung pro Sekunde beim Halten
    [SerializeField] private float coolSpeed = 0.05f;  // Abkühlung pro Sekunde beim Loslassen

    private bool holding;
    private bool won;

    void OnEnable()
    {
        won = false;
        holding = false;
        if (temperatureBar) temperatureBar.value = 0f;
        if (statusText) statusText.text = string.Empty;

        BigYahuDialogSystem.Instance.ShowDialog(new[]
        {
            "Big Yahu: Das Schloss! Wir brauchen Hitze um es zu knacken!",
            "Big Yahu: Halte den Erhitzen-Knopf so lange gedrückt bis die Temperatur 100% erreicht!"
        });
    }

    void Start()
    {
        // EventTrigger für PointerDown / PointerUp auf dem Button
        EventTrigger trigger = heatButton.gameObject.AddComponent<EventTrigger>();

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => holding = true);
        trigger.triggers.Add(down);

        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => holding = false);
        trigger.triggers.Add(up);

        // Auch Maus-Verlassen des Buttons berücksichtigen
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => holding = false);
        trigger.triggers.Add(exit);
    }

    void Update()
    {
        if (won) return;

        float delta = holding ? heatSpeed : -coolSpeed;
        temperatureBar.value = Mathf.Clamp01(temperatureBar.value + delta * Time.deltaTime);

        int pct = Mathf.RoundToInt(temperatureBar.value * 100f);
        if (temperatureLabel) temperatureLabel.text = $"{pct}°C";

        // Farbe der Bar: kalt=blau → heiß=rot
        if (temperatureBar.fillRect)
        {
            Image fill = temperatureBar.fillRect.GetComponent<Image>();
            if (fill) fill.color = Color.Lerp(new Color(0.3f, 0.6f, 1f), new Color(1f, 0.15f, 0f), temperatureBar.value);
        }

        if (temperatureBar.value >= 1f)
            StartCoroutine(Win());
    }

    IEnumerator Win()
    {
        won = true;
        holding = false;

        if (statusText) statusText.text = "✓ Schloss geknackt!";
        if (lockImage && lockOpenSprite) lockImage.sprite = lockOpenSprite;
        if (heatButton) heatButton.interactable = false;

        yield return new WaitForSeconds(1.2f);

        BigYahuDialogSystem.Instance.ShowDialog(new[]
        {
            "Big Yahu: WIR SIND FREI!!!",
            "Big Yahu: Ich hab's immer gewusst – du schaffst das! Herzlichen Glückwunsch!"
        }, () => GameManager.Instance.CompleteCurrentLevel());
    }
}
