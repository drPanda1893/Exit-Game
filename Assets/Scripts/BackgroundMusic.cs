using UnityEngine;

/// <summary>
/// Spielt einen AudioClip dauerhaft auf Loop ab.
/// DontDestroyOnLoad sorgt dafür dass die Musik beim Szenenwechsel weiterläuft.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BackgroundMusic : MonoBehaviour
{
    private static BackgroundMusic instance;
    public static BackgroundMusic Instance => instance;

    public AudioSource Source => GetComponent<AudioSource>();

    public static void StopAll()
    {
        if (instance != null && instance.Source != null && instance.Source.isPlaying)
            instance.Source.Stop();
    }

    /// <summary>Pausiert die Hintergrundmusik – Wiedergabezeit bleibt erhalten.</summary>
    public static void PauseAll()
    {
        if (instance != null && instance.Source != null && instance.Source.isPlaying)
            instance.Source.Pause();
    }

    /// <summary>Setzt die Hintergrundmusik an der gepausten Stelle fort.</summary>
    public static void ResumeAll()
    {
        if (instance == null || instance.Source == null) return;
        var s = instance.Source;
        if (!s.isPlaying) s.UnPause();
        if (!s.isPlaying) s.Play();
    }

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        var src = GetComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = true;
        if (!src.isPlaying)
            src.Play();
    }
}
