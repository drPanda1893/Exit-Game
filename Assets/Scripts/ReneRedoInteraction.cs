using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rene Redo steht nach dem Aufschweißen des Tors in der Freiheit hinter dem Tor.
/// Trigger-Zone: Spieler im Radius → Hinweis "[E] Sprechen".
/// Drückt der Spieler E:
///   1. Kamera zoomt nah an Renes Gesicht (Cinematic-Close-up)
///   2. AudioClip wird gestartet und dreimal hintereinander abgespielt
///   3. Nach dem dritten Durchlauf → Kamera zurueck + Level6_FinalGate.ShowWinScreen()
/// </summary>
[RequireComponent(typeof(Collider))]
public class ReneRedoInteraction : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject hintGO;

    [Header("Audio")]
    [SerializeField] private AudioClip voiceClip;
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Min(1)] private int loopCount = 3;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("Cinematic-Kamera")]
    [Tooltip("Auf welche Stelle die Kamera schaut – z. B. Renes Kopf-Bone oder ein leeres Anchor-GO.")]
    [SerializeField] private Transform faceAnchor;
    [Tooltip("Abstand der Kamera zum Face-Anchor (Meter).")]
    [SerializeField] private float cameraDistance = 2.0f;
    [Tooltip("Hoehen-Offset relativ zum Anchor (Meter, positiv = hoeher).")]
    [SerializeField] private float cameraHeightOffset = 0.10f;
    [Tooltip("Sekunden fuer den weichen Zoom-In bzw. Zoom-Out.")]
    [SerializeField] private float blendSeconds = 0.6f;

    private bool inRange;
    private bool playing;
    private Coroutine playRoutine;
    private TopDownCameraFollow follow;
    private Camera cinematicCam;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Start()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        if (hintGO != null) hintGO.SetActive(false);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop        = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume       = volume;
    }

    void Update()
    {
        if (!inRange || playing) return;
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            StartTalk();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        inRange = true;
        if (hintGO != null && !playing) hintGO.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        inRange = false;
        if (!playing && hintGO != null) hintGO.SetActive(false);
    }

    private void StartTalk()
    {
        playing = true;
        if (hintGO != null) hintGO.SetActive(false);

        // Falls Big Yahus Freiheits-Dialog noch sichtbar ist (schwarzer Balken
        // am unteren Rand), forciert schliessen – die Cinematic gehoert Rene allein.
        BigYahuDialogSystem.Instance?.HideDialog();

        // Hintergrundmusik komplett stoppen, damit nur noch Renes Stimme zu
        // hoeren ist. Stoppt auch alle weiteren als BackgroundMusic markierten
        // AudioSources – wir suchen sicherheitshalber alle in der Scene.
        BackgroundMusic.StopAll();
        foreach (var bgm in FindObjectsByType<BackgroundMusic>(FindObjectsSortMode.None))
        {
            var s = bgm.GetComponent<AudioSource>();
            if (s != null) s.Stop();
        }

        if (playRoutine != null) StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(TalkRoutine());
    }

    IEnumerator TalkRoutine()
    {
        // 1. Kamera-Close-up vorbereiten
        cinematicCam = Camera.main;
        if (cinematicCam == null)
        {
            cinematicCam = FindAnyObjectByType<Camera>();
        }

        if (cinematicCam != null)
        {
            follow = cinematicCam.GetComponent<TopDownCameraFollow>();
            if (follow != null) follow.enabled = false;
            Vector3 camStartPos = cinematicCam.transform.position;
            Quaternion camStartRot = cinematicCam.transform.rotation;

            (Vector3 targetPos, Quaternion targetRot) = ComputeCloseupTransform();
            yield return BlendCamera(cinematicCam, camStartPos, camStartRot, targetPos, targetRot, blendSeconds);
        }

        // 2. AudioClip dreimal hintereinander spielen.
        if (audioSource != null && voiceClip != null)
        {
            // MP3-Clips koennen mit LoadType "Compressed In Memory" verzoegert
            // laden – auf "ready" warten, sonst startet Play() ohne Daten.
            if (voiceClip.loadState != AudioDataLoadState.Loaded)
            {
                voiceClip.LoadAudioData();
                float t0 = Time.realtimeSinceStartup;
                while (voiceClip.loadState == AudioDataLoadState.Loading &&
                       Time.realtimeSinceStartup - t0 < 2f)
                    yield return null;
            }

            audioSource.clip         = voiceClip;
            audioSource.loop         = false;
            audioSource.volume       = volume;
            audioSource.spatialBlend = 0f;
            audioSource.mute         = false;
            audioSource.bypassEffects = false;
            audioSource.bypassListenerEffects = false;
            audioSource.bypassReverbZones = false;

            // Clip-Laenge ist robuster als isPlaying – isPlaying ist im selben
            // Frame nach Play() teils noch false und der while-Loop bricht
            // sofort ab. Wir warten stattdessen die exakte Clip-Dauer pro Loop.
            float clipLength = voiceClip.length;
            for (int i = 0; i < loopCount; i++)
            {
                audioSource.Stop();
                audioSource.time = 0f;
                audioSource.Play();
                Debug.Log($"[ReneRedo] Voice-Loop {i + 1}/{loopCount} gestartet ({clipLength:F2}s)");
                yield return new WaitForSeconds(clipLength);
            }
            audioSource.Stop();
        }
        else
        {
            Debug.LogWarning("[ReneRedo] Kein AudioClip/Source – ueberspringe Voice-Playback. " +
                             $"voiceClip={voiceClip}, audioSource={audioSource}");
            yield return new WaitForSeconds(1.5f);
        }

        // 3. Kamera bleibt im Close-up – KEIN Zurueck-Blenden, TopDownCameraFollow
        //    bleibt deaktiviert, damit die Pose erhalten bleibt.

        // 4. Timer stoppen + LCD-"ENDLICH FREI"-Wechselanzeige starten,
        //    danach Win-Screen einblenden.
        GameTimerLcd.Instance?.FinishAndCelebrate();

        if (Level6_FinalGate.Instance != null)
            Level6_FinalGate.Instance.ShowWinScreen();

        playing = false;
        playRoutine = null;
    }

    (Vector3 pos, Quaternion rot) ComputeCloseupTransform()
    {
        Transform anchor = faceAnchor != null ? faceAnchor : transform;
        // Rene schaut Richtung -Z (Tor). Kamera soll IHM ins Gesicht schauen, also
        // VOR ihm stehen – entlang seines Forward-Vektors.
        Vector3 fwd = anchor.forward;
        Vector3 facePos = anchor.position + Vector3.up * cameraHeightOffset;
        Vector3 camPos  = facePos + fwd * cameraDistance;
        Quaternion camRot = Quaternion.LookRotation((facePos - camPos).normalized, Vector3.up);
        return (camPos, camRot);
    }

    IEnumerator BlendCamera(Camera cam,
                            Vector3 fromPos, Quaternion fromRot,
                            Vector3 toPos,   Quaternion toRot,
                            float duration)
    {
        if (cam == null || duration <= 0f)
        {
            if (cam != null) { cam.transform.position = toPos; cam.transform.rotation = toRot; }
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            cam.transform.position = Vector3.Lerp(fromPos, toPos, s);
            cam.transform.rotation = Quaternion.Slerp(fromRot, toRot, s);
            yield return null;
        }
        cam.transform.position = toPos;
        cam.transform.rotation = toRot;
    }
}
