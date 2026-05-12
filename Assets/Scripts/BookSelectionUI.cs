using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Zeigt drei Buch-Optionen an. Auswahl der Bibel schließt das Puzzle ab.
/// Wird von HeliosInteraction aktiviert.
/// </summary>
public class BookSelectionUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas overlayCanvas;
    public Button[] bookButtons;        // 3 Buttons
    public TextMeshProUGUI[] bookLabels;
    public TextMeshProUGUI feedbackText;

    public Action OnCorrectBook;

    private static readonly string[] BookTitles = { "Die Bibel", "Faust – Goethe", "Das Kapital" };
    private const int CorrectIndex = 0; // Die Bibel

    void Start()
    {
        if (overlayCanvas != null)
            overlayCanvas.gameObject.SetActive(false);
    }

    public void Show()
    {
        if (overlayCanvas == null) return;
        overlayCanvas.gameObject.SetActive(true);
        if (feedbackText != null) feedbackText.text = "";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // Bücher in zufälliger Reihenfolge anzeigen
        int[] order = ShuffleOrder();
        for (int i = 0; i < bookButtons.Length; i++)
        {
            int bookIdx = order[i];
            int capturedIdx = bookIdx;
            bookLabels[i].text = BookTitles[bookIdx];
            bookButtons[i].onClick.RemoveAllListeners();
            bookButtons[i].onClick.AddListener(() => OnBookChosen(capturedIdx));
        }
    }

    public void Hide()
    {
        if (overlayCanvas != null)
            overlayCanvas.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void OnBookChosen(int bookIdx)
    {
        if (bookIdx == CorrectIndex)
        {
            if (feedbackText != null) feedbackText.text = "✓ Die richtige Wahl.";
            OnCorrectBook?.Invoke();
            Invoke(nameof(Hide), 1.5f);
        }
        else
        {
            if (feedbackText != null) feedbackText.text = "✗ Das ist nicht das gesuchte Buch.";
        }
    }

    private int[] ShuffleOrder()
    {
        int[] arr = { 0, 1, 2 };
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr;
    }
}
