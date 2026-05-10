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
///   Idle → WaitingInteraction → Scratching → WaitingLock → ArrowCombo → Done
///
/// Scratch-Panel schließt nach 65 % → Joshi-Dialog → Spieler sucht
/// Schloss am Eingang → [E] → Pfeil-Combo ↑↑↓↓ → Tür öffnet sich.
/// </summary>
public class Level2_DustWall : MonoBehaviour
{
    public static Level2_DustWall Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject dustWallPanel;
    [SerializeField] private GameObject arrowPanel;
    [SerializeField] private GameObject interactionPrompt;

    [Header("3D Wand-Trigger")]
    [SerializeField] private DustyWallSpot dustyWallSpot;

    [Header("3D Schloss-Trigger")]
    [SerializeField] private DustyWallSpot lockSpot;
    [SerializeField] private GameObject lockInteractionPrompt;
    [SerializeField] private GameObject entranceBorderGO;

    [Header("Scratch-Bereich")]
    [SerializeField] private RawImage dustOverlay;
    [SerializeField] private int brushRadius = 28;
    [SerializeField][Range(0f, 1f)] private float revealThreshold = 0.65f;

    [Header("Pfeil-Combo")]
    [SerializeField] private TextMeshProUGUI arrowHintText;
    [SerializeField] private TextMeshProUGUI inputFeedbackText;
    [SerializeField] private TextMeshProUGUI instructionText;

    private enum State { Idle, WaitingInteraction, Scratching, WaitingLock, ArrowCombo, Done }
    private State state = State.Idle;

    private Texture2D dustTex;
    private int totalPixels;
    private int clearedPixels;
    private int comboIndex;
    private bool dustClearing;

    private static readonly Key[] correctKeys =
        { Key.UpArrow, Key.UpArrow, Key.DownArrow, Key.DownArrow };
    private static readonly string[] arrowSymbols = { "hoch", "hoch", "runter", "runter" };

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
        dustClearing  = false;

        if (dustWallPanel)         dustWallPanel.SetActive(false);
        if (arrowPanel)            arrowPanel.SetActive(false);
        if (interactionPrompt)     interactionPrompt.SetActive(false);
        if (lockInteractionPrompt) lockInteractionPrompt.SetActive(false);
    }

    void Start() => StartJoshiDialog();

    void OnDisable()
    {
        state = State.Idle;
        if (interactionPrompt)     interactionPrompt.SetActive(false);
        if (lockInteractionPrompt) lockInteractionPrompt.SetActive(false);
    }

    void Update()
    {
        switch (state)
        {
            case State.WaitingInteraction: HandleWaiting();     break;
            case State.Scratching:         HandleScratch();     break;
            case State.WaitingLock:        HandleWaitingLock(); break;
            case State.ArrowCombo:         HandleArrowInput();  break;
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
    // Phase 0 – Wand suchen
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
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
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
        if (dustClearing) return;
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
        {
            dustClearing = true;
            StartCoroutine(OnDustCleared());
        }
    }

    IEnumerator OnDustCleared()
    {
        state = State.Idle;
        yield return new WaitForSeconds(0.6f);

        if (dustWallPanel) dustWallPanel.SetActive(false);
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (BigYahuDialogSystem.Instance != null)
        {
            BigYahuDialogSystem.Instance.SetSpeaker("Joshi");
            BigYahuDialogSystem.Instance.ShowDialog(new[]
            {
                "Joshi: Da! Siehst du die Pfeile? Das ist mein Code.",
                "Joshi: Merk dir die Sequenz. Das Schloss haengt am Eingang – geh hin und gib den Code ein!"
            }, () =>
            {
                BigYahuDialogSystem.Instance.ResetSpeaker();
                state = State.WaitingLock;
            });
        }
        else
        {
            state = State.WaitingLock;
        }
    }

    // =========================================================================
    // Phase 2 – Schloss suchen
    // =========================================================================

    void HandleWaitingLock()
    {
        if (lockSpot == null) { state = State.ArrowCombo; OpenArrowPanel(); return; }

        if (lockInteractionPrompt)
            lockInteractionPrompt.SetActive(lockSpot.PlayerNearby);

        if (!lockSpot.PlayerNearby) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (lockInteractionPrompt) lockInteractionPrompt.SetActive(false);
            OpenArrowPanel();
        }
    }

    void OpenArrowPanel()
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        if (arrowPanel) arrowPanel.SetActive(true);
        comboIndex = 0;
        state      = State.ArrowCombo;
        RefreshFeedback();
    }

    // =========================================================================
    // Phase 3 – Arrow Combo
    // =========================================================================

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
            inputFeedbackText.text = "<color=#00FF88>hoch  hoch  runter  runter  OK</color>";
        yield return new WaitForSeconds(0.8f);

        if (arrowPanel) arrowPanel.SetActive(false);

        // Eingangs-Sperre entfernen
        if (entranceBorderGO) entranceBorderGO.SetActive(false);

        if (BigYahuDialogSystem.Instance != null)
        {
            BigYahuDialogSystem.Instance.SetSpeaker("Joshi");
            BigYahuDialogSystem.Instance.ShowDialog(
                "Joshi: Perfekt! Das Schloss ist offen. Los, weiter – ich zeig dir den Weg!",
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
