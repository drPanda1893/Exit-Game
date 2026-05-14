using System.Collections;
using UnityEngine;

/// <summary>
/// Spielt zufaellige Rainer-Wächtler-Voice-Clips ab.
/// Immer nur ein Clip gleichzeitig, naechster startet sobald der vorige
/// fertig ist. Laeuft bis <see cref="StopLoop"/> aufgerufen wird (typisch:
/// wenn der Spieler in Level 4 das Ziel erreicht).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RainerVoiceLoop : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private float gapBetweenClipsSeconds = 0.15f;

    private AudioSource _src;
    private Coroutine   _loop;
    private int         _lastIndex = -1;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.loop         = false;
        _src.playOnAwake  = false;
        _src.spatialBlend = 0f;
        _src.volume       = volume;
    }

    void OnEnable()
    {
        if (clips == null || clips.Length == 0) return;
        if (_loop != null) StopCoroutine(_loop);
        _loop = StartCoroutine(LoopRoutine());
    }

    void OnDisable()
    {
        StopLoop();
    }

    /// <summary>Stoppt sofort den aktuellen Clip und die Loop-Coroutine.</summary>
    public void StopLoop()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        if (_src != null && _src.isPlaying) _src.Stop();
    }

    IEnumerator LoopRoutine()
    {
        while (true)
        {
            var clip = PickRandomClip();
            if (clip == null) yield break;

            _src.clip   = clip;
            _src.volume = volume;
            _src.Play();

            // Warte bis der Clip fertig ist. Wir koppeln das an die echte Clip-Laenge
            // statt an isPlaying – das ist robuster gegen Audio-Glitches.
            float wait = clip.length;
            while (wait > 0f)
            {
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (gapBetweenClipsSeconds > 0f)
                yield return new WaitForSecondsRealtime(gapBetweenClipsSeconds);
        }
    }

    AudioClip PickRandomClip()
    {
        if (clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];

        // Nie zweimal hintereinander denselben Clip.
        int idx;
        int safety = 8;
        do
        {
            idx = Random.Range(0, clips.Length);
            safety--;
        }
        while (idx == _lastIndex && safety > 0);

        _lastIndex = idx;
        return clips[idx];
    }
}
