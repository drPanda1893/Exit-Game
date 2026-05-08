using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Level 3 – Phase B: Farbcode-Eingabe.
///
/// Empfängt die Lösung (4 Farbnamen + Color-Werte) von Level3_Controller,
/// nachdem Phase A (Bibel) abgeschlossen wurde. Der Spieler gibt die Sequenz
/// über vier farbige UI-Buttons ein. Falsche Eingabe → Buttons blinken rot,
/// Reset. Richtige Eingabe → Level3_Controller.NextPhase().
///
/// Arduino-Modus (0x20): Farb-Sensor scannt dieselben Namen.
/// </summary>
public class Level3_ColorCode : MonoBehaviour
{
    [Header("Farb-Buttons (Fallback-Modus)")]
    [SerializeField] private Button redButton;
    [SerializeField] private Button greenButton;
    [SerializeField] private Button blueButton;
    [SerializeField] private Button yellowButton;

    [Header("Sequenz-Anzeige (4 Indikatoren)")]
    [SerializeField] private Image[] sequenceIndicators;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Arduino-Integration")]
    [Tooltip("Wenn true, werden UI-Buttons als Fallback angezeigt.")]
    [SerializeField] private bool arduinoFallback = true;
    [SerializeField] private GameObject fallbackButtonPanel;
    [SerializeField] private TextMeshProUGUI arduinoInstructionText;

    [Header("Helios NPC")]
    [SerializeField] private Sprite heliosPortrait;

    // Fallback-Lösung, wenn Phase A keine Lösung übergibt
    private static readonly string[] DefaultSolutionNames = { "Red", "Blue", "Yellow", "Green" };
    private static readonly Color[] DefaultSolutionColors =
    {
        Color.red,
        new Color(0.2f, 0.4f, 1f),
        Color.yellow,
        Color.green
    };

    private string[] solutionNames;
    private Color[] solutionColors;
    private readonly List<string> playerInput = new();
    private bool locked;
    private Button[] allColorButtons;

    // ══════════════════════════════════════════════════════════
    // API für Level3_Controller
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Wird von Level3_Controller aufgerufen, sobald Phase A die Bibel-Farben liefert.
    /// </summary>
    public void SetSolution(string[] names, Color[] colors)
    {
        solutionNames = names;
        solutionColors = colors;
    }

    // ══════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════

    void Awake()
    {
        allColorButtons = new[] { redButton, greenButton, blueButton, yellowButton };
    }

    void OnEnable()
    {
        if (solutionNames == null || solutionNames.Length == 0)
        {
            solutionNames = DefaultSolutionNames;
            solutionColors = DefaultSolutionColors;
        }

        playerInput.Clear();
        locked = false;
        ResetIndicators();

        if (feedbackText != null)
            feedbackText.text = string.Empty;

        ConfigureInputMode();
        ShowHeliosColorPrompt();
    }

    void Start()
    {
        redButton.onClick.AddListener(()    => Press("Red",    Color.red));
        blueButton.onClick.AddListener(()   => Press("Blue",   new Color(0.2f, 0.4f, 1f)));
        yellowButton.onClick.AddListener(() => Press("Yellow", Color.yellow));
        greenButton.onClick.AddListener(()  => Press("Green",  Color.green));
    }

    void OnDisable()
    {
        // ArduinoBridge.OnColorReading -= OnArduinoColor;
    }

    // ══════════════════════════════════════════════════════════
    // Input-Modus
    // ══════════════════════════════════════════════════════════

    void ConfigureInputMode()
    {
        bool useArduino = !arduinoFallback && IsArduinoConnected();

        if (fallbackButtonPanel != null)
            fallbackButtonPanel.SetActive(!useArduino);

        if (arduinoInstructionText != null)
        {
            arduinoInstructionText.gameObject.SetActive(useArduino);
            arduinoInstructionText.text = "Halte den Farb-Sensor an die richtige Farbe …";
        }

        // if (useArduino) ArduinoBridge.Instance.OnColorReading += OnArduinoColor;
    }

    bool IsArduinoConnected() => false;
    // TODO: return ArduinoBridge.Instance != null && ArduinoBridge.Instance.IsConnected;

    // ══════════════════════════════════════════════════════════
    // NPC Dialog
    // ══════════════════════════════════════════════════════════

