using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Login-Terminal der Bibliothek (Level 3, Phase B).
///
/// Erscheint als Popup direkt nachdem der Spieler die Bibel im Regal gewählt hat
/// (HeliosInteraction → Level3_ComputerInteraction.OpenLoginScreen()).
/// Optik: ein Computer-Login-Fenster. Der Hinweistext bricht die vierte Wand und
/// verweist auf die *echte* Bibel, die als Requisite vor dem Spieler auf dem Tisch
/// liegt – dort sind die drei Farben markiert.
///
/// Code (3-stellig):  GRÜN → BLAU → GRÜN
/// Verfügbare Farben: ROT, BLAU, GRÜN.
///
/// Eingabe:
///   • Arduino-Farbsensor (TCS3200) – der Sketch sendet "COLOR:RED|GREEN|BLUE"
///     NUR wenn sich die erkannte Farbe wirklich ändert (eine Farbe = ein Input),
///     und "COLOR:RESET" wenn der physische Reset-Taster gedrückt wird.
///   • Maus-Klick auf die Farb-Buttons – Fallback (arduinoFallback = true), damit
///     das Spiel auch ohne Hardware lauffähig bleibt.
///   • UI-Button "Zurücksetzen" – setzt die Eingabe ebenfalls zurück.
///
/// Wird vom BuildLevel3Library im Editor vollständig verkabelt.
/// </summary>
public class Level3_ColorCodeUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas overlayCanvas;
    public Button   redButton;
    public Button   blueButton;
    public Button   greenButton;
    public Button   resetButton;
    public Button   closeButton;
    public Image[]  slotImages;          // 3 Slots, die sich beim Eingeben einfärben
    public TextMeshProUGUI feedbackText;
    public TextMeshProUGUI titleText;    // Status-Zeile ("ZUGANG GESPERRT" / ...)
    public TextMeshProUGUI messageText;  // Terminal-Hinweistext (auf die echte Bibel)
    public TextMeshProUGUI promptText;   // Blinkender Terminal-Cursor "> _"

    [Header("Lösung (3-stellig)")]
    [Tooltip("Reihenfolge der Farbnamen wie in der echten Bibel markiert.")]
    public string[] solution = { "Green", "Blue", "Green" };

    [Header("Arduino")]
    [Tooltip("Wenn true, funktionieren auch die Maus-Buttons – Spiel ohne Hardware spielbar.")]
    public bool arduinoFallback = true;

    public event Action OnCodeAccepted;

    private static readonly Color SlotEmpty = new Color(0.06f, 0.09f, 0.11f, 1f);
    private static readonly Color C_Red     = new Color(0.90f, 0.18f, 0.16f);
    private static readonly Color C_Blue    = new Color(0.20f, 0.45f, 1.00f);
    private static readonly Color C_Green   = new Color(0.22f, 0.86f, 0.32f);

    private static readonly Color TermGreen = new Color(0.55f, 0.95f, 0.70f);
    private static readonly Color TermRed   = new Color(0.95f, 0.35f, 0.30f);

    private const string MessageBody =
        "> SYS: AUTHENTIFIZIERUNG ERFORDERLICH\n" +
        ">\n" +
        "> Der Zugangscode ist NICHT im System hinterlegt.\n" +
        "> Er liegt verborgen in der echten Bibel auf dem Tisch.\n" +
        ">\n" +
        "> Drei Stellen sind darin markiert -- in ROT, GRÜN, BLAU.\n" +
        "> Lies sie in der Reihenfolge ihrer Seitenzahlen und\n" +
        "> halte den Farbsensor an jede Markierung.\n" +
        ">\n" +
        "> Vertippt? -> RESET-Taster am Sensor drücken\n" +
        ">             (oder \"Zurücksetzen\" unten).";

    private readonly List<string> input = new();
    private bool locked;
    private bool arduinoSubscribed;
    private Coroutine promptBlink;

    // ══════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════

    void Awake()
    {
        if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(false);
    }

    void Start()
    {
        if (redButton   != null) redButton.onClick.AddListener(()   => OnButton("Red"));
        if (blueButton  != null) blueButton.onClick.AddListener(()  => OnButton("Blue"));
        if (greenButton != null) greenButton.onClick.AddListener(() => OnButton("Green"));
        if (resetButton != null) resetButton.onClick.AddListener(ResetInput);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }

    void OnDisable()
    {
        UnsubscribeArduino();
        StopPromptBlink();
    }

    // ══════════════════════════════════════════════════════════
    // Show / Hide
    // ══════════════════════════════════════════════════════════

    public void Show()
    {
        if (overlayCanvas == null) return;
        overlayCanvas.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        ResetInput();
        locked = false;

        if (titleText   != null) { titleText.text   = "ZUGANG GESPERRT"; titleText.color   = TermRed;   }
        if (messageText != null) { messageText.text = MessageBody;        messageText.color = TermGreen; }

        SubscribeArduino();
        StartPromptBlink();
    }

    public void Hide()
    {
        if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        UnsubscribeArduino();
        StopPromptBlink();
    }

    // ══════════════════════════════════════════════════════════
    // Arduino-Farbsensor (CMD 0x20)
    // ══════════════════════════════════════════════════════════

    private void SubscribeArduino()
    {
        if (arduinoSubscribed || ArduinoBridge.Instance == null) return;
        ArduinoBridge.Instance.OnColor += HandleArduinoColor;
        ArduinoBridge.Instance.Send(0x20, "START");  // -> "20:START": aktiviert den Sensor
        arduinoSubscribed = true;
    }

    private void UnsubscribeArduino()
    {
        if (!arduinoSubscribed) return;
        arduinoSubscribed = false;
        if (ArduinoBridge.Instance == null) return;
        ArduinoBridge.Instance.OnColor -= HandleArduinoColor;
        ArduinoBridge.Instance.Send(0x20, "STOP");
    }

    // raw kommt bereits ohne "COLOR:"-Präfix von der ArduinoBridge.
    // Der Sketch liefert genau einen Eintrag pro echter Farbänderung – kein
    // zusätzliches Entprellen nötig. "RESET" kommt vom physischen Taster.
    private void HandleArduinoColor(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        if (raw.Trim().Equals("RESET", StringComparison.OrdinalIgnoreCase)) { ResetInput(); return; }

        string c = NormalizeColor(raw);
        if (c != null) Press(c);
    }

    private static string NormalizeColor(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        if (raw.StartsWith("COLOR:", StringComparison.OrdinalIgnoreCase)) raw = raw[6..].Trim();
        return raw.ToLowerInvariant() switch
        {
            "red"   or "rot"             => "Red",
            "blue"  or "blau"            => "Blue",
            "green" or "grün" or "gruen" => "Green",
            _ => null
        };
    }

    // ══════════════════════════════════════════════════════════
    // Eingabe-Logik
    // ══════════════════════════════════════════════════════════

    private void OnButton(string color)
    {
        if (!arduinoFallback) return;   // Hardware-Pflicht: Buttons deaktiviert
        Press(color);
    }

    private void Press(string color)
    {
        if (locked || input.Count >= solution.Length) return;

        input.Add(color);
        UpdateSlots();

        if (input.Count == solution.Length)
            StartCoroutine(Evaluate());
    }

    private IEnumerator Evaluate()
    {
        locked = true;
        if (feedbackText != null) { feedbackText.text = "> prüfe Zugangscode ..."; feedbackText.color = TermGreen; }
        yield return new WaitForSeconds(0.5f);

        if (SequenceMatches())
        {
            if (feedbackText != null) { feedbackText.text = "> ZUGRIFF GEWÄHRT"; feedbackText.color = TermGreen; }
            if (titleText    != null) { titleText.text    = "ZUGANG FREI";       titleText.color    = TermGreen; }
            yield return new WaitForSeconds(1.4f);
            Hide();
            OnCodeAccepted?.Invoke();
        }
        else
        {
            if (feedbackText != null) { feedbackText.text = "> ZUGRIFF VERWEIGERT -- falsche Sequenz"; feedbackText.color = TermRed; }
            yield return BlinkSlotsError();
            ResetInput();
            locked = false;
        }
    }

    private bool SequenceMatches()
    {
        if (input.Count != solution.Length) return false;
        for (int i = 0; i < solution.Length; i++)
            if (input[i] != solution[i]) return false;
        return true;
    }

    private IEnumerator BlinkSlotsError()
    {
        if (slotImages == null) yield break;
        for (int b = 0; b < 2; b++)
        {
            foreach (var s in slotImages) if (s != null) s.color = TermRed;
            yield return new WaitForSeconds(0.18f);
            foreach (var s in slotImages) if (s != null) s.color = SlotEmpty;
            yield return new WaitForSeconds(0.12f);
        }
    }

    public void ResetInput()
    {
        input.Clear();
        UpdateSlots();
        if (feedbackText != null) feedbackText.text = string.Empty;
        if (locked) locked = false;
    }

    private void UpdateSlots()
    {
        if (slotImages == null) return;
        for (int i = 0; i < slotImages.Length; i++)
        {
            if (slotImages[i] == null) continue;
            slotImages[i].color = (i < input.Count) ? NameToColor(input[i]) : SlotEmpty;
        }
    }

    private static Color NameToColor(string name) => name switch
    {
        "Red"   => C_Red,
        "Blue"  => C_Blue,
        "Green" => C_Green,
        _       => SlotEmpty
    };

    // ══════════════════════════════════════════════════════════
    // Blinkender Terminal-Cursor
    // ══════════════════════════════════════════════════════════

    private void StartPromptBlink()
    {
        if (promptText == null) return;
        StopPromptBlink();
        promptBlink = StartCoroutine(PromptBlinkLoop());
    }

    private void StopPromptBlink()
    {
        if (promptBlink != null) { StopCoroutine(promptBlink); promptBlink = null; }
        if (promptText != null) promptText.text = "> _";
    }

    private IEnumerator PromptBlinkLoop()
    {
        bool on = true;
        var wait = new WaitForSeconds(0.5f);
        while (true)
        {
            promptText.text = on ? "> _" : "> ";
            on = !on;
            yield return wait;
        }
    }
}
