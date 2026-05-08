using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Level 2 – Staubige Wand.
///
/// State-Machine:
///   Idle → JoshiDialog → WaitingInteraction → Scratching → ArrowCombo → Done
///
/// Spieler bekommt Rätseldialog von Joshi, sucht den Staubfleck in der Wand,
/// drückt [E] → Scratch-UI öffnet sich → Pfeil-Combo ↑↑↓↓ → nächstes Level.
/// </summary>
public class Level2_DustWall : MonoBehaviour
{
    public static Level2_DustWall Instance { get; private set; }

    // ── Panels ────────────────────────────────────────────────────────────────

    [Header("Panels")]
    [SerializeField] private GameObject dustWallPanel;
    [SerializeField] private GameObject arrowPanel;
    [SerializeField] private GameObject interactionPrompt;   // "[E] Untersuchen"

    // ── 3D Wand-Trigger ───────────────────────────────────────────────────────

    [Header("3D Interaktion")]
    [SerializeField] private DustyWallSpot dustyWallSpot;

    // ── Scratch-Bereich ───────────────────────────────────────────────────────

    [Header("Scratch-Bereich")]
    [SerializeField] private RawImage dustOverlay;
    [SerializeField] private int brushRadius = 28;
    [SerializeField][Range(0f, 1f)] private float revealThreshold = 0.65f;

    // ── Pfeil-Combo ───────────────────────────────────────────────────────────

    [Header("Pfeil-Combo")]
    [SerializeField] private TextMeshProUGUI arrowHintText;
    [SerializeField] private TextMeshProUGUI inputFeedbackText;
    [SerializeField] private TextMeshProUGUI instructionText;

    // ── State ─────────────────────────────────────────────────────────────────

    private enum State { Idle, WaitingInteraction, Scratching, ArrowCombo, Done }
    private State state = State.Idle;

    private Texture2D dustTex;
    private int totalPixels;
    private int clearedPixels;
    private int comboIndex;

    private static readonly Key[] correctKeys =
        { Key.UpArrow, Key.UpArrow, Key.DownArrow, Key.DownArrow };
    private static readonly string[] arrowSymbols = { "↑", "↑", "↓", "↓" };

    // =========================================================================
    // Lifecycle
    // =========================================================================

    void Awake() => Instance = this;

    void OnEnable()
    {
        state         = State.Idle;
        clearedPixels = 0;
        comboIndex    = 0;
        dustTex       = null;

        if (dustWallPanel)      dustWallPanel.SetActive(false);
        if (arrowPanel)         arrowPanel.SetActive(false);
        if (interactionPrompt)  interactionPrompt.SetActive(false);
    }

    void Start()
    {
        StartJoshiDialog();
    }

    void OnDisable()
    {
        state = State.Idle;
        if (interactionPrompt) interactionPrompt.SetActive(false);
    }

    void Update()
    {
        switch (state)
        {
            case State.WaitingInteraction: HandleWaiting();    break;
            case State.Scratching:         HandleScratch();    break;
            case State.ArrowCombo:         HandleArrowInput(); break;
        }
    }

    // =========================================================================
    // Joshi-Rätseldialog
    // =========================================================================

    void StartJoshiDialog()
    {
        if (BigYahuDialogSystem.Instance == null) { state = State.WaitingInteraction; return; }

        BigYahuDialogSystem.Instance.SetSpeaker("Joshi");
        BigYahuDialogSystem.Instance.ShowDialog(new[]
        {
            "Joshi: Oh – du bist wach! Gut. Ich warte schon eine Weile auf jemanden.",
            "Joshi: Weißt du... ich war schon mal kurz davor, hier rauszukommen. Hatte alles vorbereitet.",
            "Joshi: Die Antwort steckt noch in diesen Wänden. Buchstäblich.",
            "Joshi: Such nach dem, was die Zeit vergessen hat. Wo der Staub am dicksten liegt."
        }, () =>
        {
            BigYahuDialogSystem.Instance.ResetSpeaker();
            state = State.WaitingInteraction;
        });
    }

    // =========================================================================
    // Phase 0 – Warten auf Interaktion
    // =========================================================================