    void ShowHeliosColorPrompt()
    {
        var dialog = BigYahuDialogSystem.Instance;
        if (dialog == null) { Debug.LogWarning("[Level3] BigYahuDialogSystem nicht in Scene gefunden."); return; }
        dialog.SetSpeaker("Helios", heliosPortrait);

        if (arduinoFallback || !IsArduinoConnected())
        {
            dialog.ShowDialog(new[]
            {
                "Helios: Du kennst jetzt die Farbsequenz aus der Bibel.",
                "Helios: Gib sie über die Tasten ein – in der richtigen Reihenfolge!"
            });
        }
        else
        {
            dialog.ShowDialog(new[]
            {
                "Helios: Nimm den Farb-Sensor und scanne die Farben!",
                "Helios: Die Reihenfolge aus der Bibel – genau so."
            });
        }
    }

    // ══════════════════════════════════════════════════════════
    // Eingabe
    // ══════════════════════════════════════════════════════════

    /// <summary>Wird von UI-Buttons ODER Arduino-Callback aufgerufen.</summary>
    void Press(string color, Color uiColor)
    {
        if (locked || playerInput.Count >= solutionNames.Length) return;

        playerInput.Add(color);
        int idx = playerInput.Count - 1;

        if (sequenceIndicators != null && idx < sequenceIndicators.Length)
            sequenceIndicators[idx].color = uiColor;

        if (playerInput.Count == solutionNames.Length)
            StartCoroutine(Evaluate());
    }

    /// <summary>Arduino-Farbsensor Callback (Command 0x20). Format: "COLOR:Red"</summary>
    void OnArduinoColor(string sensorValue)
    {
        string colorName = sensorValue.Replace("COLOR:", "").Trim();

        Color uiColor = colorName switch
        {
            "Red"    => Color.red,
            "Blue"   => new Color(0.2f, 0.4f, 1f),
            "Yellow" => Color.yellow,
            "Green"  => Color.green,
            _        => Color.gray
        };

        if (uiColor == Color.gray)
        {
            Debug.LogWarning($"[Level3] Unbekannte Farbe vom Sensor: {sensorValue}");
            return;
        }

        Press(colorName, uiColor);
    }

    // ══════════════════════════════════════════════════════════
    // Auswertung
    // ══════════════════════════════════════════════════════════

    IEnumerator Evaluate()
    {
        locked = true;
        yield return new WaitForSeconds(0.4f);

        bool correct = true;
        for (int i = 0; i < solutionNames.Length; i++)
        {
            if (playerInput[i] != solutionNames[i])
            {
                correct = false;
                break;
            }
        }

        if (correct)
        {
            if (feedbackText != null)
                feedbackText.text = "✓ Korrekte Sequenz!";

            yield return new WaitForSeconds(0.9f);
            Level3_Controller.NextPhase();
        }
        else
        {
            if (feedbackText != null)
                feedbackText.text = "✗ Falsche Reihenfolge!";

            yield return StartCoroutine(BlinkButtonsRed());

            playerInput.Clear();
            ResetIndicators();
            if (feedbackText != null)
                feedbackText.text = string.Empty;
            locked = false;
        }
    }

    // ══════════════════════════════════════════════════════════
    // Visuelles Fehler-Feedback
    // ══════════════════════════════════════════════════════════

    IEnumerator BlinkButtonsRed()
    {
        Color errorRed = new Color(0.85f, 0.15f, 0.15f);
        var originals = new Color[allColorButtons.Length];

        for (int blink = 0; blink < 2; blink++)
        {
            for (int i = 0; i < allColorButtons.Length; i++)
            {
                if (allColorButtons[i] == null) continue;
                if (blink == 0) originals[i] = allColorButtons[i].colors.normalColor;
                var cb = allColorButtons[i].colors;
                cb.normalColor = errorRed;
                allColorButtons[i].colors = cb;
            }
            yield return new WaitForSeconds(0.3f);

            for (int i = 0; i < allColorButtons.Length; i++)
            {
                if (allColorButtons[i] == null) continue;
                var cb = allColorButtons[i].colors;
                cb.normalColor = originals[i];
                allColorButtons[i].colors = cb;
            }
            yield return new WaitForSeconds(0.2f);
        }
    }

    // ══════════════════════════════════════════════════════════
    // Helfer
    // ══════════════════════════════════════════════════════════

    void ResetIndicators()
    {
        if (sequenceIndicators == null) return;
        foreach (var ind in sequenceIndicators)
            ind.color = new Color(0.2f, 0.2f, 0.2f);
    }
}
