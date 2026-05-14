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
