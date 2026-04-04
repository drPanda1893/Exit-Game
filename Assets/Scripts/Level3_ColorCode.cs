using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Level 3 – Phase B: Farbcode-Eingabe.
///
/// Spieler hat die Bibel aufgeschlagen (Phase A abgeschlossen) und kennt
/// die Farbsequenz. Nun muss er sie eingeben:
///   • Fallback-Modus: 4 farbige UI-Buttons (Maus/Touch)
///   • Arduino-Modus:  Farb-Sensor (Command 0x20) über ArduinoBridge
///
/// Nach korrekter Eingabe → Level3_Controller.NotifyColorSolved().
/// </summary>
public class Level3_ColorCode : MonoBehaviour
{
    // ── UI-Buttons (Fallback) ─────────────────────────────────
    [Header("Farb-Buttons (Fallback-Modus)")]
    [SerializeField] private Button redButton;
    [SerializeField] private Button greenButton;
    [SerializeField] private Button blueButton;
    [SerializeField] private Button yellowButton;

    [Header("Sequenz-Anzeige (4 Indikatoren)")]
    [SerializeField] private Image[] sequenceIndicators;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    // ── Arduino ───────────────────────────────────────────────
    [Header("Arduino-Integration")]
    [Tooltip("Wenn true, werden UI-Buttons angezeigt als Fallback.")]
    [SerializeField] private bool arduinoFallback = true;

    [Tooltip("Panel mit den manuellen Farb-Buttons.")]
    [SerializeField] private GameObject fallbackButtonPanel;

    [Tooltip("Anweisung für Arduino-Modus ('Halte den Sensor an die Farbe …')")]
    [SerializeField] private TextMeshProUGUI arduinoInstructionText;

    // ── Helios NPC ────────────────────────────────────────────
    [Header("Helios NPC")]
    [SerializeField] private Sprite heliosPortrait;

    // ── Lösung ────────────────────────────────────────────────
    private static readonly string[] Solution = { "Red", "Blue", "Yellow", "Green" };
    private static readonly Color[] SolutionColors =
    {
        Color.red,
        new Color(0.2f, 0.4f, 1f),
        Color.yellow,
        Color.green
    };

    private readonly List<string> playerInput = new();
    private bool locked;

    // ══════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════

    void OnEnable()
    {
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
        // UI-Button Listener (Fallback)
        redButton.onClick.AddListener(()    => Press("Red",    Color.red));
        blueButton.onClick.AddListener(()   => Press("Blue",   new Color(0.2f, 0.4f, 1f)));
        yellowButton.onClick.AddListener(() => Press("Yellow", Color.yellow));
        greenButton.onClick.AddListener(()  => Press("Green",  Color.green));
    }

    void OnDisable()
    {
        // Arduino-Events abmelden (wenn implementiert)
        // ArduinoBridge.OnColorReading -= OnArduinoColor;
    }

    // ══════════════════════════════════════════════════════════
    // Input-Modus konfigurieren
    // ══════════════════════════════════════════════════════════

    void ConfigureInputMode()
    {
        bool useArduino = !arduinoFallback && IsArduinoConnected();

        // Fallback-Buttons ein/ausblenden
        if (fallbackButtonPanel != null)
            fallbackButtonPanel.SetActive(!useArduino);

        // Arduino-Anweisung ein/ausblenden
        if (arduinoInstructionText != null)
        {
            arduinoInstructionText.gameObject.SetActive(useArduino);
            arduinoInstructionText.text = "Halte den Farb-Sensor an die richtige Farbe …";
        }

        if (useArduino)
        {
            // TODO: Arduino-Event registrieren
            // ArduinoBridge.Instance.OnColorReading += OnArduinoColor;
        }
    }

    /// <summary>
    /// Prüft, ob ArduinoBridge verbunden ist.
    /// Placeholder bis ArduinoBridge-Singleton existiert.
    /// </summary>
    bool IsArduinoConnected()
    {
        // TODO: return ArduinoBridge.Instance != null && ArduinoBridge.Instance.IsConnected;
        return false;
    }

    // ══════════════════════════════════════════════════════════
    // Helios Dialog (Phase B)
    // ══════════════════════════════════════════════════════════

    void ShowHeliosColorPrompt()
    {
        var dialog = BigYahuDialogSystem.Instance;
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
    // Eingabe-Verarbeitung
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Wird von UI-Buttons ODER Arduino-Callback aufgerufen.
    /// </summary>
    void Press(string color, Color uiColor)
    {
        if (locked || playerInput.Count >= Solution.Length) return;

        playerInput.Add(color);
        int idx = playerInput.Count - 1;

        if (sequenceIndicators != null && idx < sequenceIndicators.Length)
            sequenceIndicators[idx].color = uiColor;

        if (playerInput.Count == Solution.Length)
            StartCoroutine(Evaluate());
    }

    /// <summary>
    /// Arduino-Farbsensor Callback (Command 0x20).
    /// Mappt den Sensor-String auf unsere Farbnamen.
    /// </summary>
    void OnArduinoColor(string sensorValue)
    {
        // Erwartetes Format von Arduino: "COLOR:Red", "COLOR:Blue", etc.
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
        for (int i = 0; i < Solution.Length; i++)
        {
            if (playerInput[i] != Solution[i])
            {
                correct = false;
                break;
            }
        }

        if (correct)
        {
            feedbackText.text = "✓ Korrekte Sequenz!";
            yield return new WaitForSeconds(0.9f);

            // Über Controller weiterleiten (nicht direkt CompleteCurrentLevel)
            Level3_Controller.NotifyColorSolved();
        }
        else
        {
            feedbackText.text = "✗ Falsche Reihenfolge!";
            yield return new WaitForSeconds(1.1f);

            playerInput.Clear();
            ResetIndicators();
            feedbackText.text = string.Empty;
            locked = false;
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
