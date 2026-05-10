using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

/// <summary>
/// Baut Level 5 – Schuppen (Breadboard-Puzzle) + Werkstatt (Brenner-Pickup).
///
/// Layout (Top-Down):
///   Schuppen  z = -3 … +3   Spieler startet hier, Breadboard am Werktisch
///   Werkstatt z = +3 … +7   hinter verschlossener Tuer, Bunsenbrenner liegt drin
///
/// Nach dem Loesen der 3 Verbindungen (1-6 | 3-5 | 4-8) oeffnet sich die Tuer.
/// Dann [E] am Brenner → Level 6.
///
/// Menue: Tools → Build Level 5 Breadboard
/// </summary>
public class BuildLevel5Breadboard : EditorWindow
{
    [MenuItem("Tools/Build Level 5 Breadboard")]
    public static void ShowWindow() => GetWindow<BuildLevel5Breadboard>("Level 5 Builder");

    void OnGUI()
    {
        GUILayout.Label("Level 5 – Schuppen + Werkstatt", EditorStyles.boldLabel);
        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Schuppen: Breadboard-Puzzle (3 Verbindungen: 1-6 | 3-5 | 4-8) loesen.\n" +
            "Tuer oeffnet sich → Werkstatt → [E] Bunsenbrenner aufnehmen → Level 6.",
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

        // ── Kamera ───────────────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.03f, 0.02f);
        cam.farClipPlane    = 35f;
        cam.tag             = "MainCamera";
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 8f, -3f);
        camGO.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        var follow = camGO.AddComponent<TopDownCameraFollow>();
        follow.fixedWorldPosition = new Vector3(0f, 1.8f, -4.4f);
        SceneManager.MoveGameObjectToScene(camGO, scene);

        // ── Umgebung ─────────────────────────────────────────────────────────
        var root = new GameObject("Environment");
        SceneManager.MoveGameObjectToScene(root, scene);
        var doorGO = BuildRooms(root.transform);

        // ── Spieler ──────────────────────────────────────────────────────────
        var player = AddPlayer(scene);
        if (player != null) follow.SetTarget(player.transform);

