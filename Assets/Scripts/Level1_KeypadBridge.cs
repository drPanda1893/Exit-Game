using UnityEngine;

/// <summary>
/// Bridges ArduinoBridge keypad events (cmd 0x05) to NumpadController.
/// Sends FF:START / FF:STOP to Arduino when the numpad panel opens or closes.
/// </summary>
public class Level1_KeypadBridge : MonoBehaviour
{
    [SerializeField] private NumpadController numpadController;

    private bool _wasActive = false;

    void Start()
    {
        if (ArduinoBridge.Instance == null)
        {
            Debug.LogWarning("[KeypadBridge] ArduinoBridge not found – no hardware input.");
            return;
        }
        ArduinoBridge.Instance.RegisterHandler(0x05, HandleKey);
    }

    void OnDestroy()
    {
        if (ArduinoBridge.Instance != null)
            ArduinoBridge.Instance.UnregisterHandler(0x05, HandleKey);
    }

    void Update()
    {
        if (numpadController == null || ArduinoBridge.Instance == null) return;

        bool isActive = numpadController.gameObject.activeSelf;

        if (isActive && !_wasActive)
        {
            ArduinoBridge.Instance.Send(0xFF, "START");
            Debug.Log("[KeypadBridge] Numpad opened → FF:START sent");
        }
        else if (!isActive && _wasActive)
        {
            ArduinoBridge.Instance.Send(0xFF, "STOP");
            Debug.Log("[KeypadBridge] Numpad closed → FF:STOP sent");
        }

        _wasActive = isActive;
    }

    // payload = "0"–"9" | "DEL" | "ENT"
    void HandleKey(string payload)
    {
        Debug.Log($"[KeypadBridge] Received: {payload}");

        if (numpadController == null || !numpadController.gameObject.activeSelf) return;

        numpadController.ButtonPressed(payload);
    }
}
