using UnityEngine;
using TMPro;
using System;

public class NumpadController : MonoBehaviour
{
    [Header("Display")]
    public TextMeshProUGUI displayText;
    [SerializeField] private int maxDigits = 4;

    public event Action<string> OnCodeEntered;

    private string currentCode = "";

    public void ButtonPressed(string value)
    {
        if (value == "DEL")
        {
            if (currentCode.Length > 0)
                currentCode = currentCode[..^1];
        }
        else if (currentCode.Length < maxDigits)
        {
            currentCode += value;
            if (currentCode.Length == maxDigits)
                OnCodeEntered?.Invoke(currentCode);
        }

        UpdateDisplay();
    }

    public void ResetCode()
    {
        currentCode = "";
        UpdateDisplay();
    }

    public void Show()
    {
        ResetCode();
        gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Toggle()
    {
        if (gameObject.activeSelf) Hide();
        else Show();
    }

    private void UpdateDisplay()
    {
        displayText.text = currentCode.PadRight(maxDigits, '_');
    }
}
