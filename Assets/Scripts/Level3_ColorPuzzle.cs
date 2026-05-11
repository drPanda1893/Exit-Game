using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Level 3 – Phase B: Farbrätsel.
///
/// <para>
/// Vier farbige Buttons; der Spieler gibt die in Phase A (Bibel) entdeckte
/// Sequenz ein. Bei Erfolg wird <see cref="Level3_Controller.NextPhase"/>
/// aufgerufen, bei Fehler blinken die Buttons rot und der State setzt zurück.
/// </para>
///
/// <para>
/// Die Lösung wird vom <see cref="Level3_Controller"/> via <see cref="SetSolution"/>
/// gepusht, bevor das Panel aktiviert wird. Falls Phase A übersprungen wurde,
/// gilt die Default-Sequenz <c>Red, Blue, Yellow, Green</c>.
/// </para>
///
/// <para>
/// Beim Aktivieren des Panels begrüßt Helios automatisch den Spieler über
/// <see cref="BigYahuDialogSystem"/> mit den in <see cref="heliosIntroLines"/>
/// konfigurierten Zeilen.
/// </para>
/// </summary>
public class Level3_ColorPuzzle : MonoBehaviour
{
    /// <summary>Roter Button im Farbrätsel. Index 0 in <see cref="allButtons"/>.</summary>
    [Header("Farb-Buttons")]
    [SerializeField] private Button redButton;

    /// <summary>Grüner Button im Farbrätsel. Index 1 in <see cref="allButtons"/>.</summary>
    [SerializeField] private Button greenButton;

    /// <summary>Blauer Button im Farbrätsel. Index 2 in <see cref="allButtons"/>.</summary>
    [SerializeField] private Button blueButton;

    /// <summary>Gelber Button im Farbrätsel. Index 3 in <see cref="allButtons"/>.</summary>
    [SerializeField] private Button yellowButton;

    /// <summary>
    /// Optionales Feedback-Label für „✓ Korrekt" / „✗ Falsch"-Meldungen.
    /// Bleibt leer, wenn nicht im Inspector zugewiesen.
    /// </summary>
    [Header("Feedback (optional)")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    /// <summary>
    /// Highlight-Farbe beim einzelnen Button-Press (siehe <see cref="FlashButton"/>).
    /// Default Weiß.
    /// </summary>
    [Header("Visuelles Feedback")]
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 1f);

    /// <summary>
    /// Fehler-Farbe für das Falsch-Blinken aller Buttons (siehe <see cref="BlinkButtonsRed"/>).
    /// Default kräftiges Rot.
    /// </summary>
    [SerializeField] private Color errorColor = new Color(0.85f, 0.15f, 0.15f);

    /// <summary>Dauer eines einzelnen Blink-Frames in Sekunden.</summary>
    [Header("Konfiguration")]
    [SerializeField, Range(0.1f, 1f)] private float blinkDuration = 0.3f;

    /// <summary>Anzahl der Blink-Wiederholungen bei Fehler-Sequenz.</summary>
    [SerializeField, Range(1, 4)]     private int   blinkCount    = 2;

    /// <summary>
    /// Optionales Override für das Helios-Portrait. Bleibt leer, wenn das
    /// <see cref="BigYahuDialogSystem"/> bereits ein Helios-Portrait hat;
    /// in dem Fall wird auf <see cref="BigYahuDialogSystem.HeliosPortrait"/>
    /// zurückgegriffen.
    /// </summary>
    [Header("Helios NPC (Phase B Intro)")]
    [Tooltip("Optionales Override. Bleibt leer, falls das BigYahuDialogSystem bereits ein Helios-Portrait hat.")]
    [SerializeField] private Sprite heliosPortrait;

    /// <summary>
    /// Begrüßungs-Zeilen, die Helios beim Phase-B-Start spricht.
    /// Default: drei Zeilen, die den Spieler auf die Farbsequenz hinweisen.
    /// </summary>
    [SerializeField, TextArea(2, 4)] private string[] heliosIntroLines =
    {
        "Helios: Gut. Du erinnerst dich an die Farbsequenz?",
        "Helios: Drücke die Knöpfe in der richtigen Reihenfolge.",
        "Helios: Vier Farben — eine Tür."
    };

    /// <summary>
    /// True, sobald Helios den Spieler einmal begrüßt hat.
    /// Verhindert Re-Trigger bei OnEnable, falls das Panel mehrfach aktiviert wird.
    /// </summary>
    private bool introShown;

    /// <summary>
    /// Default-Lösungssequenz, falls Phase A übersprungen wurde
    /// und kein <see cref="SetSolution"/>-Call erfolgte.
    /// </summary>
    private static readonly string[] DefaultSolutionNames =
        { "Red", "Blue", "Yellow", "Green" };

    /// <summary>
    /// Default-Lösungsfarben parallel zu <see cref="DefaultSolutionNames"/>.
    /// Aktuell informativ; die Validierung läuft über die Namen.
    /// </summary>
    private static readonly Color[] DefaultSolutionColors =
    {
        Color.red,
        new Color(0.2f, 0.4f, 1f),
        Color.yellow,
        Color.green
    };

