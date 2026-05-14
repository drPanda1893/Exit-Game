using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// Übergangs-Cinematic zwischen Level 3 und Level 4.
/// 1. Pausiert die Hintergrundmusik.
/// 2. Zeigt ein schwarzes Vollbild mit der Schock-Zeile.
/// 3. Spielt das WhatsApp-Video aus dem Rainer-Wächtler-Ordner full-screen ab.
/// 4. Laedt Level 4 (per GameManager.CompleteCurrentLevel falls vorhanden, sonst Scene-Load).
/// </summary>
public class Level3to4Cinematic : MonoBehaviour
{
    public string nextSceneName  = "Level4";
    public float  textHoldSeconds = 6.0f;       // laenger fuer den Schock-Moment
    public float  fadeToVideoSeconds = 0.8f;    // weicher Uebergang Text → Video
    public string videoFolder = "Assets/Scripts/Rainer Wächtler";
    public string preferredVideoName = "Dragon Monday";

    private Canvas      _canvas;
    private Image       _blackPanel;
    private TextMeshProUGUI _line;
    private RawImage    _videoOut;
    private AspectRatioFitter _videoFitter;
    private VideoPlayer _video;
    private RenderTexture _rt;

    public static void Play()
    {
        // Falls schon eine Cinematic laeuft, nicht doppelt starten.
        if (FindAnyObjectByType<Level3to4Cinematic>() != null) return;
        var go = new GameObject("Level3to4Cinematic");
        DontDestroyOnLoad(go);
        go.AddComponent<Level3to4Cinematic>();
    }

    void Awake()
    {
        BackgroundMusic.PauseAll();
        BuildUI();
    }

    void Start()
    {
        StartCoroutine(Run());
    }

    void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;          // ueber allem
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        // Schwarze Vollbild-Flaeche.
        var bgGO = new GameObject("BlackBG");
        bgGO.transform.SetParent(transform, false);
        _blackPanel = bgGO.AddComponent<Image>();
        _blackPanel.color = Color.black;
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

