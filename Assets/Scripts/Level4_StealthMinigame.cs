using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Level 4 – Wärter-Stealth-Minigame.
/// – Kleinere Körper-Hitboxen (45 % der visuellen Größe)
/// – Suspicion-Meter statt Sofort-Catch aus dem Sichtkegel
/// – Alarm-Phase nach jedem Erwischt-Vorfall
/// – Frame-unabhängige Bewegung, Wandkollision für Spieler UND Wärter
/// </summary>
public class Level4_StealthMinigame : MonoBehaviour
{
    [Header("Spieler")]
    [SerializeField] private RectTransform player;
    [SerializeField] private float playerSpeed = 185f;

    [Header("Wärter")]
    [SerializeField] private List<RectTransform> guards;
    [SerializeField] private float guardSpeed  = 100f;
    [SerializeField] private float visionRange = 140f;   // Pixel
    [SerializeField] private float visionAngle = 52f;    // halber Kegel-Winkel °

    [Header("Wände")]
    [SerializeField] private List<RectTransform> walls;

    [Header("Ziel")]
    [SerializeField] private RectTransform goal;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private RectTransform   playArea;

    [Header("Arduino-Joystick (Freenove A1/A2/A3)")]
    [SerializeField] private bool  arduinoFallback   = true;
    [SerializeField] private bool  joystickInvertX   = true;   // Freenove-Joystick auf diesem Board: X gespiegelt
    [SerializeField] private bool  joystickInvertY   = false;  // VRY: rauf=positiv = Y oben (UI-Koordinaten)
    [SerializeField] private float joystickDeadzone  = 0.15f;

    private const byte      JOY_CMD = 0x30;
    private Action<string>  joystickHandler;
    private Vector2         joystickAxis;
    private bool            joystickActive;

    // ── Patrol-State ─────────────────────────────────────────────────────
    private Vector2[] guardStartPos;
    private Vector2[] guardDir;
    private float[]   guardOrbitAngle;
    private int[]     guardPattern;     // 0=H, 1=V, 2=Diagonal, 3=Ellipse, 4=Chase

    // ── Spielzustand ─────────────────────────────────────────────────────
    private bool  active;
    private bool  caughtRunning;
    private int   lives   = 3;
    private float elapsed = 0f;

    // ── Suspicion-Meter ───────────────────────────────────────────────────
    // Sichtkegel löst NICHT sofort Catch aus – baut Verdacht auf.
    // Bei 100 % → Erwischt. Verdacht fällt außerhalb des Kegels schneller.
    private float suspicion     = 0f;       // 0..1
    private const float FILL_RATE  = 1f / 1.1f;   // voll in 1,1 s
    private const float DRAIN_RATE = 1f / 0.55f;  // leer in 0,55 s

    // ── Alarm-Phase ───────────────────────────────────────────────────────
    // Nach jedem Erwischt-Vorfall sind Wärter für ALARM_DURATION schneller
    private float alarmTimer  = 0f;
    private const float ALARM_DURATION    = 8f;
    private const float ALARM_SPEED_BONUS = 0.35f;  // +35 %

    // ── Gecachte Spielfeldgrenzen ─────────────────────────────────────────
    private Rect areaRect;

    // ═════════════════════════════════════════════════════════════════════

