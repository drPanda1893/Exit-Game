using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Level 3 – Phase A: Bücherregal & Bibel.
///
/// Ablauf:
///   1. Helios begrüßt den Spieler und gibt einen Hinweis.
///   2. Bücherregal mit N Büchern wird angezeigt.
///   3. Falsche Wahl → Helios-Kommentar + visuelles Feedback.
///   4. Bibel gewählt → Buch öffnet sich, Seite mit Farbhinweisen erscheint.
///   5. Spieler bestätigt → Level3_Controller.NotifyBookSolved().
///
/// Unity-Setup:
///   - Dieses Script liegt auf dem Phase-A-Panel (Child von Level3-Root).
///   - bookButtons[]: Array von Buttons, einer davon ist die Bibel.
///   - bibleIndex: Index des Bibel-Buttons im Array.
///   - openBookPanel: Panel, das die aufgeschlagene Bibel-Seite zeigt.
/// </summary>
public class Level3_BookSelection : MonoBehaviour
{
    // ── Bücherregal ───────────────────────────────────────────
    [Header("Bücherregal")]
    [Tooltip("Alle Buch-Buttons im Regal. Einer davon ist die Bibel.")]
    [SerializeField] private Button[] bookButtons;

    [Tooltip("Buch-Titel (TextMeshPro Labels, gleiche Reihenfolge wie bookButtons).")]
    [SerializeField] private TextMeshProUGUI[] bookLabels;

    [Tooltip("Index des Bibel-Buttons innerhalb des bookButtons-Arrays.")]
    [SerializeField] private int bibleIndex = 2;

    // ── Aufgeschlagene Bibel ──────────────────────────────────
    [Header("Bibel-Seite")]
    [Tooltip("Panel, das die geöffnete Bibel-Seite darstellt.")]
    [SerializeField] private GameObject openBookPanel;

    [Tooltip("UI-Image-Elemente auf der Bibelseite, die die Farb-Hinweise zeigen.")]
    [SerializeField] private Image[] colorHintImages;

    [Tooltip("Seitenzahl-Anzeige (z.B. 'Seite 316').")]
    [SerializeField] private TextMeshProUGUI pageNumberText;

    [Tooltip("Button zum Bestätigen / Weiterblättern nach dem Lesen.")]
    [SerializeField] private Button confirmReadButton;

    // ── NPC-Dialog ────────────────────────────────────────────
    [Header("Helios NPC")]
    [Tooltip("Portrait-Sprite für Helios (wird an BigYahuDialogSystem übergeben).")]
    [SerializeField] private Sprite heliosPortrait;

    // ── Feedback ──────────────────────────────────────────────
    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    // ── Konfiguration ─────────────────────────────────────────
    [Header("Farbsequenz-Hinweis (auf der Bibel-Seite)")]
    [SerializeField] private Color[] hintColors = {
        Color.red,
        new Color(0.2f, 0.4f, 1f),   // Blau
        Color.yellow,
        Color.green
    };

    [SerializeField] private string pageNumber = "316";

    // ── Bücherliste (Default-Titel) ───────────────────────────
    private static readonly string[] DefaultBookTitles =
    {
        "Erta Ale",
        "Erta Ale II",
        "Die Bibel",
        "Erta Ale III",
        "Codex Mechanicus",
        "Prisma der Schatten"
    };

    // ── State ─────────────────────────────────────────────────
    private bool selectionLocked;
    private bool bookOpened;

    // ══════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════

    void OnEnable()
    {
        selectionLocked = false;
        bookOpened = false;

        if (openBookPanel != null)
            openBookPanel.SetActive(false);

        ShowHeliosIntro();
    }

    void Start()
    {
        InitBookshelf();

        if (confirmReadButton != null)
            confirmReadButton.onClick.AddListener(OnConfirmRead);
    }

    // ══════════════════════════════════════════════════════════
    // Helios NPC Dialog
    // ══════════════════════════════════════════════════════════

    void ShowHeliosIntro()
    {
        var dialog = BigYahuDialogSystem.Instance;

        // Dynamisch auf Helios umschalten
        dialog.SetSpeaker("Helios", heliosPortrait);

        dialog.ShowDialog(new[]
        {
            "Helios: Willkommen in der Bibliothek.",
            "Helios: Eines dieser Bücher birgt den Schlüssel zur nächsten Tür.",
            "Helios: Such das Buch des Lichts … und schlage es auf."
        });
    }