    /// <summary>
    /// Aktuelle Lösungssequenz (Farbnamen). Wird via <see cref="SetSolution"/>
    /// gepusht oder fällt auf <see cref="DefaultSolutionNames"/> zurück.
    /// </summary>
    private string[] solutionNames;

    /// <summary>Buffer der Spieler-Eingaben in Reihenfolge der Klicks.</summary>
    private readonly List<string> playerInput = new();

    /// <summary>True während der Auswertung — verhindert weitere Eingaben.</summary>
    private bool locked;

    /// <summary>
    /// Cache der Buttons als indexierbares Array für schnellen Zugriff
    /// (Flash, Blink, Reset). Reihenfolge: Red(0), Green(1), Blue(2), Yellow(3) —
    /// entspricht <see cref="ColorIndex"/>.
    /// </summary>
    private Button[] allButtons;

    /// <summary>
    /// Cached die Buttons in einem indexierbaren Array.
    /// Wird vor allen anderen Lifecycle-Aufrufen ausgeführt, damit
    /// <see cref="OnEnable"/> schon auf das Array zugreifen kann.
    /// </summary>
    void Awake()
    {
        allButtons = new[] { redButton, greenButton, blueButton, yellowButton };
    }

    /// <summary>
    /// Lifecycle-Hook beim Aktivieren des Panels:
    /// <list type="number">
    ///   <item>Lädt Default-Lösung, falls keine vorhanden.</item>
    ///   <item>Resettet Eingabe-Buffer und Lock.</item>
    ///   <item>Triggert die Helios-Begrüßung (einmalig).</item>
    /// </list>
    /// </summary>
    void OnEnable()
    {
        if (solutionNames == null || solutionNames.Length == 0)
            solutionNames = DefaultSolutionNames;

        ResetState();
        ShowHeliosIntro();
    }

    /// <summary>
    /// Triggert Helios' Begrüßung beim ersten Phase-B-Aufruf.
    /// Setzt Sprecher + Portrait via <see cref="BigYahuDialogSystem.SetSpeaker"/>
    /// und queued die Zeilen aus <see cref="heliosIntroLines"/>.
    /// </summary>
    /// <remarks>
    /// Wird durch das <see cref="introShown"/>-Flag gegen Re-Trigger geschützt.
    /// Bricht stillschweigend ab, falls kein <see cref="BigYahuDialogSystem"/>
    /// in der Scene existiert.
    /// </remarks>
    void ShowHeliosIntro()
    {
        if (introShown) return;
        var dialog = BigYahuDialogSystem.Instance;
        if (dialog == null) return;

        var portrait = heliosPortrait != null ? heliosPortrait : dialog.HeliosPortrait;
        dialog.SetSpeaker("Helios", portrait);

        if (heliosIntroLines != null && heliosIntroLines.Length > 0)
            dialog.ShowDialog(heliosIntroLines);

        introShown = true;
    }

    /// <summary>
    /// Verkabelt die OnClick-Listener der vier Farb-Buttons, sodass Klicks
    /// auf den korrekten <see cref="Press"/>-Aufruf gemappt werden.
    /// </summary>
    void Start()
    {
        if (redButton    != null) redButton.onClick.AddListener(()    => Press("Red"));
        if (greenButton  != null) greenButton.onClick.AddListener(()  => Press("Green"));
        if (blueButton   != null) blueButton.onClick.AddListener(()   => Press("Blue"));
        if (yellowButton != null) yellowButton.onClick.AddListener(() => Press("Yellow"));
    }

    /// <summary>
    /// Wird vom <see cref="Level3_Controller"/> aufgerufen, sobald Phase A
    /// die Bibel-Farbsequenz liefert. Überschreibt die Default-Lösung.
    /// </summary>
    /// <param name="names">Farbnamen in korrekter Reihenfolge (z.B. ["Red", "Blue", …]).</param>
    /// <param name="colors">Parallele Color-Repräsentation. Aktuell nicht für Validierung verwendet.</param>
    public void SetSolution(string[] names, Color[] colors)
    {
        if (names != null && names.Length > 0)
            solutionNames = names;
    }

    /// <summary>
    /// Verarbeitet einen Button-Klick: Flash-Feedback, in Buffer eintragen,
    /// und bei vollständiger Eingabe Auswertung anstoßen.
    /// </summary>
    /// <param name="color">Farb-Name des gedrückten Buttons ("Red", "Green", "Blue", "Yellow").</param>
    void Press(string color)
    {
        if (locked || playerInput.Count >= solutionNames.Length) return;

        int idx = ColorIndex(color);
        if (idx >= 0)
            StartCoroutine(FlashButton(idx, highlightColor));

        playerInput.Add(color);

        if (playerInput.Count == solutionNames.Length)
            StartCoroutine(Evaluate());
    }

