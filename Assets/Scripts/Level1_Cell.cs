using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Level 1 – Die Zelle.
/// Spieler klickt auf den Stein → Numpad öffnet sich.
/// Korrekte Eingabe "1634" → Level 2 wird geladen.
/// </summary>
public class Level1_Cell : MonoBehaviour
{
    [Header("Stein")]
    [SerializeField] private Button stoneButton;

    [Header("Numpad")]
    [SerializeField] private GameObject numpadPanel;
    [SerializeField] private TextMeshProUGUI codeDisplay;

    private const string SOLUTION = "1634";
    private string input = string.Empty;

    void OnEnable()
    {
        if (ArduinoBridge.Instance != null)
            ArduinoBridge.Instance.RegisterHandler(0x05, HandleArduinoKey);
    }

    void OnDisable()
    {
        if (ArduinoBridge.Instance != null)
        {
            ArduinoBridge.Instance.Send(0xFF, "STOP");
            ArduinoBridge.Instance.UnregisterHandler(0x05, HandleArduinoKey);
        }
    }

    void Start()
    {
        BigYahuDialogSystem.Instance?.ShowDialog(new[]
        {
            "Big Yahu: Hmm... dieser Stein sieht locker aus. Klick drauf!",
            "Big Yahu: Aber ich brauche einen Code... den hat sicher jemand irgendwo versteckt."
        });

        numpadPanel.SetActive(false);
        stoneButton.onClick.AddListener(OpenNumpad);
    }

    // ── Arduino Keypad Input ──────────────────────────────────────────────

    void HandleArduinoKey(string key)
    {
        if (!numpadPanel.activeSelf) return;

        switch (key)
        {
            case "DEL":
                PressDelete();
                break;
            case "ENT":
                // Auto-evaluation already triggers at 4 digits – ENT reserved for future use
                break;
            default:
                if (key.Length == 1 && key[0] >= '0' && key[0] <= '9')
                    PressDigit(key);
                break;
        }
    }

    public void OpenNumpad()
    {
        numpadPanel.SetActive(true);
        input = string.Empty;
        Refresh();
    }

    /// <summary>Wird von jedem Zahlen-Button per UnityEvent aufgerufen (Argument: "0"–"9").</summary>
    public void PressDigit(string digit)
    {
        if (input.Length >= 4) return;
        input += digit;
        Refresh();
        if (input.Length == 4)
            StartCoroutine(Evaluate());
    }

    public void PressDelete()
    {
        if (input.Length > 0)
            input = input[..^1];
        Refresh();
    }

    void Refresh() => codeDisplay.text = input.PadRight(4, '_');

    IEnumerator Evaluate()
    {
        yield return new WaitForSeconds(0.3f);

        if (input == SOLUTION)
        {
            codeDisplay.text = "✓ RICHTIG!";
            yield return new WaitForSeconds(0.8f);
            numpadPanel.SetActive(false);
            GameManager.Instance.CompleteCurrentLevel();
        }
        else
        {
            codeDisplay.text = "✗ FALSCH!";
            yield return new WaitForSeconds(0.9f);
            input = string.Empty;
            Refresh();
        }
    }
}
