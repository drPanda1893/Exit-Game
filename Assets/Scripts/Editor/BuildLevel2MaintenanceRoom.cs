using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

/// <summary>
/// Baut Level 2 – Geheimer High-End Wartungsraum.
/// Menü: Tools → Build Level 2 Maintenance Room
/// </summary>
public class BuildLevel2MaintenanceRoom : EditorWindow
{
    [MenuItem("Tools/Build Level 2 Maintenance Room")]
    public static void ShowWindow() => GetWindow<BuildLevel2MaintenanceRoom>("Level 2 Builder");

    void OnGUI()
    {
        GUILayout.Label("Level 2 – Geheimer Wartungsraum", EditorStyles.boldLabel);
        GUILayout.Space(8);
        EditorGUILayout.HelpBox("Versteckter High-End Wartungsraum mit Joshi (sitzt, redet).", MessageType.Info);
        GUILayout.Space(12);
        if (GUILayout.Button("Wartungsraum bauen", GUILayout.Height(34)))
            Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Scene Root
    // ═══════════════════════════════════════════════════════════════════════

    private void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level2.unity");

        // ── Kamera (zuerst, damit sie auch bei Fehlern existiert) ─────────────
        GameObject camGO = new GameObject("Main Camera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.03f, 0.07f);
        cam.farClipPlane    = 30f;
        cam.nearClipPlane   = 0.1f;
        cam.tag             = "MainCamera";
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 6f, -3f);
        camGO.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        TopDownCameraFollow follow = camGO.AddComponent<TopDownCameraFollow>();
        // Feste Kamera am Eingang – schaut in den Raum, folgt nicht dem Spieler
        follow.fixedWorldPosition = new Vector3(0f, 1.8f, -4.4f);
        SceneManager.MoveGameObjectToScene(camGO, scene);

        // ── Umgebung ──────────────────────────────────────────────────────────
        GameObject root = new GameObject("Environment");
        SceneManager.MoveGameObjectToScene(root, scene);
        var entranceBorder = BuildRoom(root.transform);

        // ── Spieler-Charakter ─────────────────────────────────────────────────
        GameObject player = AddPlayer(scene);
        if (player != null) follow.SetTarget(player.transform);

        // ── Joshi NPC ─────────────────────────────────────────────────────────
        try { AddJoshi(scene); }
        catch (System.Exception e) { Debug.LogWarning("[Level2] Joshi fehlgeschlagen: " + e.Message); }

        // ── Globales Licht ────────────────────────────────────────────────────
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.15f, 0.10f);

        AddBackgroundMusic(scene);

        // ── GameManager ───────────────────────────────────────────────────────
        var gmGO = new GameObject("GameManager");
        SceneManager.MoveGameObjectToScene(gmGO, scene);
        var gm = gmGO.AddComponent<GameManager>();
        var gmso = new SerializedObject(gm);
        var sceneNamesArr = gmso.FindProperty("levelSceneNames");
        sceneNamesArr.arraySize = 6;
        string[] names = { "Level1", "Level2", "Level3", "Level4", "Level5", "Level6" };
        for (int i = 0; i < names.Length; i++)
            sceneNamesArr.GetArrayElementAtIndex(i).stringValue = names[i];
        gmso.ApplyModifiedPropertiesWithoutUndo();

        // ── Staubfleck an der Wand + Trigger ──────────────────────────────────
        var wallSpot = BuildDustyWallSpot(root.transform, scene);

        // ── Schloss am Eingang ────────────────────────────────────────────────
        var lockSpotGO = BuildLockSpot(root.transform);

