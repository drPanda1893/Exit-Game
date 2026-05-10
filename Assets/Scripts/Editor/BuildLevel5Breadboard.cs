using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

/// <summary>
/// Baut Level 5 – Werkstatt (3D).
/// Spieler betritt die Werkstatt, findet den Bunsenbrenner auf dem Werktisch
/// und nimmt ihn auf → Level 6 wird geladen.
/// Menü: Tools → Build Level 5 Breadboard
/// </summary>
public class BuildLevel5Breadboard : EditorWindow
{
    [MenuItem("Tools/Build Level 5 Breadboard")]
    public static void ShowWindow() => GetWindow<BuildLevel5Breadboard>("Level 5 Builder");

    void OnGUI()
    {
        GUILayout.Label("Level 5 – Werkstatt / Brenner-Pickup", EditorStyles.boldLabel);
        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "3D Werkstatt: Spieler findet den Bunsenbrenner auf dem Werktisch.\n" +
            "[E] aufnehmen → Dialog → Level 6 lädt.",
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

        // ── Kamera ────────────────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.03f, 0.02f);
        cam.farClipPlane = 30f;
        cam.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 6f, -3f);
        camGO.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        var follow = camGO.AddComponent<TopDownCameraFollow>();
        follow.fixedWorldPosition = new Vector3(0f, 1.8f, -4.4f);
        SceneManager.MoveGameObjectToScene(camGO, scene);

        // ── Umgebung ──────────────────────────────────────────────────────────
        var root = new GameObject("Environment");
        SceneManager.MoveGameObjectToScene(root, scene);
        BuildRoom(root.transform);

        // ── Spieler ───────────────────────────────────────────────────────────
        var player = AddPlayer(scene);
        if (player != null) follow.SetTarget(player.transform);

