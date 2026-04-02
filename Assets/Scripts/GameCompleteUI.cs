using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

/// <summary>
/// Wird aktiviert wenn der Spieler den Ausgang von Level 2 betritt.
/// Zeigt "Geheimnis gelüftet!" Bildschirm und erlaubt Neustart.
/// </summary>
public class GameCompleteUI : MonoBehaviour
{
    [Header("UI")]
    public Canvas overlayCanvas;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;
    public Button restartButton;

    private bool triggered = false;

    void Start()
    {
        if (overlayCanvas != null)
            overlayCanvas.gameObject.SetActive(false);
    }

    public void TriggerComplete()
    {
        if (triggered) return;
        triggered = true;
        StartCoroutine(ShowComplete());
    }

    private IEnumerator ShowComplete()
    {
        // Spieler einfrieren
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
            var pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = false;
        }

        yield return new WaitForSeconds(0.4f);

        if (overlayCanvas != null)
            overlayCanvas.gameObject.SetActive(true);
    }

    public void Restart()
    {
        SceneManager.LoadScene("Level1");
    }
}
