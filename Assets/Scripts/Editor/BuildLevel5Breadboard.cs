using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Baut Level 5 – Breadboard-Puzzle.
/// Menü: Tools → Build Level 5 Breadboard
/// </summary>
public class BuildLevel5Breadboard : EditorWindow
{
    [MenuItem("Tools/Build Level 5 Breadboard")]
    public static void ShowWindow() => GetWindow<BuildLevel5Breadboard>("Level 5 Builder");

    void OnGUI()
    {
        GUILayout.Label("Level 5 – Werkstatt / Breadboard", EditorStyles.boldLabel);
        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Kabel-Drag-&-Drop Puzzle: Drei Kabelpaare (A→A, B→B, C→C) richtig verbinden.\n" +
            "Alle korrekt? → Stromkreis geschlossen → Level 6.",
            MessageType.Info);
        GUILayout.Space(12);
        if (GUILayout.Button("Level 5 bauen", GUILayout.Height(36)))
            Build();
    }

    // =========================================================================
    // Haupt-Build
    // =========================================================================

    void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level5.unity");

        // Kamera
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.06f, 0.03f);
        cam.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        SceneManager.MoveGameObjectToScene(camGO, scene);

        // Canvas
        var canvasGO = new GameObject("Level5Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        SceneManager.MoveGameObjectToScene(canvasGO, scene);

        // EventSystem
        var evGO = new GameObject("EventSystem");
        evGO.AddComponent<EventSystem>();
        evGO.AddComponent<StandaloneInputModule>();
        SceneManager.MoveGameObjectToScene(evGO, scene);

        BuildBackground(canvasGO.transform);
        BuildPuzzleArea(canvasGO.transform);
        BuildHUD(canvasGO.transform);
        AddBackgroundMusic(scene);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level5] Breadboard-Werkstatt fertig gebaut.");
    }

    // =========================================================================
    // Hintergrund
    // =========================================================================

    void BuildBackground(Transform parent)
    {
        var bg = MakePanel(parent, "BG", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.03f);

        // Werkstatt-Boden-Muster (horizontale Linien)
        for (int i = 0; i < 12; i++)
        {
            float y = -500f + i * 90f;
            var line = Anchored(bg, $"Line_{i}", new Vector2(0, y), new Vector2(1920f, 1f));
            line.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.09f, 0.05f, 0.5f);
        }

        // Vignette
        Color vig = new Color(0f, 0f, 0f, 0.6f);
        Anchored(bg, "VigT", new Vector2(0,  440f), new Vector2(1920f, 200f)).gameObject.AddComponent<Image>().color = vig;
        Anchored(bg, "VigB", new Vector2(0, -440f), new Vector2(1920f, 200f)).gameObject.AddComponent<Image>().color = vig;
        Anchored(bg, "VigL", new Vector2(-860f, 0),  new Vector2(200f, 1080f)).gameObject.AddComponent<Image>().color = vig;
        Anchored(bg, "VigR", new Vector2( 860f, 0),  new Vector2(200f, 1080f)).gameObject.AddComponent<Image>().color = vig;
    }

    // =========================================================================
    // Puzzle-Bereich
    // =========================================================================

    void BuildPuzzleArea(Transform parent)
    {
        // Äußerer Rahmen
        var frame = Anchored(parent, "Frame", Vector2.zero, new Vector2(960f, 720f));
        frame.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.08f);

        // Innere Arbeitsfläche
        var board = Anchored(frame, "Board", Vector2.zero, new Vector2(940f, 700f));
        board.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.05f);

        // Titelzeile im Board
        var titleRow = Anchored(board, "TitleRow", new Vector2(0, 300f), new Vector2(900f, 50f));
        var titleImg = titleRow.gameObject.AddComponent<Image>();
        titleImg.color = new Color(0.15f, 0.20f, 0.10f);
        var titleTxt = Anchored(titleRow, "TitleTxt", Vector2.zero, new Vector2(880f, 40f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "SCHALTKREIS REPARIEREN — Verbinde die Kabel mit den passenden Pins";
        titleTxt.fontSize = 18f;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(0.6f, 0.9f, 0.4f);
        titleTxt.fontStyle = FontStyles.Bold;

        // Breadboard-Visual (Lochstreifen-Optik)
        var bbVisual = Anchored(board, "BBVisual", new Vector2(0, 50f), new Vector2(880f, 340f));
        bbVisual.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.07f);
        BuildBreadboardHoles(bbVisual);

        // Manager-Script
        var managerGO = new GameObject("BreadboardManager");
        managerGO.transform.SetParent(board, false);
        var rt = managerGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var manager = managerGO.AddComponent<Level5_Breadboard>();

        // Feedback-Text
        var feedbackGO = Anchored(board, "FeedbackText", new Vector2(0, -150f), new Vector2(800f, 40f));
        var feedbackTxt = feedbackGO.gameObject.AddComponent<TextMeshProUGUI>();
        feedbackTxt.text = string.Empty;
        feedbackTxt.fontSize = 20f;
        feedbackTxt.alignment = TextAlignmentOptions.Center;
        feedbackTxt.color = new Color(0.7f, 1f, 0.5f);

        // Drag-Icon (kleines schwebendes Kabel-Ende)
        var dragIconGO = new GameObject("DragIcon");
        dragIconGO.transform.SetParent(board, false);
        var dragRT = dragIconGO.AddComponent<RectTransform>();
        dragRT.anchorMin = new Vector2(0.5f, 0.5f);
        dragRT.anchorMax = new Vector2(0.5f, 0.5f);
        dragRT.sizeDelta = new Vector2(20f, 20f);
        var dragImg = dragIconGO.AddComponent<Image>();
        dragImg.color = new Color(1f, 0.8f, 0f);
        dragIconGO.SetActive(false);

        // Source- und Target-Pins aufbauen
        string[] ids = { "A", "B", "C" };
        Color[] colors =
        {
            new Color(1.0f, 0.25f, 0.25f),   // A = Rot
            new Color(0.25f, 0.55f, 1.0f),   // B = Blau
            new Color(0.25f, 0.85f, 0.25f),  // C = Grün
        };

        var sourcePins = new List<BreadboardPin>();
        var targetPins = new List<BreadboardPin>();
        var pairs      = new List<Level5_Breadboard.CablePair>();

        for (int i = 0; i < ids.Length; i++)
        {
            float yPos = 80f - i * 100f;  // A oben, B mitte, C unten (relativ zu bbVisual)
            Color col = colors[i];

            // Source-Pin (links)
            var srcPin = BuildPin(bbVisual, $"SrcPin_{ids[i]}", new Vector2(-280f, yPos),
                ids[i], true, col, manager);
            sourcePins.Add(srcPin);

            // Target-Pin (rechts)
            var tgtPin = BuildPin(bbVisual, $"TgtPin_{ids[i]}", new Vector2(280f, yPos),
                ids[i], false, col, manager);
            targetPins.Add(tgtPin);

            // Verbindungsindikator (Linie zwischen den Pins, wird grün bei Verbindung)
            var indicator = Anchored(bbVisual, $"Indicator_{ids[i]}", new Vector2(0, yPos),
                new Vector2(440f, 6f));
            var indicatorImg = indicator.gameObject.AddComponent<Image>();
            indicatorImg.color = new Color(col.r, col.g, col.b, 0.15f);

            // CablePair konfigurieren
            var pair = new Level5_Breadboard.CablePair
            {
                startId             = ids[i],
                endId               = ids[i],
                connectionIndicator = indicatorImg
            };
            pairs.Add(pair);
        }

        // Manager-Felder per Serialisierung setzen
        var so = new SerializedObject(manager);
        so.FindProperty("feedbackText").objectReferenceValue = feedbackTxt;
        so.FindProperty("dragIcon").objectReferenceValue = dragRT;

        var pairsProp = so.FindProperty("requiredPairs");
        pairsProp.arraySize = pairs.Count;
        for (int i = 0; i < pairs.Count; i++)
        {
            var elem = pairsProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("startId").stringValue = pairs[i].startId;
            elem.FindPropertyRelative("endId").stringValue   = pairs[i].endId;
            elem.FindPropertyRelative("connectionIndicator").objectReferenceValue =
                pairs[i].connectionIndicator;
        }
        so.ApplyModifiedProperties();
    }

    BreadboardPin BuildPin(RectTransform parent, string name, Vector2 pos,
                           string pinId, bool isSource, Color col,
                           Level5_Breadboard manager)
    {
        // Äußerer Leuchtring
        var glow = Anchored(parent, $"{name}_Glow", pos, new Vector2(60f, 60f));
        glow.gameObject.AddComponent<Image>().color = new Color(col.r, col.g, col.b, 0.18f);

        // Pin-Körper
        var pinGO = Anchored(parent, name, pos, new Vector2(44f, 44f));
        pinGO.gameObject.AddComponent<Image>().color = col;

        // Pin-Label
        var lbl = Anchored(pinGO, "Label", Vector2.zero, new Vector2(40f, 40f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = pinId;
        lbl.fontSize = 18f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white;
        lbl.fontStyle = FontStyles.Bold;

        // Richtungspfeil
        float arrowX = isSource ? -35f : 35f;
        string arrowChar = isSource ? "→" : "←";
        var arrow = Anchored(parent, $"{name}_Arrow", new Vector2(pos.x + arrowX, pos.y),
            new Vector2(28f, 28f)).gameObject.AddComponent<TextMeshProUGUI>();
        arrow.text = arrowChar;
        arrow.fontSize = 18f;
        arrow.alignment = TextAlignmentOptions.Center;
        arrow.color = new Color(col.r, col.g, col.b, 0.7f);

        // BreadboardPin-Komponente
        var pin = pinGO.gameObject.AddComponent<BreadboardPin>();
        var pso = new SerializedObject(pin);
        pso.FindProperty("pinId").stringValue = pinId;
        pso.FindProperty("isDragSource").boolValue = isSource;
        pso.FindProperty("manager").objectReferenceValue = manager;
        pso.ApplyModifiedProperties();

        return pin;
    }

    void BuildBreadboardHoles(RectTransform parent)
    {
        // Lochstreifen-Muster – zwei Reihen mit je 20 Löchern
        Color holeCol = new Color(0.04f, 0.06f, 0.03f);
        for (int row = 0; row < 2; row++)
        {
            float yOff = row == 0 ? 50f : -50f;
            for (int i = 0; i < 20; i++)
            {
                float x = -380f + i * 40f;
                Anchored(parent, $"Hole_{row}_{i}", new Vector2(x, yOff), new Vector2(8f, 8f))
                    .gameObject.AddComponent<Image>().color = holeCol;
            }
        }
    }

    // =========================================================================
    // HUD
    // =========================================================================

    void BuildHUD(Transform parent)
    {
        // Oben
        var top = Anchored(parent, "TopHUD", new Vector2(0, 495f), new Vector2(1920f, 70f));
        top.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.03f, 0.92f);
        MakeAccent(top, -200f, new Color(0.3f, 0.9f, 0.3f));
        MakeAccent(top,  200f, new Color(0.3f, 0.9f, 0.3f));

        var titleTxt = Anchored(top, "Title", Vector2.zero, new Vector2(500f, 50f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "GEFÄNGNISAUSBRUCH — PHASE 5";
        titleTxt.fontSize = 26f;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(0.8f, 1f, 0.6f);
        titleTxt.fontStyle = FontStyles.Bold;

        var levelLbl = Anchored(top, "LevelLbl", new Vector2(-700f, 0), new Vector2(200f, 40f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        levelLbl.text = "LEVEL 05";
        levelLbl.fontSize = 20f;
        levelLbl.alignment = TextAlignmentOptions.Left;
        levelLbl.color = new Color(0.4f, 0.8f, 0.4f, 0.8f);

        // Unten
        var bot = Anchored(parent, "BotHUD", new Vector2(0, -495f), new Vector2(1920f, 60f));
        bot.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.03f, 0.92f);

        var hint = Anchored(bot, "Hint", Vector2.zero, new Vector2(900f, 40f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        hint.text = "Ziehe die Kabel vom Quell-Pin (links) auf den passenden Ziel-Pin (rechts)";
        hint.fontSize = 16f;
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = new Color(0.5f, 0.7f, 0.4f);
    }

    // =========================================================================
    // Hintergrundmusik
    // =========================================================================

    void AddBackgroundMusic(Scene scene)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Big Yahu/Untitled.mp3");
        if (clip == null) return;
        var go = new GameObject("BackgroundMusic");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip; src.loop = true; src.playOnAwake = true; src.volume = 0.5f;
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    // =========================================================================
    // UI-Helfer
    // =========================================================================

    void MakeAccent(RectTransform parent, float x, Color col)
    {
        Anchored(parent, $"Accent_{x}", new Vector2(x, 0), new Vector2(2f, 50f))
            .gameObject.AddComponent<Image>().color = col;
    }

    RectTransform MakePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax  = offsetMax;
        return rt;
    }

    RectTransform Anchored(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return rt;
    }
}
