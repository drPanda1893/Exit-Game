using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Level 5 – Breadboard Puzzle.
/// Spieler zieht Kabel-Enden (BreadboardPin) per Drag &amp; Drop auf die richtigen Ziel-Pins.
/// Alle korrekten Verbindungen → Level abgeschlossen.
///
/// Setup in Unity:
/// - Jeder Kabel-Start-Pin bekommt BreadboardPin (isDragSource = true) + diese Manager-Referenz.
/// - Jeder Kabel-Ziel-Pin bekommt BreadboardPin (isDragSource = false) + diese Manager-Referenz.
/// - requiredPairs: z.B. "A" → "A", "B" → "B" (Start-ID muss zu End-ID passen).
/// </summary>
public class Level5_Breadboard : MonoBehaviour
{
    [System.Serializable]
    public class CablePair
    {
        public string startId;
        public string endId;
        [HideInInspector] public bool connected;
        public Image connectionIndicator; // Optional: Bild das grün wird bei Verbindung
    }

    [Header("Erforderliche Verbindungen")]
    [SerializeField] private List<CablePair> requiredPairs;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private RectTransform dragIcon; // Kleines Kabel-Icon das der Maus folgt

    private string currentDragId;

    void OnEnable()
    {
        foreach (var pair in requiredPairs) pair.connected = false;
        if (dragIcon) dragIcon.gameObject.SetActive(false);

        BigYahuDialogSystem.Instance.ShowDialog(new[]
        {
            "Big Yahu: Das Breadboard braucht die richtigen Verbindungen!",
            "Big Yahu: Ziehe die Kabel-Enden auf die passenden Pins!"
        });
    }

    public void BeginCableDrag(string startId)
    {
        currentDragId = startId;
        if (dragIcon) dragIcon.gameObject.SetActive(true);
    }

    public void UpdateDragPosition(Vector2 screenPos)
    {
        if (!dragIcon || !dragIcon.gameObject.activeSelf) return;
        Canvas canvas = dragIcon.GetComponentInParent<Canvas>();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, screenPos, cam, out Vector2 local);
        dragIcon.anchoredPosition = local;
    }

    public void EndCableDrag()
    {
        currentDragId = null;
        if (dragIcon) dragIcon.gameObject.SetActive(false);
    }

    public void TryConnect(string endId)
    {
        if (string.IsNullOrEmpty(currentDragId)) return;

        CablePair match = requiredPairs.Find(p => p.startId == currentDragId && p.endId == endId);
        if (match != null && !match.connected)
        {
            match.connected = true;
            if (match.connectionIndicator) match.connectionIndicator.color = Color.green;
            feedbackText.text = $"✓ Verbindung {match.startId}→{match.endId} korrekt!";
            CheckAllConnected();
        }
        else
        {
            feedbackText.text = "✗ Falsche Verbindung!";
        }

        EndCableDrag();
    }

    void CheckAllConnected()
    {
        foreach (var pair in requiredPairs)
            if (!pair.connected) return;

        feedbackText.text = "✓ Alle Verbindungen korrekt! Stromkreis geschlossen!";
        BigYahuDialogSystem.Instance.ShowDialog(
            "Big Yahu: Perfekt verdrahtet! Der Stromkreis ist geschlossen!",
            () => GameManager.Instance.CompleteCurrentLevel());
    }
}

/// <summary>
/// Kommt auf jeden Drag-Source und Drop-Target Pin.
/// isDragSource = true  → Kabel-Anfang (Drag von hier weg)
/// isDragSource = false → Kabel-Ende   (hier fallen lassen)
/// </summary>
public class BreadboardPin : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private string pinId;
    [SerializeField] private bool isDragSource = true;
    [SerializeField] private Level5_Breadboard manager;

    public void OnBeginDrag(PointerEventData e)
    {
        if (isDragSource) manager.BeginCableDrag(pinId);
    }

    public void OnDrag(PointerEventData e)
    {
        if (isDragSource) manager.UpdateDragPosition(e.position);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (isDragSource) manager.EndCableDrag();
    }

    public void OnDrop(PointerEventData e)
    {
        if (!isDragSource) manager.TryConnect(pinId);
    }
}
