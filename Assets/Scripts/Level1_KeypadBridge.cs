using UnityEngine;

/// <summary>
/// Leitet ArduinoBridge-Keypad-Events an Level1_Cell weiter.
/// Nur aktiv solange das Numpad-Panel sichtbar ist.
///
/// Wiring (Arduino D2–D9, von rechts nach links):
///   D2=Col4  D3=Col3  D4=Col2  D5=Col1
///   D6=Row4  D7=Row3  D8=Row2  D9=Row1
///
/// Keypad-Layout:
///   [ 1 ][ 2 ][ 3 ][ A ]
///   [ 4 ][ 5 ][ 6 ][ B ]
///   [ 7 ][ 8 ][ 9 ][ C ]
///   [ * ][ 0 ][ # ][ D ]
///   * / A  → DEL   # / B → ENT (auto-evaluate nach 4 Stellen)
/// </summary>
public class Level1_KeypadBridge : MonoBehaviour
{
    [SerializeField] private Level1_Cell level1Cell;
    [SerializeField] private GameObject  numpadPanel;

    void Start()
    {
        if (ArduinoBridge.Instance == null)
        {
            Debug.LogWarning("[KeypadBridge] ArduinoBridge nicht gefunden – kein Hardware-Input.");
            return;
        }
        ArduinoBridge.Instance.OnKeypadKey += HandleKey;
    }

    void OnDestroy()
    {
        if (ArduinoBridge.Instance != null)
            ArduinoBridge.Instance.OnKeypadKey -= HandleKey;
    }

    void HandleKey(string key)
    {
        if (numpadPanel == null || !numpadPanel.activeSelf) return;
        if (level1Cell == null) return;

        switch (key)
        {
            case "DEL":
                level1Cell.PressDelete();
                break;

            case "ENT":
                // Level1_Cell evaluiert automatisch nach 4 Stellen – ENT ignorieren
                break;

            default:
                // "0"–"9"
                if (key.Length == 1 && key[0] >= '0' && key[0] <= '9')
                    level1Cell.PressDigit(key);
                break;
        }
    }
}
