using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Level 2 – Staubige Wand.
/// Big Yahu erklärt den Ausbruchsplan.
/// Spieler reibt mit der Maus Staub weg → versteckte Zahl erscheint.
/// Wenn genug enthüllt → Weiter-Button erscheint.
/// </summary>
public class Level2_DustWall : MonoBehaviour
{
    [Header("Scratch-Bereich")]
    [SerializeField] private RawImage dustOverlay;
    [SerializeField] private int brushRadius = 28;
    [SerializeField][Range(0f, 1f)] private float revealThreshold = 0.65f;

    [Header("Versteckter Inhalt")]
    [SerializeField] private TextMeshProUGUI revealedNumberText;
    [SerializeField] private string hiddenNumber = "7";
    [SerializeField] private GameObject continueButtonObj;

    private Texture2D dustTex;
    private int totalPixels;
    private int clearedPixels;
    private bool done;

    void OnEnable()
    {
        done = false;
        clearedPixels = 0;
        dustTex = null;

        if (continueButtonObj) continueButtonObj.SetActive(false);
        if (revealedNumberText) revealedNumberText.text = string.Empty;

        BigYahuDialogSystem.Instance.ShowDialog(new[]
        {
            "Big Yahu: Psst – ich hab den Ausbruchsplan!",
            "Big Yahu: Hinter dieser staubigen Wand ist eine geheime Zahl.",
            "Big Yahu: Reibe den Staub mit der Maus weg!"
        }, () => StartCoroutine(BuildTexture()));
    }

    IEnumerator BuildTexture()
    {
        // Warte auf Canvas-Layout-Update um korrekte Rect-Größe zu erhalten
        yield return new WaitForEndOfFrame();

        Rect r = dustOverlay.rectTransform.rect;
        int w = Mathf.Max(64, Mathf.RoundToInt(r.width));
        int h = Mathf.Max(64, Mathf.RoundToInt(r.height));

        dustTex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        // Sand-/Staubfarbe
        Color fill = new Color(0.55f, 0.45f, 0.28f, 1f);
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = fill;
        dustTex.SetPixels(px);
        dustTex.Apply();

        dustOverlay.texture = dustTex;
        totalPixels = w * h;
    }

    void Update()
    {
        if (done || dustTex == null) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed) return;

        Vector2 screenPos = mouse.position.ReadValue();
        RectTransform rt = dustOverlay.rectTransform;
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out Vector2 local)) return;

        float nx = (local.x - rt.rect.xMin) / rt.rect.width;
        float ny = (local.y - rt.rect.yMin) / rt.rect.height;
        int cx = Mathf.RoundToInt(nx * dustTex.width);
        int cy = Mathf.RoundToInt(ny * dustTex.height);

        Erase(cx, cy);
    }

    void Erase(int cx, int cy)
    {
        bool changed = false;
        int r2 = brushRadius * brushRadius;

        for (int x = cx - brushRadius; x <= cx + brushRadius; x++)
        {
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
        }

        if (!changed) return;
        dustTex.Apply();

        if (!done && (float)clearedPixels / totalPixels >= revealThreshold)
            StartCoroutine(OnRevealed());
    }

    IEnumerator OnRevealed()
    {
        done = true;
        if (revealedNumberText) revealedNumberText.text = hiddenNumber;
        yield return new WaitForSeconds(0.5f);

        BigYahuDialogSystem.Instance.ShowDialog(
            $"Big Yahu: Ja! Die Zahl ist '{hiddenNumber}'! Merke sie dir gut!",
            () => { if (continueButtonObj) continueButtonObj.SetActive(true); });
    }

    public void OnContinueClicked() => GameManager.Instance.CompleteCurrentLevel();
}
