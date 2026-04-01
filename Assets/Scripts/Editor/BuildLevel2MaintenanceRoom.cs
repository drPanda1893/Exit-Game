using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
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
        EditorGUILayout.HelpBox(
            "Erstellt einen versteckten High-End Wartungsraum mit Joshi als sitzendem NPC.",
            MessageType.Info);
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
        Debug.Log("[Level2] Starte Aufbau...");

        // Kamera zuerst – damit sie auch bei späteren Fehlern existiert
        GameObject camGO = new GameObject("Main Camera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.03f, 0.07f);
        cam.farClipPlane    = 30f;
        cam.nearClipPlane   = 0.1f;
        cam.tag             = "MainCamera";
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 11f, 0f);
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        SceneManager.MoveGameObjectToScene(camGO, scene);

        // Umgebung
        GameObject root = new GameObject("Environment");
        SceneManager.MoveGameObjectToScene(root, scene);
        BuildRoom(root.transform);

        // NPC Joshi
        try { AddJoshi(scene); }
        catch (System.Exception e) { Debug.LogWarning("[Level2] Joshi-Setup fehlgeschlagen: " + e.Message); }

        // Globales Licht
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.07f, 0.09f, 0.16f);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level2] Fertig.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Material-Helfer
    // ═══════════════════════════════════════════════════════════════════════

    private Material M(Color c, float metal = 0f, float smooth = 0.35f)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Metallic",    metal);
        m.SetFloat("_Glossiness",  smooth);
        return m;
    }

    private Material Emit(Color c, Color glow, float intensity = 1.5f)
    {
        var m = M(c, 0f, 0.05f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", glow * intensity);
        return m;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Primitive-Helfer
    // ═══════════════════════════════════════════════════════════════════════

    private GameObject Box(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent,
                           Quaternion? rot = null, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        if (rot.HasValue) go.transform.rotation = rot.Value;
        go.GetComponent<Renderer>().material = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private GameObject Cyl(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent,
                            Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        if (rot.HasValue) go.transform.rotation = rot.Value;
        go.GetComponent<Renderer>().material = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private void Light(string name, Transform parent, Vector3 localPos,
                       LightType type, Color color, float intensity, float range,
                       float spotAngle = 60f, LightShadows shadows = LightShadows.None)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        var l    = go.AddComponent<Light>();
        l.type       = type;
        l.color      = color;
        l.intensity  = intensity;
        l.range      = range;
        l.spotAngle  = spotAngle;
        l.shadows    = shadows;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Raum
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildRoom(Transform root)
    {
        // ── Materialien ──────────────────────────────────────────────────────
        var steelDark   = M(new Color(0.09f, 0.10f, 0.12f), 0.80f, 0.58f);
        var steelMid    = M(new Color(0.14f, 0.15f, 0.18f), 0.72f, 0.50f);
        var steelBright = M(new Color(0.26f, 0.28f, 0.33f), 0.88f, 0.72f);
        var floorMat    = M(new Color(0.10f, 0.11f, 0.13f), 0.55f, 0.68f);
        var grateMat    = M(new Color(0.07f, 0.07f, 0.08f), 0.90f, 0.45f);
        var woodDark    = M(new Color(0.14f, 0.09f, 0.05f), 0.04f, 0.18f);
        var concrete    = M(new Color(0.22f, 0.22f, 0.24f), 0.05f, 0.10f);
        var whiteMat    = M(new Color(0.86f, 0.89f, 0.94f), 0.05f, 0.20f);
        var rubberMat   = M(new Color(0.08f, 0.08f, 0.09f), 0.01f, 0.05f);

        // Emissive
        var ledBlue   = Emit(new Color(0.05f, 0.10f, 0.25f), new Color(0.15f, 0.45f, 1.00f), 2.0f);
        var ledAmber  = Emit(new Color(0.30f, 0.18f, 0.02f), new Color(1.00f, 0.62f, 0.08f), 1.8f);
        var ledGreen  = Emit(new Color(0.03f, 0.22f, 0.05f), new Color(0.08f, 1.00f, 0.18f), 1.5f);
        var ledRed    = Emit(new Color(0.28f, 0.03f, 0.03f), new Color(1.00f, 0.08f, 0.08f), 1.6f);
        var screenMat = Emit(new Color(0.04f, 0.10f, 0.22f), new Color(0.10f, 0.40f, 0.95f), 2.8f);
        var screenGrn = Emit(new Color(0.03f, 0.15f, 0.06f), new Color(0.05f, 0.95f, 0.15f), 2.2f);

        // ── Boden ────────────────────────────────────────────────────────────
        Box("Floor", new Vector3(0, -0.08f, 0), new Vector3(5f, 0.16f, 5f), floorMat, root);

        // Gitter-Overlay (dekorativ)
        for (int i = -2; i <= 2; i++)
        {
            Box("GrateX", new Vector3(i * 1.0f, 0.005f, 0), new Vector3(0.035f, 0.005f, 5f), grateMat, root, collider: false);
            Box("GrateZ", new Vector3(0, 0.005f, i * 1.0f), new Vector3(5f, 0.005f, 0.035f), grateMat, root, collider: false);
        }

        // Boden-LED-Linien
        Box("FLed1", new Vector3(-1.85f, 0.008f, 0), new Vector3(0.012f, 0.004f, 4.7f), ledBlue,  root, collider: false);
        Box("FLed2", new Vector3( 1.85f, 0.008f, 0), new Vector3(0.012f, 0.004f, 4.7f), ledBlue,  root, collider: false);
        Box("FLed3", new Vector3(0,      0.008f, -1.85f), new Vector3(4.7f, 0.004f, 0.012f), ledAmber, root, collider: false);

        // ── Wände ────────────────────────────────────────────────────────────
        // Rückwand (+Z)
        Box("BackWall",      new Vector3(0,    2.5f,  2.5f), new Vector3(5.0f, 5f, 0.22f), steelDark, root);
        // Linke Wand (-X)
        Box("LeftWall",      new Vector3(-2.5f, 2.5f, 0),    new Vector3(0.22f, 5f, 5.0f), steelDark, root);
        // Rechte Wand (+X)
        Box("RightWall",     new Vector3( 2.5f, 2.5f, 0),    new Vector3(0.22f, 5f, 5.0f), steelDark, root);
        // Vorderwand (-Z) mit Durchgang (1.4 m breit) – Spieler kommt hier rein
        Box("FrontWall_L",   new Vector3(-1.8f, 2.5f, -2.5f), new Vector3(1.4f, 5f, 0.22f), steelDark, root);
        Box("FrontWall_R",   new Vector3( 1.8f, 2.5f, -2.5f), new Vector3(1.4f, 5f, 0.22f), steelDark, root);
        Box("FrontWall_Top", new Vector3(0,     3.7f, -2.5f), new Vector3(5.0f, 2.6f, 0.22f), steelDark, root);

        // Wandpaneel-Unterteilung (horizontale Trenner)
        float[] strips = { 0.6f, 1.2f, 2.0f, 3.0f, 4.0f };
        foreach (float h in strips)
        {
            Box("BW_S",  new Vector3(0,     h, 2.39f), new Vector3(4.85f, 0.028f, 0.012f), steelBright, root, collider: false);
            Box("LW_S",  new Vector3(-2.39f, h, 0),    new Vector3(0.012f, 0.028f, 4.85f), steelBright, root, collider: false);
            Box("RW_S",  new Vector3(2.39f,  h, 0),    new Vector3(0.012f, 0.028f, 4.85f), steelBright, root, collider: false);
        }

        // Wand-LED-Streifen (oben)
        Box("LedBack_Top",  new Vector3(0,      4.62f, 2.37f), new Vector3(4.70f, 0.038f, 0.016f), ledBlue,  root, collider: false);
        Box("LedLeft_Top",  new Vector3(-2.37f, 4.62f, 0),     new Vector3(0.016f, 0.038f, 4.70f), ledBlue,  root, collider: false);
        Box("LedRight_Top", new Vector3( 2.37f, 4.62f, 0),     new Vector3(0.016f, 0.038f, 4.70f), ledBlue,  root, collider: false);

        // Sockel-LED (bernstein, bodennah)
        Box("LedBack_Low",  new Vector3(0,     0.18f, 2.37f), new Vector3(4.70f, 0.030f, 0.012f), ledAmber, root, collider: false);
        Box("LedLeft_Low",  new Vector3(-2.37f, 0.18f, 0),    new Vector3(0.012f, 0.030f, 4.70f), ledAmber, root, collider: false);

        // ── Deckenkabelkanal ──────────────────────────────────────────────────
        Box("CableTray_Z", new Vector3(-0.7f, 4.72f, 0.4f), new Vector3(0.20f, 0.055f, 4.85f), grateMat, root, collider: false);
        Box("CableTray_X", new Vector3(0.3f,  4.72f, 1.4f), new Vector3(4.85f, 0.055f, 0.20f), grateMat, root, collider: false);
        // Kabel im Kanal
        Color[] cableColors = {
            new Color(0.10f, 0.10f, 0.55f), new Color(0.55f, 0.10f, 0.10f),
            new Color(0.10f, 0.45f, 0.10f), new Color(0.45f, 0.40f, 0.05f),
            new Color(0.45f, 0.45f, 0.45f)
        };
        for (int i = 0; i < 5; i++)
            Box($"CabZ_{i}", new Vector3(-0.66f + i * 0.04f, 4.695f, 0.4f),
                new Vector3(0.016f, 0.016f, 4.6f), M(cableColors[i], 0f, 0.08f), root, collider: false);

        // ── Workstation (hinten rechts) ───────────────────────────────────────
        BuildWorkstation(new Vector3(1.15f, 0f, 1.55f), root,
            woodDark, steelMid, steelBright, screenMat, screenGrn, ledGreen, ledAmber);

        // ── Serverrack (linke Wand) ───────────────────────────────────────────
        BuildServerRack(new Vector3(-2.05f, 0f, 1.1f), root,
            steelDark, grateMat, ledGreen, ledAmber, ledBlue, ledRed);

        // ── Notfall-Kontrollpanel (rechte Wand) ───────────────────────────────
        BuildControlPanel(new Vector3(2.06f, 1.85f, -0.7f), root,
            steelMid, ledRed, ledGreen, ledAmber, steelBright);

        // ── Joshis Sessel (Mitte-Links) ────────────────────────────────────────
        BuildErgonomicChair(new Vector3(-0.55f, 0f, -0.25f), root, steelDark, rubberMat, steelBright);

        // ── Beistelltisch ─────────────────────────────────────────────────────
        BuildSideTable(new Vector3(0.60f, 0f, -0.25f), root, woodDark, whiteMat, ledBlue, screenGrn);

        // ── Lüftungsöffnung (Vorderwand links – Eingang) ──────────────────────
        BuildVentEntry(new Vector3(0f, 1.2f, -2.39f), root, grateMat, steelBright);

        // ── Wandsafe / Verteilerkasten (links, Ecke) ──────────────────────────
        BuildDistributionBox(new Vector3(-2.39f, 1.8f, -1.4f), root, steelMid, ledGreen, ledAmber);

        // ── Lichtquellen ──────────────────────────────────────────────────────
        AddLights(root);
    }

    // ─── Workstation ─────────────────────────────────────────────────────────

    private void BuildWorkstation(Vector3 pos, Transform root,
        Material wood, Material steel, Material steelBright,
        Material screen1, Material screen2, Material ledG, Material ledA)
    {
        var g = new GameObject("Workstation");
        g.transform.position = pos;
        g.transform.SetParent(root);

        // L-förmiger Schreibtisch
        // Hauptplatte (entlang Z)
        Box("Desk_Main", new Vector3(0, 0.74f, 0), new Vector3(1.10f, 0.055f, 0.72f), wood, g.transform);
        // Seitenflügel (entlang X)
        Box("Desk_Wing", new Vector3(-0.55f, 0.74f, 0.38f), new Vector3(0.58f, 0.055f, 0.52f), wood, g.transform);

        // Beine
        foreach (var (dx, dz) in new[] { (0.48f, -0.32f), (-0.48f, -0.32f), (0.48f, 0.32f) })
            Cyl($"Leg_{dx}", new Vector3(dx, 0.37f, dz), new Vector3(0.04f, 0.37f, 0.04f), steel, g.transform);

        // Hauptmonitor (leicht geneigt)
        Box("Monitor1_Stand", new Vector3(0.05f, 0.80f, 0.10f), new Vector3(0.04f, 0.26f, 0.04f), steel, g.transform, collider: false);
        Box("Monitor1", new Vector3(0.05f, 1.14f, 0.14f), new Vector3(0.72f, 0.42f, 0.032f),
            screen1, g.transform, Quaternion.Euler(8f, 0f, 0f), false);
        Box("Monitor1_Bezel", new Vector3(0.05f, 1.14f, 0.138f), new Vector3(0.76f, 0.46f, 0.018f),
            steel, g.transform, Quaternion.Euler(8f, 0f, 0f), false);

        // Zweitmonitor (links, gedreht)
        Box("Monitor2_Stand", new Vector3(-0.52f, 0.80f, 0.54f), new Vector3(0.04f, 0.22f, 0.04f), steel, g.transform, collider: false);
        Box("Monitor2", new Vector3(-0.52f, 1.08f, 0.56f), new Vector3(0.55f, 0.34f, 0.028f),
            screen2, g.transform, Quaternion.Euler(8f, 20f, 0f), false);
        Box("Monitor2_Bezel", new Vector3(-0.52f, 1.08f, 0.558f), new Vector3(0.59f, 0.38f, 0.015f),
            steel, g.transform, Quaternion.Euler(8f, 20f, 0f), false);

        // Tastatur
        Box("Keyboard", new Vector3(0.05f, 0.762f, -0.08f), new Vector3(0.40f, 0.012f, 0.14f),
            M(new Color(0.10f, 0.10f, 0.11f), 0.3f, 0.25f), g.transform, collider: false);

        // LED-Streifen unterm Schreibtisch
        Box("DeskLED", new Vector3(0, 0.62f, -0.35f), new Vector3(1.0f, 0.018f, 0.010f), ledA, g.transform, collider: false);

        // Kleine grüne Statuslichter am Monitorrahmen
        for (int i = 0; i < 3; i++)
            Box($"StatLED_{i}", new Vector3(-0.28f + i * 0.28f, 0.945f, 0.126f),
                new Vector3(0.018f, 0.018f, 0.010f), ledG, g.transform, collider: false);

        // Boxkollider für ganzen Tisch
        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.38f, 0.1f);
        bc.size   = new Vector3(1.15f, 0.78f, 0.78f);

        // Kleines Point-Light für Monitor-Glow
        Light("MonitorGlow", g.transform, new Vector3(0.05f, 1.05f, -0.2f),
            LightType.Point, new Color(0.15f, 0.42f, 1.00f), 0.9f, 2.2f);
        Light("Monitor2Glow", g.transform, new Vector3(-0.52f, 1.05f, 0.3f),
            LightType.Point, new Color(0.10f, 0.92f, 0.25f), 0.7f, 1.8f);
    }

    // ─── Serverrack ──────────────────────────────────────────────────────────

    private void BuildServerRack(Vector3 pos, Transform root,
        Material chassis, Material grate,
        Material ledG, Material ledA, Material ledB, Material ledR)
    {
        var g = new GameObject("ServerRack");
        g.transform.position = pos;
        g.transform.SetParent(root);

        // Gehäuse
        Box("Chassis", new Vector3(0, 1.05f, 0), new Vector3(0.58f, 2.10f, 0.55f), chassis, g.transform);

        // Vorderes Gitter
        Box("FrontGrate", new Vector3(0, 1.05f, -0.265f), new Vector3(0.52f, 2.0f, 0.025f), grate, g.transform, collider: false);

        // Server-Einschübe (je 8 U)
        Material[] slotColors = {
            M(new Color(0.12f, 0.12f, 0.14f), 0.6f, 0.4f),
            M(new Color(0.10f, 0.10f, 0.12f), 0.6f, 0.4f)
        };
        for (int i = 0; i < 10; i++)
        {
            float y = 0.12f + i * 0.19f;
            Box($"Slot_{i}", new Vector3(0, y, -0.262f), new Vector3(0.50f, 0.16f, 0.020f),
                slotColors[i % 2], g.transform, collider: false);
        }

        // Status-LEDs pro Einschub
        Material[] rackLeds = { ledG, ledG, ledA, ledG, ledR, ledG, ledG, ledA, ledG, ledG };
        for (int i = 0; i < 10; i++)
            Box($"LED_{i}", new Vector3(0.22f, 0.12f + i * 0.19f, -0.270f),
                new Vector3(0.018f, 0.018f, 0.008f), rackLeds[i], g.transform, collider: false);

        // Lüftungsgitter oben
        Box("TopVent", new Vector3(0, 2.07f, 0), new Vector3(0.52f, 0.06f, 0.50f), grate, g.transform, collider: false);

        // Kabelauslass unten
        Box("CableOut", new Vector3(0.10f, 0.04f, -0.24f), new Vector3(0.22f, 0.04f, 0.10f), M(new Color(0.08f, 0.08f, 0.09f)), g.transform, collider: false);

        // Kollider
        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 1.05f, 0);
        bc.size   = new Vector3(0.60f, 2.12f, 0.58f);

        // Light
        Light("RackGlow", g.transform, new Vector3(0, 1.1f, -0.5f),
            LightType.Point, new Color(0.08f, 0.95f, 0.20f), 0.5f, 1.5f);
    }

    // ─── Kontrollpanel ───────────────────────────────────────────────────────

    private void BuildControlPanel(Vector3 pos, Transform root,
        Material panel, Material ledR, Material ledG, Material ledA, Material trim)
    {
        var g = new GameObject("ControlPanel");
        g.transform.position = pos;
        g.transform.SetParent(root);

        // Gehäuse (an der Wand)
        Box("Body", new Vector3(0, 0, 0), new Vector3(0.75f, 1.0f, 0.12f), panel, g.transform);
        Box("Frame", new Vector3(0, 0, -0.055f), new Vector3(0.81f, 1.06f, 0.015f), trim, g.transform, collider: false);

        // Großes Display oben
        Box("MainDisplay", new Vector3(0, 0.25f, -0.062f), new Vector3(0.58f, 0.30f, 0.015f),
            Emit(new Color(0.04f, 0.08f, 0.18f), new Color(0.10f, 0.35f, 0.95f), 2.0f),
            g.transform, collider: false);

        // Schalterreihe
        Material[] switchLeds = { ledR, ledG, ledG, ledA, ledR, ledG };
        for (int i = 0; i < 6; i++)
        {
            float x = -0.25f + i * 0.10f;
            Box($"Switch_{i}", new Vector3(x, -0.05f, -0.068f), new Vector3(0.042f, 0.055f, 0.030f),
                M(new Color(0.15f, 0.15f, 0.16f), 0.5f, 0.4f), g.transform, collider: false);
            Box($"SwitchLED_{i}", new Vector3(x, -0.07f, -0.073f), new Vector3(0.016f, 0.016f, 0.008f),
                switchLeds[i], g.transform, collider: false);
        }

        // Drehregler-Reihe
        for (int i = 0; i < 4; i++)
            Cyl($"Knob_{i}", new Vector3(-0.15f + i * 0.10f, -0.22f, -0.076f),
                new Vector3(0.038f, 0.020f, 0.038f),
                M(new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.5f), g.transform,
                Quaternion.Euler(90f, 0f, 0f));

        // Großer roter Notfall-Knopf
        Cyl("EmergencyBtn", new Vector3(0.28f, -0.24f, -0.078f),
            new Vector3(0.06f, 0.028f, 0.06f),
            Emit(new Color(0.7f, 0.05f, 0.05f), new Color(1f, 0.05f, 0.05f), 2.0f),
            g.transform, Quaternion.Euler(90f, 0f, 0f));

        // Label über Notfall-Knopf
        var label = new GameObject("EmergencyLabel");
        label.transform.SetParent(g.transform);
        label.transform.localPosition = new Vector3(0.28f, -0.13f, -0.065f);
        label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var tmp = label.AddComponent<TextMeshPro>();
        tmp.text      = "NOTFALL";
        tmp.fontSize  = 0.6f;
        tmp.color     = new Color(1f, 0.2f, 0.2f);
        tmp.alignment = TextAlignmentOptions.Center;
        var rt = label.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0.3f, 0.12f);

        Light("PanelGlow", g.transform, new Vector3(0, 0.25f, -0.3f),
            LightType.Point, new Color(0.95f, 0.15f, 0.08f), 0.35f, 1.2f);
    }

    // ─── Ergonomischer Sessel ─────────────────────────────────────────────────

    private void BuildErgonomicChair(Vector3 pos, Transform root,
        Material chassis, Material rubber, Material trim)
    {
        var g = new GameObject("ErgonomicChair");
        g.transform.position = pos;
        g.transform.SetParent(root);
        // Joshi schaut Richtung Workstation (+Z leicht)
        g.transform.rotation = Quaternion.Euler(0f, 30f, 0f);

        // Sitzfläche
        Box("Seat", new Vector3(0, 0.46f, 0), new Vector3(0.50f, 0.075f, 0.48f), rubber, g.transform);
        // Sitz-Polsterung (leicht erhöht)
        Box("SeatPad", new Vector3(0, 0.502f, 0), new Vector3(0.44f, 0.032f, 0.42f),
            M(new Color(0.11f, 0.11f, 0.12f), 0.01f, 0.06f), g.transform, collider: false);

        // Rückenlehne
        Box("Backrest", new Vector3(0, 0.90f, 0.22f), new Vector3(0.46f, 0.78f, 0.07f), rubber, g.transform);
        Box("BackrestPad", new Vector3(0, 0.90f, 0.188f), new Vector3(0.40f, 0.70f, 0.025f),
            M(new Color(0.12f, 0.12f, 0.13f), 0.01f, 0.06f), g.transform, collider: false);
        // Kopfstütze
        Box("Headrest", new Vector3(0, 1.32f, 0.19f), new Vector3(0.26f, 0.22f, 0.07f), rubber, g.transform, collider: false);

        // Armstützen
        foreach (float sx in new[] { -0.26f, 0.26f })
        {
            Box($"Armrest_{sx}", new Vector3(sx, 0.62f, 0.06f), new Vector3(0.055f, 0.045f, 0.38f), rubber, g.transform, collider: false);
            Cyl($"ArmPost_{sx}", new Vector3(sx, 0.54f, 0.05f), new Vector3(0.032f, 0.09f, 0.032f), chassis, g.transform);
        }

        // Gasfeder / Säule
        Cyl("Stem", new Vector3(0, 0.23f, 0), new Vector3(0.055f, 0.23f, 0.055f), chassis, g.transform);
        // Fußkreuz (5 Arme)
        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f * Mathf.Deg2Rad;
            var armDir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var arm = Box($"FootArm_{i}", new Vector3(armDir.x * 0.20f, 0.032f, armDir.z * 0.20f),
                new Vector3(0.04f, 0.032f, 0.40f), chassis, g.transform, collider: false);
            arm.transform.rotation = Quaternion.Euler(0f, -i * 72f, 0f);
            // Rollen
            Cyl($"Wheel_{i}", new Vector3(armDir.x * 0.38f, 0.022f, armDir.z * 0.38f),
                new Vector3(0.045f, 0.022f, 0.045f), rubber, g.transform);
        }

        // Blauer Akzentstreifen (Rückenlehne oben)
        Box("AccentStrip", new Vector3(0, 1.265f, 0.185f), new Vector3(0.38f, 0.014f, 0.012f),
            Emit(new Color(0.05f, 0.10f, 0.30f), new Color(0.15f, 0.45f, 1.00f), 1.2f),
            g.transform, collider: false);

        // Kollider
        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.65f, 0.05f);
        bc.size   = new Vector3(0.58f, 1.30f, 0.68f);
    }

    // ─── Beistelltisch ───────────────────────────────────────────────────────

    private void BuildSideTable(Vector3 pos, Transform root,
        Material wood, Material white, Material ledB, Material ledG)
    {
        var g = new GameObject("SideTable");
        g.transform.position = pos;
        g.transform.SetParent(root);

        // Tischplatte
        Box("Top", new Vector3(0, 0.64f, 0), new Vector3(0.42f, 0.038f, 0.42f), wood, g.transform);
        // Beine
        foreach (var (dx, dz) in new[] { (0.17f, 0.17f), (-0.17f, 0.17f), (0.17f, -0.17f), (-0.17f, -0.17f) })
            Cyl($"Leg", new Vector3(dx, 0.32f, dz), new Vector3(0.028f, 0.32f, 0.028f),
                M(new Color(0.20f, 0.20f, 0.22f), 0.85f, 0.55f), g.transform);

        // Kaffeetasse
        Cyl("Cup", new Vector3(-0.10f, 0.70f, 0.05f), new Vector3(0.055f, 0.060f, 0.055f), white, g.transform);
        Cyl("CupContent", new Vector3(-0.10f, 0.748f, 0.05f), new Vector3(0.042f, 0.008f, 0.042f),
            M(new Color(0.18f, 0.10f, 0.04f), 0f, 0.05f), g.transform);

        // Mini-Tablet / Datenpanel
        Box("Tablet", new Vector3(0.08f, 0.662f, -0.04f), new Vector3(0.18f, 0.008f, 0.13f),
            M(new Color(0.10f, 0.10f, 0.11f), 0.7f, 0.6f), g.transform, collider: false);
        Box("TabletScreen", new Vector3(0.08f, 0.668f, -0.04f), new Vector3(0.15f, 0.006f, 0.10f),
            ledG, g.transform, collider: false);

        // LED-Unterkante Tisch
        Box("TableLED", new Vector3(0, 0.618f, 0), new Vector3(0.40f, 0.010f, 0.40f), ledB, g.transform, collider: false);
    }

    // ─── Lüftungsöffnung / Eingang ────────────────────────────────────────────

    private void BuildVentEntry(Vector3 pos, Transform root, Material grate, Material trim)
    {
        var g = new GameObject("VentEntry");
        g.transform.position = pos;
        g.transform.SetParent(root);

        // Rahmen
        Box("Frame", new Vector3(0, 0, 0), new Vector3(1.46f, 0.82f, 0.055f), trim, g.transform, collider: false);
        // Gitterstäbe horizontal
        for (int i = 0; i < 5; i++)
            Box($"Bar_{i}", new Vector3(0, -0.32f + i * 0.16f, -0.01f),
                new Vector3(1.36f, 0.025f, 0.040f), grate, g.transform, collider: false);
        // Gitterstäbe vertikal
        for (int i = 0; i < 8; i++)
            Box($"VBar_{i}", new Vector3(-0.59f + i * 0.17f, 0, -0.01f),
                new Vector3(0.025f, 0.74f, 0.040f), grate, g.transform, collider: false);
    }

    // ─── Verteilerkasten ──────────────────────────────────────────────────────

    private void BuildDistributionBox(Vector3 pos, Transform root,
        Material panel, Material ledG, Material ledA)
    {
        var g = new GameObject("DistributionBox");
        g.transform.position = pos;
        g.transform.SetParent(root);

        Box("Body",  new Vector3(0, 0, 0),     new Vector3(0.50f, 0.65f, 0.08f),  panel, g.transform);
        Box("Door",  new Vector3(0, 0, -0.048f), new Vector3(0.46f, 0.61f, 0.015f),
            M(new Color(0.18f, 0.19f, 0.22f), 0.7f, 0.5f), g.transform, collider: false);

        // Sicherungsreihe
        for (int i = 0; i < 8; i++)
        {
            float x = -0.17f + (i % 4) * 0.115f;
            float y = (i < 4) ? 0.10f : -0.10f;
            Box($"Fuse_{i}", new Vector3(x, y, -0.058f), new Vector3(0.08f, 0.12f, 0.012f),
                M(new Color(0.15f, 0.15f, 0.16f), 0.4f, 0.3f), g.transform, collider: false);
            Box($"FuseLED_{i}", new Vector3(x, y + 0.07f, -0.062f),
                new Vector3(0.014f, 0.014f, 0.006f),
                i == 4 ? ledA : ledG, g.transform, collider: false);
        }

        // Kollider
        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0, 0);
        bc.size   = new Vector3(0.52f, 0.67f, 0.10f);
    }

    // ─── Lichter ──────────────────────────────────────────────────────────────

    private void AddLights(Transform root)
    {
        // Hauptlicht – kühles Deckenlicht
        Light("CeilingMain", root, new Vector3(0, 4.9f, 0.3f),
            LightType.Point, new Color(0.72f, 0.82f, 1.00f), 2.8f, 9f, shadows: LightShadows.Soft);

        // Accent – warmes Fülllicht
        Light("FillAmber", root, new Vector3(-1.2f, 3.8f, -1.0f),
            LightType.Point, new Color(1.00f, 0.72f, 0.32f), 1.0f, 5f);

        // Workstation-Bereich
        Light("WorkLight", root, new Vector3(1.2f, 3.2f, 1.6f),
            LightType.Spot, new Color(0.60f, 0.80f, 1.00f), 1.8f, 4f, 55f, LightShadows.Soft);

        // Server-Rack Bereich
        Light("RackLight", root, new Vector3(-1.8f, 2.5f, 1.1f),
            LightType.Point, new Color(0.10f, 1.00f, 0.22f), 0.55f, 2.5f);

        // Notfall-Panel Bereich
        Light("PanelLight", root, new Vector3(2.0f, 2.5f, -0.7f),
            LightType.Point, new Color(1.00f, 0.18f, 0.10f), 0.40f, 2.0f);

        // Blaues Raumambiente (Wand-LEDs simulieren)
        Light("AmbBlue1", root, new Vector3( 0.0f, 0.5f,  2.2f),
            LightType.Point, new Color(0.15f, 0.40f, 1.00f), 0.45f, 3.5f);
        Light("AmbBlue2", root, new Vector3(-2.2f, 0.5f,  0.0f),
            LightType.Point, new Color(0.15f, 0.40f, 1.00f), 0.35f, 3.0f);
        Light("AmbAmber", root, new Vector3( 0.0f, 0.5f, -2.2f),
            LightType.Point, new Color(1.00f, 0.62f, 0.08f), 0.35f, 3.0f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Joshi NPC
    // ═══════════════════════════════════════════════════════════════════════

    private void AddJoshi(Scene scene)
    {
        // Modell laden
        GameObject joshiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Sitting Talking.fbx");
        if (joshiPrefab == null)
        {
            Debug.LogWarning("[Level2] 'Sitting Talking.fbx' nicht gefunden – Joshi wird übersprungen.");
            return;
        }

        GameObject joshi = (GameObject)PrefabUtility.InstantiatePrefab(joshiPrefab);
        joshi.name = "Joshi";

        // Position: auf dem Sessel (-0.55, 0, -0.25), Stuhl ist 0.46 m hoch
        // FBX-Modelle kommen oft in cm-Skalierung (100:1) – wir prüfen und skalieren ggf.
        Bounds bounds = GetModelBounds(joshi);
        float modelHeight = bounds.size.y;
        float targetHeight = 1.75f; // Joshi soll ca. 1.75 m hoch sein (sitzend ca. 1.1 m)
        float scaleFactor  = (modelHeight > 0.5f) ? 1f : targetHeight / Mathf.Max(modelHeight, 0.01f);

        joshi.transform.position = new Vector3(-0.55f, 0.50f, -0.25f);
        joshi.transform.rotation = Quaternion.Euler(0f, 200f, 0f); // schaut Richtung Workstation
        joshi.transform.localScale = Vector3.one * scaleFactor;

        // Material mit PBR-Texturen erstellen und zuweisen
        Material joshiMat = CreateJoshiMaterial();
        if (joshiMat != null)
            foreach (var r in joshi.GetComponentsInChildren<Renderer>(true))
                r.material = joshiMat;

        // Sitz-Animation einrichten
        try { SetupSittingAnimation(joshi); }
        catch (System.Exception e) { Debug.LogWarning("[Level2] Animation-Setup fehlgeschlagen: " + e.Message); }

        SceneManager.MoveGameObjectToScene(joshi, scene);
        Debug.Log("[Level2] Joshi platziert.");
    }

    private Bounds GetModelBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    private Material CreateJoshiMaterial()
    {
        const string basePath = "Assets/Big Yahu/3dcartooncharactermodel_Clone1_";
        var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "basecolor.JPEG");
        var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "normal.JPEG");
        var metallic  = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "metallic.JPEG");
        var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "roughness.JPEG");

        if (baseColor == null)
        {
            Debug.LogWarning("[Level2] Joshi-Textur 'basecolor.JPEG' nicht gefunden.");
            return null;
        }

        var mat = new Material(Shader.Find("Standard"));
        mat.name = "Joshi_PBR";

        mat.SetTexture("_MainTex", baseColor);
        mat.SetColor("_Color", Color.white);

        if (normalMap != null)
        {
            // Normal-Map muss als NormalMap-Textur importiert sein
            mat.SetTexture("_BumpMap", normalMap);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }

        if (metallic != null)
        {
            mat.SetTexture("_MetallicGlossMap", metallic);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        }

        // Roughness → Smoothness (invertiert). Wir setzen einen mittleren Wert
        // da wir keine Textur-Inversion zur Laufzeit durchführen.
        float smoothnessFromRoughness = (roughness != null) ? 0.35f : 0.40f;
        mat.SetFloat("_Metallic",   0.0f);
        mat.SetFloat("_Glossiness", smoothnessFromRoughness);

        // Material speichern damit Unity es serialisiert
        const string matPath = "Assets/Big Yahu/Joshi_PBR.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
            AssetDatabase.DeleteAsset(matPath);
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();

        return AssetDatabase.LoadAssetAtPath<Material>(matPath);
    }

    private void SetupSittingAnimation(GameObject joshiInstance)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Big Yahu/Sitting Talking.fbx");
        AnimationClip sourceClip = null;
        foreach (Object a in assets)
            if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            { sourceClip = clip; break; }

        if (sourceClip == null)
        {
            Debug.LogWarning("[Level2] Kein AnimationClip in 'Sitting Talking.fbx' gefunden.");
            return;
        }

        const string clipPath = "Assets/Big Yahu/Joshi_Sitting_Loop.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);

        AnimationClip loopClip = Object.Instantiate(sourceClip);
        loopClip.name = "Joshi_Sitting_Loop";
        var settings = AnimationUtility.GetAnimationClipSettings(loopClip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(loopClip, settings);
        AssetDatabase.CreateAsset(loopClip, clipPath);

        const string ctrlPath = "Assets/Big Yahu/Joshi_Sitting.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null)
            AssetDatabase.DeleteAsset(ctrlPath);

        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState sit = sm.AddState("Sitting");
        sit.motion      = loopClip;
        sm.defaultState = sit;
        AssetDatabase.SaveAssets();

        Animator anim = joshiInstance.GetComponent<Animator>() ?? joshiInstance.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        Debug.Log("[Level2] Joshi-Animation eingerichtet: " + sourceClip.name);
    }
}