    void OnEnable()
    {
        active        = false;
        caughtRunning = false;
        lives         = 3;
        elapsed       = 0f;
        suspicion     = 0f;
        alarmTimer    = 0f;
        joystickAxis  = Vector2.zero;
        if (statusText != null) statusText.text = string.Empty;

        EnableJoystickInput();

        if (BigYahuDialogSystem.Instance != null)
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Big Yahu: Achtung! Wärter patrouillieren den Hof!",
                "Big Yahu: Weiche ihren Blicken aus – du hast 3 Leben!"
            }, () => { InitGuards(); ResetGame(); active = true; });
        else
        {
            InitGuards();
            ResetGame();
            active = true;
        }
    }

    void OnDisable()
    {
        DisableJoystickInput();
        joystickAxis = Vector2.zero;
    }

    void Start()
    {
        if (playArea == null) playArea = GetComponent<RectTransform>();
        CacheAreaRect();
        InitGuards();
    }

    void CacheAreaRect()
    {
        Vector2 half = playArea.sizeDelta * 0.5f;
        areaRect = new Rect(-half.x, -half.y, playArea.sizeDelta.x, playArea.sizeDelta.y);
    }

    void InitGuards()
    {
        if (guards == null || guards.Count == 0) return;
        int n           = guards.Count;
        guardStartPos   = new Vector2[n];
        guardDir        = new Vector2[n];
        guardOrbitAngle = new float[n];
        guardPattern    = new int[n];

        for (int i = 0; i < n; i++)
        {
            if (guards[i] == null) continue;
            guardStartPos[i]   = guards[i].anchoredPosition;
            guardOrbitAngle[i] = i * Mathf.PI * 2f / n;
            guardPattern[i]    = i % 5;
            guardDir[i]        = guardPattern[i] == 1 ? Vector2.up
                               : guardPattern[i] == 2 ? new Vector2(1f, 0.7f).normalized
                               : Vector2.right;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!active) return;

        float dt = Time.deltaTime;
        elapsed    += dt;
        alarmTimer  = Mathf.Max(0f, alarmTimer - dt);

        // Speed-Eskalation: +55 % nach ~32 s; Alarm-Bonus obendrauf
        float speedMult = 1f + Mathf.Min(elapsed * 0.017f, 0.55f)
                            + (alarmTimer > 0f ? ALARM_SPEED_BONUS : 0f);

        MovePlayer();
        MoveGuards(speedMult, dt);
        UpdateSuspicion(dt);
        CheckGoal();
    }

    // ── Spieler ──────────────────────────────────────────────────────────

    void MovePlayer()
    {
        float h = 0f, v = 0f;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h = -1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h =  1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v =  1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v = -1f;
        }

        // Joystick übersteuert Tastatur, sobald er außerhalb der Deadzone steht.
        // Analoge Auslenkung → langsamer bei Teil-Ausschlag.
        if (joystickAxis.sqrMagnitude > joystickDeadzone * joystickDeadzone)
        {
            h = Mathf.Clamp(joystickAxis.x, -1f, 1f);
            v = Mathf.Clamp(joystickAxis.y, -1f, 1f);
        }

        Vector2 dir = new Vector2(h, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();   // Diagonale nicht schneller als 1 Achse
        Vector2 delta = dir * playerSpeed * Time.deltaTime;

        // Axis-separated Kollision: nach X-Bewegung NUR in X zurueckschieben,
        // nach Y-Bewegung NUR in Y. Sonst kann der Minimum-Overlap-Pushout
        // den Spieler an Ecken seitlich durch die Wand schieben.
        player.anchoredPosition += new Vector2(delta.x, 0f);
        ResolvePlayerWallsAxis(true);
        player.anchoredPosition += new Vector2(0f, delta.y);
        ResolvePlayerWallsAxis(false);
        ClampToArea(player);
    }

    void ResolvePlayerWallsAxis(bool axisX)
    {
        if (walls == null) return;
        Rect rr = GetRect(player);
        foreach (var wall in walls)
        {
            if (wall == null) continue;
            Rect wr = GetRect(wall);
            if (!rr.Overlaps(wr)) continue;

            Vector2 pos = player.anchoredPosition;
            if (axisX)
            {
                float pushLeft  = rr.xMax - wr.xMin;  // rechts in Wand -> nach links zurueck
                float pushRight = wr.xMax - rr.xMin;  // links in Wand -> nach rechts zurueck
                if (pushLeft < pushRight) pos.x -= pushLeft;
                else                      pos.x += pushRight;
            }
            else
            {
                float pushDown = rr.yMax - wr.yMin;   // oben in Wand -> nach unten zurueck
                float pushUp   = wr.yMax - rr.yMin;   // unten in Wand -> nach oben zurueck
                if (pushDown < pushUp) pos.y -= pushDown;
                else                   pos.y += pushUp;
            }
            player.anchoredPosition = pos;
            rr = GetRect(player);
        }
    }

    // ── Arduino-Joystick (Freenove A1/A2/A3) ─────────────────────────────
    // Nur aktiv, solange dieses Level-Panel an ist. OnEnable/OnDisable
    // registriert/entfernt den Handler und schickt "30:START"/"30:STOP",
    // damit der Sketch A1/A2/A3 nicht spammt, wenn ein anderes Level laeuft.

    void EnableJoystickInput()
    {
        if (!arduinoFallback) return;
        if (joystickActive) return;
        var bridge = ArduinoBridge.Instance;
        if (bridge == null) return;

        joystickHandler = OnJoystickMessage;
        bridge.RegisterHandler(JOY_CMD, joystickHandler);
        bridge.Send(JOY_CMD, "START");
        joystickActive = true;
    }

    void DisableJoystickInput()
    {
        if (!joystickActive) return;
        var bridge = ArduinoBridge.Instance;
        if (bridge != null)
        {
            if (joystickHandler != null) bridge.UnregisterHandler(JOY_CMD, joystickHandler);
            bridge.Send(JOY_CMD, "STOP");
        }
        joystickHandler = null;
        joystickActive  = false;
    }

    void OnJoystickMessage(string payload)
    {
        // erwartet "JOY:<x>,<y>,<btn>" – Button wird hier (noch) nicht genutzt
        if (string.IsNullOrEmpty(payload)) return;
        if (!payload.StartsWith("JOY:", StringComparison.Ordinal)) return;

        var parts = payload.Substring(4).Split(',');
        if (parts.Length < 2) return;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return;

        if (joystickInvertX) x = -x;
        if (joystickInvertY) y = -y;
        joystickAxis = new Vector2(x, y);
    }

    // ── Wärter ───────────────────────────────────────────────────────────

    void MoveGuards(float mult, float dt)
    {
        if (guards == null) return;

        for (int i = 0; i < guards.Count; i++)
        {
            if (guards[i] == null) continue;
            float spd = guardSpeed * mult;
            // guardDir NIE Nullvektor lassen → false-positive-Falle
            if (guardDir[i].sqrMagnitude < 0.001f) guardDir[i] = Vector2.right;

            switch (guardPattern[i])
            {
                case 0: case 1: case 2:
                    MoveLinear(i, spd, dt);
                    break;

                case 3:
                {
                    // Ellipse – MUSS mit dt multipliziert werden (Spinning-Fix)
                    guardOrbitAngle[i] += spd * 0.007f * dt;
                    Vector2 next = guardStartPos[i]
                        + new Vector2(Mathf.Cos(guardOrbitAngle[i]) * 150f,
                                      Mathf.Sin(guardOrbitAngle[i]) * 82f);
                    next.x = Mathf.Clamp(next.x, areaRect.xMin + 22f, areaRect.xMax - 22f);
                    next.y = Mathf.Clamp(next.y, areaRect.yMin + 22f, areaRect.yMax - 22f);
                    Vector2 move = next - guards[i].anchoredPosition;
                    if (move.sqrMagnitude > 0.001f) guardDir[i] = move.normalized;
                    guards[i].anchoredPosition = next;
                    break;
                }

                case 4:
                {
                    // Chase – gedämpfte Lenkung verhindert Oscillation
                    Vector2 toPlayer = player.anchoredPosition - guards[i].anchoredPosition;
                    if (toPlayer.sqrMagnitude > 0.01f)
                        guardDir[i] = Vector2.Lerp(guardDir[i], toPlayer.normalized, 4f * dt).normalized;
                    guards[i].anchoredPosition += guardDir[i] * spd * 0.80f * dt;
                    PushOutWalls(guards[i]);
                    BounceOnBorder(i);
                    break;
                }
            }
        }
    }

    void MoveLinear(int i, float spd, float dt)
    {
        guards[i].anchoredPosition += guardDir[i] * spd * dt;

        // Spielfeldrand-Bounce
        const float margin = 18f;
        Vector2 p = guards[i].anchoredPosition;
        if (p.x <= areaRect.xMin + margin) { guardDir[i].x =  Mathf.Abs(guardDir[i].x); p.x = areaRect.xMin + margin; }
        if (p.x >= areaRect.xMax - margin) { guardDir[i].x = -Mathf.Abs(guardDir[i].x); p.x = areaRect.xMax - margin; }
        if (p.y <= areaRect.yMin + margin) { guardDir[i].y =  Mathf.Abs(guardDir[i].y); p.y = areaRect.yMin + margin; }
        if (p.y >= areaRect.yMax - margin) { guardDir[i].y = -Mathf.Abs(guardDir[i].y); p.y = areaRect.yMax - margin; }
        guards[i].anchoredPosition = p;

        // Wand-Bounce
        if (walls != null)
        {
            Rect gr = GetContactRect(guards[i], 1f);   // volle Größe für Wand-Bounce
            foreach (var w in walls)
            {
                if (w == null) continue;
                Rect wr = GetRect(w);
                if (!gr.Overlaps(wr)) continue;
                float ox = Mathf.Min(gr.xMax - wr.xMin, wr.xMax - gr.xMin);
                float oy = Mathf.Min(gr.yMax - wr.yMin, wr.yMax - gr.yMin);
                if (ox < oy) guardDir[i].x = -guardDir[i].x;
                else         guardDir[i].y = -guardDir[i].y;
                break;
            }
            PushOutWalls(guards[i]);
        }
    }

    void BounceOnBorder(int i)
    {
        const float margin = 18f;
        Vector2 p = guards[i].anchoredPosition;
        if (p.x < areaRect.xMin + margin) { guardDir[i].x =  Mathf.Abs(guardDir[i].x); p.x = areaRect.xMin + margin; }
        if (p.x > areaRect.xMax - margin) { guardDir[i].x = -Mathf.Abs(guardDir[i].x); p.x = areaRect.xMax - margin; }
        if (p.y < areaRect.yMin + margin) { guardDir[i].y =  Mathf.Abs(guardDir[i].y); p.y = areaRect.yMin + margin; }
        if (p.y > areaRect.yMax - margin) { guardDir[i].y = -Mathf.Abs(guardDir[i].y); p.y = areaRect.yMax - margin; }
        guards[i].anchoredPosition = p;
    }

    // ── Wand-Separation ───────────────────────────────────────────────────

    void PushOutWalls(RectTransform rt)
    {
        if (walls == null) return;
        Rect rr = GetRect(rt);
        foreach (var wall in walls)
        {
            if (wall == null) continue;
            Rect wr = GetRect(wall);
            if (!rr.Overlaps(wr)) continue;

            float ol  = rr.xMax - wr.xMin;
            float or_ = wr.xMax - rr.xMin;
            float ob  = rr.yMax - wr.yMin;
            float ot  = wr.yMax - rr.yMin;
            float mn  = Mathf.Min(ol, or_, ob, ot);

            Vector2 pos = rt.anchoredPosition;
            if      (mn == ol)  pos.x -= ol;
            else if (mn == or_) pos.x += or_;
            else if (mn == ob)  pos.y -= ob;
            else                pos.y += ot;
            rt.anchoredPosition = pos;
            rr = GetRect(rt);
        }
    }

    // ── Suspicion-Meter (ersetzt Sofort-Catch aus Sichtkegel) ────────────

    void UpdateSuspicion(float dt)
    {
        if (caughtRunning) return;

        bool inCone = false;

        // Körperkontakt → sofort Erwischt (kein Suspicion-Buffer)
        Rect pr = GetContactRect(player, 1f);
        for (int i = 0; i < guards.Count; i++)
        {
            if (guards[i] == null) continue;
            // Kleiner Hitbox für Körperkontakt (45 % der visuellen Größe)
            if (pr.Overlaps(GetContactRect(guards[i], 0.45f)))
            {
                StartCoroutine(CaughtRoutine());
                return;
            }

            // Sichtkegel
            Vector2 diff = player.anchoredPosition - guards[i].anchoredPosition;
            if (diff.sqrMagnitude > visionRange * visionRange) continue;
            if (guardDir[i].sqrMagnitude < 0.001f) continue;           // kein Richtungsvektor → kein Kegel
            if (Vector2.Angle(guardDir[i], diff) > visionAngle) continue;
            if (WallBlocksLOS(guards[i].anchoredPosition, player.anchoredPosition)) continue;
            inCone = true;
        }

        if (inCone)
        {
            suspicion += FILL_RATE * dt;
            if (suspicion >= 1f)
            {
                suspicion = 0f;
                StartCoroutine(CaughtRoutine());
                return;
            }
        }
        else
        {
            suspicion = Mathf.Max(0f, suspicion - DRAIN_RATE * dt);
        }

        UpdateStatusText();
    }

    void UpdateStatusText()
    {
        if (statusText == null) return;
        if (suspicion <= 0.01f)
        {
            string alarm = alarmTimer > 0f ? $"  [ALARM {alarmTimer:F0}s]" : string.Empty;
            statusText.text = alarm;
            return;
        }

        int pct   = Mathf.RoundToInt(suspicion * 100f);
        int bars  = Mathf.RoundToInt(suspicion * 10f);
        string filled = new string('#', bars);
        string empty  = new string('-', 10 - bars);
        statusText.text = $"! VERDACHT: [{filled}{empty}] {pct}%";
    }

    bool WallBlocksLOS(Vector2 from, Vector2 to)
    {
        if (walls == null) return false;
        for (int s = 1; s <= 8; s++)
        {
            Vector2 pt = Vector2.Lerp(from, to, s / 9f);
            foreach (var w in walls)
                if (w != null && GetRect(w).Contains(pt)) return true;
        }
        return false;
    }

    void CheckGoal()
    {
        if (!active) return;
        if (!GetRect(player).Overlaps(GetRect(goal))) return;
        active = false;
        SnapPlayerInFrontOfGoal();
        if (statusText != null) statusText.text = "Schuppen erreicht! Level abgeschlossen!";
        StartCoroutine(DelayedComplete());
    }

    /// <summary>
    /// Beim Erreichen des Schuppens den Spieler exakt davor positionieren –
    /// nicht halb überlappend. Damit "steht" Big Yahu vor der Tür, wenn
    /// der Fade zum Werkstatt-Level beginnt.
    /// </summary>
    void SnapPlayerInFrontOfGoal()
    {
        if (player == null || goal == null) return;
        float frontY = goal.anchoredPosition.y
                     - goal.sizeDelta.y * 0.5f
                     - player.sizeDelta.y * 0.5f
                     - 4f;   // kleine Lücke, damit Player visuell vor der Tür steht
        player.anchoredPosition = new Vector2(goal.anchoredPosition.x, frontY);
    }

    // ── Coroutines ────────────────────────────────────────────────────────

    IEnumerator CaughtRoutine()
    {
        caughtRunning = true;
        active        = false;
        suspicion     = 0f;
        lives--;

        alarmTimer = ALARM_DURATION;  // Alarm-Phase startet

        string liveTxt = lives > 0 ? $"Leben: {lives}" : "Alle Leben verloren!";
        if (statusText != null) statusText.text = $"Erwischt!  {liveTxt}  [ALARM]";

        BigYahuDialogSystem.Instance?.ShowDialog(
            lives > 0 ? "Big Yahu: Autsch! Noch mal!" : "Big Yahu: Alle Leben verloren!");

        yield return new WaitForSeconds(1.8f);

        if (lives <= 0) lives = 3;
        if (statusText != null) statusText.text = string.Empty;
        caughtRunning = false;
        ResetGame();
        active = true;
    }

    IEnumerator DelayedComplete()
    {
        yield return new WaitForSeconds(0.6f);
        StartCoroutine(BigYahuPullInTransition());
    }

    /// <summary>
    /// "Big Yahu wird in die Matrix gezogen" – Cinematic-Übergang zu Level 5.
    /// </summary>
    IEnumerator BigYahuPullInTransition()
    {
        active = false;

        // ── Dialog: Big Yahu reagiert ──────────────────────────────────
        bool dialogDone = false;
        if (BigYahuDialogSystem.Instance != null)
        {
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Big Yahu: Ausgezeichnet! Die Wärter haben dich nicht erwischt!",
                "Big Yahu: Warte... ich spüre etwas... das System zieht mich rein!",
                "Big Yahu: Die Werkstatt ruft... Finde den Bunsenbrenner!"
            }, () => dialogDone = true);
        }
        else dialogDone = true;

        // Warte bis Dialog fertig (max 6 s)
        float wait = 0f;
        while (!dialogDone && wait < 6f) { wait += Time.deltaTime; yield return null; }

        // ── Erstelle Fade-Overlay (schwarz) über allem ─────────────────
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        CanvasGroup fadeGroup = null;
        if (rootCanvas != null)
        {
            var fadeGO  = new GameObject("FadeOverlay");
            fadeGO.transform.SetParent(rootCanvas.transform, false);
            var fadeRT  = fadeGO.AddComponent<RectTransform>();
            fadeRT.anchorMin = Vector2.zero;
            fadeRT.anchorMax = Vector2.one;
            fadeRT.offsetMin = Vector2.zero;
            fadeRT.offsetMax = Vector2.zero;
            fadeGO.AddComponent<Image>().color = Color.black;
            fadeGroup = fadeGO.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;

            // "Big Yahu wird eingezogen…" Text
            var textGO = new GameObject("PullInText");
            textGO.transform.SetParent(fadeGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = textRT.anchorMax = textRT.pivot = new Vector2(0.5f, 0.5f);
            textRT.anchoredPosition = Vector2.zero;
            textRT.sizeDelta = new Vector2(800f, 100f);
            var txt = textGO.AddComponent<TextMeshProUGUI>();
            txt.text      = "BIG YAHU WIRD IN DIE MATRIX GEZOGEN...";
            txt.fontSize  = 32f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color     = new Color(0.3f, 1f, 0.8f);
            txt.fontStyle = FontStyles.Bold;
        }

        // ── Fade In (schwarz) ──────────────────────────────────────────
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 0.6f;
            if (fadeGroup != null) fadeGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }

        yield return new WaitForSeconds(1.2f);

        // ── Szene wechseln ─────────────────────────────────────────────
        if (GameManager.Instance != null)
            GameManager.Instance.CompleteCurrentLevel();
        else
            SceneManager.LoadScene("Level5");
    }

    // ── Hilfsmethoden ────────────────────────────────────────────────────

    void ResetGame()
    {
        elapsed   = 0f;
        suspicion = 0f;
        if (player != null) player.anchoredPosition = new Vector2(-330f, -250f);
        if (guardStartPos == null || guards == null) return;
        for (int i = 0; i < guards.Count; i++)
            if (guards[i] != null) guards[i].anchoredPosition = guardStartPos[i];
    }

    void ClampToArea(RectTransform rt)
    {
        Vector2 half = rt.sizeDelta * 0.5f;
        Vector2 pos  = rt.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x, areaRect.xMin + half.x, areaRect.xMax - half.x);
        pos.y = Mathf.Clamp(pos.y, areaRect.yMin + half.y, areaRect.yMax - half.y);
        rt.anchoredPosition = pos;
    }

    // Volle Rect für Wandberechnung
    Rect GetRect(RectTransform rt) =>
        new Rect(rt.anchoredPosition - rt.sizeDelta * 0.5f, rt.sizeDelta);

    // Skalierter Rect für Kollisionsabfragen (factor < 1 → kleinere Hitbox)
    Rect GetContactRect(RectTransform rt, float factor)
    {
        Vector2 size = rt.sizeDelta * factor;
        return new Rect(rt.anchoredPosition - size * 0.5f, size);
    }
}
