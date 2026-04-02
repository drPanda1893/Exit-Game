using UnityEngine;

/// <summary>
/// Trigger-Zone um Helios. Spieler betritt sie → Buch-Auswahl öffnet sich.
/// Richtige Wahl (Bibel) → LevelTransitionTrigger zu Level3 wird aktiviert.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HeliosInteraction : MonoBehaviour
{
    public BookSelectionUI bookUI;
    public GameObject exitTriggerGO; // wird aktiviert wenn Bibel gewählt

    private bool solved = false;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        if (exitTriggerGO != null)
            exitTriggerGO.SetActive(false);

        if (bookUI != null)
            bookUI.OnCorrectBook += OnBibleChosen;
    }

    void OnDestroy()
    {
        if (bookUI != null)
            bookUI.OnCorrectBook -= OnBibleChosen;
    }

    void OnTriggerEnter(Collider other)
    {
        if (solved) return;
        if (!other.CompareTag("Player")) return;
        bookUI?.Show();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        bookUI?.Hide();
    }

    private void OnBibleChosen()
    {
        solved = true;
        if (exitTriggerGO != null)
            exitTriggerGO.SetActive(true);
    }
}
