using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// Zentrales Singleton-Dialogsystem für sämtliche NPC-Gespräche im Spiel.
/// Zeigt Text-Boxen mit Typewriter-Effekt, optionalem Audio und
/// dynamischem Speaker-Wechsel (Big Yahu, Helios, Joshi …).
///
/// <para>
/// Aufruf-Beispiel:
/// <code>
/// BigYahuDialogSystem.Instance.SetSpeaker("Helios", heliosPortrait);
/// BigYahuDialogSystem.Instance.ShowDialog("Willkommen.", () => Debug.Log("done"));
/// </code>
/// </para>
///
/// <para>
/// <b>Standard-Sprecher</b> ist <c>Helios</c> mit gelbem Speaker-Label. Diese Defaults
/// werden in <see cref="Start"/> sowie bei jedem <see cref="SetSpeaker"/>- und
/// <see cref="ResetSpeaker"/>-Aufruf erzwungen — die Speaker-Farbe kann von
/// Designern oder Editor-Tools nicht versehentlich überschrieben werden.
/// </para>
/// </summary>
public class BigYahuDialogSystem : MonoBehaviour
{
    /// <summary>
    /// Globaler Singleton-Zugriff. Wird in <see cref="Awake"/> gesetzt;
    /// Dubletten in derselben Scene werden zerstört.
    /// </summary>
    public static BigYahuDialogSystem Instance { get; private set; }

    /// <summary>
    /// Wurzel-GameObject der Dialog-UI (DialogPanel).
    /// Wird beim Öffnen sichtbar geschaltet und beim Schließen versteckt.
    /// </summary>
    [Header("UI Referenzen")]
    [SerializeField] private GameObject dialogPanel;

    /// <summary>TMP-Field für den eigentlichen Dialog-Text mit Typewriter-Effekt.</summary>
    [SerializeField] private TextMeshProUGUI dialogText;

    /// <summary>
    /// TMP-Field für den Sprecher-Namen ("Helios", "Joshi" …).
    /// Wird zur Laufzeit immer in Gelb dargestellt — siehe <see cref="SetSpeaker"/>
    /// und <see cref="ResetSpeaker"/>.
    /// </summary>
    [SerializeField] private TextMeshProUGUI speakerLabel;

    /// <summary>Image-Komponente, die das Sprecher-Portrait anzeigt.</summary>
    [SerializeField] private Image portraitImage;

    /// <summary>"Weiter"-Button. Alternative zur Tastatur-Bestätigung (Space, Enter).</summary>
    [SerializeField] private Button continueButton;

    /// <summary>
    /// Verzögerung zwischen einzelnen Zeichen beim Typewriter (in Sekunden).
    /// Niedrigere Werte = schnellerer Text-Lauf.
    /// </summary>
    [Header("Einstellungen")]
    [SerializeField] private float typewriterDelay = 0.04f;

    /// <summary>Default-Portrait für Big Yahu. Fallback, falls Helios-Portrait fehlt.</summary>
    [SerializeField] private Sprite bigYahuPortrait;

    /// <summary>Portrait für Helios. Wird per Default beim Spielstart angezeigt.</summary>
    [SerializeField] private Sprite heliosPortrait;

    /// <summary>AudioSource für optionale Voice-Lines pro Dialogzeile.</summary>
    [SerializeField] private AudioSource audioSource;

    /// <summary>
    /// Default-Sprechername. Wird beim Spielstart und bei <see cref="ResetSpeaker"/>
    /// auf das <see cref="speakerLabel"/> geschrieben.
    /// </summary>
    [Header("Default-Sprecher")]
    [SerializeField] private string defaultSpeakerName = "Helios";

    /// <summary>
    /// Public-Read-Only-Zugriff auf das Helios-Portrait, damit andere Skripte
    /// (z.B. <see cref="Level3_ColorPuzzle"/>) das Portrait holen können,
    /// ohne ihre eigene Asset-Referenz halten zu müssen.
    /// </summary>
    public Sprite HeliosPortrait => heliosPortrait;

    /// <summary>FIFO-Queue der noch zu zeigenden Dialogzeilen + zugehörigen Audio-Clips.</summary>
    private readonly Queue<(string text, AudioClip clip)> queue = new();