        // Text-Zeile.
        var txtGO = new GameObject("ShockText");
        txtGO.transform.SetParent(transform, false);
        _line = txtGO.AddComponent<TextMeshProUGUI>();
        _line.text      = "AUWEIA NICHT DIE MEDDLER RAINER WÄCHTLER, AN DENEN WERDE ICH NIE VORBEIKOMMEN!";
        _line.fontSize  = 56f;
        _line.fontStyle = FontStyles.Bold;
        _line.alignment = TextAlignmentOptions.Center;
        _line.color     = new Color(1f, 0.85f, 0.15f);
        _line.enableWordWrapping = true;
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0.05f, 0.30f);
        txtRT.anchorMax = new Vector2(0.95f, 0.70f);
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;

        // Video-Output – RawImage + RenderTexture. AspectRatioFitter sorgt dafuer,
        // dass das Video unabhaengig vom Bildschirm-Seitenverhaeltnis ohne
        // Verzerrung "fit"-gerendert wird (schwarzer Letterbox-Rand wenn noetig).
        var vidGO = new GameObject("VideoOut");
        vidGO.transform.SetParent(transform, false);
        _videoOut = vidGO.AddComponent<RawImage>();
        _videoOut.color   = Color.white;
        _videoOut.enabled = false;          // erst beim Abspielen einblenden
        var fitter = vidGO.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;       // wird gleich aus dem Clip aktualisiert
        var vidRT = vidGO.GetComponent<RectTransform>();
        vidRT.anchorMin = new Vector2(0.05f, 0.05f);
        vidRT.anchorMax = new Vector2(0.95f, 0.95f);
        vidRT.offsetMin = Vector2.zero; vidRT.offsetMax = Vector2.zero;
        _videoFitter = fitter;

        // RenderTexture passend zur Bildschirmaufloesung. Wir legen sie etwas
        // grosszuegig an (max 1920x1080), damit Skalierung auf einem grossen
        // Game-View nicht im Player Ressourcen frisst und ruckelt.
        int w = Mathf.Clamp(Screen.width,  1280, 1920);
        int h = Mathf.Clamp(Screen.height, 720,  1080);
        _rt = new RenderTexture(w, h, 0, RenderTextureFormat.Default);
        _rt.useMipMap = false;
        _rt.autoGenerateMips = false;
        _rt.Create();
        _videoOut.texture = _rt;

        // VideoPlayer. Audio ueber eigene AudioSource → exaktes Sync und keine
        // Direct-Mode-Glitches. skipOnDrop verhindert Stotter-Cascades bei
        // kurzen Frame-Drops, waitForFirstFrame haelt das Video an, bis das
        // erste Bild da ist (sonst springt es).
        var audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake  = false;
        audioSrc.spatialBlend = 0f;

        _video = gameObject.AddComponent<VideoPlayer>();
        _video.renderMode        = VideoRenderMode.RenderTexture;
        _video.targetTexture     = _rt;
        _video.playOnAwake       = false;
        _video.isLooping         = false;
        _video.skipOnDrop        = true;
        _video.waitForFirstFrame = true;
        _video.playbackSpeed     = 1f;
        _video.timeUpdateMode    = VideoTimeUpdateMode.UnscaledGameTime;
        _video.audioOutputMode   = VideoAudioOutputMode.AudioSource;
        _video.SetTargetAudioSource(0, audioSrc);
    }

    string FindVideoPath()
    {
        string absFolder = Path.Combine(Application.dataPath, "Scripts/Rainer Wächtler");
        if (!Directory.Exists(absFolder)) return null;
        var files = Directory.GetFiles(absFolder, "*.mp4");
        if (files.Length == 0) return null;

        // Bevorzugt 'Dragon Monday' – sonst nimm einfach die erste .mp4.
        foreach (var f in files)
        {
            string name = Path.GetFileNameWithoutExtension(f);
            if (name.Equals(preferredVideoName, System.StringComparison.OrdinalIgnoreCase))
                return f;
        }
        return files[0];
    }

    IEnumerator Run()
    {
        // 1. Schwarz + Text fuer textHoldSeconds.
        yield return new WaitForSecondsRealtime(textHoldSeconds);

        // 2. Video bereits parallel vorbereiten, waehrend der Text noch sichtbar ist.
        string vidPath = FindVideoPath();
        if (vidPath != null)
        {
            _video.url = "file://" + vidPath.Replace('\\', '/');
            _video.Prepare();
            float prep = 0f;
            while (!_video.isPrepared && prep < 5f) { prep += Time.unscaledDeltaTime; yield return null; }

            // Aspect-Ratio des Clips uebernehmen, damit das Video unverzerrt skaliert.
            if (_video.isPrepared && _video.width > 0 && _video.height > 0)
                _videoFitter.aspectRatio = (float)_video.width / _video.height;
        }

        // 3. Text-Fade auf Video. Schwarzes Panel bleibt, Text verschwindet langsam.
        yield return StartCoroutine(FadeText(1f, 0f, fadeToVideoSeconds));
        _line.gameObject.SetActive(false);
        _videoOut.enabled = true;

        if (vidPath != null)
        {
            _video.Play();
            // Kurz Warten, damit der VideoPlayer wirklich startet.
            yield return new WaitForSecondsRealtime(0.1f);

            // Warten bis Video durch ist (oder max 90 s als Sicherheit).
            float guard = 0f;
            while (_video.isPlaying && guard < 90f) { guard += Time.unscaledDeltaTime; yield return null; }
        }
        else
        {
            Debug.LogWarning("[Level3to4Cinematic] Kein Video gefunden in " + videoFolder);
            yield return new WaitForSecondsRealtime(1.5f);
        }

        // 3. Level 4 laden.
        if (GameManager.Instance != null)
            GameManager.Instance.CompleteCurrentLevel();
        else
            SceneManager.LoadScene(nextSceneName);

        // 4. Cinematic-GO loeschen. RenderTexture freigeben.
        if (_rt != null) { _rt.Release(); Destroy(_rt); }
        Destroy(gameObject);
    }

    IEnumerator FadeText(float from, float to, float seconds)
    {
        if (seconds <= 0f || _line == null) { yield break; }
        float t = 0f;
        var startCol = _line.color;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            var c = startCol; c.a = Mathf.Lerp(from, to, s);
            _line.color = c;
            yield return null;
        }
    }
}
