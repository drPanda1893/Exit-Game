using UnityEngine;
using System.Collections;

/// <summary>
/// Hört auf NumpadController.OnCodeEntered.
/// Korrekte Eingabe "1642" + ENT → Tür in der rechten Wand öffnet sich.
/// </summary>
[RequireComponent(typeof(NumpadController))]
public class CellPuzzleHandler : MonoBehaviour
{
    public DoorController exitDoor;

    private NumpadController numpad;
    private const string SOLUTION = "1642";

    void Start()
    {
        numpad = GetComponent<NumpadController>();
        numpad.OnCodeEntered += OnCodeEntered;
    }

    void OnDestroy()
    {
        if (numpad != null)
            numpad.OnCodeEntered -= OnCodeEntered;
    }

    private void OnCodeEntered(string code)
    {
        StartCoroutine(Evaluate(code));
    }

    private IEnumerator Evaluate(string code)
    {
        if (code == SOLUTION)
        {
            if (numpad.displayText != null)
                numpad.displayText.text = "✓ RICHTIG!";
            yield return new WaitForSeconds(0.8f);
            numpad.Hide();
            exitDoor?.OpenDoor();
            // Szenenwechsel geschieht durch LevelTransitionTrigger sobald Spieler durchläuft
        }
        else
        {
            if (numpad.displayText != null)
                numpad.displayText.text = "✗ FALSCH!";
            yield return new WaitForSeconds(0.9f);
            numpad.ResetCode();
        }
    }
}
