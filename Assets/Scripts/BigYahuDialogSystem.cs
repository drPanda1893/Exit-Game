using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Singleton – zeigt Big Yahu's Dialog-Boxen mit Typewriter-Effekt.
/// Aufruf: BigYahuDialogSystem.Instance.ShowDialog("Text", callback);
/// </summary>
public class BigYahuDialogSystem : MonoBehaviour
{
    public static BigYahuDialogSystem Instance { get; private set; }

    [Header("UI Referenzen")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private TextMeshProUGUI dialogText;
    [SerializeField] private TextMeshProUGUI speakerLabel;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Button continueButton;

    [Header("Einstellungen")]
    [SerializeField] private float typewriterDelay = 0.04f;
    [SerializeField] private Sprite bigYahuPortrait;
    [SerializeField] private AudioSource audioSource;

    private readonly Queue<(string text, AudioClip clip)> queue = new();
    private bool isTyping;
    private string fullText;
    private Coroutine typingCoroutine;
    private Action onComplete;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Sofort verstecken, damit kein Flackern beim Start
        if (dialogPanel) dialogPanel.SetActive(false);
    }

    void Start()
    {
        if (continueButton) continueButton.onClick.AddListener(OnContinue);
        if (portraitImage && bigYahuPortrait) portraitImage.sprite = bigYahuPortrait;
        if (speakerLabel) speakerLabel.text = "Big Yahu";
    }

    /// <summary>Zeigt mehrere Zeilen nacheinander. Callback nach letzter Zeile.</summary>
    public void ShowDialog(string[] lines, Action onComplete = null, AudioClip[] clips = null)
    {
        this.onComplete = onComplete;
        queue.Clear();
        for (int i = 0; i < lines.Length; i++)
        {
            AudioClip clip = (clips != null && i < clips.Length) ? clips[i] : null;
            queue.Enqueue((lines[i], clip));
        }
        if (dialogPanel) dialogPanel.SetActive(true);
        ShowNext();
    }

    /// <summary>Zeigt eine einzelne Zeile. Callback danach.</summary>
    public void ShowDialog(string line, Action onComplete = null, AudioClip clip = null)
        => ShowDialog(new[] { line }, onComplete, clip != null ? new[] { clip } : null);

    void ShowNext()
    {
        if (queue.Count == 0) { Close(); return; }

        var (text, clip) = queue.Dequeue();
        fullText = text;

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(Typewrite(text));

        if (audioSource && clip) { audioSource.clip = clip; audioSource.Play(); }
    }

    IEnumerator Typewrite(string text)
    {
        isTyping = true;
        dialogText.text = string.Empty;
        foreach (char c in text)
        {
            dialogText.text += c;
            yield return new WaitForSeconds(typewriterDelay);
        }
        isTyping = false;
    }

    void OnContinue()
    {
        if (isTyping)
        {
            // Sofort vollständigen Text anzeigen
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            dialogText.text = fullText;
            isTyping = false;
        }
        else
        {
            ShowNext();
        }
    }

    void Close()
    {
        if (dialogPanel) dialogPanel.SetActive(false);
        onComplete?.Invoke();
        onComplete = null;
    }
}
