using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Farbcode-Terminal am PC der Bibliothek.
/// Spieler muss die in der Bibel entdeckte Farbsequenz eingeben.
/// Bei Erfolg: OnCodeAccepted feuert -> Level3_ComputerInteraction aktiviert ExitTrigger.
///
/// Wird vom BuildLevel3Library im Editor vollständig verkabelt.
/// </summary>
public class Level3_ColorCodeUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas overlayCanvas;
    public Button   redButton;
    public Button   blueButton;
    public Button   yellowButton;
    public Button   greenButton;
    public Button   resetButton;
    public Button   closeButton;
    public Image[]  slotImages;          // 4 leere Slots, die sich beim Drücken einfärben
    public TextMeshProUGUI feedbackText;
    public TextMeshProUGUI titleText;

    [Header("Lösung")]
    [Tooltip("Reihenfolge der Farbnamen wie auf der Bibel-Seite zu lesen.")]
    public string[] solution = { "Red", "Blue", "Yellow", "Green" };

    public event Action OnCodeAccepted;

    private static readonly Color SlotEmpty = new Color(0.10f, 0.10f, 0.13f, 1f);
    private static readonly Color C_Red    = new Color(0.85f, 0.15f, 0.15f);
    private static readonly Color C_Blue   = new Color(0.20f, 0.40f, 1.00f);
    private static readonly Color C_Yellow = new Color(0.95f, 0.85f, 0.18f);
    private static readonly Color C_Green  = new Color(0.20f, 0.85f, 0.30f);

    private readonly List<string> input = new();
    private bool locked;

    void Awake()
    {
        if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(false);
    }

    void Start()
    {
        if (redButton    != null) redButton.onClick.AddListener(()    => Press("Red"));
        if (blueButton   != null) blueButton.onClick.AddListener(()   => Press("Blue"));
        if (yellowButton != null) yellowButton.onClick.AddListener(() => Press("Yellow"));
        if (greenButton  != null) greenButton.onClick.AddListener(()  => Press("Green"));
        if (resetButton  != null) resetButton.onClick.AddListener(ResetInput);
        if (closeButton  != null) closeButton.onClick.AddListener(Hide);
    }

    public void Show()
    {
        if (overlayCanvas == null) return;
        overlayCanvas.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        ResetInput();
        if (titleText != null) titleText.text = "Farbcode eingeben";
    }

    public void Hide()
    {
        if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
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
        yield return new WaitForSeconds(0.4f);

        if (SequenceMatches())
        {
            if (feedbackText != null) feedbackText.text = "✓ Zugang gewährt.";
            yield return new WaitForSeconds(1.4f);
            Hide();
            OnCodeAccepted?.Invoke();
        }
        else
        {
            if (feedbackText != null) feedbackText.text = "✗ Falscher Code.";
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
        var err = new Color(0.85f, 0.15f, 0.15f);
        for (int b = 0; b < 2; b++)
        {
            foreach (var s in slotImages) if (s != null) s.color = err;
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

    private Color NameToColor(string name) => name switch
    {
        "Red"    => C_Red,
        "Blue"   => C_Blue,
        "Yellow" => C_Yellow,
        "Green"  => C_Green,
        _        => SlotEmpty
    };
}
