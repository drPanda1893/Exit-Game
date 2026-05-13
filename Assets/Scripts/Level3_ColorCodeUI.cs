using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using EscapeTheMatrix.Sektor03;

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
    [Header("UI References (Legacy Canvas — Fallback)")]
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

    [Header("Sektor03 Terminal (UI Toolkit) — bevorzugt wenn gesetzt")]
    [Tooltip("Wenn beide Felder gesetzt sind, wird das UI-Toolkit-Terminal benutzt und das Legacy-Canvas oben ignoriert.")]
    public UIDocument                 terminalDocument;
    public Sektor03TerminalController terminalController;

    private bool UseSektor => terminalDocument != null && terminalController != null;
    private int  sektorInputCount;
    private bool sektorResetHooked;

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
        "> Der Zugangscode ist nicht im System hinterlegt.\n" +
        "> Er liegt verborgen in der echten Bibel vor dir auf dem Tisch.\n" +
        ">\n" +
        "> Drei Stellen sind darin markiert -- in ROT, GRÜN und BLAU.\n" +
        "> Scanne sie in der Reihenfolge ihrer Seitenzahlen.\n" +
        ">\n" +
        "> Vertippt?  RESET-Taster am Sensor (oder \"Zurücksetzen\" unten).";

    private readonly List<string> input = new();
    private bool locked;
    private bool arduinoSubscribed;
    private Coroutine promptBlink;
    private bool cursorOn = true;
    private string sensorReadout = "";   // letzte Live-Werte des Farbscanners

    // ══════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════

    void Awake()
    {
        if (UseSektor)
        {
            if (terminalDocument != null) terminalDocument.gameObject.SetActive(false);
        }
        else if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(false);
    }

    void Start()
    {
        if (UseSektor)
        {
            if (terminalController.OnAuthenticationSuccess == null)
                terminalController.OnAuthenticationSuccess = new UnityEngine.Events.UnityEvent();
            if (terminalController.OnAuthenticationFailure == null)
                terminalController.OnAuthenticationFailure = new UnityEngine.Events.UnityEvent();

            terminalController.OnAuthenticationSuccess.AddListener(OnSektorSuccess);
            terminalController.OnAuthenticationFailure.AddListener(OnSektorFailure);
        }
        else
        {
            if (redButton   != null) redButton.onClick.AddListener(()   => OnButton("Red"));
            if (blueButton  != null) blueButton.onClick.AddListener(()  => OnButton("Blue"));
            if (greenButton != null) greenButton.onClick.AddListener(() => OnButton("Green"));
            if (resetButton != null) resetButton.onClick.AddListener(ResetInput);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }
    }

    void OnDisable()
    {
        UnsubscribeArduino();
        StopPromptBlink();
    }

    void OnDestroy()
    {
        if (terminalController != null)
        {
            terminalController.OnAuthenticationSuccess?.RemoveListener(OnSektorSuccess);
            terminalController.OnAuthenticationFailure?.RemoveListener(OnSektorFailure);
        }
    }

    // ══════════════════════════════════════════════════════════
    // Show / Hide
    // ══════════════════════════════════════════════════════════

    public void Show()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        ResetInput();
        locked = false;
        sensorReadout = "";

        if (UseSektor)
        {
            terminalDocument.gameObject.SetActive(true);
            StartCoroutine(HookSektorResetButtonNextFrame());
            SubscribeArduino(resetSensor: true);
            return;
        }

        if (overlayCanvas == null) return;
        overlayCanvas.gameObject.SetActive(true);

        if (titleText   != null) { titleText.text   = "ZUGANG GESPERRT"; titleText.color   = TermRed;   }
        if (messageText != null) { messageText.text = MessageBody;        messageText.color = TermGreen; }

        SubscribeArduino(resetSensor: true);
        StartPromptBlink();
        RenderPromptLine();
    }

    public void Hide()
    {
        if (UseSektor)
        {
            if (terminalDocument != null) terminalDocument.gameObject.SetActive(false);
        }
        else if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        UnsubscribeArduino();
        StopPromptBlink();
    }

    // ══════════════════════════════════════════════════════════
    // Arduino-Farbsensor (CMD 0x20)
    // ══════════════════════════════════════════════════════════

    private void SubscribeArduino(bool resetSensor = false)
    {
        if (ArduinoBridge.Instance == null)
        {
            Debug.LogWarning("[Level3_ColorSensor] Kein ArduinoBridge gefunden - Farbsensor nicht verbunden.");
            return;
        }

        if (!arduinoSubscribed)
        {
            ArduinoBridge.Instance.OnColor += HandleArduinoColor;
            ArduinoBridge.Instance.OnConnectionChanged += HandleArduinoConnectionChanged;
            arduinoSubscribed = true;
        }

        if (!ArduinoBridge.Instance.IsConnected)
        {
            Debug.LogWarning("[Level3_ColorSensor] ArduinoBridge noch nicht verbunden - START wird nach Verbindung erneut gesendet.");
            return;
        }

        if (resetSensor)
            ArduinoBridge.Instance.Send(0x20, "STOP");

        ArduinoBridge.Instance.Send(0x20, "START");  // -> "20:START": aktiviert den Sensor
        Debug.Log("[Level3_ColorSensor] Farbscanner aktiviert.");
    }

    private void HandleArduinoConnectionChanged(bool connected)
    {
        if (!connected || !arduinoSubscribed || ArduinoBridge.Instance == null) return;
        ArduinoBridge.Instance.Send(0x20, "START");
        Debug.Log("[Level3_ColorSensor] Farbscanner aktiviert.");
    }

    private void UnsubscribeArduino()
    {
        if (!arduinoSubscribed) return;
        arduinoSubscribed = false;
        if (ArduinoBridge.Instance == null) return;
        ArduinoBridge.Instance.OnColor -= HandleArduinoColor;
        ArduinoBridge.Instance.OnConnectionChanged -= HandleArduinoConnectionChanged;
        if (ArduinoBridge.Instance.IsConnected)
            ArduinoBridge.Instance.Send(0x20, "STOP");
    }

    // raw kommt bereits ohne "COLOR:"-Präfix von der ArduinoBridge:
    //   "RGB:r,g,b,NAME[,rawR,rawG,rawB]" -> nur Live-Anzeige im Terminal, keine Eingabe
    //   "RESET"          -> Eingabe zurücksetzen (physischer Taster)
    //   "RED"|"GREEN"|"BLUE" -> bestätigte Farbe (Sketch sendet nur bei echter Änderung)
    public void BeginArduinoScan() => SubscribeArduino();

    private void HandleArduinoColor(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        string t = raw.Trim();

        if (t.StartsWith("RGB:", StringComparison.OrdinalIgnoreCase)) { UpdateSensorReadout(t.Substring(4)); return; }
        if (t.Equals("RESET", StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("[Level3_ColorSensor] Reset vom Sensor.");
            ResetInput();
            return;
        }

        string c = NormalizeColor(raw);
        if (c != null)
        {
            Debug.Log($"[Level3_ColorSensor] Bestaetigte Farbe: {c}");
            Press(c);
        }
    }

    // csv = "r,g,b,NAME[,rawR,rawG,rawB]" -> Live-Zeile im Terminal
    private void UpdateSensorReadout(string csv)
    {
        var p = csv.Split(',');
        if (p.Length >= 4 &&
            int.TryParse(p[0].Trim(), out int r) &&
            int.TryParse(p[1].Trim(), out int g) &&
            int.TryParse(p[2].Trim(), out int b))
        {
            string label = NormalizeColor(p[3]) switch
            {
                "Red"   => "ROT",
                "Blue"  => "BLAU",
                "Green" => "GRÜN",
                _       => "--"
            };
            if (p.Length >= 7 &&
                long.TryParse(p[4].Trim(), out long rawR) &&
                long.TryParse(p[5].Trim(), out long rawG) &&
                long.TryParse(p[6].Trim(), out long rawB))
            {
                sensorReadout = $"ROH R={rawR} G={rawG} B={rawB} | RGB {r:000},{g:000},{b:000} -> {label}";
            }
            else
            {
                sensorReadout = $"RGB {r:000},{g:000},{b:000} -> {label}";
            }
        }
        else
        {
            sensorReadout = csv.Trim();
        }
        Debug.Log($"[Level3_ColorSensor] {sensorReadout}");
        RenderPromptLine();
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
        if (UseSektor)
        {
            if (locked) return;
            if (sektorInputCount >= solution.Length) return;
            if (!TryMapToSektor(color, out var sc)) return;
            terminalController.AddColor(sc);
            sektorInputCount++;
            if (sektorInputCount >= solution.Length)
                StartCoroutine(SektorAutoSubmit());
            return;
        }

        if (locked || input.Count >= solution.Length) return;

        input.Add(color);
        UpdateSlots();

        if (input.Count == solution.Length)
            StartCoroutine(Evaluate());
    }

    // Auto-Submit damit Arduino-Scans wie früher direkt geprüft werden,
    // ohne dass der Spieler den AUTHENTICATE-Button drücken muss.
    private IEnumerator SektorAutoSubmit()
    {
        locked = true;
        yield return new WaitForSeconds(0.4f);
        if (terminalController != null) terminalController.SubmitSequence();
        locked = false;
    }

    private static bool TryMapToSektor(string color,
        out Sektor03TerminalController.ColorCode sc)
    {
        switch (color)
        {
            case "Red":   sc = Sektor03TerminalController.ColorCode.RED;   return true;
            case "Blue":  sc = Sektor03TerminalController.ColorCode.BLUE;  return true;
            case "Green": sc = Sektor03TerminalController.ColorCode.GREEN; return true;
            default:      sc = default;                                    return false;
        }
    }

    // Sektor-Events
    private void OnSektorSuccess()
    {
        StartCoroutine(SektorSuccessRoutine());
    }

    private IEnumerator SektorSuccessRoutine()
    {
        // 1.6s = etwas länger als das ACCESS_GRANTED-Overlay des Terminals,
        // damit der Spieler den Erfolg sieht bevor das UI ausblendet.
        yield return new WaitForSeconds(1.6f);
        Hide();
        OnCodeAccepted?.Invoke();
    }

    private void OnSektorFailure()
    {
        // Sektor setzt seine Slots intern nach ~1.9s zurück – also auch
        // unseren Eingabe-Counter wieder freigeben.
        sektorInputCount = 0;
        locked           = false;
    }

    // Wenn der Spieler den RESET-Button im Sektor-Terminal direkt klickt,
    // bekommt unsere Logik das sonst nicht mit. Beim ersten Show klemmen
    // wir uns einmalig dran.
    private IEnumerator HookSektorResetButtonNextFrame()
    {
        if (sektorResetHooked) yield break;
        yield return null;   // ein Frame, bis UIDocument die VisualTree erstellt hat
        var root = terminalDocument != null ? terminalDocument.rootVisualElement : null;
        if (root == null) yield break;
        var btn = root.Q<UnityEngine.UIElements.Button>("reset-btn");
        if (btn != null)
        {
            btn.clicked += () => { sektorInputCount = 0; locked = false; };
            sektorResetHooked = true;
        }
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
        if (UseSektor)
        {
            sektorInputCount = 0;
            if (terminalController != null) terminalController.ResetSequence();
            locked = false;
            return;
        }

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
    // Terminal-Zeile: Live-Scannerwerte + blinkender Cursor
    // ══════════════════════════════════════════════════════════

    private void RenderPromptLine()
    {
        if (promptText == null) return;
        string body = string.IsNullOrEmpty(sensorReadout) ? "" : sensorReadout + "   ";
        promptText.text = "> " + body + (cursorOn ? "_" : " ");
    }

    private void StartPromptBlink()
    {
        if (promptText == null) return;
        StopPromptBlink();
        cursorOn    = true;
        promptBlink = StartCoroutine(PromptBlinkLoop());
    }

    private void StopPromptBlink()
    {
        if (promptBlink != null) { StopCoroutine(promptBlink); promptBlink = null; }
        if (promptText != null) promptText.text = "> _";
    }

    private IEnumerator PromptBlinkLoop()
    {
        var wait = new WaitForSeconds(0.5f);
        while (true)
        {
            cursorOn = !cursorOn;
            RenderPromptLine();
            yield return wait;
        }
    }
}