    void ShowHeliosWrongBook(string bookTitle)
    {
        var dialog = BigYahuDialogSystem.Instance;
        dialog.SetSpeaker("Helios", heliosPortrait);

        // Verschiedene Reaktionen für Abwechslung
        string[] responses =
        {
            $"Helios: '{bookTitle}'? Nein … das ist nicht das richtige Buch.",
            $"Helios: Interessante Wahl, aber '{bookTitle}' hilft dir hier nicht weiter.",
            $"Helios: Versuch es nochmal. Das Buch des Lichts ist ein anderes."
        };

        int pick = Random.Range(0, responses.Length);
        dialog.ShowDialog(new[] { responses[pick] });
    }

    void ShowHeliosCorrectBook()
    {
        var dialog = BigYahuDialogSystem.Instance;
        dialog.SetSpeaker("Helios", heliosPortrait);

        dialog.ShowDialog(new[]
        {
            "Helios: Ja! Die Bibel – das Buch des Lichts.",
            $"Helios: Schlag Seite {pageNumber} auf … dort findest du den Code."
        });
    }

    // ══════════════════════════════════════════════════════════
    // Bücherregal-Logik
    // ══════════════════════════════════════════════════════════

    void InitBookshelf()
    {
        for (int i = 0; i < bookButtons.Length; i++)
        {
            // Labels setzen (falls kein manueller Text im Editor)
            if (bookLabels != null && i < bookLabels.Length &&
                string.IsNullOrEmpty(bookLabels[i].text) &&
                i < DefaultBookTitles.Length)
            {
                bookLabels[i].text = DefaultBookTitles[i];
            }

            int index = i; // Closure-Capture
            bookButtons[i].onClick.AddListener(() => OnBookSelected(index));
        }
    }

    void OnBookSelected(int index)
    {
        if (selectionLocked || bookOpened) return;

        if (index == bibleIndex)
        {
            StartCoroutine(HandleCorrectBook());
        }
        else
        {
            StartCoroutine(HandleWrongBook(index));
        }
    }

    // ── Falsche Wahl ──────────────────────────────────────────
    IEnumerator HandleWrongBook(int index)
    {
        selectionLocked = true;

        // Visuelles Feedback: Button kurz rot blinken
        var colors = bookButtons[index].colors;
        var original = colors.normalColor;
        colors.normalColor = new Color(0.8f, 0.2f, 0.2f);
        bookButtons[index].colors = colors;

        string title = (bookLabels != null && index < bookLabels.Length)
            ? bookLabels[index].text
            : "Unbekannt";

        ShowHeliosWrongBook(title);

        if (feedbackText != null)
            feedbackText.text = "Falsches Buch …";

        yield return new WaitForSeconds(1.5f);

        // Reset
        colors.normalColor = original;
        bookButtons[index].colors = colors;

        if (feedbackText != null)
            feedbackText.text = string.Empty;

        selectionLocked = false;
    }

    // ── Richtige Wahl (Bibel) ─────────────────────────────────
    IEnumerator HandleCorrectBook()
    {
        selectionLocked = true;
        bookOpened = true;

        // Visuelles Feedback: Button grün
        var colors = bookButtons[bibleIndex].colors;
        colors.normalColor = new Color(0.2f, 0.8f, 0.3f);
        bookButtons[bibleIndex].colors = colors;

        if (feedbackText != null)
            feedbackText.text = "✓ Die Bibel!";

        ShowHeliosCorrectBook();

        yield return new WaitForSeconds(2.0f);

        // Bibel-Seite öffnen
        OpenBiblePage();
    }

    // ══════════════════════════════════════════════════════════
    // Bibel-Seite (Farb-Hinweise anzeigen)
    // ══════════════════════════════════════════════════════════

    void OpenBiblePage()
    {
        if (openBookPanel == null) return;

        openBookPanel.SetActive(true);

        // Seitenzahl anzeigen
        if (pageNumberText != null)
            pageNumberText.text = $"Seite {pageNumber}";

        // Farb-Hinweise in die Image-Elemente schreiben
        if (colorHintImages != null)
        {
            for (int i = 0; i < colorHintImages.Length && i < hintColors.Length; i++)
            {
                colorHintImages[i].color = hintColors[i];
                colorHintImages[i].gameObject.SetActive(true);
            }
        }
    }

    // ── Spieler hat die Seite gelesen → weiter zu Phase B ─────
    void OnConfirmRead()
    {
        if (!bookOpened) return;

        Level3_Controller.NotifyBookSolved();
    }

    // ══════════════════════════════════════════════════════════
    // Cleanup
    // ══════════════════════════════════════════════════════════

    void OnDisable()
    {
        // Listener entfernen, um Memory Leaks zu vermeiden
        if (confirmReadButton != null)
            confirmReadButton.onClick.RemoveListener(OnConfirmRead);

        foreach (var btn in bookButtons)
            btn.onClick.RemoveAllListeners();
    }
}
