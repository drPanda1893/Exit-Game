using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Level 5 – Werkstatt: Schaltkreis-Rotations-Puzzle.
/// Drehe die Kabel-Segmente so, dass Strom von der Quelle (links)
/// zum Bunsenbrenner (rechts) fließt.
/// WASD / Pfeiltasten → Tile auswählen, ENTER / LEERTASTE → 90° drehen.
/// </summary>
public class Level5_Breadboard : MonoBehaviour
{
    // ── Serialized (Builder setzt diese Felder via Reflection) ───────────
    [Header("Grid")]
    [SerializeField] private RectTransform[] tileRoots;   // 25 Entries, row-major (row 0 = oben)

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Image           burnerFlame; // Bunsenbrenner-Flamme (dunkel→orange)

    [Header("Verhalten")]
    [Tooltip("Standalone-Szene: nach Lösung Fade-To-Black + Level 6 laden. "
           + "Als Overlay: false – nur OnPuzzleSolved feuern.")]
    [SerializeField] private bool autoTransitionAfterSolve = true;

    /// <summary>Feuert genau einmal, sobald der Schaltkreis geschlossen ist.</summary>
    public event Action OnPuzzleSolved;

    // ── Puzzle-Definition ─────────────────────────────────────────────────
    // TileType: 0=Leer  1=Gerade  2=Ecke  3=Quelle(fix)  4=Ziel(fix)
    // Connections-Base: Gerade=[N,S], Ecke=[N,E]
    // Rotation: 0..3 × 90° CW; [N,E,S,W] dreht zu [W,N,E,S]

    private const int ROWS = 5, COLS = 5;

    // Tile-Typen (row 0 = oben)
    private static readonly int[] TILE_TYPES =
    {
        0, 0, 2, 1, 2,   // Zeile 0
        0, 0, 1, 0, 1,   // Zeile 1
        3, 1, 2, 0, 4,   // Zeile 2  (3=Quelle links, 4=Ziel rechts)
        0, 0, 0, 0, 0,   // Zeile 3
        0, 0, 0, 0, 0,   // Zeile 4
    };

    // Gelöste Rotationen:
    // (0,2)→Ecke rot1=ES  (0,3)→Gerade rot1=EW  (0,4)→Ecke rot2=SW
    // (1,2)→Gerade rot0=NS  (1,4)→Gerade rot0=NS
    // (2,1)→Gerade rot1=EW  (2,2)→Ecke rot3=NW
    private static readonly int[] SOLVED_ROT =
    {
        0, 0, 1, 1, 2,
        0, 0, 0, 0, 0,
        0, 1, 3, 0, 0,
        0, 0, 0, 0, 0,
        0, 0, 0, 0, 0,
    };

    // Startrotationen (absichtlich falsch, damit Puzzle lösbar aber nicht trivial)
    private static readonly int[] START_ROT =
    {
        0, 0, 2, 0, 1,
        0, 0, 1, 0, 1,
        0, 0, 1, 0, 0,
        0, 0, 0, 0, 0,
        0, 0, 0, 0, 0,
    };

    // ── Laufzeit ──────────────────────────────────────────────────────────
    private int[] curRot;
    private int   selRow = 2, selCol = 1;
    private bool  solved;
    private bool  active;

    private static readonly Color ColOn  = new Color(0.18f, 0.92f, 0.38f);
    private static readonly Color ColOff = new Color(0.22f, 0.26f, 0.32f);
    private static readonly Color ColSrc = new Color(0.95f, 0.65f, 0.05f);
    private static readonly Color ColSel = new Color(0.95f, 0.92f, 0.15f);

    // ═════════════════════════════════════════════════════════════════════