        // ── Licht ─────────────────────────────────────────────────────────────
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.15f, 0.12f, 0.08f);
        AddLight("KeyLight", root.transform, new Vector3(0, 4f, 0.5f),
            LightType.Point, new Color(1f, 0.85f, 0.55f), 2.5f, 10f, shadows: LightShadows.Soft);
        AddLight("FillLight", root.transform, new Vector3(0, 4f, 0),
            LightType.Directional, new Color(0.5f, 0.55f, 0.8f), 0.4f, 0f,
            rotation: Quaternion.Euler(70f, 0f, 0f));

        // ── Brenner-Spot ──────────────────────────────────────────────────────
        var brennerSpotGO = BuildBrennerSpot(root.transform);

        // ── GameManager ───────────────────────────────────────────────────────
        var gmGO = new GameObject("GameManager");
        SceneManager.MoveGameObjectToScene(gmGO, scene);
        var gm = gmGO.AddComponent<GameManager>();
        var gmso = new SerializedObject(gm);
        var arr = gmso.FindProperty("levelSceneNames");
        arr.arraySize = 6;
        string[] names = { "Level1", "Level2", "Level3", "Level4", "Level5", "Level6" };
        for (int i = 0; i < names.Length; i++)
            arr.GetArrayElementAtIndex(i).stringValue = names[i];
        gmso.ApplyModifiedPropertiesWithoutUndo();

        // ── UI ────────────────────────────────────────────────────────────────
        BuildUI(scene, brennerSpotGO);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level5] Werkstatt fertig gebaut.");
    }

    // =========================================================================
    // 3D Raum
    // =========================================================================

    void BuildRoom(Transform root)
    {
        var wallMat    = M(new Color(0.28f, 0.24f, 0.18f), 0.02f, 0.06f);
        var floorMat   = M(new Color(0.22f, 0.20f, 0.16f), 0.04f, 0.10f);
        var metalMat   = M(new Color(0.30f, 0.27f, 0.22f), 0.35f, 0.20f);
        var darkMetal  = M(new Color(0.18f, 0.16f, 0.12f), 0.50f, 0.25f);
        var woodMat    = M(new Color(0.32f, 0.22f, 0.12f), 0.01f, 0.06f);

        // Boden & Wände
        Box("Floor",      new Vector3(0,     -0.08f,  0),    new Vector3(6f, 0.16f, 6f),   floorMat, root);
        Box("BackWall",   new Vector3(0,      2.5f,   3.0f), new Vector3(6f, 5f, 0.22f),   wallMat,  root);
        Box("LeftWall",   new Vector3(-3.0f,  2.5f,   0),    new Vector3(0.22f, 5f, 6f),   wallMat,  root);
        Box("RightWall",  new Vector3( 3.0f,  2.5f,   0),    new Vector3(0.22f, 5f, 6f),   wallMat,  root);
        Box("FrontWall_L",new Vector3(-2.0f,  2.5f,  -3.0f), new Vector3(2.0f, 5f, 0.22f), wallMat,  root);
        Box("FrontWall_R",new Vector3( 2.0f,  2.5f,  -3.0f), new Vector3(2.0f, 5f, 0.22f), wallMat,  root);
        Box("FrontWall_T",new Vector3( 0f,    3.8f,  -3.0f), new Vector3(6.0f, 2.4f, 0.22f), wallMat, root);

        // Eingangs-Rahmen
        Box("DoorFrame_L",  new Vector3(-1.0f, 1.3f, -2.92f), new Vector3(0.06f, 2.6f, 0.06f), darkMetal, root, col: false);
        Box("DoorFrame_R",  new Vector3( 1.0f, 1.3f, -2.92f), new Vector3(0.06f, 2.6f, 0.06f), darkMetal, root, col: false);
        Box("DoorFrame_Top",new Vector3( 0f,   2.62f,-2.92f), new Vector3(2.06f, 0.06f, 0.06f), darkMetal, root, col: false);

        // Werktisch (hinten links)
        Box("Bench_Top",  new Vector3(-1.6f, 0.88f, 1.8f), new Vector3(2.0f, 0.08f, 0.85f), woodMat,  root);
        Box("Bench_Leg_FL",new Vector3(-0.72f,0.44f, 1.42f),new Vector3(0.08f, 0.88f, 0.08f), darkMetal, root);
        Box("Bench_Leg_FR",new Vector3(-2.48f,0.44f, 1.42f),new Vector3(0.08f, 0.88f, 0.08f), darkMetal, root);
        Box("Bench_Leg_BL",new Vector3(-0.72f,0.44f, 2.18f),new Vector3(0.08f, 0.88f, 0.08f), darkMetal, root);
        Box("Bench_Leg_BR",new Vector3(-2.48f,0.44f, 2.18f),new Vector3(0.08f, 0.88f, 0.08f), darkMetal, root);
        // Regal über Werktisch
        Box("Shelf",      new Vector3(-1.6f, 1.85f, 2.22f), new Vector3(2.0f, 0.05f, 0.28f), woodMat,  root, col: false);

        // Werkzeugwand rechts (dekorativ)
        Box("ToolBoard",  new Vector3( 2.72f, 1.6f, 0.5f), new Vector3(0.04f, 1.4f, 2.0f), woodMat,  root, col: false);

        // Metallfässer hinten rechts
        Cyl("Barrel1", new Vector3(2.1f, 0.42f, 2.3f), new Vector3(0.36f, 0.84f, 0.36f), darkMetal, root);
        Cyl("Barrel2", new Vector3(2.5f, 0.42f, 1.9f), new Vector3(0.36f, 0.84f, 0.36f), metalMat,  root);

        // Kleine Kiste auf dem Boden
        Box("Crate",    new Vector3(1.6f, 0.22f, 0.8f), new Vector3(0.55f, 0.44f, 0.55f),
            M(new Color(0.22f, 0.15f, 0.08f)), root);
    }

    // =========================================================================
    // Brenner-Objekt auf dem Werktisch
    // =========================================================================

    GameObject BuildBrennerSpot(Transform root)
    {
        var brennerMat = M(new Color(0.55f, 0.18f, 0.06f), 0.5f, 0.3f);  // orange-rot
        var tubeMat    = M(new Color(0.25f, 0.22f, 0.18f), 0.6f, 0.4f);

        var spotGO = new GameObject("BrennerSpot");
        spotGO.transform.SetParent(root);

        // Brenner-Koerper (auf dem Werktisch)
        Box("Brenner_Body", new Vector3(-1.6f, 0.98f, 1.8f),
            new Vector3(0.12f, 0.18f, 0.32f), brennerMat, spotGO.transform, col: false);
        // Schlauch
        Cyl("Brenner_Hose", new Vector3(-1.6f, 0.96f, 2.02f),
            new Vector3(0.04f, 0.10f, 0.04f), tubeMat, spotGO.transform);
        // Düse
        Box("Brenner_Tip", new Vector3(-1.6f, 1.06f, 1.65f),
            new Vector3(0.04f, 0.04f, 0.08f), tubeMat, spotGO.transform, col: false);

        // Glüh-Schein (emissiv)
        var glowMat = Emit(new Color(0.8f, 0.3f, 0.05f), new Color(1f, 0.45f, 0.05f), 2.0f);
        Box("Brenner_Glow", new Vector3(-1.6f, 1.08f, 1.60f),
            new Vector3(0.03f, 0.03f, 0.03f), glowMat, spotGO.transform, col: false);

        // Trigger-Zone um den Brenner
        var trigGO = new GameObject("BrennerTrigger");
        trigGO.transform.SetParent(spotGO.transform);
        trigGO.transform.position = new Vector3(-1.6f, 0.95f, 1.8f);
        var bc = trigGO.AddComponent<BoxCollider>();
        bc.size      = new Vector3(1.4f, 1.6f, 1.2f);
        bc.isTrigger = true;
        trigGO.AddComponent<DustyWallSpot>();

        return trigGO;
    }

    // =========================================================================
    // Spieler
    // =========================================================================

    GameObject AddPlayer(Scene scene)
    {
        var playerGO = new GameObject("Player");
        playerGO.transform.position = new Vector3(0f, 0f, -2.0f);
        playerGO.tag = "Player";

        var rb = playerGO.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        var col = playerGO.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.28f;
        col.center = new Vector3(0, 0.9f, 0);

        playerGO.AddComponent<PlayerController>();
        SceneManager.MoveGameObjectToScene(playerGO, scene);
        return playerGO;
    }

    // =========================================================================
    // UI
    // =========================================================================

    void BuildUI(Scene scene, GameObject brennerSpotGO)
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
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Dialog-Panel
        var dialogPanelGO = UiPanel("DialogPanel", canvasGO.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, 260f),
            new Vector2(0.5f, 0f), new Color(0.03f, 0.03f, 0.06f, 0.97f));
        dialogPanelGO.SetActive(false);

        // Goldener Rand oben
        UiImage("TopBorder", dialogPanelGO.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            Vector2.zero, new Vector2(0f, 3f),
            new Color(0.55f, 0.45f, 0.20f));

        // Portrait
        var portraitGO = UiImage("Portrait", dialogPanelGO.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(110f, 0f), new Vector2(190f, 190f),
            new Color(0.30f, 0.28f, 0.38f, 1f));

        // Speaker-Label
        var speakerGO = new GameObject("SpeakerLabel");
        speakerGO.transform.SetParent(dialogPanelGO.transform, false);
        var speakerTMP = speakerGO.AddComponent<TextMeshProUGUI>();
        speakerTMP.text      = "Joshi";
        speakerTMP.fontSize  = 26f;
        speakerTMP.fontStyle = FontStyles.Bold;
        speakerTMP.color     = new Color(1f, 0.85f, 0.45f);
        var speakerRT = speakerGO.GetComponent<RectTransform>();
        speakerRT.anchorMin        = new Vector2(0f, 1f);
        speakerRT.anchorMax        = new Vector2(1f, 1f);
        speakerRT.pivot            = new Vector2(0f, 1f);
        speakerRT.anchoredPosition = new Vector2(240f, -12f);
        speakerRT.sizeDelta        = new Vector2(-370f, 36f);

        // Dialog-Text
        var dialogTextGO = new GameObject("DialogText");
        dialogTextGO.transform.SetParent(dialogPanelGO.transform, false);
        var dialogTMP = dialogTextGO.AddComponent<TextMeshProUGUI>();
        dialogTMP.text     = "";
        dialogTMP.fontSize = 24f;
        dialogTMP.color    = Color.white;
        var dialogRT = dialogTextGO.GetComponent<RectTransform>();
        dialogRT.anchorMin = Vector2.zero;
        dialogRT.anchorMax = Vector2.one;
        dialogRT.offsetMin = new Vector2(240f, 48f);
        dialogRT.offsetMax = new Vector2(-145f, -52f);

        // Weiter-Button
        var continueBtnGO = UiButton("ContinueButton", dialogPanelGO.transform,
            new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-14f, 14f), new Vector2(130f, 42f),
            new Vector2(1f, 0f), new Color(0.12f, 0.52f, 0.22f), "Weiter ▶");

        // Interaction Prompt
        var promptGO = UiPanel("InteractionPrompt", canvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 80f), new Vector2(360f, 60f),
            new Vector2(0.5f, 0f), new Color(0f, 0f, 0f, 0.72f));
        promptGO.SetActive(false);
        var promptTextGO = new GameObject("Text");
        promptTextGO.transform.SetParent(promptGO.transform, false);
        var promptTMP = promptTextGO.AddComponent<TextMeshProUGUI>();
        promptTMP.text      = "[E] Brenner aufnehmen";
        promptTMP.fontSize  = 22f;
        promptTMP.alignment = TextAlignmentOptions.Center;
        promptTMP.color     = Color.white;
        var promptRT = promptTextGO.GetComponent<RectTransform>();
        promptRT.anchorMin = Vector2.zero;
        promptRT.anchorMax = Vector2.one;
        promptRT.offsetMin = new Vector2(8f, 4f);
        promptRT.offsetMax = new Vector2(-8f, -4f);

        // Pickup-Flash (kurzes weißes Aufleuchten)
        var flashGO = UiPanel("PickupFlash", canvasGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Vector2(0.5f, 0.5f), new Color(1f, 0.7f, 0.2f, 0.35f));
        flashGO.SetActive(false);

        // ── BigYahuDialogSystem ────────────────────────────────────────────────
        var dsGO = new GameObject("BigYahuDialogSystem");
        SceneManager.MoveGameObjectToScene(dsGO, scene);
        var ds   = dsGO.AddComponent<BigYahuDialogSystem>();
        var dso  = new SerializedObject(ds);
        dso.FindProperty("dialogPanel").objectReferenceValue    = dialogPanelGO;
        dso.FindProperty("dialogText").objectReferenceValue     = dialogTMP;
        dso.FindProperty("speakerLabel").objectReferenceValue   = speakerTMP;
        dso.FindProperty("portraitImage").objectReferenceValue  = portraitGO.GetComponent<Image>();
        dso.FindProperty("continueButton").objectReferenceValue = continueBtnGO.GetComponent<Button>();
        dso.ApplyModifiedPropertiesWithoutUndo();

        // ── Level5_Werkstatt ───────────────────────────────────────────────────
        var l5GO = new GameObject("Level5_Werkstatt");
        SceneManager.MoveGameObjectToScene(l5GO, scene);
        var l5   = l5GO.AddComponent<Level5_Werkstatt>();
        var l5so = new SerializedObject(l5);
        l5so.FindProperty("brennerSpot").objectReferenceValue       =
            brennerSpotGO != null ? brennerSpotGO.GetComponent<DustyWallSpot>() : null;
        l5so.FindProperty("interactionPrompt").objectReferenceValue = promptGO;
        l5so.FindProperty("pickupFlash").objectReferenceValue       = flashGO;
        l5so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[Level5] UI aufgebaut.");
    }

    // =========================================================================
    // Hilfs-Methoden
    // =========================================================================

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
                  float spotAngle = 60f, LightShadows shadows = LightShadows.None,
                  Quaternion? rotation = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        if (rotation.HasValue) go.transform.localRotation = rotation.Value;
        var l = go.AddComponent<Light>();
        l.type = type; l.color = color; l.intensity = intensity;
        l.range = range; l.spotAngle = spotAngle; l.shadows = shadows;
    }

    GameObject UiPanel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Vector2 pivot, Color color)
    {
        var go  = new GameObject(name);
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
        var img = go.AddComponent<Image>();
        img.color = bgColor;
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
