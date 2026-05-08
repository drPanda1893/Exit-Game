using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Level 3 – Phase B: Schlankes Farbrätsel.
///
/// Vier farbige Buttons. Der Spieler gibt die Sequenz aus Phase A (Bibel) ein.
/// Bei Erfolg → Level3_Controller.NextPhase().
/// Bei Fehler → Reset + kurzes rotes Blinken aller Buttons.
///
/// Lösung wird vom Level3_Controller via SetSolution(...) gepusht, bevor das
/// Panel aktiviert wird. Falls Phase A übersprungen wurde, gilt eine Default-Sequenz.
/// </summary>
public class Level3_ColorPuzzle : MonoBehaviour
{
    [Header("Farb-Buttons")]
    [SerializeField] private Button redButton;
    [SerializeField] private Button greenButton;
    [SerializeField] private Button blueButton;
    [SerializeField] private Button yellowButton;

    [Header("Feedback (optional)")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Konfiguration")]
    [SerializeField, Range(0.1f, 1f)] private float blinkDuration = 0.3f;
    [SerializeField, Range(1, 4)]     private int   blinkCount    = 2;

    private static readonly string[] DefaultSolutionNames =
        { "Red", "Blue", "Yellow", "Green" };

    private static readonly Color[] DefaultSolutionColors =
    {
        Color.red,
        new Color(0.2f, 0.4f, 1f),
        Color.yellow,
        Color.green
    };

    private string[] solutionNames;
    private readonly List<string> playerInput = new();
    private bool locked;
    private Button[] allButtons;

    // ══════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════

    void Awake()
    {
        allButtons = new[] { redButton, greenButton, blueButton, yellowButton };
    }

    void OnEnable()
    {
        if (solutionNames == null || solutionNames.Length == 0)
            solutionNames = DefaultSolutionNames;

        ResetState();
    }

    void Start()
    {
        if (redButton    != null) redButton.onClick.AddListener(()    => Press("Red"));
        if (greenButton  != null) greenButton.onClick.AddListener(()  => Press("Green"));
        if (blueButton   != null) blueButton.onClick.AddListener(()   => Press("Blue"));
        if (yellowButton != null) yellowButton.onClick.AddListener(() => Press("Yellow"));
    }

    // ══════════════════════════════════════════════════════════
    // API für Level3_Controller
    // ══════════════════════════════════════════════════════════

    /// <summary>Wird vom Level3_Controller aufgerufen, sobald Phase A die Bibel-Farben liefert.</summary>
    public void SetSolution(string[] names, Color[] colors)
    {
        if (names != null && names.Length > 0)
            solutionNames = names;
    }

    // ══════════════════════════════════════════════════════════
    // Eingabe
    // ══════════════════════════════════════════════════════════

    void Press(string color)
    {
        if (locked || playerInput.Count >= solutionNames.Length) return;

        playerInput.Add(color);

        if (playerInput.Count == solutionNames.Length)
            StartCoroutine(Evaluate());
    }

    // ══════════════════════════════════════════════════════════
    // Auswertung
    // ══════════════════════════════════════════════════════════

    IEnumerator Evaluate()
    {
        locked = true;
        yield return new WaitForSeconds(0.25f);

        bool correct = SequenceMatches();

        if (correct)
        {
            if (feedbackText != null) feedbackText.text = "✓ Korrekte Sequenz!";
            yield return new WaitForSeconds(0.7f);
            Level3_Controller.NextPhase();
        }
        else
        {
            if (feedbackText != null) feedbackText.text = "✗ Falsche Reihenfolge!";
            yield return StartCoroutine(BlinkButtonsRed());
            ResetState();
        }
    }

    bool SequenceMatches()
    {
        for (int i = 0; i < solutionNames.Length; i++)
        {
            if (playerInput[i] != solutionNames[i]) return false;
        }
        return true;
    }

    // ══════════════════════════════════════════════════════════
    // Visuelles Fehler-Feedback
    // ══════════════════════════════════════════════════════════

    IEnumerator BlinkButtonsRed()
    {
        Color errorRed = new Color(0.85f, 0.15f, 0.15f);
        var originals = new Color[allButtons.Length];

        for (int i = 0; i < allButtons.Length; i++)
        {
            if (allButtons[i] != null)
                originals[i] = allButtons[i].colors.normalColor;
        }

        for (int blink = 0; blink < blinkCount; blink++)
        {
            ApplyButtonColor(errorRed);
            yield return new WaitForSeconds(blinkDuration);
            ApplyButtonColors(originals);
            yield return new WaitForSeconds(blinkDuration * 0.6f);
        }
    }

    void ApplyButtonColor(Color c)
    {
        foreach (var b in allButtons)
        {
            if (b == null) continue;
            var cb = b.colors;
            cb.normalColor = c;
            b.colors = cb;
        }
    }

    void ApplyButtonColors(Color[] colors)
    {
        for (int i = 0; i < allButtons.Length; i++)
        {
            if (allButtons[i] == null) continue;
            var cb = allButtons[i].colors;
            cb.normalColor = colors[i];
            allButtons[i].colors = cb;
        }
    }

    // ══════════════════════════════════════════════════════════
    // Helfer
    // ══════════════════════════════════════════════════════════

    void ResetState()
    {
        playerInput.Clear();
        locked = false;
        if (feedbackText != null) feedbackText.text = string.Empty;
    }
}
