using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Level 2 – Staubige Wand (zwei Phasen).
///
/// Phase 1 – Scratch: Joshi-Dialog → Spieler reibt Staub mit Maus weg.
/// Phase 2 – Combo:  Pfeilsequenz ↑↑↓↓ per Pfeiltasten eingeben.
/// Erfolg → GameManager.CompleteCurrentLevel()
/// </summary>
public class Level2_DustWall : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject dustWallPanel;
    [SerializeField] private GameObject arrowPanel;

    [Header("Scratch-Bereich")]
    [SerializeField] private RawImage dustOverlay;
    [SerializeField] private int brushRadius = 28;
    [SerializeField][Range(0f, 1f)] private float revealThreshold = 0.65f;

    [Header("Pfeil-Combo")]
    [SerializeField] private TextMeshProUGUI arrowHintText;
    [SerializeField] private TextMeshProUGUI inputFeedbackText;
    [SerializeField] private TextMeshProUGUI instructionText;

    private Texture2D dustTex;
    private int totalPixels;
    private int clearedPixels;
    private bool dustDone;
    private bool comboActive;
    private int comboIndex;

    private static readonly Key[] correctKeys =
        { Key.UpArrow, Key.UpArrow, Key.DownArrow, Key.DownArrow };
    private static readonly string[] arrowSymbols = { "↑", "↑", "↓", "↓" };

    // =========================================================================
    // Lifecycle
    // =========================================================================

    void OnEnable()
    {
        dustDone      = false;
        comboActive   = false;
        comboIndex    = 0;
        clearedPixels = 0;
        dustTex       = null;

        if (dustWallPanel) dustWallPanel.SetActive(false);
        if (arrowPanel)    arrowPanel.SetActive(false);

        if (BigYahuDialogSystem.Instance == null) { StartDustPhase(); return; }

        BigYahuDialogSystem.Instance.SetSpeaker("Joshi");
        BigYahuDialogSystem.Instance.ShowDialog(new[]
        {
            "Joshi: Hey! Endlich wacht jemand auf.",
            "Joshi: Ich bin schon eine Weile hier drin. Aber ich kenn den Weg raus.",
            "Joshi: Siehst du diese staubige Wand? Dahinter ist ein Code.",
            "Joshi: Reib den Staub mit der Maus weg!"
        }, () =>
        {
            BigYahuDialogSystem.Instance.ResetSpeaker();
            StartDustPhase();
        });
    }

    void OnDisable()
    {
        comboActive = false;
    }

    void Update()
    {
        if (!dustDone && dustTex != null) HandleScratch();
        if (comboActive)                  HandleArrowInput();
    }

    // =========================================================================
    // Phase 1 – Scratch
    // =========================================================================

    void StartDustPhase()
    {
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
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed) return;

        Vector2 screenPos = mouse.position.ReadValue();
        RectTransform rt = dustOverlay.rectTransform;
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

        if (!dustDone && (float)clearedPixels / totalPixels >= revealThreshold)
            StartCoroutine(OnDustCleared());
    }

    IEnumerator OnDustCleared()
    {
        dustDone = true;
        yield return new WaitForSeconds(0.4f);

        if (BigYahuDialogSystem.Instance != null)
        {
            BigYahuDialogSystem.Instance.SetSpeaker("Joshi");
            BigYahuDialogSystem.Instance.ShowDialog(
                "Joshi: Da! Siehst du die Pfeile? Gib die Sequenz mit den Pfeiltasten ein!",
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
        comboIndex  = 0;
        comboActive = true;
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
        comboActive = false;
        if (inputFeedbackText)
            inputFeedbackText.text = "<color=#00FF88>↑  ↑  ↓  ↓  ✓</color>";
        yield return new WaitForSeconds(0.6f);

        if (BigYahuDialogSystem.Instance != null)
        {
            BigYahuDialogSystem.Instance.SetSpeaker("Joshi");
            BigYahuDialogSystem.Instance.ShowDialog(
                "Joshi: Perfekt! Das Schloss öffnet sich. Los, weiter!",
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
