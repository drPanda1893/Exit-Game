using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor-Helfer: Baut ein vollständiges BigYahuDialogSystem in die aktive Scene.
///
/// Menü:  Window → Custom Tools → Build Dialog System
/// (vorher Helios → Build Dialog System — der eigene Helios-Reiter wurde
///  zugunsten der Standard-Unity-Menüstruktur entfernt.)
///
/// Nutzung:
///   - Sceneöffnen (z.B. eine Level-Szene), Menüpunkt klicken.
///   - Erstellt Canvas + DialogPanel + Portrait + SpeakerLabel + DialogText + ContinueButton.
///   - Hängt BigYahuDialogSystem an und verkabelt alle SerializeFields.
///   - Idempotent: re-runs überschreiben sauber, statt zu duplizieren.
/// </summary>
public static class BigYahuDialog_AutoBuilder
{
    private const string ROOT_NAME      = "BigYahuDialogSystem";
    private const string CANVAS_NAME    = "DialogCanvas";
    private const string PANEL_NAME     = "DialogPanel";
    private const string PORTRAIT_NAME  = "Portrait";
    private const string SPEAKER_NAME   = "SpeakerLabel";
    private const string TEXT_NAME      = "DialogText";
    private const string CONTINUE_NAME  = "ContinueButton";