    /// <summary>
    /// Mapped einen Farbnamen auf den Index im <see cref="allButtons"/>-Array.
    /// </summary>
    /// <param name="color">Farbname ("Red", "Green", "Blue", "Yellow").</param>
    /// <returns>0–3 für gültige Farben, -1 für unbekannte.</returns>
    int ColorIndex(string color) => color switch
    {
        "Red"    => 0,
        "Green"  => 1,
        "Blue"   => 2,
        "Yellow" => 3,
        _        => -1
    };

    /// <summary>
    /// Coroutine: validiert die Spieler-Eingabe gegen die Lösung,
    /// triggert Erfolgs- oder Fehler-Feedback und schaltet ggf. zur nächsten Phase.
    /// </summary>
    /// <returns>Yield-Iterator für Unity's Coroutine-System.</returns>
    IEnumerator Evaluate()
    {
        locked = true;
        yield return new WaitForSeconds(0.25f);

        bool correct = SequenceMatches();

        if (correct)
        {
            if (feedbackText != null) feedbackText.text = "✓ Korrekte Sequenz!";
            yield return StartCoroutine(SuccessSequence());
            Level3_Controller.NextPhase();
        }
        else
        {
            if (feedbackText != null) feedbackText.text = "✗ Falsche Reihenfolge!";
            yield return StartCoroutine(BlinkButtonsRed());
            ResetState();
        }
    }

    /// <summary>
    /// Vergleicht <see cref="playerInput"/> elementweise mit <see cref="solutionNames"/>.
    /// </summary>
    /// <returns>True bei exakter Übereinstimmung, sonst false.</returns>
    bool SequenceMatches()
    {
        for (int i = 0; i < solutionNames.Length; i++)
        {
            if (playerInput[i] != solutionNames[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Coroutine: lässt alle Buttons mehrfach in <see cref="errorColor"/> blinken,
    /// bevor die ursprünglichen Farben wiederhergestellt werden.
    /// </summary>
    /// <returns>Yield-Iterator für Unity's Coroutine-System.</returns>
    IEnumerator BlinkButtonsRed()
    {
        var originals = new Color[allButtons.Length];

        for (int i = 0; i < allButtons.Length; i++)
        {
            if (allButtons[i] != null)
                originals[i] = allButtons[i].colors.normalColor;
        }

        for (int blink = 0; blink < blinkCount; blink++)
        {
            ApplyButtonColor(errorColor);
            yield return new WaitForSeconds(blinkDuration);
            ApplyButtonColors(originals);
            yield return new WaitForSeconds(blinkDuration * 0.6f);
        }
    }

    /// <summary>
    /// Lässt einen einzelnen Button kurz in <paramref name="targetColor"/> aufleuchten
    /// und stellt danach die Original-Farbe wieder her.
    /// </summary>
    /// <param name="index">Index im <see cref="allButtons"/>-Array (0–3).</param>
    /// <param name="targetColor">Farbe des Aufleuchtens.</param>
    /// <returns>Yield-Iterator für Unity's Coroutine-System.</returns>
    public IEnumerator FlashButton(int index, Color targetColor)
    {
        if (allButtons == null || index < 0 || index >= allButtons.Length) yield break;
        var btn = allButtons[index];
        if (btn == null) yield break;

        var cb = btn.colors;
        var original = cb.normalColor;

        cb.normalColor = targetColor;
        btn.colors = cb;

        yield return new WaitForSeconds(blinkDuration);

        cb = btn.colors;
        cb.normalColor = original;
        btn.colors = cb;
    }

    /// <summary>
    /// Public Wrapper, der die <see cref="SuccessSequence"/>-Coroutine startet.
    /// Für externe Trigger gedacht (z.B. Cheat-Buttons im Editor).
    /// </summary>
    public void ShowSuccess() => StartCoroutine(SuccessSequence());

    /// <summary>
    /// Coroutine: lässt alle vier Buttons nacheinander grün blinken
    /// als visuelles Erfolgs-Feedback.
    /// </summary>
    /// <returns>Yield-Iterator für Unity's Coroutine-System.</returns>
    IEnumerator SuccessSequence()
    {
        Color successGreen = new Color(0.20f, 0.85f, 0.30f);
        for (int i = 0; i < allButtons.Length; i++)
            yield return StartCoroutine(FlashButton(i, successGreen));
    }

    /// <summary>
    /// Setzt die <c>normalColor</c> aller Buttons auf <paramref name="c"/>.
    /// </summary>
    /// <param name="c">Neue Farbe für alle Buttons.</param>
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

    /// <summary>
    /// Setzt die <c>normalColor</c>-Werte aller Buttons aus dem Array
    /// <paramref name="colors"/> (parallel zu <see cref="allButtons"/> indiziert).
    /// </summary>
    /// <param name="colors">Farb-Array, gleiche Länge wie <see cref="allButtons"/>.</param>
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

    /// <summary>
    /// Setzt Eingabe-Buffer und Lock-Flag zurück, leert das Feedback-Label.
    /// Wird beim Panel-Enable und nach Fehler-Sequenzen aufgerufen.
    /// </summary>
    void ResetState()
    {
        playerInput.Clear();
        locked = false;
        if (feedbackText != null) feedbackText.text = string.Empty;
    }
}
