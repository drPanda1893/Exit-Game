using System.Collections;
using UnityEngine;

/// <summary>
/// Spielzeit-Singleton.
/// - Bootstrapt automatisch beim Unity-Start (vor jeder Scene). Damit laeuft
///   die Uhr ab dem Moment, in dem das Spiel anspringt.
/// - Anzeige in ganzen Spielminuten ("00 MIN" … "99 MIN"), update nur bei
///   echtem Minutenwechsel – serieller Verkehr also max. 1x pro 60 s.
/// - LCD-Command-ID 0x70 ueber ArduinoBridge. Solange die Bridge noch nicht
///   verbunden ist, queuen wir den letzten Wert und schicken ihn, sobald sie
///   da ist (keine verpassten Updates).
/// - Nach <see cref="FinishAndCelebrate"/> wechselt das LCD zwischen Endzeit
///   und "ENDLICH FREI" im 2-Sekunden-Rhythmus.
/// </summary>
public class GameTimerLcd : MonoBehaviour
{
    public const byte LCD_CMD_ID = 0x70;

    public static GameTimerLcd Instance { get; private set; }

    public bool  IsRunning      => _running;
    public float ElapsedSeconds => _elapsedSeconds;
    public int   ElapsedMinutes => Mathf.FloorToInt(_elapsedSeconds / 60f);

    private float     _elapsedSeconds;
    private bool      _running;
    private string    _pending = string.Empty;   // Letzter zu sendender Wert
    private string    _onAir   = string.Empty;   // Was tatsaechlich angekommen ist
    private Coroutine _tickRoutine;
    private Coroutine _celebrateRoutine;

    // ─────────────────────────────────────────────────────────────────────────
    // Auto-Bootstrap: beim allerersten Szenen-Load wird ein GameTimerLcd-GO
    // im DontDestroyOnLoad-Bereich erzeugt, BEVOR irgendein Level-Skript laeuft.
    // Damit startet die Uhr garantiert sofort beim Unity-Start.
    // ─────────────────────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("GameTimerLcd");
        DontDestroyOnLoad(go);
        var t = go.AddComponent<GameTimerLcd>();
        t.Begin();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Startet den Timer von 00 MIN. Idempotent.</summary>
    public void Begin()
    {
        if (_running) return;
        StopCelebrate();
        _running        = true;
        _elapsedSeconds = 0f;
        _onAir          = string.Empty;
        _pending        = BuildTimerPayload();
        TryFlushToLcd();
        if (_tickRoutine == null) _tickRoutine = StartCoroutine(TickRoutine());
    }

    /// <summary>Stoppt den Timer. Wert bleibt erhalten.</summary>
    public void Stop()
    {
        _running = false;
        if (_tickRoutine != null) { StopCoroutine(_tickRoutine); _tickRoutine = null; }
    }

    /// <summary>Reset auf 00 MIN (z. B. fuer Neustart).</summary>
    public void ResetTimer()
    {
        Stop();
        StopCelebrate();
        _elapsedSeconds = 0f;
        _onAir          = string.Empty;
        _pending        = BuildTimerPayload();
        TryFlushToLcd();
    }

    /// <summary>
    /// Nach Renes Dialog: Timer stoppen und das LCD im 2-Sekunden-Rhythmus
    /// zwischen Endzeit und "ENDLICH FREI" wechseln.
    /// </summary>
    public void FinishAndCelebrate()
    {
        Stop();
        StopCelebrate();
        _celebrateRoutine = StartCoroutine(CelebrateRoutine());
    }

    /// <summary>Liefert die Zeit als "MM MIN" String.</summary>
    public string FormattedTime => FormatMin(ElapsedMinutes);

    // ─────────────────────────────────────────────────────────────────────────
    // Coroutines
    // ─────────────────────────────────────────────────────────────────────────

    IEnumerator TickRoutine()
    {
        // 1-Sekunden-Wait – kein Frame-by-Frame-Update.
        var wait = new WaitForSecondsRealtime(1f);
        while (_running)
        {
            _elapsedSeconds += 1f;
            _pending = BuildTimerPayload();
            TryFlushToLcd();
            yield return wait;
        }
        _tickRoutine = null;
    }

    IEnumerator CelebrateRoutine()
    {
        var wait = new WaitForSecondsRealtime(2f);
        string finalPayload = BuildTimerPayload();        // "Spielzeit:|MM:SS"
        string freedomPayload = "ENDLICH FREI|";          // Zeile 1 leer
        bool showTime = false;
        while (true)
        {
            showTime  = !showTime;
            _pending  = showTime ? finalPayload : freedomPayload;
            _onAir    = string.Empty;   // erzwingen, dass es geschickt wird
            TryFlushToLcd();
            yield return wait;
        }
    }

    void StopCelebrate()
    {
        if (_celebrateRoutine != null) { StopCoroutine(_celebrateRoutine); _celebrateRoutine = null; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    static string FormatMin(int mm)
    {
        if (mm < 0)  mm = 0;
        if (mm > 99) mm = 99;
        return $"{mm:00} MIN";
    }

    /// <summary>
    /// Baut das Zwei-Zeilen-Payload fuers LCD: oben "Spielzeit:", unten "MM:SS".
    /// Format wird im Arduino-Sketch am '|' aufgesplittet.
    /// </summary>
    string BuildTimerPayload()
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(_elapsedSeconds));
        int mm = (total / 60) % 100;
        int ss = total % 60;
        return $"Spielzeit:|{mm:00}:{ss:00}";
    }

    private float _lastDisconnectLog;

    /// <summary>
    /// Versucht den zuletzt berechneten Wert ans LCD zu schicken. Nur wenn die
    /// ArduinoBridge erreichbar ist UND der Wert sich seit dem letzten Send
    /// geaendert hat – sonst kein serieller Traffic.
    /// </summary>
    void TryFlushToLcd()
    {
        if (string.IsNullOrEmpty(_pending) || _pending == _onAir) return;
        var br = ArduinoBridge.Instance;
        if (br == null || !br.IsConnected)
        {
            // Rate-limited Warnung (1x pro 10 s) damit wir im Log sehen koennen,
            // wenn die Bridge fehlt – sonst sucht man stundenlang die Ursache.
            if (Time.realtimeSinceStartup - _lastDisconnectLog > 10f)
            {
                Debug.Log("[GameTimerLcd] LCD-Send pending '" + _pending +
                          "' – ArduinoBridge " + (br == null ? "nicht in Scene" : "nicht verbunden") + ".");
                _lastDisconnectLog = Time.realtimeSinceStartup;
            }
            return;
        }
        br.Send(LCD_CMD_ID, _pending);
        Debug.Log("[GameTimerLcd] → 70:" + _pending);
        _onAir = _pending;
    }
}