        // ── Beleuchtung ───────────────────────────────────────────────────────
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.10f, 0.07f);
        AddLight("SchuppenLight", root.transform, new Vector3(0f, 3.8f, 0f),
            LightType.Point, new Color(1f, 0.85f, 0.55f), 2.2f, 9f, LightShadows.Soft);
        AddLight("WerkstattLight", root.transform, new Vector3(0f, 3.8f, 5f),
            LightType.Point, new Color(0.9f, 0.88f, 0.75f), 2.0f, 8f, LightShadows.None);

        // ── Trigger-Spots ─────────────────────────────────────────────────────
        var breadboardTrigger = BuildBreadboardTrigger(root.transform);
        var brennerTrigger    = BuildBrennerTrigger(root.transform);
        var exitTrigger       = BuildExitTrigger(root.transform);

        // ── GameManager ────────────────────────────────────────────────────────
        var gmGO = new GameObject("GameManager");
        SceneManager.MoveGameObjectToScene(gmGO, scene);
        var gm   = gmGO.AddComponent<GameManager>();
        var gmso = new SerializedObject(gm);
        var arr  = gmso.FindProperty("levelSceneNames");
        arr.arraySize = 6;
        string[] names = { "Level1", "Level2", "Level3", "Level4", "Level5", "Level6" };
        for (int i = 0; i < names.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = names[i];
        gmso.ApplyModifiedPropertiesWithoutUndo();

        // ── UI ─────────────────────────────────────────────────────────────────
        BuildUI(scene, breadboardTrigger, brennerTrigger, exitTrigger, doorGO);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level5] Schuppen + Werkstatt fertig gebaut.");
    }

    // =========================================================================
    // 3D Raeume
    // =========================================================================

    /// <summary>Baut Schuppen (z -3…+3) und Werkstatt (z +3…+7), gibt Tuer-GO zurueck.</summary>
    GameObject BuildRooms(Transform root)
    {
        var wallMat   = M(new Color(0.26f, 0.22f, 0.16f), 0.02f, 0.06f);
        var floorMat  = M(new Color(0.20f, 0.18f, 0.14f), 0.04f, 0.10f);
        var metalMat  = M(new Color(0.28f, 0.25f, 0.20f), 0.40f, 0.22f);
        var darkMetal = M(new Color(0.16f, 0.14f, 0.10f), 0.55f, 0.28f);
        var woodMat   = M(new Color(0.30f, 0.20f, 0.10f), 0.01f, 0.06f);

        // ── Schuppen ─────────────────────────────────────────────────────────
        // Boden + Waende
        Box("Boden_S",     new Vector3(0, -0.08f, 0),     new Vector3(6f, 0.16f, 6f),   floorMat, root);
        Box("Wand_Links",  new Vector3(-3f, 2.5f, 0),     new Vector3(0.22f, 5f, 6f),   wallMat,  root);
        Box("Wand_Rechts", new Vector3( 3f, 2.5f, 0),     new Vector3(0.22f, 5f, 6f),   wallMat,  root);
        Box("Wand_Vorne_L",new Vector3(-2f, 2.5f, -3f),   new Vector3(2f, 5f, 0.22f),   wallMat,  root);
        Box("Wand_Vorne_R",new Vector3( 2f, 2.5f, -3f),   new Vector3(2f, 5f, 0.22f),   wallMat,  root);
        Box("Wand_Vorne_T",new Vector3( 0f, 3.8f, -3f),   new Vector3(6f, 2.4f, 0.22f), wallMat,  root);
        // Trennwand Schuppen/Werkstatt (mit Tueroeffnung 2 m breit)
        Box("Trenn_Links", new Vector3(-2f, 2.5f, 3f),    new Vector3(2f, 5f, 0.22f),   wallMat,  root);
        Box("Trenn_Rechts",new Vector3( 2f, 2.5f, 3f),    new Vector3(2f, 5f, 0.22f),   wallMat,  root);
        Box("Trenn_Top",   new Vector3( 0f, 3.8f, 3f),    new Vector3(6f, 2.4f, 0.22f), wallMat,  root);

        // Tuer (wird nach Puzzle deaktiviert)
        var door = Box("Tuer", new Vector3(0f, 1.3f, 3f), new Vector3(2f, 2.6f, 0.16f), darkMetal, root);

        // Moebel Schuppen: Werktisch (hinten links)
        Box("Bench_Top",   new Vector3(-1.6f, 0.88f, 1.8f),  new Vector3(2.0f, 0.08f, 0.85f), woodMat,  root);
        Box("Bench_Leg_FL",new Vector3(-0.72f,0.44f,  1.42f),new Vector3(0.08f,0.88f, 0.08f), darkMetal,root);
        Box("Bench_Leg_FR",new Vector3(-2.48f,0.44f,  1.42f),new Vector3(0.08f,0.88f, 0.08f), darkMetal,root);
        Box("Bench_Leg_BL",new Vector3(-0.72f,0.44f,  2.18f),new Vector3(0.08f,0.88f, 0.08f), darkMetal,root);
        Box("Bench_Leg_BR",new Vector3(-2.48f,0.44f,  2.18f),new Vector3(0.08f,0.88f, 0.08f), darkMetal,root);
        Box("Regal",       new Vector3(-1.6f, 1.85f, 2.22f), new Vector3(2.0f, 0.05f, 0.28f), woodMat,  root, col:false);
        // Breadboard-Objekt auf dem Werktisch (flache gruene Platte)
        Box("Breadboard",  new Vector3(-1.6f, 0.93f, 1.80f), new Vector3(0.7f, 0.02f, 0.45f),
            M(new Color(0.08f, 0.28f, 0.10f), 0.05f, 0.3f), root, col:false);
        // Faesser rechts
        Cyl("Barrel1",new Vector3(2.1f, 0.42f, 2.3f), new Vector3(0.36f,0.84f,0.36f), darkMetal,root);
        Cyl("Barrel2",new Vector3(2.5f, 0.42f, 1.9f), new Vector3(0.36f,0.84f,0.36f), metalMat, root);
        Box("Kiste",  new Vector3(1.6f,0.22f,0.8f),   new Vector3(0.55f,0.44f,0.55f),
            M(new Color(0.20f,0.14f,0.08f)), root);

        // ── Werkstatt (z +3…+7) ──────────────────────────────────────────────
        Box("Boden_W",     new Vector3(0, -0.08f, 5f),    new Vector3(6f, 0.16f, 4f),   floorMat, root);
        Box("WW_Links",    new Vector3(-3f, 2.5f, 5f),    new Vector3(0.22f, 5f, 4f),   wallMat,  root);
        Box("WW_Rechts",   new Vector3( 3f, 2.5f, 5f),    new Vector3(0.22f, 5f, 4f),   wallMat,  root);
        Box("WW_Hinten",   new Vector3( 0f, 2.5f, 7f),    new Vector3(6.44f, 5f, 0.22f),wallMat,  root);
        // Werkstatt-Moebel: Regal mit Brenner
        Box("WW_Regal_Top",new Vector3(-1.0f,1.20f, 6.4f),new Vector3(1.6f, 0.06f,0.4f),woodMat, root);
        Box("WW_Regal_L",  new Vector3(-1.72f,0.6f, 6.4f),new Vector3(0.06f,1.2f,0.4f), darkMetal,root,col:false);
        Box("WW_Regal_R",  new Vector3(-0.28f,0.6f, 6.4f),new Vector3(0.06f,1.2f,0.4f), darkMetal,root,col:false);
        // Bunsenbrenner auf dem Regal
        var brennerMat = M(new Color(0.55f,0.18f,0.06f), 0.5f, 0.3f);
        var tubeMat    = M(new Color(0.25f,0.22f,0.18f), 0.6f, 0.4f);
        Box("Brenner_Body",new Vector3(-1.0f,1.28f,6.40f),new Vector3(0.12f,0.16f,0.30f),brennerMat,root,col:false);
        Cyl("Brenner_Hose",new Vector3(-1.0f,1.26f,6.58f),new Vector3(0.04f,0.10f,0.04f),tubeMat,root);
        Box("Brenner_Tip", new Vector3(-1.0f,1.36f,6.26f),new Vector3(0.04f,0.04f,0.08f),tubeMat,root,col:false);
        Box("Brenner_Glow",new Vector3(-1.0f,1.38f,6.22f),new Vector3(0.03f,0.03f,0.03f),
            Emit(new Color(0.8f,0.3f,0.05f), new Color(1f,0.45f,0.05f), 2.0f),root,col:false);

        return door;
    }

    // =========================================================================
    // Trigger-Spots
    // =========================================================================

    GameObject BuildBreadboardTrigger(Transform root)
    {
        var go = new GameObject("BreadboardTrigger");
        go.transform.SetParent(root);
        go.transform.position = new Vector3(-1.6f, 0.95f, 1.8f);
        var bc = go.AddComponent<BoxCollider>();
        bc.size      = new Vector3(1.6f, 1.8f, 1.4f);
        bc.isTrigger = true;
        go.AddComponent<DustyWallSpot>();
        return go;
    }

    GameObject BuildBrennerTrigger(Transform root)
    {
        var go = new GameObject("BrennerTrigger");
        go.transform.SetParent(root);
        go.transform.position = new Vector3(-1.0f, 1.0f, 6.4f);
        var bc = go.AddComponent<BoxCollider>();
        bc.size      = new Vector3(1.8f, 1.6f, 1.2f);
        bc.isTrigger = true;
        go.AddComponent<DustyWallSpot>();
        return go;
    }

    GameObject BuildExitTrigger(Transform root)
    {
        var go = new GameObject("ExitTrigger");
        go.transform.SetParent(root);
        go.transform.position = new Vector3(0f, 1f, -3.6f);
        var bc = go.AddComponent<BoxCollider>();
        bc.size      = new Vector3(2f, 2f, 0.6f);
        bc.isTrigger = true;
        go.AddComponent<DustyWallSpot>();
        return go;
    }

    // =========================================================================
    // Spieler
    // =========================================================================

    GameObject AddPlayer(Scene scene)
    {
        var go = new GameObject("Player");
        go.transform.position = new Vector3(0f, 0f, -2.0f);
        go.tag = "Player";
        var rb = go.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.constraints    = RigidbodyConstraints.FreezeRotation;
        var col = go.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.28f;
        col.center = new Vector3(0, 0.9f, 0);
        go.AddComponent<PlayerController>();
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    // =========================================================================
    // UI
    // =========================================================================

    void BuildUI(Scene scene, GameObject breadboardTrigger, GameObject brennerTrigger, GameObject exitTrigger, GameObject doorGO)
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

        // ── Dialog-Panel (BigYahuDialogSystem) ────────────────────────────────
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
        speakerTMP.text      = "Helios";
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

        // ── Breadboard-Prompt ─────────────────────────────────────────────────
        var bbPromptGO = UiPanel("BreadboardPrompt", canvasGO.transform,
            new Vector2(0.5f,0f), new Vector2(0.5f,0f),
            new Vector2(0f,80f), new Vector2(400f,60f),
            new Vector2(0.5f,0f), new Color(0f,0f,0f,0.72f));
        bbPromptGO.SetActive(false);
        AddPromptText(bbPromptGO.transform, "[E] Breadboard untersuchen");

        // ── Breadboard-Puzzle-Panel ───────────────────────────────────────────
        var bbPanelGO = UiPanel("BreadboardPanel", canvasGO.transform,
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            Vector2.zero, new Vector2(780f, 520f),
            new Vector2(0.5f,0.5f), new Color(0.06f,0.06f,0.08f,0.97f));
        bbPanelGO.SetActive(false);
        // Titel
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(bbPanelGO.transform, false);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "SCHALTKREIS REPARIEREN";
        titleTMP.fontSize  = 28f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color     = new Color(1f, 0.85f, 0.30f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f,1f); titleRT.anchorMax = new Vector2(1f,1f);
        titleRT.pivot = new Vector2(0.5f,1f);
        titleRT.anchoredPosition = new Vector2(0f,-18f);
        titleRT.sizeDelta = new Vector2(0f, 46f);
        // Schaltplan-Hinweis
        var hintGO = new GameObject("Hint");
        hintGO.transform.SetParent(bbPanelGO.transform, false);
        var hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
        hintTMP.text      = "Schaltplan:   1 — 6     3 — 5     4 — 8";
        hintTMP.fontSize  = 20f;
        hintTMP.color     = new Color(0.75f, 0.88f, 1.00f);
        hintTMP.alignment = TextAlignmentOptions.Center;
        var hintRT = hintGO.GetComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0f,1f); hintRT.anchorMax = new Vector2(1f,1f);
        hintRT.pivot = new Vector2(0.5f,1f);
        hintRT.anchoredPosition = new Vector2(0f,-76f);
        hintRT.sizeDelta = new Vector2(0f, 32f);
        // Breadboard-Untergrund (gruen wie echtes Breadboard)
        var bbBgGO = UiPanel("BBBackground", bbPanelGO.transform,
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0f,-30f), new Vector2(680f, 320f),
            new Vector2(0.5f,0.5f), new Color(0.06f, 0.20f, 0.08f, 1f));
        // 8 Node-Buttons: Reihe A (oben): 1-4, Reihe B (unten): 5-8
        var nodeButtons = new Button[8];
        string[] labels = { "1","2","3","4","5","6","7","8" };
        for (int i = 0; i < 8; i++)
        {
            int row = i / 4;  // 0=oben, 1=unten
            int col = i % 4;
            float x = -255f + col * 170f;
            float y = (row == 0) ? 90f : -90f;
            var btnGO = UiButton($"Node_{i+1}", bbBgGO.transform,
                new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                new Vector2(x, y), new Vector2(110f, 110f),
                new Vector2(0.5f,0.5f), new Color(0.15f,0.45f,0.15f), labels[i]);
            // Node-Nummer fett und gross
            var lbl = btnGO.GetComponentInChildren<TextMeshProUGUI>();
            lbl.fontSize  = 32f;
            lbl.fontStyle = FontStyles.Bold;
            nodeButtons[i] = btnGO.GetComponent<Button>();
        }
        // Reihen-Beschriftung
        AddRowLabel(bbBgGO.transform, "A", new Vector2(-330f, 90f));
        AddRowLabel(bbBgGO.transform, "B", new Vector2(-330f,-90f));

        // ── Interaction-Prompt (Brenner) ──────────────────────────────────────
        var pickupPromptGO = UiPanel("InteractionPrompt", canvasGO.transform,
            new Vector2(0.5f,0f), new Vector2(0.5f,0f),
            new Vector2(0f,80f), new Vector2(360f,60f),
            new Vector2(0.5f,0f), new Color(0f,0f,0f,0.72f));
        pickupPromptGO.SetActive(false);
        AddPromptText(pickupPromptGO.transform, "[E] Brenner aufnehmen");

        // ── Pickup-Flash ──────────────────────────────────────────────────────
        var flashGO = UiPanel("PickupFlash", canvasGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Vector2(0.5f,0.5f), new Color(1f,0.7f,0.2f,0.35f));
        flashGO.SetActive(false);

        // ── Exit-Prompt (Ausgang nach Brenner-Pickup) ─────────────────────────
        var exitPromptGO = UiPanel("ExitPrompt", canvasGO.transform,
            new Vector2(0.5f,0f), new Vector2(0.5f,0f),
            new Vector2(0f,80f), new Vector2(360f,60f),
            new Vector2(0.5f,0f), new Color(0f,0f,0f,0.72f));
        exitPromptGO.SetActive(false);
        AddPromptText(exitPromptGO.transform, "[E] Schuppen verlassen");

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

        // ── Level5_Werkstatt ──────────────────────────────────────────────────
        var l5GO = new GameObject("Level5_Werkstatt");
        SceneManager.MoveGameObjectToScene(l5GO, scene);
        var l5   = l5GO.AddComponent<Level5_Werkstatt>();
        var l5so = new SerializedObject(l5);
        // Breadboard
        l5so.FindProperty("breadboardSpot").objectReferenceValue   =
            breadboardTrigger?.GetComponent<DustyWallSpot>();
        l5so.FindProperty("breadboardPanel").objectReferenceValue  = bbPanelGO;
        l5so.FindProperty("breadboardPrompt").objectReferenceValue = bbPromptGO;
        var nodesProp = l5so.FindProperty("nodeButtons");
        nodesProp.arraySize = nodeButtons.Length;
        for (int i = 0; i < nodeButtons.Length; i++)
            nodesProp.GetArrayElementAtIndex(i).objectReferenceValue = nodeButtons[i];
        // Tuer
        l5so.FindProperty("doorObject").objectReferenceValue = doorGO;
        // Brenner
        l5so.FindProperty("brennerSpot").objectReferenceValue       =
            brennerTrigger?.GetComponent<DustyWallSpot>();
        l5so.FindProperty("interactionPrompt").objectReferenceValue = pickupPromptGO;
        l5so.FindProperty("pickupFlash").objectReferenceValue       = flashGO;
        // Ausgang
        l5so.FindProperty("exitSpot").objectReferenceValue   =
            exitTrigger?.GetComponent<DustyWallSpot>();
        l5so.FindProperty("exitPrompt").objectReferenceValue = exitPromptGO;
        l5so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[Level5] UI und GameLogic verdrahtet.");
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

    void AddRowLabel(Transform parent, string label, Vector2 pos)
    {
        var go = new GameObject($"RowLabel_{label}");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 22f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = new Color(0.7f, 0.9f, 0.7f);
        tmp.alignment = TextAlignmentOptions.Center;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f,0.5f); rt.anchorMax = new Vector2(0.5f,0.5f);
        rt.pivot = new Vector2(0.5f,0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(40f,40f);
    }

    Material M(Color c, float metal = 0f, float smooth = 0.35f)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Metallic",   metal);
        m.SetFloat("_Glossiness", smooth);
        return m;
    }

    Material Emit(Color c, Color glow, float intensity = 1.5f)
    {
        var m = M(c, 0f, 0.05f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", glow * intensity);
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

    void Cyl(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
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