    void HandleWaiting()
    {
        if (dustyWallSpot == null) return;

        if (interactionPrompt)
            interactionPrompt.SetActive(dustyWallSpot.PlayerNearby);

        if (!dustyWallSpot.PlayerNearby) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (interactionPrompt) interactionPrompt.SetActive(false);
            ActivateScratch();
        }
    }

    // =========================================================================
    // Phase 1 – Scratch
    // =========================================================================

    void ActivateScratch()
    {
        state = State.Scratching;
        if (dustWallPanel) dustWallPanel.SetActive(true);
        StartCoroutine(BuildTexture());
    }

    IEnumerator BuildTexture()
    {
        yield return new WaitForEndOfFrame();
        if (dustOverlay == null) yield break;

        Rect r = dustOverlay.rectTransform.rect;
        int w = Mathf.Max(64, Mathf.RoundToInt(r.width));
        int h = Mathf.Max(64, Mathf.RoundToInt(r.height));

        dustTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color fill = new Color(0.55f, 0.45f, 0.28f, 1f);
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = fill;
        dustTex.SetPixels(px);
        dustTex.Apply();

        dustOverlay.texture = dustTex;
        totalPixels = w * h;
    }

    void HandleScratch()
    {
        if (dustTex == null) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed) return;

        Vector2 screenPos = mouse.position.ReadValue();
        RectTransform rt  = dustOverlay.rectTransform;
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null : Camera.main;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out Vector2 local)) return;

        float nx = (local.x - rt.rect.xMin) / rt.rect.width;
        float ny = (local.y - rt.rect.yMin) / rt.rect.height;
        Erase(Mathf.RoundToInt(nx * dustTex.width), Mathf.RoundToInt(ny * dustTex.height));
    }

    void Erase(int cx, int cy)
    {
        bool changed = false;
        int r2 = brushRadius * brushRadius;

        for (int x = cx - brushRadius; x <= cx + brushRadius; x++)
        for (int y = cy - brushRadius; y <= cy + brushRadius; y++)
        {
            if (x < 0 || x >= dustTex.width || y < 0 || y >= dustTex.height) continue;
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r2) continue;
            if (dustTex.GetPixel(x, y).a > 0f)
            {
                dustTex.SetPixel(x, y, Color.clear);
                clearedPixels++;
                changed = true;
            }
        }

        if (!changed) return;
        dustTex.Apply();

        if ((float)clearedPixels / totalPixels >= revealThreshold)
            StartCoroutine(OnDustCleared());
    }

    IEnumerator OnDustCleared()
    {
        state = State.Idle;   // Eingabe pausieren während Dialog läuft
        yield return new WaitForSeconds(0.4f);

        if (BigYahuDialogSystem.Instance != null)
        {
            BigYahuDialogSystem.Instance.SetSpeaker("Joshi");
            BigYahuDialogSystem.Instance.ShowDialog(
                "Joshi: Da! Siehst du die Pfeile? Das ist mein Code. Gib die Sequenz ein!",
                () =>
                {
                    BigYahuDialogSystem.Instance.ResetSpeaker();
                    StartArrowPhase();
                });
        }
        else
        {
            StartArrowPhase();
        }
    }

    // =========================================================================
    // Phase 2 – Arrow Combo
    // =========================================================================

    void StartArrowPhase()
    {
        if (dustWallPanel) dustWallPanel.SetActive(false);
        if (arrowPanel)    arrowPanel.SetActive(true);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        comboIndex = 0;
        state      = State.ArrowCombo;
        RefreshFeedback();
    }

    void HandleArrowInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Key[] candidates = { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow };
        foreach (var key in candidates)
        {
            if (!kb[key].wasPressedThisFrame) continue;

            if (key == correctKeys[comboIndex])
            {
                comboIndex++;
                RefreshFeedback();
                if (comboIndex >= correctKeys.Length)
                    StartCoroutine(OnComboDone());
            }
            else
            {
                comboIndex = 0;
                RefreshFeedback();
            }
            break;
        }
    }

    void RefreshFeedback()
    {
        if (inputFeedbackText == null) return;
        string s = "";
        for (int i = 0; i < arrowSymbols.Length; i++)
            s += (i < comboIndex
                ? $"<color=#00FF88>{arrowSymbols[i]}</color>"
                : $"<color=#555555>{arrowSymbols[i]}</color>") + "  ";
        inputFeedbackText.text = s.TrimEnd();
    }

    IEnumerator OnComboDone()
    {
        state = State.Done;
        if (inputFeedbackText)
            inputFeedbackText.text = "<color=#00FF88>↑  ↑  ↓  ↓  ✓</color>";
        yield return new WaitForSeconds(0.6f);

        if (BigYahuDialogSystem.Instance != null)
        {
            BigYahuDialogSystem.Instance.SetSpeaker("Joshi");
            BigYahuDialogSystem.Instance.ShowDialog(
                "Joshi: Perfekt! Das Schloss öffnet sich. Los, weiter – ich zeig dir den Weg!",
                () =>
                {
                    BigYahuDialogSystem.Instance.ResetSpeaker();
                    GameManager.Instance?.CompleteCurrentLevel();
                });
        }
        else
        {
            GameManager.Instance?.CompleteCurrentLevel();
        }
    }
}
