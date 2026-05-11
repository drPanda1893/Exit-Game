using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

/// <summary>
/// Baut Level 6 – Das finale Gefängnistor (3D-Szene).
///
/// Layout: langer Korridor endet am massiven Eisentor. Spieler kommt von Süden,
/// läuft zum Tor, drückt [E] → Bunsenbrenner-Panel → Schloss erhitzen → Sieg.
///
/// Menue: Tools → Build Level 6 Final Gate
/// </summary>
public class BuildLevel6FinalGate : LevelBuilderBase
{
    [MenuItem("Tools/Build Level 6 Final Gate")]
    public static void ShowWindow() => GetWindow<BuildLevel6FinalGate>("Level 6 Builder");

    void OnGUI()
    {
        GUILayout.Label("Level 6 – Finales Gefängnistor (3D)", EditorStyles.boldLabel);
        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "3D-Korridor mit massivem Eisentor am Ende.\n" +
            "Spieler nähert sich → [E] → Bunsenbrenner-Minispiel → Tor öffnet → Sieg.",
            MessageType.Info);
        GUILayout.Space(12);
        if (GUILayout.Button("Level 6 bauen", GUILayout.Height(36)))
            Build();
    }

    // =========================================================================
    // Haupt-Build
    // =========================================================================

    void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level6.unity");

        // ── Kamera ───────────────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
        cam.farClipPlane    = 40f;
        cam.tag             = "MainCamera";
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 11f, 0f);
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var follow = camGO.AddComponent<TopDownCameraFollow>();
        SceneManager.MoveGameObjectToScene(camGO, scene);

        // ── Umgebung ─────────────────────────────────────────────────────────
        var root = new GameObject("Environment");
        SceneManager.MoveGameObjectToScene(root, scene);
        var gateBarsGO = BuildEnvironment(root.transform);

        // ── Spieler ──────────────────────────────────────────────────────────
        var player = AddPlayer(scene, new Vector3(0f, 0f, -5f));
        if (player != null) follow.SetTarget(player.transform);

        // ── Beleuchtung ───────────────────────────────────────────────────────
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.14f, 0.12f, 0.09f);
        AddLight("SpawnLight", root.transform, new Vector3(0f, 3.5f, -4.5f),
            LightType.Point, new Color(0.82f, 0.72f, 0.52f), 1.2f, 8f, LightShadows.None);
        AddLight("CorridorLight", root.transform, new Vector3(0f, 4f, -2f),
            LightType.Point, new Color(0.85f, 0.75f, 0.55f), 1.4f, 10f, LightShadows.Soft);
        AddLight("GateSpot_L", root.transform, new Vector3(-2f, 5f, 3.5f),
            LightType.Spot, new Color(1f, 0.6f, 0.2f), 3.0f, 12f, LightShadows.Hard,
            Quaternion.Euler(60f, 30f, 0f));
        AddLight("GateSpot_R", root.transform, new Vector3(2f, 5f, 3.5f),
            LightType.Spot, new Color(1f, 0.5f, 0.15f), 3.0f, 12f, LightShadows.Hard,
            Quaternion.Euler(60f, -30f, 0f));

        // ── Gate-Trigger ──────────────────────────────────────────────────────
        var gateTrigger = BuildGateTrigger(root.transform);

        // ── Hintergrundmusik ──────────────────────────────────────────────────
        AddBackgroundMusic(scene);

        // ── GameManager ───────────────────────────────────────────────────────
        var gmGO = new GameObject("GameManager");
        SceneManager.MoveGameObjectToScene(gmGO, scene);
        var gm   = gmGO.AddComponent<GameManager>();
        var gmso = new SerializedObject(gm);
        var arr  = gmso.FindProperty("levelSceneNames");
        arr.arraySize = 6;
        string[] names = { "Level1", "Level2", "Level3", "Level4", "Level5", "Level6" };
        for (int i = 0; i < names.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = names[i];
        gmso.ApplyModifiedPropertiesWithoutUndo();

        // ── UI ────────────────────────────────────────────────────────────────
        BuildUI(scene, gateTrigger, gateBarsGO);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level6] Finales Gefängnistor fertig gebaut.");
    }

    // =========================================================================
    // 3D Umgebung
    // =========================================================================

    /// <summary>Baut den Korridor + das Eisentor. Gibt das GateBars-GO zurück.</summary>
    GameObject BuildEnvironment(Transform root)
    {
        var concreteMat = M(new Color(0.22f, 0.20f, 0.18f), 0.01f, 0.05f);
        var stoneMat    = M(new Color(0.18f, 0.17f, 0.15f), 0.00f, 0.03f);
        var ironMat     = M(new Color(0.25f, 0.23f, 0.20f), 0.65f, 0.30f);
        var rustMat     = M(new Color(0.30f, 0.15f, 0.05f), 0.40f, 0.10f);

        // ── Korridor (z -6 … +2) ─────────────────────────────────────────────
        Box("Boden",      new Vector3(0f, -0.08f, -2f), new Vector3(6f, 0.16f, 16f), concreteMat, root);
        Box("Wand_Links", new Vector3(-3f, 2.5f, -2f),  new Vector3(0.22f, 5f, 16f), stoneMat,    root);
        Box("Wand_Rechts",new Vector3( 3f, 2.5f, -2f),  new Vector3(0.22f, 5f, 16f), stoneMat,    root);
        Box("Decke",      new Vector3(0f, 5.1f, -2f),   new Vector3(6.44f, 0.2f, 16f), concreteMat, root, col: false);
        Box("EW_Links",   new Vector3(-2f, 2.5f, -6f),  new Vector3(2f, 5f, 0.22f),  stoneMat,    root);
        Box("EW_Rechts",  new Vector3( 2f, 2.5f, -6f),  new Vector3(2f, 5f, 0.22f),  stoneMat,    root);
        Box("EW_Top",     new Vector3( 0f, 3.8f, -6f),  new Vector3(6f, 2.4f, 0.22f),stoneMat,    root);

        // ── Tor-Rahmen ────────────────────────────────────────────────────────
        Box("Pfeiler_L",  new Vector3(-2.6f, 2.5f, 2f), new Vector3(1.0f, 5f, 0.6f), stoneMat, root);
        Box("Pfeiler_R",  new Vector3( 2.6f, 2.5f, 2f), new Vector3(1.0f, 5f, 0.6f), stoneMat, root);
        Box("Sturz",      new Vector3(0f, 4.1f, 2f),    new Vector3(6.44f, 0.8f, 0.6f), stoneMat, root);
        Box("Schloss",    new Vector3(0f, 1.4f, 1.76f), new Vector3(0.32f, 0.40f, 0.12f), rustMat, root, col: false);

        // ── Tor-Stäbe (werden bei Sieg deaktiviert) ───────────────────────────
        var bars = new GameObject("GateBars");
        bars.transform.SetParent(root);
        bars.transform.position = Vector3.zero;
        for (int i = 0; i < 5; i++)
        {
            float x = -1.6f + i * 0.8f;
            Box($"Bar_{i}", new Vector3(x, 2.0f, 2f), new Vector3(0.10f, 4.0f, 0.10f), ironMat, bars.transform, col: false);
        }
        Box("Bar_H1", new Vector3(0f, 3.2f, 2f), new Vector3(3.4f, 0.10f, 0.10f), ironMat, bars.transform, col: false);
        Box("Bar_H2", new Vector3(0f, 1.0f, 2f), new Vector3(3.4f, 0.10f, 0.10f), ironMat, bars.transform, col: false);
        var barsCol = bars.AddComponent<BoxCollider>();
        barsCol.size   = new Vector3(4f, 4f, 0.2f);
        barsCol.center = new Vector3(0f, 2f, 2f);

        // ── Atmosphäre ────────────────────────────────────────────────────────
        var debrisMat = M(new Color(0.18f, 0.14f, 0.09f));
        Box("Kiste_L",  new Vector3(-2.2f, 0.25f, -3f),  new Vector3(0.55f, 0.50f, 0.55f), debrisMat, root);
        Box("Kiste_R",  new Vector3( 2.3f, 0.22f, -1.5f),new Vector3(0.45f, 0.44f, 0.45f), debrisMat, root);
        Box("Stein",    new Vector3(-1.8f, 0.12f, 0.5f), new Vector3(0.30f, 0.24f, 0.28f), stoneMat,  root, col: false);

        return bars;
    }

    // =========================================================================
    // Gate-Trigger
    // =========================================================================

    GameObject BuildGateTrigger(Transform root)
    {
        var go = new GameObject("GateTrigger");
        go.transform.SetParent(root);
        go.transform.position = new Vector3(0f, 1f, 0.5f);
        var bc = go.AddComponent<BoxCollider>();
        bc.size      = new Vector3(4f, 2f, 2f);
        bc.isTrigger = true;
        go.AddComponent<DustyWallSpot>();
        return go;
    }

    // =========================================================================
    // UI
    // =========================================================================

    void BuildUI(Scene scene, GameObject gateTrigger, GameObject gateBarsGO)
    {
        // EventSystem
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();
        SceneManager.MoveGameObjectToScene(esGO, scene);

        // Canvas
        var canvasGO = new GameObject("UICanvas");
        SceneManager.MoveGameObjectToScene(canvasGO, scene);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Dialog-Panel ──────────────────────────────────────────────────────
        var dialogPanelGO = UiPanel("DialogPanel", canvasGO.transform,
            new Vector2(0f,0f), new Vector2(1f,0f),
            new Vector2(0f,0f), new Vector2(0f, 260f),
            new Vector2(0.5f,0f), new Color(0.03f, 0.03f, 0.06f, 0.97f));
        dialogPanelGO.SetActive(false);
        UiImage("TopBorder", dialogPanelGO.transform,
            new Vector2(0f,1f), new Vector2(1f,1f), Vector2.zero, new Vector2(0f,3f),
            new Color(0.55f, 0.45f, 0.20f));
        var portraitGO = UiImage("Portrait", dialogPanelGO.transform,
            new Vector2(0f,0.5f), new Vector2(0f,0.5f),
            new Vector2(110f,0f), new Vector2(190f,190f), new Color(0.30f,0.28f,0.38f));
        var speakerGO  = new GameObject("SpeakerLabel");
        speakerGO.transform.SetParent(dialogPanelGO.transform, false);
        var speakerTMP = speakerGO.AddComponent<TextMeshProUGUI>();
        speakerTMP.text      = "Big Yahu";
        speakerTMP.fontSize  = 26f;
        speakerTMP.fontStyle = FontStyles.Bold;
        speakerTMP.color     = new Color(1f, 0.85f, 0.45f);
        var speakerRT = speakerGO.GetComponent<RectTransform>();
        speakerRT.anchorMin = new Vector2(0f,1f); speakerRT.anchorMax = new Vector2(1f,1f);
        speakerRT.pivot = new Vector2(0f,1f);
        speakerRT.anchoredPosition = new Vector2(240f,-12f);
        speakerRT.sizeDelta = new Vector2(-370f, 36f);
        var dialogTextGO = new GameObject("DialogText");
        dialogTextGO.transform.SetParent(dialogPanelGO.transform, false);
        var dialogTMP = dialogTextGO.AddComponent<TextMeshProUGUI>();
        dialogTMP.text = ""; dialogTMP.fontSize = 24f; dialogTMP.color = Color.white;
        var dialogRT = dialogTextGO.GetComponent<RectTransform>();
        dialogRT.anchorMin = Vector2.zero; dialogRT.anchorMax = Vector2.one;
        dialogRT.offsetMin = new Vector2(240f, 48f);
        dialogRT.offsetMax = new Vector2(-145f,-52f);
        var continueBtnGO = UiButton("ContinueButton", dialogPanelGO.transform,
            new Vector2(1f,0f), new Vector2(1f,0f),
            new Vector2(-14f,14f), new Vector2(130f,42f),
            new Vector2(1f,0f), new Color(0.12f,0.52f,0.22f), "Weiter ▶");

        // ── Interaction Prompt ────────────────────────────────────────────────
        var promptGO = UiPanel("InteractionPrompt", canvasGO.transform,
            new Vector2(0.5f,0f), new Vector2(0.5f,0f),
            new Vector2(0f,80f), new Vector2(400f,60f),
            new Vector2(0.5f,0f), new Color(0f,0f,0f,0.72f));
        promptGO.SetActive(false);
        AddPromptText(promptGO.transform, "[E] Schloss erhitzen");

        // ── Heat Panel ────────────────────────────────────────────────────────
        var heatPanelGO = UiPanel("HeatPanel", canvasGO.transform,
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            Vector2.zero, new Vector2(700f, 440f),
            new Vector2(0.5f,0.5f), new Color(0.05f,0.04f,0.04f,0.96f));
        heatPanelGO.SetActive(false);

        // Titel
        var hpTitle = new GameObject("Title");
        hpTitle.transform.SetParent(heatPanelGO.transform, false);
        var hpTitleTMP = hpTitle.AddComponent<TextMeshProUGUI>();
        hpTitleTMP.text      = "SCHLOSS ERHITZEN";
        hpTitleTMP.fontSize  = 30f;
        hpTitleTMP.fontStyle = FontStyles.Bold;
        hpTitleTMP.color     = new Color(1f, 0.65f, 0.10f);
        hpTitleTMP.alignment = TextAlignmentOptions.Center;
        var hpTitleRT = hpTitle.GetComponent<RectTransform>();
        hpTitleRT.anchorMin = new Vector2(0f,1f); hpTitleRT.anchorMax = new Vector2(1f,1f);
        hpTitleRT.pivot = new Vector2(0.5f,1f);
        hpTitleRT.anchoredPosition = new Vector2(0f,-18f);
        hpTitleRT.sizeDelta = new Vector2(0f, 48f);

        // Instruction
        var instrGO = new GameObject("Instruction");
        instrGO.transform.SetParent(heatPanelGO.transform, false);
        var instrTMP = instrGO.AddComponent<TextMeshProUGUI>();
        instrTMP.text      = "Halte den Brenner gedrückt!";
        instrTMP.fontSize  = 22f;
        instrTMP.color     = new Color(0.8f, 0.8f, 0.8f);
        instrTMP.alignment = TextAlignmentOptions.Center;
        var instrRT = instrGO.GetComponent<RectTransform>();
        instrRT.anchorMin = new Vector2(0f,1f); instrRT.anchorMax = new Vector2(1f,1f);
        instrRT.pivot = new Vector2(0.5f,1f);
        instrRT.anchoredPosition = new Vector2(0f,-75f);
        instrRT.sizeDelta = new Vector2(0f, 34f);

        // Temperatur-Prozent Label
        var tempLabelGO = new GameObject("TemperatureLabel");
        tempLabelGO.transform.SetParent(heatPanelGO.transform, false);
        var tempLabelTMP = tempLabelGO.AddComponent<TextMeshProUGUI>();
        tempLabelTMP.text      = "0 %";
        tempLabelTMP.fontSize  = 52f;
        tempLabelTMP.fontStyle = FontStyles.Bold;
        tempLabelTMP.color     = new Color(0.25f, 0.55f, 1f);
        tempLabelTMP.alignment = TextAlignmentOptions.Center;
        var tempLabelRT = tempLabelGO.GetComponent<RectTransform>();
        tempLabelRT.anchorMin = new Vector2(0.5f,0.5f); tempLabelRT.anchorMax = new Vector2(0.5f,0.5f);
        tempLabelRT.pivot = new Vector2(0.5f,0.5f);
        tempLabelRT.anchoredPosition = new Vector2(0f, 30f);
        tempLabelRT.sizeDelta = new Vector2(300f, 70f);

        // Slider (Temperatur-Bar)
        var sliderGO = new GameObject("TemperatureBar");
        sliderGO.transform.SetParent(heatPanelGO.transform, false);
        var sliderRT = sliderGO.GetComponent<RectTransform>() ?? sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0.5f,0.5f); sliderRT.anchorMax = new Vector2(0.5f,0.5f);
        sliderRT.pivot = new Vector2(0.5f,0.5f);
        sliderRT.anchoredPosition = new Vector2(0f, -40f);
        sliderRT.sizeDelta = new Vector2(560f, 48f);
        var slider = sliderGO.AddComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0f;
        slider.interactable = false;
        UiImage("Background", sliderGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.15f,0.15f,0.15f));
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var faRT = fillAreaGO.GetComponent<RectTransform>() ?? fillAreaGO.AddComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.offsetMin = Vector2.zero; faRT.offsetMax = Vector2.zero;
        var fillGO = UiImage("Fill", fillAreaGO.transform,
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
            new Color(0.25f, 0.55f, 1f));
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = new Vector2(0f,1f);
        fillRT.sizeDelta = Vector2.zero;
        slider.fillRect = fillRT;

        // Status Text
        var statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(heatPanelGO.transform, false);
        var statusTMP = statusGO.AddComponent<TextMeshProUGUI>();
        statusTMP.text      = string.Empty;
        statusTMP.fontSize  = 26f;
        statusTMP.fontStyle = FontStyles.Bold;
        statusTMP.color     = new Color(1f, 0.90f, 0.30f);
        statusTMP.alignment = TextAlignmentOptions.Center;
        var statusRT = statusGO.GetComponent<RectTransform>();
        statusRT.anchorMin = new Vector2(0f,0f); statusRT.anchorMax = new Vector2(1f,0f);
        statusRT.pivot = new Vector2(0.5f,0f);
        statusRT.anchoredPosition = new Vector2(0f, 90f);
        statusRT.sizeDelta = new Vector2(0f, 40f);

        // Heat Button
        var heatBtnGO = UiButton("HeatButton", heatPanelGO.transform,
            new Vector2(0.5f,0f), new Vector2(0.5f,0f),
            new Vector2(0f, 18f), new Vector2(320f, 70f),
            new Vector2(0.5f,0f), new Color(0.72f, 0.18f, 0.05f), "BRENNER HALTEN");
        var heatBtnLbl = heatBtnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (heatBtnLbl) { heatBtnLbl.fontSize = 22f; heatBtnLbl.fontStyle = FontStyles.Bold; }

        // ── Win Overlay ───────────────────────────────────────────────────────
        var winGO = UiPanel("WinOverlay", canvasGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Vector2(0.5f,0.5f), new Color(0.02f,0.02f,0.04f,0.92f));
        winGO.SetActive(false);

        var winTitleGO = new GameObject("WinTitle");
        winTitleGO.transform.SetParent(winGO.transform, false);
        var winTitleTMP = winTitleGO.AddComponent<TextMeshProUGUI>();
        winTitleTMP.text      = "BIG YAHU IST FREI!";
        winTitleTMP.fontSize  = 64f;
        winTitleTMP.fontStyle = FontStyles.Bold;
        winTitleTMP.color     = new Color(1f, 0.85f, 0.15f);
        winTitleTMP.alignment = TextAlignmentOptions.Center;
        var winTitleRT = winTitleGO.GetComponent<RectTransform>();
        winTitleRT.anchorMin = new Vector2(0f,0.5f); winTitleRT.anchorMax = new Vector2(1f,0.5f);
        winTitleRT.pivot = new Vector2(0.5f,0.5f);
        winTitleRT.anchoredPosition = new Vector2(0f, 80f);
        winTitleRT.sizeDelta = new Vector2(0f, 90f);

        var timerGO = new GameObject("TimerText");
        timerGO.transform.SetParent(winGO.transform, false);
        var timerTMP = timerGO.AddComponent<TextMeshProUGUI>();
        timerTMP.text      = string.Empty;
        timerTMP.fontSize  = 30f;
        timerTMP.color     = new Color(1f, 0.92f, 0.5f);
        timerTMP.fontStyle = FontStyles.Bold;
        timerTMP.alignment = TextAlignmentOptions.Center;
        var timerRT = timerGO.GetComponent<RectTransform>();
        timerRT.anchorMin = new Vector2(0f,0.5f); timerRT.anchorMax = new Vector2(1f,0.5f);
        timerRT.pivot = new Vector2(0.5f,0.5f);
        timerRT.anchoredPosition = new Vector2(0f, -10f);
        timerRT.sizeDelta = new Vector2(0f, 44f);

        var restartBtnGO = UiButton("RestartButton", winGO.transform,
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0f, -90f), new Vector2(280f, 60f),
            new Vector2(0.5f,0.5f), new Color(0.12f,0.45f,0.18f), "Nochmal spielen");
        var restartLbl = restartBtnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (restartLbl) restartLbl.fontSize = 24f;

        // ── BigYahuDialogSystem ───────────────────────────────────────────────
        var dsGO = new GameObject("BigYahuDialogSystem");
        SceneManager.MoveGameObjectToScene(dsGO, scene);
        var ds  = dsGO.AddComponent<BigYahuDialogSystem>();
        var dso = new SerializedObject(ds);
        dso.FindProperty("dialogPanel").objectReferenceValue    = dialogPanelGO;
        dso.FindProperty("dialogText").objectReferenceValue     = dialogTMP;
        dso.FindProperty("speakerLabel").objectReferenceValue   = speakerTMP;
        dso.FindProperty("portraitImage").objectReferenceValue  = portraitGO.GetComponent<Image>();
        dso.FindProperty("continueButton").objectReferenceValue = continueBtnGO.GetComponent<Button>();
        dso.ApplyModifiedPropertiesWithoutUndo();

        // ── Level6_FinalGate ──────────────────────────────────────────────────
        var l6GO = new GameObject("Level6_FinalGate");
        SceneManager.MoveGameObjectToScene(l6GO, scene);
        var l6   = l6GO.AddComponent<Level6_FinalGate>();
        var l6so = new SerializedObject(l6);
        l6so.FindProperty("gateSpot").objectReferenceValue          =
            gateTrigger?.GetComponent<DustyWallSpot>();
        l6so.FindProperty("interactionPrompt").objectReferenceValue = promptGO;
        l6so.FindProperty("gateBarsGO").objectReferenceValue        = gateBarsGO;
        l6so.FindProperty("heatPanel").objectReferenceValue         = heatPanelGO;
        l6so.FindProperty("temperatureBar").objectReferenceValue    = slider;
        l6so.FindProperty("temperatureLabel").objectReferenceValue  = tempLabelTMP;
        l6so.FindProperty("statusText").objectReferenceValue        = statusTMP;
        l6so.FindProperty("heatButton").objectReferenceValue        =
            heatBtnGO.GetComponent<Button>();
        l6so.FindProperty("winOverlay").objectReferenceValue        = winGO;
        l6so.FindProperty("timerText").objectReferenceValue         = timerTMP;
        l6so.FindProperty("restartButton").objectReferenceValue     =
            restartBtnGO.GetComponent<Button>();
        l6so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[Level6] UI und GameLogic verdrahtet.");
    }

    // =========================================================================
    // Hilfs-Methoden
    // =========================================================================

    void AddPromptText(Transform parent, string text)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f,4f); rt.offsetMax = new Vector2(-8f,-4f);
    }

    Material M(Color c, float metal = 0f, float smooth = 0.35f)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Metallic",   metal);
        m.SetFloat("_Glossiness", smooth);
        return m;
    }

    GameObject Box(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent,
                   Quaternion? rot = null, bool col = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        if (rot.HasValue) go.transform.rotation = rot.Value;
        go.GetComponent<Renderer>().material = mat;
        if (!col) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    void AddLight(string name, Transform parent, Vector3 localPos,
                  LightType type, Color color, float intensity, float range,
                  LightShadows shadows = LightShadows.None, Quaternion? rotation = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        if (rotation.HasValue) go.transform.localRotation = rotation.Value;
        var l = go.AddComponent<Light>();
        l.type = type; l.color = color; l.intensity = intensity;
        l.range = range; l.shadows = shadows;
        if (type == LightType.Spot) l.spotAngle = 55f;
    }

    GameObject UiPanel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta, Vector2 pivot, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = pivot; rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        return go;
    }

    GameObject UiImage(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        return go;
    }

    GameObject UiButton(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Vector2 pivot, Color bgColor, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = bgColor;
        go.AddComponent<Button>();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = pivot; rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        return go;
    }
}
