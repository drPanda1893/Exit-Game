using UnityEngine;

/// <summary>
/// Leitet ArduinoBridge-Keypad-Events an NumpadController weiter.
/// Nur aktiv solange das Numpad-Panel sichtbar ist.
///
/// Wiring (Arduino D2–D9, von rechts nach links):
///   D2=Col4  D3=Col3  D4=Col2  D5=Col1
///   D6=Row4  D7=Row3  D8=Row2  D9=Row1
/// </summary>
public class Level1_KeypadBridge : MonoBehaviour
{
    [SerializeField] private NumpadController numpadController;

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
        if (numpadController == null || !numpadController.gameObject.activeSelf) return;
        // NumpadController.ButtonPressed versteht "0"-"9", "DEL", "ENT" direkt
        numpadController.ButtonPressed(key);
    }
}
