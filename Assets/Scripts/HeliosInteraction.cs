using UnityEngine;
using TMPro;

/// <summary>
/// Trigger-Zone um Helios. Spieler betritt sie → Hinweis "E zum Interagieren".
/// Drückt der Spieler E → Buch-Auswahl öffnet sich.
/// Richtige Wahl (Bibel) → LevelTransitionTrigger zu Level4 wird aktiviert.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HeliosInteraction : MonoBehaviour
{
    public BookSelectionUI bookUI;
    public GameObject exitTriggerGO;
    public GameObject hintGO;          // "[ E ] Interagieren"-Hinweis

    private bool solved     = false;
    private bool inRange    = false;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        if (exitTriggerGO != null) exitTriggerGO.SetActive(false);
        if (hintGO        != null) hintGO.SetActive(false);
        if (bookUI        != null) bookUI.OnCorrectBook += OnBibleChosen;
    }

    void OnDestroy()
    {
        if (bookUI != null) bookUI.OnCorrectBook -= OnBibleChosen;
    }

    void Update()
    {
        if (solved || !inRange) return;
        if (Input.GetKeyDown(KeyCode.E))
            bookUI?.Show();
    }

    void OnTriggerEnter(Collider other)
    {
        if (solved) return;
        if (!other.CompareTag("Player")) return;
        inRange = true;
        if (hintGO != null) hintGO.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        inRange = false;
        if (hintGO != null) hintGO.SetActive(false);
        bookUI?.Hide();
    }

    private void OnBibleChosen()
    {
        solved = true;
        inRange = false;
        if (hintGO        != null) hintGO.SetActive(false);
        if (exitTriggerGO != null) exitTriggerGO.SetActive(true);
    }
}
