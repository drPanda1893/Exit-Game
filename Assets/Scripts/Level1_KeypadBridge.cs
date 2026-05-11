using UnityEngine;

/// <summary>
/// Leitet ArduinoBridge-Keypad-Events (Cmd 0x05) an NumpadController weiter.
/// Nur aktiv solange das Numpad-Panel sichtbar ist.
///
/// Arduino-Protokoll:  "05:0" … "05:9" | "05:DEL" | "05:ENT"
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
        ArduinoBridge.Instance.RegisterHandler(0x05, HandleKey);
    }

    void OnDestroy()
    {
        if (ArduinoBridge.Instance != null)
            ArduinoBridge.Instance.UnregisterHandler(0x05, HandleKey);
    }

    // payload = "0"–"9" | "DEL" | "ENT"
    void HandleKey(string payload)
    {
        if (numpadController == null || !numpadController.gameObject.activeSelf) return;
        numpadController.ButtonPressed(payload);
    }
}
