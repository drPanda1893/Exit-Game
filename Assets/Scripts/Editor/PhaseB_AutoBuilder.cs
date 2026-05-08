using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor-Helfer: Baut die UI für Level 3 / Phase B (Farbrätsel) automatisch.
///
/// Menü:  Helios → Build Phase B
///
/// Vorgehen:
///   1. Sucht im aktuellen Scene-Hierarchie ein GameObject 'PhaseB_ColorCode'.
///      → Falls keines existiert, wird eines erstellt (in der aktiven Scene).
///   2. Stellt sicher, dass darin ein Canvas + 4 Color-Buttons vorhanden sind.
///      Existierende Buttons mit gleichem Namen werden wiederverwendet.
///   3. Hängt das Level3_ColorPuzzle-Script an das PhaseB_ColorCode-GameObject.
///   4. Verkabelt redButton, greenButton, blueButton, yellowButton, feedbackText
///      direkt im Inspector via SerializedObject.
/// </summary>
public static class PhaseB_AutoBuilder
{
    private const string ROOT_NAME       = "PhaseB_ColorCode";
    private const string CANVAS_NAME     = "ColorPuzzleCanvas";
    private const string BUTTON_PANEL    = "ButtonPanel";
    private const string FEEDBACK_NAME   = "FeedbackText";

    [MenuItem("Helios/Build Phase B")]
    public static void BuildPhaseB()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("Phase B", "Keine aktive Scene gefunden.", "OK");
            return;
        }

        var root = FindOrCreateRoot(scene);
        var canvasGO = EnsureCanvas(root);
        var buttonPanel = EnsureButtonPanel(canvasGO.transform);

        var redBtn    = EnsureColorButton(buttonPanel, "RedButton",    "Red",    new Color(0.86f, 0.18f, 0.18f), 0);
        var greenBtn  = EnsureColorButton(buttonPanel, "GreenButton",  "Green",  new Color(0.20f, 0.72f, 0.30f), 1);
        var blueBtn   = EnsureColorButton(buttonPanel, "BlueButton",   "Blue",   new Color(0.20f, 0.40f, 1.00f), 2);
        var yellowBtn = EnsureColorButton(buttonPanel, "YellowButton", "Yellow", new Color(0.96f, 0.85f, 0.18f), 3);

        var feedback = EnsureFeedbackText(canvasGO.transform);

        var puzzle = root.GetComponent<Level3_ColorPuzzle>();
        if (puzzle == null) puzzle = Undo.AddComponent<Level3_ColorPuzzle>(root);

        WireSerializedFields(puzzle, redBtn, greenBtn, blueBtn, yellowBtn, feedback);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        Debug.Log($"[PhaseB_AutoBuilder] '{ROOT_NAME}' aufgebaut: 4 Buttons + Level3_ColorPuzzle verkabelt.");
    }

    // ══════════════════════════════════════════════════════════
    // Hierarchie-Aufbau
    // ══════════════════════════════════════════════════════════

    private static GameObject FindOrCreateRoot(Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            var found = FindRecursive(go.transform, ROOT_NAME);
            if (found != null) return found.gameObject;
        }

        var root = new GameObject(ROOT_NAME);
        SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Create PhaseB Root");
        return root;
    }

    private static Transform FindRecursive(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var found = FindRecursive(t.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject EnsureCanvas(GameObject parent)
    {
        var existing = FindRecursive(parent.transform, CANVAS_NAME);
        if (existing != null) return existing.gameObject;

        var canvasGO = new GameObject(CANVAS_NAME);
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
        canvasGO.transform.SetParent(parent.transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        return canvasGO;
    }

    private static Transform EnsureButtonPanel(Transform canvas)
    {
        var existing = FindRecursive(canvas, BUTTON_PANEL);
        if (existing != null) return existing;

        var panel = MakeRect(canvas, BUTTON_PANEL,
            new Vector2(0.18f, 0.30f), new Vector2(0.82f, 0.55f));

        var img = panel.gameObject.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.45f);

        var hlg = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24f;
        hlg.padding = new RectOffset(24, 24, 24, 24);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;

        return panel;
    }

    private static Button EnsureColorButton(Transform parent, string name, string label,
                                             Color tint, int siblingIndex)
    {
        var existing = FindRecursive(parent, name);
        Transform btnRect;

        if (existing != null)
        {
            btnRect = existing;
        }
        else
        {
            btnRect = MakeRect(parent, name, Vector2.zero, Vector2.one);
            Undo.RegisterCreatedObjectUndo(btnRect.gameObject, $"Create {name}");
        }

        btnRect.SetSiblingIndex(siblingIndex);

        var img = btnRect.GetComponent<Image>() ?? btnRect.gameObject.AddComponent<Image>();
        img.color = tint;

        var btn = btnRect.GetComponent<Button>() ?? btnRect.gameObject.AddComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = tint;
        cb.highlightedColor = Color.Lerp(tint, Color.white, 0.25f);
        cb.pressedColor     = Color.Lerp(tint, Color.black, 0.30f);
        btn.colors = cb;

        var lblRect = FindRecursive(btnRect, "Label");
        if (lblRect == null)
            lblRect = MakeRect(btnRect, "Label", Vector2.zero, Vector2.one);

        var tmp = lblRect.GetComponent<TextMeshProUGUI>()
                  ?? lblRect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.fontStyle = FontStyles.Bold;

        return btn;
    }

    private static TextMeshProUGUI EnsureFeedbackText(Transform canvas)
    {
        var existing = FindRecursive(canvas, FEEDBACK_NAME);
        Transform rect = existing != null
            ? existing
            : MakeRect(canvas, FEEDBACK_NAME, new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.26f));

        var tmp = rect.GetComponent<TextMeshProUGUI>()
                  ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = string.Empty;
        tmp.fontSize  = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = new Color(0.95f, 0.92f, 0.78f);

        return tmp;
    }

    // ══════════════════════════════════════════════════════════
    // Inspector-Verkabelung via SerializedObject
    // ══════════════════════════════════════════════════════════

    private static void WireSerializedFields(Level3_ColorPuzzle puzzle,
                                              Button red, Button green, Button blue, Button yellow,
                                              TextMeshProUGUI feedback)
    {
        var so = new SerializedObject(puzzle);
        so.FindProperty("redButton").objectReferenceValue    = red;
        so.FindProperty("greenButton").objectReferenceValue  = green;
        so.FindProperty("blueButton").objectReferenceValue   = blue;
        so.FindProperty("yellowButton").objectReferenceValue = yellow;
        so.FindProperty("feedbackText").objectReferenceValue = feedback;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ══════════════════════════════════════════════════════════
    // RectTransform-Helfer
    // ══════════════════════════════════════════════════════════

    private static Transform MakeRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
}