    void OnEnable()
    {
        solved = false;
        active = false;
        if (statusText) statusText.text = string.Empty;

        if (BigYahuDialogSystem.Instance != null)
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Big Yahu: Die Werkstatt! Das Kabel-Netz ist durcheinander geraten.",
                "Big Yahu: Verbinde Strom-Quelle mit dem Bunsenbrenner!",
                "Big Yahu: WASD zum Auswählen, ENTER oder LEERTASTE zum Drehen."
            }, StartPuzzle);
        else
            StartPuzzle();
    }

    void Start()
    {
        if (curRot == null) StartPuzzle();
    }

    void StartPuzzle()
    {
        curRot = (int[])START_ROT.Clone();
        ApplyAllRotations();
        RefreshColors(GetConnectedSet());
        UpdateSelectionFrame();
        active = true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Eingabe
    // ─────────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!active || solved) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        int dr = 0, dc = 0;
        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)    dr = -1;
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)  dr =  1;
        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)  dc = -1;
        if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) dc =  1;

        if (dr != 0 || dc != 0)
        {
            int nr = Mathf.Clamp(selRow + dr, 0, ROWS - 1);
            int nc = Mathf.Clamp(selCol + dc, 0, COLS - 1);
            // Überspringe leere Tiles
            if (TILE_TYPES[nr * COLS + nc] != 0) { selRow = nr; selCol = nc; }
            UpdateSelectionFrame();
        }

        if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            RotateTile(selRow, selCol);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Drehen (auch via UI-Klick aufrufbar)
    // ─────────────────────────────────────────────────────────────────────

    public void RotateTile(int row, int col)
    {
        int idx = row * COLS + col;
        int t   = TILE_TYPES[idx];
        if (t == 0 || t == 3 || t == 4) return;

        curRot[idx] = (curRot[idx] + 1) % 4;
        ApplyRotation(idx);

        var connected = GetConnectedSet();
        RefreshColors(connected);
        if (connected.Contains(2 * COLS + 4)) OnSolved();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Verbindungs-BFS
    // ─────────────────────────────────────────────────────────────────────

    static bool[] GetConn(int tileType, int rot)
    {
        bool[] b = tileType switch
        {
            1 => new[] { true,  false, true,  false },  // Gerade N+S
            2 => new[] { true,  true,  false, false },  // Ecke   N+E
            3 => new[] { false, true,  false, false },  // Quelle →E
            4 => new[] { true,  false, false, false },  // Ziel   ←N
            _ => new[] { false, false, false, false },
        };
        // rot × 90° CW: [N,E,S,W] → CW → [W,N,E,S]
        for (int r = 0; r < rot; r++)
        {
            bool w = b[3];
            b[3] = b[2]; b[2] = b[1]; b[1] = b[0]; b[0] = w;
        }
        return b;
    }

    HashSet<int> GetConnectedSet()
    {
        var vis   = new HashSet<int>();
        var queue = new Queue<int>();
        int src   = 2 * COLS + 0;
        vis.Add(src); queue.Enqueue(src);

        int[] DR = { -1, 0, 1, 0 };
        int[] DC = {  0, 1, 0,-1 };
        int[] OPP= {  2, 3, 0, 1 };

        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            int cr = cur / COLS, cc = cur % COLS;
            var myConn = GetConn(TILE_TYPES[cur], curRot[cur]);

            for (int d = 0; d < 4; d++)
            {
                if (!myConn[d]) continue;
                int nr = cr + DR[d], nc = cc + DC[d];
                if (nr < 0 || nr >= ROWS || nc < 0 || nc >= COLS) continue;
                int ni = nr * COLS + nc;
                if (vis.Contains(ni)) continue;
                if (!GetConn(TILE_TYPES[ni], curRot[ni])[OPP[d]]) continue;
                vis.Add(ni); queue.Enqueue(ni);
            }
        }
        return vis;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Visuals
    // ─────────────────────────────────────────────────────────────────────

    void ApplyAllRotations()
    {
        for (int i = 0; i < ROWS * COLS; i++) ApplyRotation(i);
    }

    void ApplyRotation(int idx)
    {
        if (tileRoots == null || idx >= tileRoots.Length || tileRoots[idx] == null) return;
        var interior = tileRoots[idx].Find("Interior");
        if (interior != null)
            interior.localRotation = Quaternion.Euler(0, 0, -90f * curRot[idx]);
    }

    void RefreshColors(HashSet<int> connected)
    {
        if (tileRoots == null) return;
        for (int i = 0; i < ROWS * COLS; i++)
        {
            if (tileRoots[i] == null) continue;
            int t = TILE_TYPES[i];
            if (t == 0) continue;
            Color c = (t == 3 || t == 4) ? ColSrc
                    : connected.Contains(i) ? ColOn
                    : ColOff;
            PaintInterior(tileRoots[i], c);
        }
    }

    void UpdateSelectionFrame()
    {
        if (tileRoots == null) return;
        for (int i = 0; i < ROWS * COLS; i++)
        {
            if (tileRoots[i] == null) continue;
            var f = tileRoots[i].Find("SelFrame");
            if (f) f.gameObject.SetActive(i == selRow * COLS + selCol);
        }
    }

    static void PaintInterior(RectTransform root, Color col)
    {
        var interior = root.Find("Interior");
        if (interior == null) return;
        foreach (var img in interior.GetComponentsInChildren<Image>(true))
            img.color = col;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Gewinn
    // ─────────────────────────────────────────────────────────────────────

    void OnSolved()
    {
        solved = true;
        active = false;
        if (statusText) statusText.text = "Schaltkreis geschlossen!  Bunsenbrenner aktiviert!";
        StartCoroutine(SolveRoutine());
    }

    IEnumerator SolveRoutine()
    {
        // Alle Tiles leuchten grün
        if (tileRoots != null)
            foreach (var tr in tileRoots)
                if (tr != null) PaintInterior(tr, ColOn);

        // Bunsenbrenner-Flamme leuchtet auf
        if (burnerFlame != null)
        {
            float t = 0f;
            Color dark   = new Color(0.20f, 0.15f, 0.08f);
            Color bright = new Color(1.00f, 0.48f, 0.02f);
            while (t < 1f)
            {
                t += Time.deltaTime * 1.2f;
                burnerFlame.color = Color.Lerp(dark, bright, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.8f);

        OnPuzzleSolved?.Invoke();

        if (!autoTransitionAfterSolve) yield break;

        if (BigYahuDialogSystem.Instance != null)
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Big Yahu: Ausgezeichnet! Der Bunsenbrenner brennt!",
                "Big Yahu: Jetzt können wir das Schloss aufschmelzen – zum Tor!"
            }, () => StartCoroutine(FadeOut()));
        else
            StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        Canvas root = GetComponentInParent<Canvas>();
        CanvasGroup cg = null;
        if (root != null)
        {
            var go = new GameObject("Fade");
            go.transform.SetParent(root.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = Color.black;
            cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
        }
        float t = 0f;
        while (t < 1f) { t += Time.deltaTime * 0.65f; if (cg) cg.alpha = t; yield return null; }

        if (GameManager.Instance != null) GameManager.Instance.CompleteCurrentLevel();
        else SceneManager.LoadScene("Level6");
    }
}
