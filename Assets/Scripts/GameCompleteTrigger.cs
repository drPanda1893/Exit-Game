using UnityEngine;

/// <summary>
/// Unsichtbare Zone am Ausgang von Level 2.
/// Spieler betritt sie → GameCompleteUI.TriggerComplete()
/// </summary>
public class GameCompleteTrigger : MonoBehaviour
{
    public GameCompleteUI ui;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && ui != null)
            ui.TriggerComplete();
    }
}