    /// <summary>True während die Typewriter-Coroutine den aktuellen Text abspult.</summary>
    private bool isTyping;

    /// <summary>Voller Text der aktuellen Zeile — wird bei Skip in einem Rutsch angezeigt.</summary>
    private string fullText;

    /// <summary>Referenz auf die laufende Typewriter-Coroutine, damit sie gestoppt werden kann.</summary>
    private Coroutine typingCoroutine;

    /// <summary>Callback nach der letzten Zeile der aktuellen Sequenz.</summary>
    private Action onComplete;

    /// <summary>
    /// Setzt den Singleton, zerstört Dubletten in derselben Scene
    /// und versteckt das Dialog-Panel initial, damit es beim Szenen-Start
    /// nicht aufflackert.
    /// </summary>
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (dialogPanel) dialogPanel.SetActive(false);
    }

    /// <summary>
    /// Verkabelt den Continue-Button, setzt das Default-Portrait
    /// (Helios mit Big-Yahu-Fallback), erzwingt den Default-Sprechernamen
    /// in Gelb und justiert das DialogText-Layout.
    /// </summary>
    void Start()
    {
        if (continueButton) continueButton.onClick.AddListener(OnContinue);

        if (portraitImage)
            portraitImage.sprite = heliosPortrait != null ? heliosPortrait : bigYahuPortrait;

        if (speakerLabel)
        {
            speakerLabel.text  = defaultSpeakerName;
            speakerLabel.color = Color.yellow;
        }

        EnsureDialogTextTopMargin();
    }

    /// <summary>
    /// Justiert das DialogText-Feld so, dass es perfekt mittig im
    /// schwarzen Dialog-Balken sitzt. Die Methode arbeitet auf drei Ebenen:
    /// <list type="number">
    ///   <item>Großzügige Insets oben (60) und unten (40) via
    ///         <see cref="RectTransform.offsetMax"/> und
    ///         <see cref="RectTransform.offsetMin"/>.</item>
    ///   <item>Explizite <see cref="RectTransform.anchoredPosition"/>-Justierung
    ///         (y = -10), um den Text-Mittelpunkt nach unten in den Balken zu
    ///         schieben. Schützt zusätzlich gegen Setups mit nicht-gestreckten Anchors.</item>
    ///   <item>TMP-Alignment auf <see cref="TMPro.TextAlignmentOptions.MidlineLeft"/>
    ///         — vertikal zentriert im Rect, horizontal linksbündig
    ///         (typewriter-konsistent, wächst nach rechts).</item>
    /// </list>
    /// Wirkt zur Laufzeit unabhängig vom Editor-AutoBuilder.
    /// </summary>
    void EnsureDialogTextTopMargin()
    {
        if (dialogText == null) return;
        var rt = dialogText.rectTransform;

        var offsetMax = rt.offsetMax;
        var offsetMin = rt.offsetMin;
        if (offsetMax.y > -60f) offsetMax.y = -60f;
        if (offsetMin.y <  40f) offsetMin.y =  40f;
        rt.offsetMax = offsetMax;
        rt.offsetMin = offsetMin;

        var pos = rt.anchoredPosition;
        if (pos.y > -10f) pos.y = -10f;
        rt.anchoredPosition = pos;

        dialogText.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
    }

    /// <summary>
    /// Reiht mehrere Zeilen in die Dialog-Queue ein und startet sofort die
    /// erste Anzeige. Nach der letzten Zeile wird <paramref name="onComplete"/>
    /// aufgerufen.
    /// </summary>
    /// <param name="lines">Array der anzuzeigenden Zeilen, in Reihenfolge.</param>
    /// <param name="onComplete">Optionaler Callback nach der letzten Zeile.</param>
    /// <param name="clips">
    /// Optionale Audio-Clips, parallel zu <paramref name="lines"/> indiziert.
    /// Indizes ohne Clip bleiben stumm.
    /// </param>
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

    /// <summary>Convenience-Overload für eine einzelne Zeile.</summary>
    /// <param name="line">Anzuzeigende Zeile.</param>
    /// <param name="onComplete">Optionaler Callback nach Anzeige.</param>
    /// <param name="clip">Optionaler Audio-Clip.</param>
    public void ShowDialog(string line, Action onComplete = null, AudioClip clip = null)
        => ShowDialog(new[] { line }, onComplete, clip != null ? new[] { clip } : null);

    /// <summary>
    /// Setzt den aktiven Sprecher (Name + Portrait). Die Speaker-Farbe wird
    /// hart auf Gelb gesetzt — egal welche Designer-Konfiguration vorher galt.
    /// </summary>
    /// <param name="name">Anzeige-Name (z.B. "Helios", "Joshi").</param>
    /// <param name="portrait">
    /// Optionales Portrait. Bleibt unverändert, falls <c>null</c> übergeben wird.
    /// </param>
    public void SetSpeaker(string name, Sprite portrait = null)
    {
        if (speakerLabel)
        {
            speakerLabel.text  = name;
            speakerLabel.color = Color.yellow;
        }
        if (portraitImage && portrait) portraitImage.sprite = portrait;
    }

    /// <summary>
    /// Setzt den Sprecher zurück auf den Default (Helios + Helios-Portrait).
    /// Nützlich nach NPC-spezifischen Dialog-Sequenzen, um wieder den
    /// allgemeinen Erzähler zu haben.
    /// </summary>
    public void ResetSpeaker()
    {
        if (speakerLabel)
        {
            speakerLabel.text  = defaultSpeakerName;
            speakerLabel.color = Color.yellow;
        }
        if (portraitImage)
        {
            var fallback = heliosPortrait != null ? heliosPortrait : bigYahuPortrait;
            if (fallback != null) portraitImage.sprite = fallback;
        }
    }

    /// <summary>
    /// Pollt Tastatur-Eingabe (Space, Enter), solange das Dialog-Panel aktiv ist
    /// und triggert <see cref="OnContinue"/> bei jeder erkannten Taste.
    /// </summary>
    void Update()
    {
        if (dialogPanel == null || !dialogPanel.activeSelf) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            OnContinue();
    }

    /// <summary>
    /// Holt die nächste Zeile aus der <see cref="queue"/> und startet die
    /// Typewriter-Coroutine. Schließt den Dialog, falls die Queue leer ist.
    /// </summary>
    void ShowNext()
    {
        if (queue.Count == 0) { Close(); return; }

        var (text, clip) = queue.Dequeue();
        fullText = text;

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(Typewrite(text));

        if (audioSource && clip) { audioSource.clip = clip; audioSource.Play(); }
    }

    /// <summary>
    /// Coroutine: schreibt den übergebenen Text Zeichen für Zeichen
    /// in das DialogText-Field, mit <see cref="typewriterDelay"/> zwischen
    /// jedem Zeichen.
    /// </summary>
    /// <param name="text">Anzuzeigender Text.</param>
    /// <returns>Yield-Iterator für Unity's Coroutine-System.</returns>
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

    /// <summary>
    /// Continue-Trigger: Skipt den Typewriter, falls noch am Schreiben,
    /// oder geht zur nächsten Zeile.
    /// </summary>
    void OnContinue()
    {
        if (isTyping)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            dialogText.text = fullText;
            isTyping = false;
        }
        else
        {
            ShowNext();
        }
    }

    /// <summary>
    /// Schließt das Dialog-Panel und ruft den onComplete-Callback auf.
    /// Setzt den Callback danach auf <c>null</c>, um doppelte Aufrufe
    /// zu verhindern.
    /// </summary>
    void Close()
    {
        if (dialogPanel) dialogPanel.SetActive(false);
        onComplete?.Invoke();
        onComplete = null;
    }

    /// <summary>
    /// Forciert das Schliessen des Dialogs ohne den onComplete-Callback aufzurufen.
    /// Nuetzlich, wenn ein anderer Trigger (z. B. Rene Redos Cinematic) das Panel
    /// abrupt verstecken will – egal wie viele Zeilen noch in der Queue stehen.
    /// </summary>
    public void HideDialog()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = null;
        isTyping = false;
        queue.Clear();
        onComplete = null;
        if (audioSource && audioSource.isPlaying) audioSource.Stop();
        if (dialogPanel) dialogPanel.SetActive(false);
    }
}
