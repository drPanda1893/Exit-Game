using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Trigger-Zone um Helios. Spieler betritt sie → Hinweis "E zum Interagieren".
/// Drückt der Spieler E → Buch-Auswahl öffnet sich.
/// Richtige Wahl (Bibel) → Computer wird aktiviert.
/// Farbcode am PC erfolgreich → Szenenwechsel zu Level 4 (Computer-Minigame).
/// </summary>
[RequireComponent(typeof(Collider))]
public class HeliosInteraction : MonoBehaviour
{
    public BookSelectionUI bookUI;
    public GameObject hintGO;          // "[ E ] Interagieren"-Hinweis
    public Level3_ComputerInteraction computer;

    private bool solved     = false;
    private bool inRange    = false;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        if (hintGO != null) hintGO.SetActive(false);
        if (bookUI != null) bookUI.OnCorrectBook += OnBibleChosen;
    }

    void OnDestroy()
    {
        if (bookUI != null) bookUI.OnCorrectBook -= OnBibleChosen;
    }

    void Update()
    {
        if (solved || !inRange) return;
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
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
        if (hintGO != null) hintGO.SetActive(false);

        // Nur den PC scharf schalten (Monitor leuchtet, Spot geht an), damit der
        // Spieler ihn im Raum findet. Das Terminal selbst oeffnet sich erst,
        // wenn der Spieler zum PC laeuft und dort [E] drueckt – siehe
        // Level3_ComputerInteraction.Update().
        if (computer != null) computer.ActivateComputer();
    }
}