    [MenuItem("Window/Custom Tools/Build Dialog System")]
    public static void BuildDialogSystem()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("Dialog System", "Keine aktive Scene gefunden.", "OK");
            return;
        }

        var root     = FindOrCreateRoot(scene);
        var canvasGO = EnsureCanvas(root.transform);
        var panel    = EnsureDialogPanel(canvasGO.transform);

        var portraitImg  = EnsurePortrait(panel);
        var speakerTMP   = EnsureSpeakerLabel(panel);
        var dialogTMP    = EnsureDialogText(panel);
        var continueBtn  = EnsureContinueButton(panel);

        var ds = root.GetComponent<BigYahuDialogSystem>();
        if (ds == null) ds = Undo.AddComponent<BigYahuDialogSystem>(root);

        WireSerializedFields(ds, panel.gameObject, dialogTMP, speakerTMP, portraitImg, continueBtn);

        // Panel startet versteckt — wird von ShowDialog aktiviert
        panel.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        Debug.Log($"[BigYahuDialog_AutoBuilder] '{ROOT_NAME}' aufgebaut: Canvas, Panel, Portrait, Speaker, Text, ContinueButton verkabelt.");
    }

    // ══════════════════════════════════════════════════════════
    // Hierarchie
    // ══════════════════════════════════════════════════════════

    private static GameObject FindOrCreateRoot(Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == ROOT_NAME) return go;
            var found = FindRecursive(go.transform, ROOT_NAME);
            if (found != null) return found.gameObject;
        }

        var root = new GameObject(ROOT_NAME);
        SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Create BigYahuDialogSystem");
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

    private static GameObject EnsureCanvas(Transform parent)
    {
        var existing = FindRecursive(parent, CANVAS_NAME);
        if (existing != null) return existing.gameObject;

        var canvasGO = new GameObject(CANVAS_NAME);
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create DialogCanvas");
        canvasGO.transform.SetParent(parent, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        return canvasGO;
    }

    private static Transform EnsureDialogPanel(Transform canvas)
    {
        var existing = FindRecursive(canvas, PANEL_NAME);
        Transform panel;

        if (existing != null)
        {
            panel = existing;
        }
        else
        {
            panel = MakeRect(canvas, PANEL_NAME,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, 260f),
                new Vector2(0.5f, 0f));
        }

        var img = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
        img.color = new Color(0.03f, 0.03f, 0.06f, 0.97f);

        var border = FindRecursive(panel, "TopBorder");
        if (border == null)
        {
            border = MakeRect(panel, "TopBorder",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(0f, 3f),
                new Vector2(0.5f, 1f));
            border.gameObject.AddComponent<Image>().color = new Color(0.55f, 0.45f, 0.20f);
        }

        return panel;
    }

    private static Image EnsurePortrait(Transform panel)
    {
        var existing = FindRecursive(panel, PORTRAIT_NAME);
        Transform rect;

        if (existing != null)
        {
            rect = existing;
        }
        else
        {
            rect = MakeRect(panel, PORTRAIT_NAME,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(110f, 0f), new Vector2(190f, 190f),
                new Vector2(0.5f, 0.5f));
        }

        var img = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
        if (img.sprite == null)
            img.color = new Color(0.30f, 0.28f, 0.38f, 1f);
        return img;
    }

    private static TextMeshProUGUI EnsureSpeakerLabel(Transform panel)
    {
        var existing = FindRecursive(panel, SPEAKER_NAME);
        Transform rect;

        if (existing != null)
        {
            rect = existing;
        }
        else
        {
            rect = MakeRect(panel, SPEAKER_NAME,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(240f, -12f), new Vector2(-370f, 36f),
                new Vector2(0f, 1f));
        }

        var tmp = rect.GetComponent<TextMeshProUGUI>()
                  ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = "Helios";
        tmp.fontSize  = 26f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.yellow;
        return tmp;
    }

    private static TextMeshProUGUI EnsureDialogText(Transform panel)
    {
        var existing = FindRecursive(panel, TEXT_NAME);
        Transform rect;

        if (existing != null)
        {
            rect = existing;
        }
        else
        {
            rect = MakeRect(panel, TEXT_NAME,
                Vector2.zero, Vector2.one,
                new Vector2(240f, 48f), new Vector2(-145f, -52f),
                new Vector2(0.5f, 0.5f));
        }

        var tmp = rect.GetComponent<TextMeshProUGUI>()
                  ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = string.Empty;
        tmp.fontSize  = 24f;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        return tmp;
    }

    private static Button EnsureContinueButton(Transform panel)
    {
        var existing = FindRecursive(panel, CONTINUE_NAME);
        Transform rect;

        if (existing != null)
        {
            rect = existing;
        }
        else
        {
            rect = MakeRect(panel, CONTINUE_NAME,
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-14f, 14f), new Vector2(130f, 42f),
                new Vector2(1f, 0f));
        }

        var img = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
        img.color = new Color(0.12f, 0.52f, 0.22f);

        var btn = rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();

        var lblRect = FindRecursive(rect, "Label");
        if (lblRect == null)
            lblRect = MakeRect(rect, "Label", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

        var lblTMP = lblRect.GetComponent<TextMeshProUGUI>()
                     ?? lblRect.gameObject.AddComponent<TextMeshProUGUI>();
        lblTMP.text      = "Weiter >";
        lblTMP.fontSize  = 22f;
        lblTMP.color     = Color.white;
        lblTMP.alignment = TextAlignmentOptions.Center;
        lblTMP.fontStyle = FontStyles.Bold;

        return btn;
    }

    // ══════════════════════════════════════════════════════════
    // SerializeField-Verkabelung
    // ══════════════════════════════════════════════════════════

    private static void WireSerializedFields(BigYahuDialogSystem ds,
                                              GameObject dialogPanel,
                                              TextMeshProUGUI dialogText,
                                              TextMeshProUGUI speakerLabel,
                                              Image portraitImage,
                                              Button continueButton)
    {
        var so = new SerializedObject(ds);
        so.FindProperty("dialogPanel").objectReferenceValue    = dialogPanel;
        so.FindProperty("dialogText").objectReferenceValue     = dialogText;
        so.FindProperty("speakerLabel").objectReferenceValue   = speakerLabel;
        so.FindProperty("portraitImage").objectReferenceValue  = portraitImage;
        so.FindProperty("continueButton").objectReferenceValue = continueButton;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ══════════════════════════════════════════════════════════
    // RectTransform-Helfer
    // ══════════════════════════════════════════════════════════

    private static Transform MakeRect(Transform parent, string name,
                                       Vector2 anchorMin, Vector2 anchorMax,
                                       Vector2 anchoredPos, Vector2 sizeDelta,
                                       Vector2 pivot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.pivot            = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = sizeDelta;
        return rect;
    }
}
