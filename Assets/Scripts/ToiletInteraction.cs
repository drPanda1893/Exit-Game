using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class ToiletInteraction : MonoBehaviour
{
    [Header("References")]
    public NumpadController numpad;
    public TextMeshProUGUI hintText;

    [Header("Settings")]
    [SerializeField] private string hintMessage = "Drücke [E] um das Numpad zu öffnen";

    private bool isPlayerNear = false;

    private void Update()
    {
        if (!isPlayerNear || Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
            numpad?.Toggle();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerNear = true;
        SetHint(hintMessage);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerNear = false;
        SetHint("");

        if (numpad != null && numpad.gameObject.activeSelf)
            numpad.Hide();
    }

    private void SetHint(string message)
    {
        if (hintText != null)
            hintText.text = message;
    }
}