        BuildUI(scene, wallSpot, lockSpotGO, entranceBorder);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level2] Wartungsraum fertig.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Material-Helfer
    // ═══════════════════════════════════════════════════════════════════════

    private Material M(Color c, float metal = 0f, float smooth = 0.35f)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Metallic",   metal);
        m.SetFloat("_Glossiness", smooth);
        return m;
    }

    private Material Emit(Color c, Color glow, float intensity = 1.5f)
    {
        var m = M(c, 0f, 0.05f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", glow * intensity);
        return m;
    }

    private Material Trans(Color c)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_ALPHABLEND_ON");
        m.renderQueue = 3000;
        return m;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Primitive-Helfer
    // ═══════════════════════════════════════════════════════════════════════

    private GameObject Box(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent,
                           Quaternion? rot = null, bool col = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent);
        go.transform.position = pos; go.transform.localScale = scale;
        if (rot.HasValue) go.transform.rotation = rot.Value;
        go.GetComponent<Renderer>().material = mat;
        if (!col) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private GameObject Cyl(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent,
                            Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name; go.transform.SetParent(parent);
        go.transform.position = pos; go.transform.localScale = scale;
        if (rot.HasValue) go.transform.rotation = rot.Value;
        go.GetComponent<Renderer>().material = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private void AddLight(string name, Transform parent, Vector3 localPos,
                          LightType type, Color color, float intensity, float range,
                          float spotAngle = 60f, LightShadows shadows = LightShadows.None,
                          Quaternion? rotation = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent); go.transform.localPosition = localPos;
        if (rotation.HasValue) go.transform.localRotation = rotation.Value;
        var l = go.AddComponent<Light>();
        l.type = type; l.color = color; l.intensity = intensity;
        l.range = range; l.spotAngle = spotAngle; l.shadows = shadows;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Raum
    // ═══════════════════════════════════════════════════════════════════════

    private GameObject BuildRoom(Transform root)
    {
        // ── Materialien – verlassene Abstellkammer ───────────────────────────
        var wallMat     = M(new Color(0.30f, 0.27f, 0.23f), 0.03f, 0.07f);  // schmutziger Putz
        var oldMetal    = M(new Color(0.22f, 0.20f, 0.17f), 0.18f, 0.14f);  // altes Metall
        var brightMetal = M(new Color(0.32f, 0.28f, 0.22f), 0.25f, 0.18f);  // etwas heller
        var rustMetal   = M(new Color(0.38f, 0.20f, 0.08f), 0.06f, 0.06f);  // Rost
        var floorMat    = M(new Color(0.24f, 0.22f, 0.19f), 0.02f, 0.08f);  // fleckiger Betonboden
        var grateMat    = M(new Color(0.20f, 0.17f, 0.13f), 0.10f, 0.10f);  // altes Gitter
        var dirtyWood   = M(new Color(0.22f, 0.15f, 0.08f), 0.01f, 0.08f);  // morsches Holz
        var rubberMat   = M(new Color(0.12f, 0.10f, 0.09f), 0.01f, 0.04f);  // abgenutztes Gummi
        var dirtyWhite  = M(new Color(0.72f, 0.68f, 0.58f), 0.02f, 0.08f);  // vergilbtes Weiß
        var concreteMat = M(new Color(0.28f, 0.26f, 0.22f), 0.02f, 0.06f);  // Betongrau

        // Emissive (nur minimale Signallampen)
        var ledRed   = Emit(new Color(0.26f, 0.03f, 0.03f), new Color(1.00f, 0.06f, 0.06f), 1.2f);
        var ledAmber = Emit(new Color(0.28f, 0.16f, 0.02f), new Color(1.00f, 0.58f, 0.06f), 1.0f);
        var ledGreen = Emit(new Color(0.03f, 0.20f, 0.05f), new Color(0.06f, 1.00f, 0.16f), 0.9f);
        var hazardYel= M(new Color(0.55f, 0.45f, 0.04f), 0f, 0.10f);  // verblasste Warnung
        var hazardBlk= M(new Color(0.08f, 0.08f, 0.08f), 0f, 0.05f);

        // ── Boden – schmutziger Betonboden ───────────────────────────────────
        Box("Floor", new Vector3(0, -0.08f, 0), new Vector3(6f, 0.16f, 6f), floorMat, root);
        // Betonplatten-Fugen
        for (int x = -2; x <= 2; x++)
        for (int z = -2; z <= 2; z++)
        {
            Box($"FloorSlab_{x}_{z}", new Vector3(x * 1.0f, 0.001f, z * 1.0f),
                new Vector3(0.97f, 0.002f, 0.97f), concreteMat, root, col: false);
        }
        // Flecken / alte Ölflecken auf dem Boden
        Box("Stain1", new Vector3(-0.8f, 0.003f,  0.4f), new Vector3(0.55f, 0.002f, 0.35f), M(new Color(0.10f,0.08f,0.06f),0f,0.02f), root, col: false);
        Box("Stain2", new Vector3( 1.2f, 0.003f, -0.8f), new Vector3(0.40f, 0.002f, 0.28f), M(new Color(0.09f,0.07f,0.05f),0f,0.02f), root, col: false);

        // Gefahrenstreifen am Eingang (Vorderwand)
        for (int i = -3; i <= 3; i++)
        {
            var hazMat = (i % 2 == 0) ? hazardYel : hazardBlk;
            Box($"Haz_{i}", new Vector3(i * 0.18f, 0.005f, -2.15f), new Vector3(0.16f, 0.003f, 0.5f),
                hazMat, root, Quaternion.Euler(0, 45f, 0), false);
        }

        // ── Wände – schmutziger Putz/Beton ───────────────────────────────────
        Box("BackWall",      new Vector3( 0f,    2.5f,  3.0f), new Vector3(6.0f, 5f, 0.24f), wallMat, root);
        Box("LeftWall",      new Vector3(-3.0f,  2.5f,  0f  ), new Vector3(0.24f, 5f, 6.0f), wallMat, root);
        Box("RightWall",     new Vector3( 3.0f,  2.5f,  0f  ), new Vector3(0.24f, 5f, 6.0f), wallMat, root);
        Box("FrontWall_L",   new Vector3(-2.0f,  2.5f, -3.0f), new Vector3(2.0f, 5f, 0.24f), wallMat, root);
        Box("FrontWall_R",   new Vector3( 2.0f,  2.5f, -3.0f), new Vector3(2.0f, 5f, 0.24f), wallMat, root);
        Box("FrontWall_Top", new Vector3( 0f,    3.8f, -3.0f), new Vector3(6.0f, 2.4f, 0.24f), wallMat, root);

        // Unsichtbare Border – verhindert Rauslaufen, sitzt innerhalb des Raums
        GameObject border = new GameObject("EntranceBorder");
        border.transform.position = new Vector3(0f, 1.5f, -2.6f);
        border.transform.SetParent(root);
        var bc = border.AddComponent<BoxCollider>();
        bc.size = new Vector3(6.0f, 3.0f, 0.2f);  // volle Raumbreite – keine Lücke möglich
        bc.isTrigger = false;

        // Eingangsrahmen (verrostetes Metall)
        Box("DoorFrame_L",  new Vector3(-1.0f, 1.3f, -2.92f), new Vector3(0.06f, 2.6f, 0.06f), rustMetal, root, col: false);
        Box("DoorFrame_R",  new Vector3( 1.0f, 1.3f, -2.92f), new Vector3(0.06f, 2.6f, 0.06f), rustMetal, root, col: false);
        Box("DoorFrame_Top",new Vector3( 0f,   2.62f,-2.92f), new Vector3(2.06f, 0.06f, 0.06f), rustMetal, root, col: false);

        // Wandabnutzung – alte horizontale Schadensstreifen
        float[] scratchY = { 0.9f, 1.6f, 2.3f };
        foreach (float sy in scratchY)
        {
            Box("ScB", new Vector3(0,      sy, 2.88f), new Vector3(5.85f, 0.018f, 0.010f), rustMetal, root, col: false);
            Box("ScL", new Vector3(-2.88f, sy, 0),    new Vector3(0.010f, 0.018f, 5.85f), rustMetal, root, col: false);
            Box("ScR", new Vector3( 2.88f, sy, 0),    new Vector3(0.010f, 0.018f, 5.85f), rustMetal, root, col: false);
        }

        // Decke absichtlich weggelassen – Top-Down-Perspektive

        // ── Rohre an der Wand (hoch, realistisch) ────────────────────────────
        BuildPipes(root, rustMetal);

        // ── Holzkisten gestapelt (hinten links) ───────────────────────────────
        var crateMatA = dirtyWood;
        var crateMatB = M(new Color(0.19f, 0.13f, 0.07f), 0.01f, 0.06f);
        // Stapel 1
        Box("CrateA1", new Vector3(-1.9f, 0.22f, 2.1f), new Vector3(0.55f, 0.44f, 0.55f), crateMatA, root);
        Box("CrateA2", new Vector3(-1.9f, 0.66f, 2.1f), new Vector3(0.55f, 0.44f, 0.55f), crateMatB, root);
        Box("CrateA3", new Vector3(-1.3f, 0.22f, 2.1f), new Vector3(0.55f, 0.44f, 0.55f), crateMatA, root);
        // Lattenstruktur auf den Kisten
        foreach (var cx in new[]{-1.9f, -1.3f})
            for (int i = 0; i < 3; i++)
                Box($"Lat_{cx}_{i}", new Vector3(cx, 0.44f, 2.1f - 0.18f + i * 0.18f),
                    new Vector3(0.55f, 0.006f, 0.028f), rustMetal, root, col: false);

        // Stapel 2 (rechts hinten, etwas anders)
        Box("CrateB1", new Vector3(1.6f, 0.22f, 2.2f), new Vector3(0.60f, 0.44f, 0.50f), crateMatB, root);
        Box("CrateB2", new Vector3(1.6f, 0.66f, 2.2f), new Vector3(0.60f, 0.44f, 0.50f), crateMatA, root);
        Box("CrateB3", new Vector3(2.1f, 0.22f, 1.7f), new Vector3(0.50f, 0.44f, 0.60f), crateMatB, root);

        // ── Metall-Fässer (hinten rechts) ─────────────────────────────────────
        Cyl("Barrel1", new Vector3(2.0f, 0.42f, 2.3f), new Vector3(0.38f, 0.84f, 0.38f), rustMetal, root);
        Cyl("Barrel2", new Vector3(2.4f, 0.42f, 1.8f), new Vector3(0.38f, 0.84f, 0.38f), oldMetal, root);
        Box("BarrelLid1", new Vector3(2.0f, 0.85f, 2.3f), new Vector3(0.40f, 0.025f, 0.40f), brightMetal, root, col: false);
        Box("BarrelLid2", new Vector3(2.4f, 0.85f, 1.8f), new Vector3(0.40f, 0.025f, 0.40f), rustMetal, root, col: false);

        // ── Altes Metallregal (linke Wand) ────────────────────────────────────
        var shelfG = new GameObject("OldShelf"); shelfG.transform.position = new Vector3(-2.7f, 0f, 0.8f); shelfG.transform.SetParent(root);
        Box("SFrame_L",  new Vector3(-0.02f, 1.0f, -0.28f), new Vector3(0.04f, 2.0f, 0.04f), oldMetal, shelfG.transform);
        Box("SFrame_R",  new Vector3(-0.02f, 1.0f,  0.28f), new Vector3(0.04f, 2.0f, 0.04f), oldMetal, shelfG.transform);
        Box("SBoard1",   new Vector3(-0.02f, 0.40f,  0f),   new Vector3(0.12f, 0.025f, 0.60f), dirtyWood, shelfG.transform, col: false);
        Box("SBoard2",   new Vector3(-0.02f, 0.90f,  0f),   new Vector3(0.12f, 0.025f, 0.60f), dirtyWood, shelfG.transform, col: false);
        Box("SBoard3",   new Vector3(-0.02f, 1.45f,  0f),   new Vector3(0.12f, 0.025f, 0.60f), dirtyWood, shelfG.transform, col: false);
        // Kleinkram auf Regal
        Box("Rag",       new Vector3(-0.02f, 0.43f, -0.10f), new Vector3(0.08f, 0.025f, 0.14f), M(new Color(0.28f,0.18f,0.10f),0f,0.04f), shelfG.transform, col: false);
        Cyl("Canister",  new Vector3(-0.02f, 0.56f,  0.12f), new Vector3(0.07f, 0.14f, 0.07f), rustMetal, shelfG.transform);
        Box("Manual",    new Vector3(-0.02f, 0.95f, -0.08f), new Vector3(0.06f, 0.10f, 0.10f), dirtyWhite, shelfG.transform, col: false);
        var shelfBC = shelfG.AddComponent<BoxCollider>();
        shelfBC.center = new Vector3(0, 1.0f, 0); shelfBC.size = new Vector3(0.18f, 2.0f, 0.65f);

        // ── Einfacher Werkstattisch (rechte Wand) ─────────────────────────────
        var benchG = new GameObject("Workbench"); benchG.transform.position = new Vector3(2.55f, 0f, 0.5f); benchG.transform.SetParent(root);
        Box("BTop",   new Vector3(0f, 0.82f, 0f),     new Vector3(0.55f, 0.045f, 0.80f), dirtyWood, benchG.transform);
        Box("BLeg_FL",new Vector3(-0.22f, 0.41f,-0.36f), new Vector3(0.05f, 0.82f, 0.05f), oldMetal, benchG.transform);
        Box("BLeg_FR",new Vector3(-0.22f, 0.41f, 0.36f), new Vector3(0.05f, 0.82f, 0.05f), oldMetal, benchG.transform);
        Box("BLeg_BL",new Vector3( 0.22f, 0.41f,-0.36f), new Vector3(0.05f, 0.82f, 0.05f), oldMetal, benchG.transform);
        Box("BLeg_BR",new Vector3( 0.22f, 0.41f, 0.36f), new Vector3(0.05f, 0.82f, 0.05f), oldMetal, benchG.transform);
        // Werkzeug auf dem Tisch
        Box("Wrench",  new Vector3( 0.06f, 0.845f,-0.15f), new Vector3(0.04f, 0.010f, 0.28f), rustMetal, benchG.transform, col: false);
        Box("Hammer",  new Vector3(-0.08f, 0.845f, 0.10f), new Vector3(0.035f, 0.010f, 0.22f), oldMetal, benchG.transform, col: false);
        Box("HammerH", new Vector3(-0.08f, 0.855f, 0.21f), new Vector3(0.085f, 0.020f, 0.070f), rustMetal, benchG.transform, col: false);
        var benchBC = benchG.AddComponent<BoxCollider>();
        benchBC.center = new Vector3(0, 0.42f, 0); benchBC.size = new Vector3(0.60f, 0.84f, 0.85f);

        // ── Joshis Thron (zentral, dramatisch beleuchtet) ─────────────────────
        BuildThrone(new Vector3(0f, 0f, -0.5f), root, oldMetal, rustMetal, brightMetal);

        // ── Kleiner Tisch neben Joshi ──────────────────────────────────────────
        BuildSideTable(new Vector3(1.0f, 0f, -0.5f), root, dirtyWood, dirtyWhite, ledAmber, ledAmber);

        // Spot-Licht auf den Thron
        AddLight("ThroneSpot", root, new Vector3(0f, 3.8f, -0.5f), LightType.Spot,
            new Color(1.0f, 0.88f, 0.60f), 3.5f, 6f, 38f, LightShadows.Soft);

        // ── Sicherungskasten (rostig, linke Wand) ─────────────────────────────
        BuildDistributionBox(new Vector3(-2.88f, 1.85f, -1.6f), root, oldMetal, ledGreen, ledAmber);

        // ── Kontrollpanel (rechte Wand, alt) ─────────────────────────────────
        BuildControlPanel(new Vector3(2.88f, 1.85f, -0.8f), root, oldMetal, ledRed, ledGreen, ledAmber, brightMetal);

        // ── Lüftungsgitter Eingang ─────────────────────────────────────────────
        BuildVentGrille(new Vector3(0f, 1.4f, -2.88f), root, grateMat, brightMetal);

        // ── Warnschilder (verblasst) ──────────────────────────────────────────
        BuildWarningSigns(root, oldMetal, ledRed, ledAmber);

        // ── Beleuchtung – einzelne trübe Glühbirne ────────────────────────────
        // Lampenfassung
        Cyl("BulbSocket", new Vector3(0f, 4.6f, 0f), new Vector3(0.06f, 0.08f, 0.06f), oldMetal, root);
        Cyl("BulbGlass",  new Vector3(0f, 4.44f, 0f), new Vector3(0.055f, 0.10f, 0.055f),
            Emit(new Color(0.9f,0.8f,0.5f), new Color(1.0f,0.85f,0.45f), 2.5f), root);
        // Licht der Glühbirne (warmes Gelb, schwache Intensität)
        AddLight("Bulb", root, new Vector3(0f, 4.3f, 0f), LightType.Point,
            new Color(1.0f, 0.78f, 0.42f), 2.8f, 9f, shadows: LightShadows.Soft);
        // Schwaches Füll-Licht für dunkle Ecken
        AddLight("FillDim", root, new Vector3(0f, 3.0f, 0f), LightType.Directional,
            new Color(0.55f, 0.45f, 0.30f), 0.35f, 0f, rotation: Quaternion.Euler(90f, 0f, 0f));

        return border;
    }

    // ─── Workstation ─────────────────────────────────────────────────────────

    private void BuildWorkstation(Vector3 pos, Transform root,
        Material wood, Material steel, Material bright, Material scr1, Material scr2,
        Material ledG, Material ledA)
    {
        var g = new GameObject("Workstation"); g.transform.position = pos; g.transform.SetParent(root);

        // L-Tisch
        Box("Main",   new Vector3(0,      0.74f, 0),      new Vector3(1.20f, 0.055f, 0.80f), wood, g.transform);
        Box("Wing",   new Vector3(-0.60f, 0.74f, 0.44f),  new Vector3(0.58f, 0.055f, 0.58f), wood, g.transform);
        Box("Edge_F", new Vector3(0,      0.705f,-0.37f), new Vector3(1.20f, 0.030f, 0.055f), bright, g.transform, col: false);

        // Beine
        foreach (var (dx, dz) in new[]{(0.52f,-0.35f),(-0.52f,-0.35f),(0.52f,0.35f)})
            Cyl("Leg", new Vector3(dx, 0.37f, dz), new Vector3(0.04f, 0.37f, 0.04f), steel, g.transform);

        // Monitor 1 (groß, blau)
        Box("M1Stand", new Vector3(0.08f, 0.80f, 0.14f), new Vector3(0.04f, 0.28f, 0.04f), steel, g.transform, col: false);
        Box("M1Bezel", new Vector3(0.08f, 1.16f, 0.17f), new Vector3(0.80f, 0.50f, 0.025f), steel, g.transform,
            Quaternion.Euler(7f, 0f, 0f), false);
        Box("M1Screen",new Vector3(0.08f, 1.16f, 0.168f),new Vector3(0.74f, 0.44f, 0.018f), scr1, g.transform,
            Quaternion.Euler(7f, 0f, 0f), false);

        // Monitor 2 (seitlich, grün)
        Box("M2Stand", new Vector3(-0.58f, 0.80f, 0.58f), new Vector3(0.04f, 0.22f, 0.04f), steel, g.transform, col: false);
        Box("M2Bezel", new Vector3(-0.58f, 1.09f, 0.60f), new Vector3(0.58f, 0.38f, 0.022f), steel, g.transform,
            Quaternion.Euler(7f, 22f, 0f), false);
        Box("M2Screen",new Vector3(-0.58f, 1.09f, 0.598f),new Vector3(0.52f, 0.32f, 0.016f), scr2, g.transform,
            Quaternion.Euler(7f, 22f, 0f), false);

        // Kleines drittes Display (stehend)
        Box("M3Bezel", new Vector3(-0.08f, 0.83f, 0.00f), new Vector3(0.22f, 0.30f, 0.022f), steel, g.transform,
            Quaternion.Euler(80f, 0f, 0f), false);
        Box("M3Screen",new Vector3(-0.08f, 0.83f, 0.002f),new Vector3(0.18f, 0.024f, 0.26f), scr1, g.transform,
            Quaternion.Euler(80f, 0f, 0f), false);

        // Tastatur + Maus
        Box("Keyboard", new Vector3(0.08f, 0.763f,-0.06f), new Vector3(0.44f, 0.012f, 0.16f),
            M(new Color(0.09f, 0.09f, 0.10f), 0.3f, 0.25f), g.transform, col: false);
        Box("Mouse",    new Vector3(0.38f, 0.762f,-0.02f), new Vector3(0.065f, 0.010f, 0.10f),
            M(new Color(0.10f, 0.10f, 0.11f), 0.5f, 0.5f), g.transform, col: false);

        // Headset am Monitor-Ständer
        Box("HeadsetBand",new Vector3(0.08f, 1.46f, 0.15f), new Vector3(0.24f, 0.022f, 0.022f),
            M(new Color(0.12f,0.12f,0.13f),0.6f,0.4f), g.transform, col: false);

        // Kaffeebecher
        Cyl("Mug", new Vector3(0.50f, 0.77f,-0.14f), new Vector3(0.050f, 0.055f, 0.050f), whiteMat(0.82f), g.transform);
        Cyl("MugContent", new Vector3(0.50f, 0.814f,-0.14f), new Vector3(0.036f, 0.010f, 0.036f),
            M(new Color(0.14f,0.08f,0.03f), 0f, 0.04f), g.transform);

        // LED unter Tischkante
        Box("DeskLED", new Vector3(0, 0.62f,-0.36f), new Vector3(1.10f, 0.018f, 0.010f), ledA, g.transform, col: false);

        // Status-LEDs Monitore
        for (int i = 0; i < 4; i++)
            Box($"SL_{i}", new Vector3(-0.32f + i*0.22f, 0.948f, 0.128f),
                new Vector3(0.016f, 0.016f, 0.010f), ledG, g.transform, col: false);

        // Monitor-Glows
        AddLight("Glow1", g.transform, new Vector3(0.08f, 1.05f,-0.25f),  LightType.Point, new Color(0.12f,0.38f,1.0f), 1.0f, 2.5f);
        AddLight("Glow2", g.transform, new Vector3(-0.58f, 1.00f, 0.3f),  LightType.Point, new Color(0.08f,0.92f,0.2f), 0.7f, 2.0f);

        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.38f, 0.12f); bc.size = new Vector3(1.25f, 0.80f, 0.90f);
    }

    private Material whiteMat(float brightness) =>
        M(new Color(brightness, brightness * 0.98f, brightness * 0.96f), 0.05f, 0.22f);

    // ─── Serverrack ──────────────────────────────────────────────────────────

    private void BuildServerRack(Vector3 pos, Transform root,
        Material chassis, Material grate, Material ledG, Material ledA, Material ledB, Material ledR)
    {
        var g = new GameObject("ServerRack"); g.transform.position = pos; g.transform.SetParent(root);

        Box("Chassis",    new Vector3(0, 1.05f, 0),     new Vector3(0.52f, 2.10f, 0.52f), chassis, g.transform);
        Box("FrontGrate", new Vector3(0, 1.05f,-0.248f),new Vector3(0.46f, 2.00f, 0.022f), grate,  g.transform, col: false);
        Box("TopVent",    new Vector3(0, 2.07f, 0),     new Vector3(0.48f, 0.055f, 0.48f), grate,  g.transform, col: false);

        Material[] rowLeds = { ledG, ledG, ledA, ledG, ledR, ledG, ledG, ledA, ledG, ledG, ledG, ledG };
        for (int i = 0; i < 12; i++)
        {
            float y = 0.08f + i * 0.16f;
            Box($"Slot_{i}", new Vector3(0, y, -0.246f), new Vector3(0.44f, 0.13f, 0.018f),
                M(new Color(0.09f + (i%2)*0.03f, 0.09f, 0.10f), 0.6f, 0.4f), g.transform, col: false);
            Box($"LED_{i}", new Vector3(0.18f, y, -0.252f), new Vector3(0.016f, 0.016f, 0.008f),
                rowLeds[i], g.transform, col: false);
        }
        AddLight("RackGlow", g.transform, new Vector3(0, 1.1f,-0.5f),
            LightType.Point, new Color(0.06f, 0.95f, 0.18f), 0.45f, 1.4f);

        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 1.05f, 0); bc.size = new Vector3(0.54f, 2.12f, 0.54f);
    }

    // ─── Kontrollpanel ───────────────────────────────────────────────────────

    private void BuildControlPanel(Vector3 pos, Transform root,
        Material panel, Material ledR, Material ledG, Material ledA, Material trim)
    {
        var g = new GameObject("ControlPanel"); g.transform.position = pos; g.transform.SetParent(root);

        Box("Body",  new Vector3(0, 0, 0),       new Vector3(0.80f, 1.10f, 0.12f), panel, g.transform);
        Box("Frame", new Vector3(0, 0, -0.058f), new Vector3(0.86f, 1.16f, 0.014f), trim, g.transform, col: false);

        // Großes Statusdisplay
        Box("Display", new Vector3(0, 0.28f,-0.065f), new Vector3(0.62f, 0.32f, 0.014f),
            Emit(new Color(0.03f,0.07f,0.18f), new Color(0.08f,0.32f,0.92f), 2.2f), g.transform, col: false);

        // 3 kleine Anzeigen
        for (int i = 0; i < 3; i++)
        {
            var sC = (i == 0) ? ledG : (i == 1) ? ledA : ledR;
            Box($"MiniScr_{i}", new Vector3(-0.22f + i*0.22f, -0.08f,-0.065f),
                new Vector3(0.16f, 0.10f, 0.010f), sC, g.transform, col: false);
        }

        // Schalterreihe
        Material[] swLeds = { ledR, ledG, ledG, ledA, ledR, ledG, ledG, ledG };
        for (int i = 0; i < 8; i++)
        {
            float x = -0.31f + i * 0.089f;
            Box($"Sw_{i}", new Vector3(x,-0.22f,-0.070f), new Vector3(0.040f, 0.052f, 0.028f),
                M(new Color(0.14f,0.14f,0.15f),0.5f,0.4f), g.transform, col: false);
            Box($"SwLED_{i}", new Vector3(x,-0.235f,-0.075f), new Vector3(0.015f,0.015f,0.007f),
                swLeds[i], g.transform, col: false);
        }

        // Drehregler
        for (int i = 0; i < 5; i++)
            Cyl($"Knob_{i}", new Vector3(-0.18f+i*0.09f,-0.34f,-0.078f),
                new Vector3(0.036f,0.018f,0.036f), M(new Color(0.11f,0.11f,0.12f),0.7f,0.5f),
                g.transform, Quaternion.Euler(90f,0f,0f));

        // Großer Notfall-Knopf
        Cyl("EmrBtn", new Vector3(0.32f,-0.36f,-0.080f), new Vector3(0.066f,0.026f,0.066f),
            Emit(new Color(0.7f,0.04f,0.04f), new Color(1f,0.04f,0.04f), 2.2f),
            g.transform, Quaternion.Euler(90f,0f,0f));

        // "NOTFALL" Text
        var lbl = new GameObject("EmrLabel"); lbl.transform.SetParent(g.transform);
        lbl.transform.localPosition = new Vector3(0.32f,-0.24f,-0.065f);
        lbl.transform.localRotation = Quaternion.Euler(0f,180f,0f);
        var t = lbl.AddComponent<TextMeshPro>();
        t.text = "NOTFALL"; t.fontSize = 0.55f;
        t.color = new Color(1f,0.18f,0.18f); t.alignment = TextAlignmentOptions.Center;
        lbl.GetComponent<RectTransform>().sizeDelta = new Vector2(0.28f, 0.10f);

        AddLight("PanelGlow", g.transform, new Vector3(0, 0.28f,-0.35f),
            LightType.Point, new Color(0.95f,0.12f,0.06f), 0.38f, 1.3f);
    }

    // ─── Wandbildschirm ───────────────────────────────────────────────────────

    private void BuildWallScreen(Vector3 pos, Transform root, Material scr, Material trim, Material led)
    {
        var g = new GameObject("WallScreen"); g.transform.position = pos; g.transform.SetParent(root);

        Box("Bezel",  new Vector3(0,0,0),      new Vector3(2.20f, 1.10f, 0.040f), trim, g.transform, col: false);
        Box("Screen", new Vector3(0,0,-0.018f),new Vector3(2.10f, 1.00f, 0.022f), scr,  g.transform, col: false);

        // LED-Rahmen
        Box("LedT",  new Vector3(0,  0.56f,-0.022f), new Vector3(2.20f, 0.014f, 0.010f), led, g.transform, col: false);
        Box("LedB",  new Vector3(0, -0.56f,-0.022f), new Vector3(2.20f, 0.014f, 0.010f), led, g.transform, col: false);
        Box("LedL",  new Vector3(-1.12f,0,-0.022f),  new Vector3(0.014f,1.10f, 0.010f), led, g.transform, col: false);
        Box("LedR",  new Vector3( 1.12f,0,-0.022f),  new Vector3(0.014f,1.10f, 0.010f), led, g.transform, col: false);

        // "HEX SYSTEMS - CLASSIFIED" Text
        var lbl = new GameObject("ScreenTitle"); lbl.transform.SetParent(g.transform);
        lbl.transform.localPosition = new Vector3(0f, 0.32f, -0.038f);
        lbl.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var t = lbl.AddComponent<TextMeshPro>();
        t.text = "HEX SYSTEMS  //  CLASSIFIED"; t.fontSize = 0.38f;
        t.color = new Color(0.55f, 0.90f, 1.0f); t.alignment = TextAlignmentOptions.Center;
        lbl.GetComponent<RectTransform>().sizeDelta = new Vector2(2.0f, 0.25f);

        AddLight("ScreenGlow", g.transform, new Vector3(0,0,-0.8f),
            LightType.Point, new Color(0.10f,0.38f,0.95f), 1.2f, 3.0f);
    }

    // ─── Locker ───────────────────────────────────────────────────────────────

    private void BuildLockers(Vector3 pos, Transform root, Material panel, Material trim, Material led)
    {
        var g = new GameObject("Lockers"); g.transform.position = pos; g.transform.SetParent(root);

        for (int i = 0; i < 3; i++)
        {
            float z = i * 0.55f;
            Box($"Door_{i}", new Vector3(0, 1.0f, z), new Vector3(0.08f, 2.0f, 0.48f), panel, g.transform);
            Box($"Handle_{i}", new Vector3(-0.048f, 1.0f, z+0.10f), new Vector3(0.014f, 0.016f, 0.16f),
                trim, g.transform, col: false);
            Box($"LockerLED_{i}", new Vector3(-0.044f, 1.5f, z+0.20f), new Vector3(0.010f, 0.040f, 0.010f),
                led, g.transform, col: false);
            // Namensschildchen
            Box($"NameTag_{i}", new Vector3(-0.043f, 1.72f, z+0.05f), new Vector3(0.008f, 0.10f, 0.14f),
                M(new Color(0.18f,0.19f,0.22f),0.5f,0.4f), g.transform, col: false);
        }
        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 1.0f, 0.55f); bc.size = new Vector3(0.12f, 2.02f, 1.72f);
    }

    // ─── Thron ───────────────────────────────────────────────────────────────

    private void BuildThrone(Vector3 pos, Transform root, Material metal, Material rust, Material bright)
    {
        var g = new GameObject("Throne"); g.transform.position = pos; g.transform.SetParent(root);

        var glow   = Emit(new Color(0.28f,0.08f,0.02f), new Color(1.0f,0.28f,0.04f), 2.2f);
        var dark   = M(new Color(0.08f,0.06f,0.05f), 0.05f, 0.05f);

        // Sitzfläche
        Box("Seat",     new Vector3(0, 0.46f, 0),     new Vector3(0.70f, 0.08f, 0.65f), metal, g.transform);
        Box("SeatPad",  new Vector3(0, 0.505f,0),     new Vector3(0.62f, 0.040f, 0.58f), dark,  g.transform, col: false);

        // Breite massive Rückenlehne
        Box("Back",     new Vector3(0, 1.10f, 0.30f), new Vector3(0.72f, 1.30f, 0.10f), metal, g.transform);
        Box("BackPad",  new Vector3(0, 1.00f, 0.24f), new Vector3(0.60f, 1.00f, 0.04f), dark,  g.transform, col: false);

        // Krone oben (3 Zacken)
        foreach (float sx in new[]{-0.24f, 0f, 0.24f})
            Box($"Spike_{sx}", new Vector3(sx, 1.92f, 0.30f), new Vector3(0.10f, 0.40f, 0.09f), rust, g.transform, col: false);
        // Quer-Verbindung zwischen Zacken
        Box("CrownBar", new Vector3(0, 1.75f, 0.30f), new Vector3(0.68f, 0.07f, 0.09f), bright, g.transform, col: false);

        // Glühende Rune-Streifen (orange)
        Box("Glow_L", new Vector3(-0.34f, 1.10f, 0.245f), new Vector3(0.018f, 1.20f, 0.018f), glow, g.transform, col: false);
        Box("Glow_R", new Vector3( 0.34f, 1.10f, 0.245f), new Vector3(0.018f, 1.20f, 0.018f), glow, g.transform, col: false);
        Box("Glow_T", new Vector3(0, 1.75f, 0.245f),      new Vector3(0.68f, 0.018f, 0.018f), glow, g.transform, col: false);

        // Massive Armstützen
        foreach (float sx in new[]{-0.38f, 0.38f})
        {
            Box($"Arm_{sx}",  new Vector3(sx, 0.66f, 0.06f), new Vector3(0.10f, 0.06f, 0.55f), metal, g.transform);
            Box($"ArmF_{sx}", new Vector3(sx, 0.52f, -0.24f),new Vector3(0.10f, 0.30f, 0.09f), metal, g.transform);
        }

        // Beine (4 massive Blöcke)
        foreach (var (bx,bz) in new[]{(-0.28f,-0.28f),(0.28f,-0.28f),(-0.28f,0.28f),(0.28f,0.28f)})
            Box("Leg", new Vector3(bx, 0.22f, bz), new Vector3(0.12f, 0.44f, 0.12f), rust, g.transform);

        // Ketten-Optik (flache Boxen diagonal)
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            Box($"ChainL_{i}", new Vector3(-0.36f + t*0.06f, 0.46f - t*0.10f, -0.15f - t*0.12f),
                new Vector3(0.022f, 0.022f, 0.10f), bright, g.transform,
                Quaternion.Euler(30f * t, 0f, 15f), false);
            Box($"ChainR_{i}", new Vector3( 0.36f - t*0.06f, 0.46f - t*0.10f, -0.15f - t*0.12f),
                new Vector3(0.022f, 0.022f, 0.10f), bright, g.transform,
                Quaternion.Euler(30f * t, 0f, -15f), false);
        }

        // Sockel-Plattform
        Box("Base",  new Vector3(0, 0.04f, 0.05f), new Vector3(0.90f, 0.08f, 0.85f), rust,  g.transform);
        Box("BaseT", new Vector3(0, 0.085f,0.05f), new Vector3(0.86f, 0.008f,0.82f), bright,g.transform, col: false);

        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.75f, 0.12f); bc.size = new Vector3(0.80f, 1.50f, 0.80f);
    }

    // ─── Sessel ───────────────────────────────────────────────────────────────

    private void BuildErgonomicChair(Vector3 pos, Transform root, Material chassis, Material rubber, Material trim)
    {
        var g = new GameObject("ErgonomicChair"); g.transform.position = pos; g.transform.SetParent(root);
        g.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

        // Sitz
        Box("Seat",    new Vector3(0, 0.46f, 0), new Vector3(0.52f, 0.075f, 0.50f), rubber, g.transform);
        Box("SeatPad", new Vector3(0, 0.503f,0), new Vector3(0.46f, 0.032f, 0.44f),
            M(new Color(0.10f,0.10f,0.11f),0.01f,0.06f), g.transform, col: false);

        // Rückenlehne
        Box("Back",    new Vector3(0, 0.92f, 0.24f), new Vector3(0.48f, 0.80f, 0.07f), rubber, g.transform);
        Box("BackPad", new Vector3(0, 0.92f, 0.20f), new Vector3(0.42f, 0.72f, 0.025f),
            M(new Color(0.11f,0.11f,0.12f),0.01f,0.06f), g.transform, col: false);
        Box("Head",    new Vector3(0, 1.35f, 0.22f), new Vector3(0.28f, 0.24f, 0.07f), rubber, g.transform, col: false);

        // Armstützen
        foreach (float sx in new[]{-0.27f, 0.27f})
        {
            Box($"Arm_{sx}", new Vector3(sx, 0.63f, 0.08f), new Vector3(0.055f, 0.045f, 0.40f), rubber, g.transform, col: false);
            Cyl($"ArmP_{sx}", new Vector3(sx, 0.545f, 0.06f), new Vector3(0.030f, 0.088f, 0.030f), chassis, g.transform);
        }

        // Gasfeder + Fußkreuz
        Cyl("Stem", new Vector3(0, 0.23f, 0), new Vector3(0.055f, 0.23f, 0.055f), chassis, g.transform);
        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var arm = Box($"FA_{i}", new Vector3(d.x*0.20f, 0.030f, d.z*0.20f),
                new Vector3(0.040f, 0.030f, 0.42f), chassis, g.transform, col: false);
            arm.transform.rotation = Quaternion.Euler(0, -i*72f, 0);
            Cyl($"Wh_{i}", new Vector3(d.x*0.38f, 0.020f, d.z*0.38f),
                new Vector3(0.042f, 0.020f, 0.042f), rubber, g.transform);
        }

        // Blauer Akzentstreifen
        Box("Accent", new Vector3(0, 1.29f, 0.197f), new Vector3(0.40f, 0.013f, 0.011f),
            Emit(new Color(0.04f,0.10f,0.28f), new Color(0.12f,0.42f,1.0f), 1.2f), g.transform, col: false);

        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.65f, 0.08f); bc.size = new Vector3(0.60f, 1.32f, 0.72f);
    }

    // ─── Beistelltisch ───────────────────────────────────────────────────────

    private void BuildSideTable(Vector3 pos, Transform root, Material wood, Material white, Material ledB, Material ledG)
    {
        var g = new GameObject("SideTable"); g.transform.position = pos; g.transform.SetParent(root);

        Box("Top", new Vector3(0, 0.65f, 0), new Vector3(0.44f, 0.038f, 0.44f), wood, g.transform);
        foreach (var (dx,dz) in new[]{(0.18f,0.18f),(-0.18f,0.18f),(0.18f,-0.18f),(-0.18f,-0.18f)})
            Cyl("Leg", new Vector3(dx, 0.325f, dz), new Vector3(0.028f, 0.325f, 0.028f),
                M(new Color(0.20f,0.20f,0.22f),0.88f,0.58f), g.transform);

        // Kaffee
        Cyl("Cup",    new Vector3(-0.10f, 0.705f, 0.06f), new Vector3(0.052f, 0.058f, 0.052f), white, g.transform);
        Cyl("Coffee", new Vector3(-0.10f, 0.752f, 0.06f), new Vector3(0.040f, 0.008f, 0.040f),
            M(new Color(0.16f,0.09f,0.03f),0f,0.04f), g.transform);
        // Tablet
        Box("Tablet", new Vector3(0.09f, 0.664f,-0.04f), new Vector3(0.19f, 0.008f, 0.13f),
            M(new Color(0.09f,0.09f,0.10f),0.7f,0.62f), g.transform, col: false);
        Box("TabScr",  new Vector3(0.09f, 0.670f,-0.04f), new Vector3(0.16f, 0.006f, 0.10f),
            ledG, g.transform, col: false);
        // Kleiner Energydrink
        Cyl("Drink",  new Vector3(0.14f, 0.705f, 0.12f), new Vector3(0.032f, 0.065f, 0.032f),
            Emit(new Color(0.05f,0.28f,0.04f), new Color(0.08f,1.00f,0.12f), 0.8f), g.transform);
        // LED Tischunterkante
        Box("TableLED", new Vector3(0, 0.620f, 0), new Vector3(0.42f, 0.010f, 0.42f), ledB, g.transform, col: false);
    }

    // ─── Lüftungsgitter ───────────────────────────────────────────────────────

    private void BuildVentGrille(Vector3 pos, Transform root, Material grate, Material trim)
    {
        var g = new GameObject("VentGrille"); g.transform.position = pos; g.transform.SetParent(root);
        Box("Frame", new Vector3(0,0,0), new Vector3(2.0f, 0.90f, 0.060f), trim, g.transform, col: false);
        for (int i = 0; i < 6; i++)
            Box($"H_{i}", new Vector3(0, -0.36f + i*0.145f, -0.012f), new Vector3(1.88f, 0.024f, 0.042f), grate, g.transform, col: false);
        for (int i = 0; i < 10; i++)
            Box($"V_{i}", new Vector3(-0.85f + i*0.19f, 0, -0.012f), new Vector3(0.024f, 0.82f, 0.042f), grate, g.transform, col: false);
    }

    // ─── Verteilerkasten ──────────────────────────────────────────────────────

    private void BuildDistributionBox(Vector3 pos, Transform root, Material panel, Material ledG, Material ledA)
    {
        var g = new GameObject("DistributionBox"); g.transform.position = pos; g.transform.SetParent(root);
        Box("Body", new Vector3(0,0,0),       new Vector3(0.52f,0.68f,0.08f), panel, g.transform);
        Box("Door", new Vector3(0,0,-0.048f), new Vector3(0.48f,0.64f,0.015f),
            M(new Color(0.17f,0.18f,0.21f),0.7f,0.5f), g.transform, col: false);
        for (int i = 0; i < 10; i++)
        {
            float x = -0.18f + (i%5) * 0.09f;
            float y = (i < 5) ? 0.12f : -0.10f;
            Box($"F_{i}", new Vector3(x,y,-0.060f), new Vector3(0.06f,0.11f,0.012f),
                M(new Color(0.14f,0.14f,0.15f),0.4f,0.3f), g.transform, col: false);
            Box($"FL_{i}", new Vector3(x,y+0.065f,-0.065f), new Vector3(0.013f,0.013f,0.006f),
                i==4 ? ledA : ledG, g.transform, col: false);
        }
        var bc = g.AddComponent<BoxCollider>();
        bc.center = Vector3.zero; bc.size = new Vector3(0.54f,0.70f,0.10f);
    }

    // ─── Rohrsystem ───────────────────────────────────────────────────────────

    private void BuildPipes(Transform root, Material pipeMat)
    {
        // Horizontale Rohre oben links
        Box("PipeH1", new Vector3(-1.4f, 4.60f, 2.7f), new Vector3(3.0f, 0.08f, 0.08f), pipeMat, root, col: false);
        Box("PipeH2", new Vector3(-2.7f, 3.8f, -0.5f), new Vector3(0.08f, 0.08f, 3.2f), pipeMat, root, col: false);
        Box("PipeH3", new Vector3(0.5f, 4.55f, 2.7f),  new Vector3(2.5f, 0.06f, 0.06f), pipeMat, root, col: false);
        // Vertikale Rohre
        Box("PipeV1", new Vector3(-2.75f, 2.2f,  2.7f), new Vector3(0.08f, 3.2f, 0.08f), pipeMat, root, col: false);
        Box("PipeV2", new Vector3( 2.75f, 2.5f, -0.5f), new Vector3(0.06f, 2.8f, 0.06f), pipeMat, root, col: false);
        // Knöchel / Verbindungen
        Box("PipeKnee1", new Vector3(-2.75f, 4.60f, 2.7f),  new Vector3(0.12f, 0.12f, 0.12f), pipeMat, root, col: false);
        Box("PipeKnee2", new Vector3(-2.70f, 3.8f, -2.75f), new Vector3(0.10f, 0.10f, 0.10f), pipeMat, root, col: false);
    }

    // ─── Warnschilder ─────────────────────────────────────────────────────────

    private void BuildWarningSigns(Transform root, Material signBg, Material ledR, Material ledA)
    {
        // "RESTRICTED ACCESS" – links oben
        var s1 = new GameObject("SignRestricted"); s1.transform.SetParent(root);
        s1.transform.position = new Vector3(-2.87f, 3.2f, -0.8f);
        s1.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        Box("Bg", new Vector3(0,0,0.01f), new Vector3(0.55f, 0.20f, 0.025f), signBg, s1.transform, col: false);
        var t1o = new GameObject("Text"); t1o.transform.SetParent(s1.transform);
        t1o.transform.localPosition = new Vector3(0, 0, -0.015f);
        var t1 = t1o.AddComponent<TextMeshPro>();
        t1.text = "!! RESTRICTED ACCESS"; t1.fontSize = 0.28f;
        t1.color = new Color(1f, 0.62f, 0.04f); t1.alignment = TextAlignmentOptions.Center;
        t1o.GetComponent<RectTransform>().sizeDelta = new Vector2(0.50f, 0.18f);
        Box("LEDs", new Vector3(0, 0.12f, 0.005f), new Vector3(0.52f, 0.014f, 0.008f), ledA, s1.transform, col: false);

        // "HEX-7 MAINTENANCE" – Rückwand rechts oben
        var s2 = new GameObject("SignMaintenance"); s2.transform.SetParent(root);
        s2.transform.position = new Vector3(1.6f, 4.1f, 2.87f);
        s2.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        Box("Bg", new Vector3(0,0,0.01f), new Vector3(0.80f, 0.18f, 0.020f), signBg, s2.transform, col: false);
        var t2o = new GameObject("Text"); t2o.transform.SetParent(s2.transform);
        t2o.transform.localPosition = new Vector3(0, 0, -0.012f);
        var t2 = t2o.AddComponent<TextMeshPro>();
        t2.text = "HEX-7  //  MAINTENANCE SECTOR"; t2.fontSize = 0.22f;
        t2.color = new Color(0.50f, 0.88f, 1.0f); t2.alignment = TextAlignmentOptions.Center;
        t2o.GetComponent<RectTransform>().sizeDelta = new Vector2(0.78f, 0.16f);
    }

    // ─── Deckenlampe (gestalterisch) ──────────────────────────────────────────

    private void BuildCeilingLight(Vector3 pos, Transform root, Material chassis, Material led)
    {
        var g = new GameObject("CeilingLightRig"); g.transform.position = pos; g.transform.SetParent(root);

        // Schiene
        Box("Rail", new Vector3(0,0,0), new Vector3(1.80f, 0.055f, 0.055f), chassis, g.transform, col: false);
        // 3 Pendelleuchten
        for (int i = -1; i <= 1; i++)
        {
            float x = i * 0.56f;
            Box($"Cord_{i}", new Vector3(x, -0.25f, 0), new Vector3(0.012f, 0.50f, 0.012f), chassis, g.transform, col: false);
            Cyl($"Shade_{i}", new Vector3(x, -0.56f, 0), new Vector3(0.18f, 0.10f, 0.18f), chassis, g.transform);
            Cyl($"Bulb_{i}",  new Vector3(x, -0.54f, 0), new Vector3(0.08f, 0.08f, 0.08f),
                Emit(new Color(0.85f,0.92f,1.0f), new Color(0.65f,0.82f,1.0f), 3.0f), g.transform);
            AddLight($"PL_{i}", g.transform, new Vector3(x, -0.62f, 0),
                LightType.Point, new Color(0.72f, 0.84f, 1.0f), 1.8f, 4.5f, shadows: LightShadows.Soft);
        }
    }

    // ─── Lichter ──────────────────────────────────────────────────────────────

    private void AddLights(Transform root)
    {
        AddLight("CeilMain",   root, new Vector3(0,    4.9f,  0.5f), LightType.Point,       new Color(0.72f,0.82f,1.00f), 4.0f, 12f, shadows: LightShadows.Soft);
        AddLight("FillAmber",  root, new Vector3(-1.5f,3.8f, -1.2f), LightType.Point,       new Color(1.00f,0.72f,0.30f), 0.9f, 5f);
        AddLight("WorkSpot",   root, new Vector3(1.6f, 3.2f,  1.8f), LightType.Spot,        new Color(0.58f,0.80f,1.00f), 2.0f, 5f, 52f, LightShadows.Soft);
        AddLight("JoshiSpot",  root, new Vector3(-0.6f,3.0f,  0.1f), LightType.Spot,        new Color(0.80f,0.76f,0.65f), 1.5f, 4f, 45f, LightShadows.Soft);
        AddLight("RackGlow",   root, new Vector3(-2.4f,2.5f,  0.0f), LightType.Point,       new Color(0.08f,1.00f,0.20f), 0.5f, 2.5f);
        AddLight("PanelGlow",  root, new Vector3(2.4f, 2.5f, -0.8f), LightType.Point,       new Color(1.00f,0.16f,0.08f), 0.38f,2.0f);
        AddLight("ScreenGlow", root, new Vector3(0,    2.5f,  2.0f), LightType.Point,       new Color(0.10f,0.38f,0.95f), 1.0f, 3.0f);
        AddLight("AmbBlue1",   root, new Vector3(0,    0.5f,  2.6f), LightType.Point,       new Color(0.14f,0.38f,1.00f), 0.40f,3.5f);
        AddLight("AmbBlue2",   root, new Vector3(-2.6f,0.5f,  0.0f), LightType.Point,       new Color(0.14f,0.38f,1.00f), 0.32f,3.0f);
        AddLight("AmbAmber",   root, new Vector3(0,    0.5f, -2.6f), LightType.Point,       new Color(1.00f,0.60f,0.06f), 0.32f,3.0f);
        AddLight("FillDir",    root, new Vector3(0,    4.0f,  0.0f), LightType.Directional, new Color(0.55f,0.65f,0.90f), 1.2f, 0f, rotation: Quaternion.Euler(90f, 0f, 0f));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Spieler
    // ═══════════════════════════════════════════════════════════════════════

    private GameObject AddPlayer(Scene scene)
    {
        GameObject idleModel    = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Big Yahu standing.fbx");
        GameObject runningModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Big Yahu jogging.fbx");
        Material   playerMat    = AssetDatabase.LoadAssetAtPath<Material>("Assets/Big Yahu/Big Yahu material.mat");

        var character = new GameObject("BigYahu") { tag = "Player" };
        character.transform.position = new Vector3(0f, 0f, -1.5f);

        if (idleModel != null && runningModel != null)
        {
            var idle = (GameObject)PrefabUtility.InstantiatePrefab(idleModel);
            PrefabUtility.UnpackPrefabInstance(idle, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            idle.name = "IdleModel"; idle.transform.SetParent(character.transform, false); idle.SetActive(true);
            try { SetupLoopController(idle, "Assets/Big Yahu/Big Yahu standing.fbx",
                "Assets/Big Yahu/BigYahu_Stand_Loop.anim", "Assets/Big Yahu/BigYahu_Stand.controller", "Stand"); }
            catch (System.Exception e) { Debug.LogWarning("Stand-Anim: " + e.Message); }

            var run = (GameObject)PrefabUtility.InstantiatePrefab(runningModel);
            PrefabUtility.UnpackPrefabInstance(run, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            run.name = "RunningModel"; run.transform.SetParent(character.transform, false); run.SetActive(false);
            try { SetupLoopController(run, "Assets/Big Yahu/Big Yahu jogging.fbx",
                "Assets/Big Yahu/BigYahu_Run_Loop.anim", "Assets/Big Yahu/BigYahu_Run.controller", "Run"); }
            catch (System.Exception e) { Debug.LogWarning("Run-Anim: " + e.Message); }

            if (playerMat != null)
                foreach (var r in character.GetComponentsInChildren<Renderer>(true))
                    r.material = playerMat;
        }
        else
        {
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            cap.transform.SetParent(character.transform, false);
            cap.transform.localPosition = new Vector3(0, 1f, 0);
        }

        var col = character.AddComponent<CapsuleCollider>();
        col.height = 1.8f; col.radius = 0.3f; col.center = new Vector3(0, 0.9f, 0);

        var rb = character.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezePositionY
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;

        character.AddComponent<CharacterAnimator>();
        character.AddComponent<PlayerController>();
        SceneManager.MoveGameObjectToScene(character, scene);
        return character;
    }

    private void SetupLoopController(GameObject instance, string fbxPath, string clipPath,
                                     string ctrlPath, string stateName)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        AnimationClip src = null;
        foreach (var a in assets)
            if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { src = c; break; }
        if (src == null) return;

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);
        var loop = Object.Instantiate(src); loop.name = stateName + "_Loop";
        var s = AnimationUtility.GetAnimationClipSettings(loop);
        s.loopTime = true; AnimationUtility.SetAnimationClipSettings(loop, s);
        AssetDatabase.CreateAsset(loop, clipPath);

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null)
            AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm   = ctrl.layers[0].stateMachine;
        var st   = sm.AddState(stateName); st.motion = loop; sm.defaultState = st;
        AssetDatabase.SaveAssets();

        var anim = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Joshi NPC
    // ═══════════════════════════════════════════════════════════════════════

    private void AddJoshi(Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Sitting Talking(1).fbx");
        if (prefab == null)
        {
            Debug.LogWarning("[Level2] 'Sitting Talking(1).fbx' nicht gefunden.");
            return;
        }

        // Normal-Map korrekt als NormalMap importieren (damit Unity sie als Bump-Map erkennt)
        EnsureNormalMapImport("Assets/Big Yahu/3dcartooncharactermodel_Clone1_normal.JPEG");

        GameObject joshi = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // Prefab-Verbindung kappen, sonst verwirft Unity Material-Overrides beim Speichern
        PrefabUtility.UnpackPrefabInstance(joshi, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        joshi.name = "Joshi";

        // Skalierung prüfen (manche FBX kommen in cm → 100x zu groß)
        var bounds = GetBounds(joshi);
        float h = bounds.size.y;
        // Sitzende Pose: Ziel ~1.15 m Höhe
        float scale = (h > 10f) ? 0.01f : (h < 0.3f) ? (1.15f / Mathf.Max(h, 0.01f)) : 1f;

        joshi.transform.localScale = Vector3.one * scale;
        // Zentral auf dem Thron, schaut direkt zur Kamera (-Z)
        joshi.transform.position = new Vector3(0f, 0.20f, -0.5f);
        joshi.transform.rotation = Quaternion.Euler(-10f, 180f, 0f);

        // PBR-Material zuweisen
        var mat = CreateJoshiMaterial();
        if (mat != null)
            foreach (var r in joshi.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = mat;

        // Sitz-Animation
        SetupSittingAnimation(joshi);

        SceneManager.MoveGameObjectToScene(joshi, scene);
        Debug.Log("[Level2] Joshi platziert und Material zugewiesen.");
    }

    private void EnsureNormalMapImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }

    private Bounds GetBounds(GameObject go)
    {
        var rens = go.GetComponentsInChildren<Renderer>();
        if (rens.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        var b = rens[0].bounds;
        foreach (var r in rens) b.Encapsulate(r.bounds);
        return b;
    }

    private Material CreateJoshiMaterial()
    {
        // Texturen – zuerst die neuen freien PNGs probieren, dann alte JPEG-Namen als Fallback
        var baseColor = LoadTex("Assets/Big Yahu/tripo_material_95577146-ea71-4ddc-8bb5-51ef3aca067e.png")
                     ?? LoadTex("Assets/Big Yahu/3dcartooncharactermodel_Clone1_basecolor.JPEG");
        var normalTex = LoadTex("Assets/Big Yahu/normalMap1.png")
                     ?? LoadTex("Assets/Big Yahu/3dcartooncharactermodel_Clone1_normal.JPEG");
        var metalTex  = LoadTex("Assets/Big Yahu/metalnessMap1.png")
                     ?? LoadTex("Assets/Big Yahu/3dcartooncharactermodel_Clone1_metallic.JPEG");

        if (baseColor == null)
        {
            Debug.LogWarning("[Level2] Keine Basis-Textur für Joshi gefunden – kein Material erstellt.");
            return null;
        }

        // Normal Map korrekt importieren
        if (normalTex != null)
            EnsureNormalMapImport(AssetDatabase.GetAssetPath(normalTex));

        var mat = new Material(Shader.Find("Standard"));
        mat.name = "Joshi_PBR";
        mat.SetTexture("_MainTex", baseColor);
        mat.SetColor("_Color", Color.white);

        if (normalTex != null)
        {
            mat.EnableKeyword("_NORMALMAP");
            // Textur nach Reimport neu laden damit Unity den korrekten Typ kennt
            normalTex = LoadTex(AssetDatabase.GetAssetPath(normalTex));
            mat.SetTexture("_BumpMap", normalTex);
            mat.SetFloat("_BumpScale", 1f);
        }

        if (metalTex != null)
        {
            mat.EnableKeyword("_METALLICGLOSSMAP");
            mat.SetTexture("_MetallicGlossMap", metalTex);
        }
        else
        {
            mat.SetFloat("_Metallic", 0.05f);
        }

        mat.SetFloat("_Glossiness", 0.40f);

        // Material auf Disk speichern
        const string matPath = "Assets/Big Yahu/Joshi_PBR.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
            AssetDatabase.DeleteAsset(matPath);
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Material>(matPath);
    }

    private Texture2D LoadTex(string path) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    private void SetupSittingAnimation(GameObject joshi)
    {
        const string fbxPath  = "Assets/Big Yahu/Sitting Talking(1).fbx";
        const string clipPath = "Assets/Big Yahu/Joshi_Sitting_Loop.anim";
        const string ctrlPath = "Assets/Big Yahu/Joshi_Sitting.controller";

        // ── FBX auf Legacy umstellen – zuverlässigste Methode ohne Avatar-Setup ─
        var modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (modelImporter != null && modelImporter.animationType != ModelImporterAnimationType.Legacy)
        {
            modelImporter.animationType = ModelImporterAnimationType.Legacy;
            modelImporter.SaveAndReimport();
        }

        // ── Clip aus FBX holen ─────────────────────────────────────────────────
        AnimationClip src = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { src = c; break; }

        if (src == null)
        {
            Debug.LogWarning("[Level2] Kein AnimationClip in 'Sitting Talking(1).fbx'.");
            return;
        }

        // ── Loop-Clip auf Disk speichern ───────────────────────────────────────
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);

        var loop = Object.Instantiate(src);
        loop.name = "Joshi_Sitting_Loop";
        loop.legacy = true;
        var cfg = AnimationUtility.GetAnimationClipSettings(loop);
        cfg.loopTime  = true;
        cfg.loopBlend = true;
        AnimationUtility.SetAnimationClipSettings(loop, cfg);
        AssetDatabase.CreateAsset(loop, clipPath);
        AssetDatabase.SaveAssets();
        loop = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

        // ── Animation-Komponente (Legacy) zuweisen ─────────────────────────────
        // Legacy Animation braucht keinen Avatar und läuft garantiert auf jedem Rig
        var legacyAnim = joshi.GetComponent<Animation>() ?? joshi.AddComponent<Animation>();
        legacyAnim.AddClip(loop, "Sitting");
        legacyAnim.clip = loop;
        legacyAnim.playAutomatically = true;
        legacyAnim.wrapMode = WrapMode.Loop;
        legacyAnim.Play("Sitting");

        // Vorhandenen Animator-Controller entfernen um Konflikte zu vermeiden
        var existingAnimator = joshi.GetComponentInChildren<Animator>(true);
        if (existingAnimator != null)
            existingAnimator.runtimeAnimatorController = null;

        // Controller-Asset aufräumen falls vorhanden
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null)
            AssetDatabase.DeleteAsset(ctrlPath);

        Debug.Log($"[Level2] Joshi Legacy-Animation: '{loop.name}' → Loop auf {joshi.name}");
    }

    private void AddBackgroundMusic(Scene scene)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Big Yahu/Untitled.mp3");
        if (clip == null) { Debug.LogWarning("[Music] Untitled.mp3 nicht gefunden."); return; }

        var go = new GameObject("BackgroundMusic");
        var src = go.AddComponent<AudioSource>();
        src.clip         = clip;
        src.loop         = true;
        src.playOnAwake  = true;
        src.volume       = 0.6f;
        src.spatialBlend = 0f;
        go.AddComponent<BackgroundMusic>();
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3D Staubfleck an der Wand
    // ═══════════════════════════════════════════════════════════════════════

    private GameObject BuildDustyWallSpot(Transform root, Scene scene)
    {
        var spotMat = M(new Color(0.58f, 0.50f, 0.32f), 0f, 0.04f);   // staubig-gelblich

        var spotGO = new GameObject("DustyWallSpot");
        spotGO.transform.SetParent(root);

        // Sichtbare Schmutzfläche – leicht hervorstehend von der Rückwand
        var patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        patch.name = "DustPatch";
        patch.transform.SetParent(spotGO.transform);
        patch.transform.position   = new Vector3(-1.2f, 1.35f, 2.89f);
        patch.transform.localScale = new Vector3(0.90f, 0.65f, 0.02f);
        patch.GetComponent<Renderer>().material = spotMat;
        Object.DestroyImmediate(patch.GetComponent<Collider>());

        // Trigger-Zone (unsichtbar, etwas tiefer in den Raum)
        var triggerGO = new GameObject("Trigger");
        triggerGO.transform.SetParent(spotGO.transform);
        triggerGO.transform.position = new Vector3(-1.2f, 1.35f, 2.4f);
        var bc = triggerGO.AddComponent<BoxCollider>();
        bc.size      = new Vector3(1.2f, 1.2f, 1.0f);
        bc.isTrigger = true;
        triggerGO.AddComponent<DustyWallSpot>();

        // Kleiner Pfeil-Hinweis auf dem Staubfleck (Rätsel-Spur)
        var markGO = new GameObject("DustMark");
        markGO.transform.SetParent(spotGO.transform);
        markGO.transform.position = new Vector3(-1.2f, 1.35f, 2.878f);
        markGO.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        var markText = markGO.AddComponent<TextMeshPro>();
        markText.text      = "?";
        markText.fontSize  = 1.4f;
        markText.color     = new Color(0.40f, 0.34f, 0.18f);   // kaum sichtbar
        markText.alignment = TextAlignmentOptions.Center;
        markGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0.9f, 0.7f);

        return triggerGO;   // DustyWallSpot-Komponente liegt hier drauf
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3D Schloss am Eingang
    // ═══════════════════════════════════════════════════════════════════════

    private GameObject BuildLockSpot(Transform root)
    {
        var lockMat  = M(new Color(0.15f, 0.13f, 0.10f), 0.6f, 0.3f);   // dunkles Metall
        var shackMat = M(new Color(0.28f, 0.25f, 0.18f), 0.7f, 0.4f);

        var lockGO = new GameObject("LockObject");
        lockGO.transform.SetParent(root);

        // Schloss-Koerper (kleiner Quader am rechten Tuerrahmen)
        var body = Box("LockBody", new Vector3(0.75f, 1.05f, -2.88f),
            new Vector3(0.14f, 0.11f, 0.06f), lockMat, lockGO.transform, col: false);
        // Bügel oben
        Cyl("LockShackle", new Vector3(0.75f, 1.17f, -2.88f),
            new Vector3(0.018f, 0.06f, 0.018f), shackMat, lockGO.transform);

        // Trigger-Zone (Spieler muss nah ran)
        var trigGO = new GameObject("LockTrigger");
        trigGO.transform.SetParent(lockGO.transform);
        trigGO.transform.position = new Vector3(0.75f, 1.05f, -2.5f);
        var bc2 = trigGO.AddComponent<BoxCollider>();
        bc2.size      = new Vector3(1.4f, 1.8f, 1.0f);
        bc2.isTrigger = true;
        trigGO.AddComponent<DustyWallSpot>();

        return trigGO;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UI – Canvas, DialogSystem, DustWallPanel, ArrowPanel
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildUI(Scene scene, GameObject wallSpotGO, GameObject lockSpotGO, GameObject entranceBorderGO)
    {
        // EventSystem (benötigt für Button-Klicks)
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();
        SceneManager.MoveGameObjectToScene(esGO, scene);

        // Root Canvas
        var canvasGO = new GameObject("UICanvas");
        SceneManager.MoveGameObjectToScene(canvasGO, scene);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Dialog Panel (unterer Streifen – größer für bessere Sichtbarkeit) ─
        var dialogPanelGO = UiPanel("DialogPanel", canvasGO.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, 260f),
            new Vector2(0.5f, 0f), new Color(0.03f, 0.03f, 0.06f, 0.97f));
        dialogPanelGO.SetActive(false);

        // Obere Trennlinie (damit Panel sich vom 3D-Bild absetzt)
        var borderGO = UiImage("TopBorder", dialogPanelGO.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            Vector2.zero, new Vector2(0f, 3f),
            new Color(0.55f, 0.45f, 0.20f));

        // Portrait links
        var portraitGO = UiImage("Portrait", dialogPanelGO.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(110f, 0f), new Vector2(190f, 190f),
            new Color(0.30f, 0.28f, 0.38f, 1f));

        // Speaker-Label
        var speakerGO  = new GameObject("SpeakerLabel");
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

        // Dialog-Text (größer)
        var dialogTextGO  = new GameObject("DialogText");
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

        // Weiter-Button (rechts unten)
        var continueBtnGO = UiButton("ContinueButton", dialogPanelGO.transform,
            new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-14f, 14f), new Vector2(130f, 42f),
            new Vector2(1f, 0f), new Color(0.12f, 0.52f, 0.22f), "Weiter ▶");

        // ── Interaktions-Prompt ("[E] Untersuchen") ──────────────────────────
        var promptGO = UiPanel("InteractionPrompt", canvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 80f), new Vector2(340f, 60f),
            new Vector2(0.5f, 0f), new Color(0.0f, 0.0f, 0.0f, 0.72f));
        promptGO.SetActive(false);
        var promptTextGO  = new GameObject("PromptText");
        promptTextGO.transform.SetParent(promptGO.transform, false);
        var promptTMP = promptTextGO.AddComponent<TextMeshProUGUI>();
        promptTMP.text      = "<color=#FFD966>[E]</color>  Untersuchen";
        promptTMP.fontSize  = 24f;
        promptTMP.alignment = TextAlignmentOptions.Center;
        promptTMP.color     = Color.white;
        var promptTxtRT = promptTextGO.GetComponent<RectTransform>();
        promptTxtRT.anchorMin = Vector2.zero;
        promptTxtRT.anchorMax = Vector2.one;
        promptTxtRT.offsetMin = Vector2.zero;
        promptTxtRT.offsetMax = Vector2.zero;

        // ── Dust Wall Panel ──────────────────────────────────────────────────
        var dustPanelGO = UiPanel("DustWallPanel", canvasGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Vector2(0.5f, 0.5f), new Color(0.03f, 0.02f, 0.04f, 0.95f));
        dustPanelGO.SetActive(false);

        // Wand-Hintergrund (hinter Staub, zeigt versteckten Inhalt wenn freigelegt)
        var wallBgGO = UiImage("WallBackground", dustPanelGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(700f, 500f),
            new Color(0.40f, 0.33f, 0.20f));

        // Pfeil-Text auf der Wand (wird sichtbar wenn Staub entfernt)
        var wallArrowGO  = new GameObject("WallArrowText");
        wallArrowGO.transform.SetParent(wallBgGO.transform, false);
        var wallArrowTMP = wallArrowGO.AddComponent<TextMeshProUGUI>();
        wallArrowTMP.text      = "↑  ↑  ↓  ↓";
        wallArrowTMP.fontSize  = 90f;
        wallArrowTMP.alignment = TextAlignmentOptions.Center;
        wallArrowTMP.color     = new Color(0.9f, 0.85f, 0.3f);
        var wallArrowRT = wallArrowGO.GetComponent<RectTransform>();
        wallArrowRT.anchorMin = Vector2.zero;
        wallArrowRT.anchorMax = Vector2.one;
        wallArrowRT.offsetMin = Vector2.zero;
        wallArrowRT.offsetMax = Vector2.zero;

        // Staub-Overlay (RawImage – Scratch-Textur, liegt über dem Wand-BG)
        var dustOverlayGO  = new GameObject("DustOverlay");
        dustOverlayGO.transform.SetParent(dustPanelGO.transform, false);
        var dustOverlayImg = dustOverlayGO.AddComponent<RawImage>();
        dustOverlayImg.color = Color.white;
        var dustRT = dustOverlayGO.GetComponent<RectTransform>();
        dustRT.anchorMin        = new Vector2(0.5f, 0.5f);
        dustRT.anchorMax        = new Vector2(0.5f, 0.5f);
        dustRT.anchoredPosition = Vector2.zero;
        dustRT.sizeDelta        = new Vector2(700f, 500f);

        // Anleitung oben
        var dustInstructGO  = new GameObject("InstructionText");
        dustInstructGO.transform.SetParent(dustPanelGO.transform, false);
        var dustInstructTMP = dustInstructGO.AddComponent<TextMeshProUGUI>();
        dustInstructTMP.text      = "Reib den Staub mit der Maus weg!";
        dustInstructTMP.fontSize  = 26f;
        dustInstructTMP.alignment = TextAlignmentOptions.Center;
        dustInstructTMP.color     = new Color(1f, 0.9f, 0.7f);
        var dustInstructRT = dustInstructGO.GetComponent<RectTransform>();
        dustInstructRT.anchorMin        = new Vector2(0f, 1f);
        dustInstructRT.anchorMax        = new Vector2(1f, 1f);
        dustInstructRT.pivot            = new Vector2(0.5f, 1f);
        dustInstructRT.anchoredPosition = new Vector2(0f, -32f);
        dustInstructRT.sizeDelta        = new Vector2(0f, 50f);

        // ── Lock Interaction Prompt ──────────────────────────────────────────
        var lockPromptGO = UiPanel("LockInteractionPrompt", canvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 80f), new Vector2(340f, 60f),
            new Vector2(0.5f, 0f), new Color(0.0f, 0.0f, 0.0f, 0.72f));
        lockPromptGO.SetActive(false);
        var lockPromptTextGO  = new GameObject("Text");
        lockPromptTextGO.transform.SetParent(lockPromptGO.transform, false);
        var lockPromptTMP = lockPromptTextGO.AddComponent<TextMeshProUGUI>();
        lockPromptTMP.text      = "[E] Schloss untersuchen";
        lockPromptTMP.fontSize  = 22f;
        lockPromptTMP.alignment = TextAlignmentOptions.Center;
        lockPromptTMP.color     = Color.white;
        var lockPromptRT = lockPromptTextGO.GetComponent<RectTransform>();
        lockPromptRT.anchorMin = Vector2.zero;
        lockPromptRT.anchorMax = Vector2.one;
        lockPromptRT.offsetMin = new Vector2(8f, 4f);
        lockPromptRT.offsetMax = new Vector2(-8f, -4f);

        // ── Arrow Panel (kompaktes Overlay unten) ────────────────────────────
        var arrowPanelGO = UiPanel("ArrowPanel", canvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 20f), new Vector2(620f, 210f),
            new Vector2(0.5f, 0f), new Color(0.03f, 0.02f, 0.04f, 0.93f));
        arrowPanelGO.SetActive(false);

        // Anleitung oben im Panel
        var arrowSubGO  = new GameObject("ArrowSubtitle");
        arrowSubGO.transform.SetParent(arrowPanelGO.transform, false);
        var arrowSubTMP = arrowSubGO.AddComponent<TextMeshProUGUI>();
        arrowSubTMP.text      = "Sequenz eingeben:";
        arrowSubTMP.fontSize  = 22f;
        arrowSubTMP.alignment = TextAlignmentOptions.Center;
        arrowSubTMP.color     = new Color(0.78f, 0.78f, 0.78f);
        var arrowSubRT = arrowSubGO.GetComponent<RectTransform>();
        arrowSubRT.anchorMin        = new Vector2(0f, 1f);
        arrowSubRT.anchorMax        = new Vector2(1f, 1f);
        arrowSubRT.pivot            = new Vector2(0.5f, 1f);
        arrowSubRT.anchoredPosition = new Vector2(0f, -14f);
        arrowSubRT.sizeDelta        = new Vector2(0f, 36f);

        // Pfeil-Hinweis (Combo-Anzeige, dummy – wird via ArrowHintText nicht genutzt)
        var arrowHintGO  = new GameObject("ArrowHintText");
        arrowHintGO.transform.SetParent(arrowPanelGO.transform, false);
        var arrowHintTMP = arrowHintGO.AddComponent<TextMeshProUGUI>();
        arrowHintTMP.text      = "";
        arrowHintTMP.fontSize  = 28f;
        arrowHintTMP.alignment = TextAlignmentOptions.Center;
        arrowHintTMP.color     = new Color(1f, 0.85f, 0.2f);
        var arrowHintRT = arrowHintGO.GetComponent<RectTransform>();
        arrowHintRT.anchorMin        = new Vector2(0.5f, 0.5f);
        arrowHintRT.anchorMax        = new Vector2(0.5f, 0.5f);
        arrowHintRT.anchoredPosition = new Vector2(0f, 20f);
        arrowHintRT.sizeDelta        = new Vector2(580f, 50f);

        // Eingabe-Feedback (zeigt Fortschritt farbig an)
        var feedbackGO  = new GameObject("InputFeedback");
        feedbackGO.transform.SetParent(arrowPanelGO.transform, false);
        var feedbackTMP = feedbackGO.AddComponent<TextMeshProUGUI>();
        feedbackTMP.text      = "<color=#555555>hoch  hoch  runter  runter</color>";
        feedbackTMP.fontSize  = 46f;
        feedbackTMP.alignment = TextAlignmentOptions.Center;
        feedbackTMP.color     = Color.white;
        var feedbackRT = feedbackGO.GetComponent<RectTransform>();
        feedbackRT.anchorMin        = new Vector2(0.5f, 0f);
        feedbackRT.anchorMax        = new Vector2(0.5f, 0f);
        feedbackRT.pivot            = new Vector2(0.5f, 0f);
        feedbackRT.anchoredPosition = new Vector2(0f, 18f);
        feedbackRT.sizeDelta        = new Vector2(580f, 80f);

        // ── BigYahuDialogSystem ──────────────────────────────────────────────
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

        // ── Level2_DustWall ──────────────────────────────────────────────────
        var dwGO = new GameObject("Level2_DustWall");
        SceneManager.MoveGameObjectToScene(dwGO, scene);
        var dw   = dwGO.AddComponent<Level2_DustWall>();
        dw.enabled = true;
        var dwso = new SerializedObject(dw);
        dwso.FindProperty("dustWallPanel").objectReferenceValue         = dustPanelGO;
        dwso.FindProperty("arrowPanel").objectReferenceValue            = arrowPanelGO;
        dwso.FindProperty("interactionPrompt").objectReferenceValue     = promptGO;
        dwso.FindProperty("dustyWallSpot").objectReferenceValue         =
            wallSpotGO != null ? wallSpotGO.GetComponent<DustyWallSpot>() : null;
        dwso.FindProperty("lockSpot").objectReferenceValue              =
            lockSpotGO != null ? lockSpotGO.GetComponent<DustyWallSpot>() : null;
        dwso.FindProperty("lockInteractionPrompt").objectReferenceValue = lockPromptGO;
        dwso.FindProperty("entranceBorderGO").objectReferenceValue      = entranceBorderGO;
        dwso.FindProperty("dustOverlay").objectReferenceValue           = dustOverlayImg;
        dwso.FindProperty("arrowHintText").objectReferenceValue         = arrowHintTMP;
        dwso.FindProperty("inputFeedbackText").objectReferenceValue     = feedbackTMP;
        dwso.FindProperty("instructionText").objectReferenceValue       = dustInstructTMP;
        dwso.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[Level2] UI aufgebaut: Canvas, DialogSystem, Prompt, Schloss, DustWall, ArrowPanel.");
    }

    // ── UI-Hilfs-Methoden ────────────────────────────────────────────────────

    private GameObject UiPanel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Vector2 pivot, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        return go;
    }

    private GameObject UiImage(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        return go;
    }

    private GameObject UiButton(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Vector2 pivot, Color bgColor, string label)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        go.AddComponent<Button>();
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;

        var lblGO  = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lbl    = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text      = label;
        lbl.fontSize  = 18f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color     = Color.white;
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;

        return go;
    }
}
