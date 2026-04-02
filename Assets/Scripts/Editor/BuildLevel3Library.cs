using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Baut Level 3 – Gefängnis-Bibliothek.
/// Menü: Tools → Build Level 3 Library
/// </summary>
public class BuildLevel3Library : EditorWindow
{
    [MenuItem("Tools/Build Level 3 Library")]
    public static void ShowWindow() => GetWindow<BuildLevel3Library>("Level 3 Builder");

    void OnGUI()
    {
        GUILayout.Label("Level 3 – Gefängnis-Bibliothek", EditorStyles.boldLabel);
        GUILayout.Space(8);
        EditorGUILayout.HelpBox("Detaillierte Bibliothek mit Helios-Thresen und Buch-Puzzle.\nInteraktion: E-Taste wenn in Reichweite.", MessageType.Info);
        GUILayout.Space(12);
        if (GUILayout.Button("Bibliothek bauen", GUILayout.Height(34)))
            Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Scene Setup
    // ═══════════════════════════════════════════════════════════════════════

    private void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level3.unity");

        // Kamera – fest am Eingang wie Level 2
        var camGO = new GameObject("Main Camera");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.03f, 0.03f);
        cam.farClipPlane    = 40f;
        cam.nearClipPlane   = 0.1f;
        cam.tag             = "MainCamera";
        camGO.AddComponent<AudioListener>();
        var follow = camGO.AddComponent<TopDownCameraFollow>();
        follow.height              = 10f;
        follow.pitchAngle          = 65f;
        follow.fixedWorldPosition  = new Vector3(0f, 3.2f, -5.4f);
        SceneManager.MoveGameObjectToScene(camGO, scene);

        // Umgebung
        var root = new GameObject("Environment");
        SceneManager.MoveGameObjectToScene(root, scene);
        BuildRoom(root.transform);

        // Spieler
        var player = AddPlayer(scene);
        if (player != null) follow.SetTarget(player.transform);

        // Helios NPC + Buch-UI
        AddHeliosSetup(scene);

        // Globales Licht – warmes Kerzenlicht
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.17f, 0.12f);

        AddBackgroundMusic(scene);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level3] Bibliothek fertig.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Materialien
    // ═══════════════════════════════════════════════════════════════════════

    private Material M(Color c, float metal = 0f, float smooth = 0.25f)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Metallic",   metal);
        m.SetFloat("_Glossiness", smooth);
        return m;
    }

    private Material Emit(Color c, Color glow, float intensity = 1.5f)
    {
        var m = M(c);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", glow * intensity);
        return m;
    }

    private Material CreateHeliosMaterial()
    {
        const string folder   = "Assets/Big Yahu/Material Helios/";
        const string albedo   = folder + "tripo_material_53722ac8-450e-4c1b-9d07-3e28df23b2ca.png";
        const string normal   = folder + "normalMap1.png";
        const string metallic = folder + "metalnessMap1.png";

        var mat = new Material(Shader.Find("Standard"));
        mat.name = "Helios_Material";

        var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
        if (albedoTex != null) mat.mainTexture = albedoTex;

        var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
        if (normalTex != null)
        {
            // Sicherstellen dass Normal-Map als NormalMap importiert ist
            var imp = AssetImporter.GetAtPath(normal) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
                normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
            }
            mat.SetTexture("_BumpMap", normalTex);
            mat.EnableKeyword("_NORMALMAP");
        }

        var metalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(metallic);
        if (metalTex != null)
        {
            mat.SetTexture("_MetallicGlossMap", metalTex);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        }

        mat.SetFloat("_Metallic",   0.1f);
        mat.SetFloat("_Glossiness", 0.3f);
        return mat;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Primitive-Helfer
    // ═══════════════════════════════════════════════════════════════════════

    private GameObject Box(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent,
                           Quaternion? rot = null, bool col = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        if (rot.HasValue) go.transform.rotation = rot.Value;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!col) Object.DestroyImmediate(go.GetComponent<Collider>());
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
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private void AddLight(string name, Transform parent, Vector3 localPos,
                          LightType type, Color color, float intensity, float range,
                          float spotAngle = 60f, LightShadows shadows = LightShadows.None,
                          Quaternion? rotation = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        if (rotation.HasValue) go.transform.localRotation = rotation.Value;
        var l = go.AddComponent<Light>();
        l.type      = type;
        l.color     = color;
        l.intensity = intensity;
        l.range     = range;
        l.spotAngle = spotAngle;
        l.shadows   = shadows;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Raum
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildRoom(Transform root)
    {
        var stoneMat   = M(new Color(0.30f, 0.26f, 0.20f), 0.02f, 0.06f);
        var stoneLight = M(new Color(0.40f, 0.36f, 0.28f), 0.02f, 0.08f);
        var woodDark   = M(new Color(0.18f, 0.11f, 0.06f), 0.01f, 0.10f);
        var woodMid    = M(new Color(0.26f, 0.17f, 0.08f), 0.01f, 0.12f);
        var woodLight  = M(new Color(0.36f, 0.24f, 0.12f), 0.01f, 0.14f);
        var ironMat    = M(new Color(0.20f, 0.18f, 0.16f), 0.20f, 0.15f);
        var brassmat   = M(new Color(0.52f, 0.40f, 0.14f), 0.55f, 0.35f);
        var parchment  = M(new Color(0.80f, 0.73f, 0.56f), 0.01f, 0.08f);
        var bookRed    = M(new Color(0.42f, 0.08f, 0.06f), 0.01f, 0.12f);
        var bookGreen  = M(new Color(0.10f, 0.26f, 0.12f), 0.01f, 0.12f);
        var bookBlue   = M(new Color(0.08f, 0.12f, 0.32f), 0.01f, 0.12f);
        var bookBrown  = M(new Color(0.30f, 0.16f, 0.07f), 0.01f, 0.10f);
        var leatherMat = M(new Color(0.16f, 0.09f, 0.04f), 0.02f, 0.08f);
        var candleWax  = M(new Color(0.90f, 0.86f, 0.70f), 0.01f, 0.06f);
        var candleFlame = Emit(new Color(0.9f, 0.7f, 0.2f), new Color(1f, 0.65f, 0.1f), 3f);
        var dirtMat    = M(new Color(0.20f, 0.17f, 0.12f), 0.01f, 0.04f);
        var ironGrate  = M(new Color(0.16f, 0.14f, 0.12f), 0.15f, 0.10f);
        var marbleMat  = M(new Color(0.52f, 0.48f, 0.42f), 0.05f, 0.40f);

        BuildFloor(root, stoneMat, stoneLight, dirtMat, marbleMat);
        BuildWalls(root, stoneMat, stoneLight, ironMat, ironGrate, brassmat, woodDark);
        BuildPillars(root, stoneMat, stoneLight, brassmat);
        BuildBookShelves(root, woodDark, woodMid, woodLight, bookRed, bookGreen, bookBlue, bookBrown, parchment);
        BuildReadingArea(root, woodDark, woodMid, leatherMat, candleWax, candleFlame, brassmat);
        BuildLighting(root, brassmat, candleWax, candleFlame);
    }

    // ── Boden ────────────────────────────────────────────────────────────────

    private void BuildFloor(Transform root, Material stone, Material stoneLight, Material dirt, Material marble)
    {
        Box("Floor", new Vector3(0, -0.1f, 0), new Vector3(10f, 0.2f, 12f), stone, root);

        // Steinplatten-Schachbrettmuster
        var mats = new[] { stone, stoneLight };
        for (int x = -4; x <= 4; x++)
        for (int z = -5; z <= 5; z++)
        {
            var mat = mats[(Mathf.Abs(x + z)) % 2];
            Box($"Slab_{x}_{z}", new Vector3(x, 0.002f, z),
                new Vector3(0.96f, 0.003f, 0.96f), mat, root, col: false);
        }

        // Laufgang-Teppich Mittelachse (rot, etwas abgenutzt)
        var carpetMat  = M(new Color(0.30f, 0.09f, 0.07f), 0f, 0.04f);
        var carpetEdge = M(new Color(0.45f, 0.28f, 0.10f), 0f, 0.06f);
        Box("Carpet",       new Vector3(0, 0.005f, 0.5f),   new Vector3(1.4f, 0.004f, 9.2f), carpetMat,  root, col: false);
        Box("CarpetEdgeL",  new Vector3(-0.72f, 0.005f, 0.5f), new Vector3(0.06f, 0.004f, 9.2f), carpetEdge, root, col: false);
        Box("CarpetEdgeR",  new Vector3( 0.72f, 0.005f, 0.5f), new Vector3(0.06f, 0.004f, 9.2f), carpetEdge, root, col: false);

        // Marmoreinlage vor Thresen
        Box("MarbleEntry", new Vector3(0, 0.004f, 2.5f), new Vector3(2.5f, 0.003f, 1.0f), marble, root, col: false);

        // Abnutzungsflecken
        Box("Worn1", new Vector3(-1.4f, 0.003f,  1.5f), new Vector3(0.5f, 0.002f, 0.4f), dirt, root, col: false);
        Box("Worn2", new Vector3( 2.1f, 0.003f, -1.2f), new Vector3(0.4f, 0.002f, 0.5f), dirt, root, col: false);
        Box("Worn3", new Vector3(-0.5f, 0.003f, -3.0f), new Vector3(0.6f, 0.002f, 0.3f), dirt, root, col: false);
    }

    // ── Wände ────────────────────────────────────────────────────────────────

    private void BuildWalls(Transform root, Material stone, Material stoneLight, Material iron,
                            Material grate, Material brass, Material wood)
    {
        // Hauptwände
        Box("BackWall",     new Vector3(0,    3.0f,  6.0f), new Vector3(10f, 6f, 0.3f),  stone, root);
        Box("FrontWall_L",  new Vector3(-3.5f,3.0f, -6.0f), new Vector3(3.0f,6f, 0.3f),  stone, root);
        Box("FrontWall_R",  new Vector3( 3.5f,3.0f, -6.0f), new Vector3(3.0f,6f, 0.3f),  stone, root);
        Box("FrontWall_Top",new Vector3(0,    4.8f, -6.0f), new Vector3(10f, 2.4f,0.3f), stone, root);
        Box("LeftWall",     new Vector3(-5.0f,3.0f,  0f  ), new Vector3(0.3f,6f, 12f),   stone, root);
        Box("RightWall",    new Vector3( 5.0f,3.0f,  0f  ), new Vector3(0.3f,6f, 12f),   stone, root);

        // Decke (niedrig sichtbar für Atmosphäre, kein Collider)
        Box("Ceiling", new Vector3(0, 6.05f, 0), new Vector3(10f, 0.2f, 12f), stone, root, col: false);

        // Deckenbalken aus Holz
        var beamMat = M(new Color(0.16f, 0.10f, 0.05f), 0.01f, 0.08f);
        for (int bi = -4; bi <= 4; bi += 2)
            Box($"Beam_{bi}", new Vector3(bi, 5.7f, 0), new Vector3(0.25f, 0.25f, 12f), beamMat, root, col: false);

        // Eingangsrahmen aus Eisen
        Box("EntryFrame_L",   new Vector3(-1.0f, 2.0f, -5.88f), new Vector3(0.10f, 4.0f, 0.10f), iron, root, col: false);
        Box("EntryFrame_R",   new Vector3( 1.0f, 2.0f, -5.88f), new Vector3(0.10f, 4.0f, 0.10f), iron, root, col: false);
        Box("EntryFrame_Top", new Vector3( 0.0f, 4.05f,-5.88f), new Vector3(2.10f, 0.10f,0.10f), iron, root, col: false);
        // Eisenzierleiste
        Box("EntryTrim_L",    new Vector3(-1.05f,2.0f,-5.85f), new Vector3(0.04f, 3.8f, 0.04f), brass, root, col: false);
        Box("EntryTrim_R",    new Vector3( 1.05f,2.0f,-5.85f), new Vector3(0.04f, 3.8f, 0.04f), brass, root, col: false);

        // Unsichtbare Border am Eingang
        var border = new GameObject("EntranceBorder");
        border.transform.position = new Vector3(0f, 2.5f, -5.6f);
        border.transform.SetParent(root);
        var bc = border.AddComponent<BoxCollider>();
        bc.size = new Vector3(10f, 5f, 0.2f);

        // Bogenfenster
        BuildArchWindow(new Vector3(-4.88f, 3.0f,  2.0f), Quaternion.Euler(0,  90f, 0), root, iron, brass);
        BuildArchWindow(new Vector3(-4.88f, 3.0f, -1.0f), Quaternion.Euler(0,  90f, 0), root, iron, brass);
        BuildArchWindow(new Vector3( 4.88f, 3.0f,  2.0f), Quaternion.Euler(0, -90f, 0), root, iron, brass);
        BuildArchWindow(new Vector3( 4.88f, 3.0f, -1.0f), Quaternion.Euler(0, -90f, 0), root, iron, brass);

        // Steinmauer-Fugenlinien
        float[] fugenY = { 0.5f, 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f, 4.5f, 5.0f, 5.5f };
        var fugenMat = M(new Color(0.14f, 0.12f, 0.10f), 0f, 0.02f);
        foreach (float fy in fugenY)
        {
            Box($"FugeB_{fy}", new Vector3(0,      fy,  5.87f), new Vector3(9.8f,  0.015f, 0.01f), fugenMat, root, col: false);
            Box($"FugeL_{fy}", new Vector3(-4.87f, fy,  0f   ), new Vector3(0.01f, 0.015f, 11.8f), fugenMat, root, col: false);
            Box($"FugeR_{fy}", new Vector3( 4.87f, fy,  0f   ), new Vector3(0.01f, 0.015f, 11.8f), fugenMat, root, col: false);
        }
    }

    private void BuildArchWindow(Vector3 pos, Quaternion rot, Transform root, Material iron, Material brass)
    {
        var g = new GameObject("ArchWindow");
        g.transform.position = pos; g.transform.rotation = rot; g.transform.SetParent(root);
        Box("FrameL",  new Vector3(0, 0,  0.02f), new Vector3(0.06f, 1.8f, 0.06f), iron,  g.transform, col: false);
        Box("FrameR",  new Vector3(0, 0, -0.02f), new Vector3(0.06f, 1.8f, 0.06f), iron,  g.transform, col: false);
        Box("FrameT",  new Vector3(0, 0.95f, 0 ), new Vector3(0.06f, 0.06f,1.2f),  iron,  g.transform, col: false);
        Box("CrossH",  new Vector3(0, 0.2f, 0  ), new Vector3(0.04f, 0.04f,1.1f),  brass, g.transform, col: false);
        Box("CrossV",  new Vector3(0, 0,    0  ), new Vector3(0.04f, 1.6f, 0.04f), brass, g.transform, col: false);
        var glassMat = M(new Color(0.04f, 0.06f, 0.10f), 0f, 0.6f);
        Box("Glass",   new Vector3(0, 0.1f, 0  ), new Vector3(0.02f, 1.7f, 1.05f), glassMat, g.transform, col: false);
    }

    // ── Säulen ───────────────────────────────────────────────────────────────

    private void BuildPillars(Transform root, Material stone, Material stoneLight, Material brass)
    {
        float[] pillarZ = { -3.5f, 0.5f, 4.0f };
        float[] pillarX = { -3.5f, 3.5f };

        foreach (float pz in pillarZ)
        foreach (float px in pillarX)
        {
            var g = new GameObject($"Pillar_{px}_{pz}");
            g.transform.position = new Vector3(px, 0f, pz);
            g.transform.SetParent(root);

            // Basis
            Box("Base",  new Vector3(0, 0.15f, 0), new Vector3(0.45f, 0.30f, 0.45f), stoneLight, g.transform);
            // Schaft
            Box("Shaft", new Vector3(0, 3.0f,  0), new Vector3(0.30f, 5.40f, 0.30f), stone, g.transform);
            // Kapitell
            Box("Cap",   new Vector3(0, 5.85f, 0), new Vector3(0.48f, 0.30f, 0.48f), stoneLight, g.transform);
            // Messingring
            Cyl("Ring", new Vector3(0, 1.2f, 0), new Vector3(0.18f, 0.04f, 0.18f), brass, g.transform);
        }
    }

    // ── Bücherregale ─────────────────────────────────────────────────────────

    private void BuildBookShelves(Transform root, Material dark, Material mid, Material light,
                                  Material bRed, Material bGreen, Material bBlue, Material bBrown,
                                  Material parchment)
    {
        var bookMats = new[] { bRed, bGreen, bBlue, bBrown, mid, dark, light };

        // Linke Wand: 3 Regale
        BuildShelf(new Vector3(-4.5f, 0f,  2.5f), Quaternion.Euler(0,  90f, 0), root, dark, mid, bookMats);
        BuildShelf(new Vector3(-4.5f, 0f, -0.5f), Quaternion.Euler(0,  90f, 0), root, dark, mid, bookMats);
        BuildShelf(new Vector3(-4.5f, 0f, -3.5f), Quaternion.Euler(0,  90f, 0), root, dark, mid, bookMats);

        // Rechte Wand: 3 Regale
        BuildShelf(new Vector3( 4.5f, 0f,  2.5f), Quaternion.Euler(0, -90f, 0), root, dark, mid, bookMats);
        BuildShelf(new Vector3( 4.5f, 0f, -0.5f), Quaternion.Euler(0, -90f, 0), root, dark, mid, bookMats);
        BuildShelf(new Vector3( 4.5f, 0f, -3.5f), Quaternion.Euler(0, -90f, 0), root, dark, mid, bookMats);

        // Freistehende Mittelregal-Reihe (zwischen Lesetisch und Thresen)
        BuildShelf(new Vector3(-2.0f, 0f, -0.5f), Quaternion.identity, root, dark, mid, bookMats);
        BuildShelf(new Vector3( 2.0f, 0f, -0.5f), Quaternion.identity, root, dark, mid, bookMats);
    }

    private void BuildShelf(Vector3 pos, Quaternion rot, Transform root, Material wood, Material trim,
                             Material[] bookMats)
    {
        var g = new GameObject("Bookshelf");
        g.transform.position = pos; g.transform.rotation = rot; g.transform.SetParent(root);

        Box("Left",   new Vector3(-0.9f, 2.0f, 0),    new Vector3(0.07f, 4.0f, 0.35f), wood, g.transform);
        Box("Right",  new Vector3( 0.9f, 2.0f, 0),    new Vector3(0.07f, 4.0f, 0.35f), wood, g.transform);
        Box("Back",   new Vector3(0, 2.0f, 0.14f),     new Vector3(1.80f, 4.0f, 0.04f), trim, g.transform, col: false);
        Box("Top",    new Vector3(0, 4.04f, 0),        new Vector3(1.87f, 0.07f, 0.35f), wood, g.transform);
        Box("Bottom", new Vector3(0, 0.04f, 0),        new Vector3(1.87f, 0.07f, 0.35f), wood, g.transform);

        // Zierleiste oben
        Box("TopMold", new Vector3(0, 4.10f, -0.05f), new Vector3(1.90f, 0.06f, 0.06f), trim, g.transform, col: false);

        float[] shelfY = { 0.65f, 1.30f, 1.95f, 2.60f, 3.25f };
        foreach (float sy in shelfY)
        {
            Box($"Board_{sy}", new Vector3(0, sy, 0), new Vector3(1.80f, 0.05f, 0.33f), wood, g.transform, col: false);
            PlaceBooks(g.transform, sy + 0.05f, bookMats);
        }

        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 2.0f, 0); bc.size = new Vector3(1.87f, 4.1f, 0.38f);
    }

    private void PlaceBooks(Transform shelf, float y, Material[] mats)
    {
        int count  = Random.Range(8, 14);
        float x    = -0.82f;
        for (int i = 0; i < count && x < 0.82f; i++)
        {
            float w    = Random.Range(0.06f, 0.11f);
            float h    = Random.Range(0.22f, 0.34f);
            float tilt = Random.Range(-5f, 5f);
            Box($"Book_{i}", new Vector3(x + w * 0.5f, y + h * 0.5f, -0.02f),
                new Vector3(w, h, 0.22f), mats[i % mats.Length], shelf,
                Quaternion.Euler(0, 0, tilt), false);
            x += w + Random.Range(0.005f, 0.012f);
        }
    }

    // ── Thresen mit Bücherregal (Helios-Bereich) ──────────────────────────────

    private void BuildThresenArea(Transform root, Material dark, Material mid, Material light,
                                   Material brass, Material parchment, Material wax, Material flame,
                                   Material leather, Material bRed, Material bGreen, Material bBlue,
                                   Material bBrown)
    {
        var bookMats = new[] { bRed, bGreen, bBlue, bBrown, mid };

        // ── Thresen (Bibliothekstheke) ──────────────────────────────────────
        var theke = new GameObject("Librarian_Theke");
        theke.transform.position = new Vector3(2.0f, 0f, 2.6f);
        theke.transform.SetParent(root);

        // Thekenoberplatte
        Box("Top",       new Vector3(0, 0.94f, 0),      new Vector3(2.8f, 0.07f, 0.70f), mid,  theke.transform);
        // Überhang vorne (Spielerseite)
        Box("FrontEdge", new Vector3(0, 0.97f, -0.38f), new Vector3(2.8f, 0.06f, 0.06f), light, theke.transform, col: false);
        // Thekenkorpus
        Box("Front",     new Vector3(0, 0.47f, -0.35f), new Vector3(2.8f, 0.94f, 0.07f), dark, theke.transform);
        Box("Left",      new Vector3(-1.37f,0.47f,0),   new Vector3(0.07f,0.94f, 0.70f), dark, theke.transform);
        Box("Right",     new Vector3( 1.37f,0.47f,0),   new Vector3(0.07f,0.94f, 0.70f), dark, theke.transform);
        Box("Back",      new Vector3(0, 0.47f, 0.32f),  new Vector3(2.8f, 0.94f, 0.07f), dark, theke.transform);

        // Holzpaneel-Zierleisten an der Thekenvorderseite
        Box("Panel1", new Vector3(-0.9f,0.47f,-0.32f), new Vector3(0.80f, 0.85f, 0.02f), mid, theke.transform, col: false);
        Box("Panel2", new Vector3( 0.0f,0.47f,-0.32f), new Vector3(0.80f, 0.85f, 0.02f), mid, theke.transform, col: false);
        Box("Panel3", new Vector3( 0.9f,0.47f,-0.32f), new Vector3(0.80f, 0.85f, 0.02f), mid, theke.transform, col: false);

        // Dekoration auf der Theke
        Box("Parchment1", new Vector3(-0.5f, 0.97f,  0.0f), new Vector3(0.32f, 0.005f, 0.42f), parchment, theke.transform, col: false);
        Box("Parchment2", new Vector3( 0.3f, 0.97f, -0.05f),new Vector3(0.28f, 0.005f, 0.36f), parchment, theke.transform,
            Quaternion.Euler(0, 15f, 0), false);
        // Tintenfass
        Cyl("InkBase",  new Vector3(0.9f, 0.975f,  0.05f), new Vector3(0.07f,0.05f,0.07f), dark,  theke.transform);
        Cyl("InkTop",   new Vector3(0.9f, 1.010f,  0.05f), new Vector3(0.055f,0.03f,0.055f), brass, theke.transform);
        // Kerze auf Theke
        BuildCandle(new Vector3(-0.9f, 0.975f, 0.0f), theke.transform, wax, flame, brass);
        AddLight("ThekeCandle", theke.transform, new Vector3(-0.9f, 1.15f, 0f),
            LightType.Point, new Color(1f, 0.68f, 0.28f), 1.0f, 3.5f, shadows: LightShadows.Soft);

        // Aufgeschlagenes Buch
        Box("BookL",    new Vector3(0.1f, 0.972f, -0.05f), new Vector3(0.24f, 0.006f, 0.30f), parchment, theke.transform,
            Quaternion.Euler(0, -5f, -3f), false);
        Box("BookR",    new Vector3(0.32f,0.972f, -0.05f), new Vector3(0.24f, 0.006f, 0.30f), parchment, theke.transform,
            Quaternion.Euler(0,  5f,  3f), false);
        Box("BookSpine",new Vector3(0.21f,0.974f, -0.05f), new Vector3(0.03f, 0.008f, 0.30f), leather, theke.transform, col: false);

        var bc = theke.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.47f, 0); bc.size = new Vector3(2.85f, 0.96f, 0.76f);

        // ── Bücherregal hinter Helios (Rückwandregal) ──────────────────────
        // Langes breites Regal direkt an der Rückwand, zentriert
        BuildBackWallShelf(new Vector3(-2.8f, 0f, 5.4f), root, dark, mid, bookMats, 1.8f);
        BuildBackWallShelf(new Vector3( 0.0f, 0f, 5.4f), root, dark, mid, bookMats, 1.8f);
        BuildBackWallShelf(new Vector3( 2.8f, 0f, 5.4f), root, dark, mid, bookMats, 1.8f);

        // Kleines Regal direkt hinter Helios-Theke (an der Thekenwand)
        BuildCounterShelf(new Vector3(1.5f, 0f, 3.2f), root, dark, mid, bookMats);
        BuildCounterShelf(new Vector3(2.8f, 0f, 3.2f), root, dark, mid, bookMats);
    }

    private void BuildBackWallShelf(Vector3 pos, Transform root, Material wood, Material trim,
                                     Material[] bookMats, float width = 1.8f)
    {
        var g = new GameObject("BackWallShelf");
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, 180f, 0);
        g.transform.SetParent(root);

        Box("Left",   new Vector3(-width*0.5f, 2.5f, 0), new Vector3(0.07f, 5.0f, 0.32f), wood, g.transform);
        Box("Right",  new Vector3( width*0.5f, 2.5f, 0), new Vector3(0.07f, 5.0f, 0.32f), wood, g.transform);
        Box("Back",   new Vector3(0, 2.5f, 0.12f),       new Vector3(width + 0.07f, 5.0f, 0.04f), trim, g.transform, col: false);
        Box("Top",    new Vector3(0, 5.07f, 0),          new Vector3(width + 0.14f, 0.07f, 0.32f), wood, g.transform);
        Box("Bottom", new Vector3(0, 0.04f, 0),          new Vector3(width + 0.14f, 0.07f, 0.32f), wood, g.transform);

        float[] shelfY = { 0.65f, 1.30f, 1.95f, 2.60f, 3.25f, 3.90f };
        foreach (float sy in shelfY)
        {
            Box($"Board_{sy}", new Vector3(0, sy, 0), new Vector3(width, 0.05f, 0.30f), wood, g.transform, col: false);
            float bx = -(width * 0.48f);
            int cnt = Random.Range(7, 13);
            for (int i = 0; i < cnt && bx < width * 0.48f; i++)
            {
                float bw = Random.Range(0.07f, 0.12f);
                float bh = Random.Range(0.20f, 0.32f);
                Box($"B_{sy}_{i}", new Vector3(bx + bw*0.5f, sy + bh*0.5f, -0.02f),
                    new Vector3(bw, bh, 0.20f), bookMats[i % bookMats.Length], g.transform,
                    Quaternion.Euler(0, 0, Random.Range(-4f, 4f)), false);
                bx += bw + Random.Range(0.005f, 0.01f);
            }
        }

        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 2.5f, 0); bc.size = new Vector3(width + 0.14f, 5.1f, 0.36f);
    }

    private void BuildCounterShelf(Vector3 pos, Transform root, Material wood, Material trim, Material[] bookMats)
    {
        var g = new GameObject("CounterShelf");
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, 180f, 0);
        g.transform.SetParent(root);

        Box("Left",   new Vector3(-0.55f, 1.3f, 0), new Vector3(0.06f, 2.6f, 0.28f), wood, g.transform);
        Box("Right",  new Vector3( 0.55f, 1.3f, 0), new Vector3(0.06f, 2.6f, 0.28f), wood, g.transform);
        Box("Back",   new Vector3(0, 1.3f, 0.10f),  new Vector3(1.10f, 2.6f, 0.04f), trim, g.transform, col: false);
        Box("Top",    new Vector3(0, 2.63f, 0),      new Vector3(1.16f, 0.06f, 0.28f), wood, g.transform);
        Box("Bottom", new Vector3(0, 1.04f, 0),      new Vector3(1.16f, 0.06f, 0.28f), wood, g.transform);

        float[] sy2 = { 1.45f, 1.90f, 2.35f };
        foreach (float sy in sy2)
        {
            Box($"Brd_{sy}", new Vector3(0, sy, 0), new Vector3(1.04f, 0.04f, 0.26f), wood, g.transform, col: false);
            float bx = -0.46f;
            for (int i = 0; i < 6 && bx < 0.46f; i++)
            {
                float bw = Random.Range(0.07f, 0.11f);
                float bh = Random.Range(0.17f, 0.26f);
                Box($"B_{sy}_{i}", new Vector3(bx + bw*0.5f, sy + bh*0.5f, -0.01f),
                    new Vector3(bw, bh, 0.18f), bookMats[i % bookMats.Length], g.transform,
                    Quaternion.Euler(0, 0, Random.Range(-3f, 3f)), false);
                bx += bw + 0.007f;
            }
        }

        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 1.8f, 0); bc.size = new Vector3(1.16f, 2.7f, 0.30f);
    }

    // ── Lesebereich ──────────────────────────────────────────────────────────

    private void BuildReadingArea(Transform root, Material dark, Material mid, Material leather,
                                   Material wax, Material flame, Material brass)
    {
        var table = new GameObject("ReadingTable");
        table.transform.position = new Vector3(0f, 0f, -2.5f);
        table.transform.SetParent(root);

        Box("TabTop",  new Vector3(0, 0.82f, 0),    new Vector3(3.0f, 0.07f, 1.1f),  dark, table.transform);
        Box("TabLeg1", new Vector3(-1.35f,0.41f,-0.46f), new Vector3(0.08f,0.82f,0.08f), mid, table.transform);
        Box("TabLeg2", new Vector3( 1.35f,0.41f,-0.46f), new Vector3(0.08f,0.82f,0.08f), mid, table.transform);
        Box("TabLeg3", new Vector3(-1.35f,0.41f, 0.46f), new Vector3(0.08f,0.82f,0.08f), mid, table.transform);
        Box("TabLeg4", new Vector3( 1.35f,0.41f, 0.46f), new Vector3(0.08f,0.82f,0.08f), mid, table.transform);
        Box("TabStrut",new Vector3(0, 0.28f, 0),    new Vector3(2.6f, 0.05f, 0.07f),  mid, table.transform, col: false);

        // Stühle
        float[] chairX = { -1.0f, 0f, 1.0f };
        foreach (float cx in chairX)
        {
            BuildChair(new Vector3(cx, 0f, -4.1f), Quaternion.identity, root, dark, leather);
            BuildChair(new Vector3(cx, 0f, -1.0f), Quaternion.Euler(0, 180f, 0), root, dark, leather);
        }

        // Bücher auf dem Tisch
        var parchment = M(new Color(0.80f, 0.73f, 0.56f), 0.01f, 0.08f);
        Box("TbBook1",  new Vector3(-0.8f, 0.86f, 0.1f), new Vector3(0.22f,0.006f,0.28f), parchment, table.transform,
            Quaternion.Euler(0, 20f, 0), false);
        Box("TbBook2",  new Vector3( 0.5f, 0.86f,-0.1f), new Vector3(0.18f,0.006f,0.24f), parchment, table.transform,
            Quaternion.Euler(0,-15f, 0), false);

        BuildCandle(new Vector3(-1.1f, 0.855f, 0f), table.transform, wax, flame, brass);
        BuildCandle(new Vector3( 1.1f, 0.855f, 0f), table.transform, wax, flame, brass);
        AddLight("TableCandle", table.transform, new Vector3(0, 1.2f, 0),
            LightType.Point, new Color(1f, 0.68f, 0.28f), 1.5f, 4.5f, shadows: LightShadows.Soft);

        var bc = table.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.41f, 0); bc.size = new Vector3(3.05f, 0.84f, 1.14f);
    }

    private void BuildChair(Vector3 pos, Quaternion rot, Transform root, Material wood, Material leather)
    {
        var g = new GameObject("Chair");
        g.transform.position = pos; g.transform.rotation = rot; g.transform.SetParent(root);
        Box("Seat",  new Vector3(0, 0.46f,  0   ), new Vector3(0.44f, 0.05f, 0.42f), wood,    g.transform);
        Box("Pad",   new Vector3(0, 0.488f, 0   ), new Vector3(0.36f, 0.03f, 0.35f), leather, g.transform, col: false);
        Box("Back",  new Vector3(0, 0.80f,  0.19f), new Vector3(0.42f, 0.60f, 0.05f), wood,   g.transform);
        Box("BackP", new Vector3(0, 0.80f,  0.16f), new Vector3(0.36f, 0.52f, 0.03f), leather,g.transform, col: false);
        foreach (var (lx, lz) in new[]{(-0.18f,-0.18f),(0.18f,-0.18f),(-0.18f,0.18f),(0.18f,0.18f)})
            Box("Leg", new Vector3(lx, 0.23f, lz), new Vector3(0.04f, 0.46f, 0.04f), wood, g.transform);
        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.46f, 0.05f); bc.size = new Vector3(0.46f, 0.95f, 0.52f);
    }

    // ── Kerze ────────────────────────────────────────────────────────────────

    private void BuildCandle(Vector3 pos, Transform parent, Material wax, Material flame, Material brass)
    {
        var g = new GameObject("Candle"); g.transform.position = pos; g.transform.SetParent(parent);
        Cyl("Stick",  new Vector3(0, 0.08f,  0), new Vector3(0.026f,0.14f,0.026f), wax,   g.transform);
        Cyl("Holder", new Vector3(0,-0.01f,  0), new Vector3(0.052f,0.02f,0.052f), brass, g.transform);
        Cyl("Flame",  new Vector3(0, 0.165f, 0), new Vector3(0.014f,0.04f,0.014f), flame, g.transform);
    }

    // ── Beleuchtung ──────────────────────────────────────────────────────────

    private void BuildLighting(Transform root, Material brass, Material wax, Material flame)
    {
        BuildChandelier(new Vector3(0f,    5.5f, 0.5f), root, brass, wax, flame);
        BuildChandelier(new Vector3(-3.2f, 5.5f, 3.5f), root, brass, wax, flame);
        BuildChandelier(new Vector3( 3.2f, 5.5f, 3.5f), root, brass, wax, flame);

        BuildWallTorch(new Vector3(-4.75f, 2.8f,  2.5f), Quaternion.Euler(0,  90f, 0), root, brass, flame);
        BuildWallTorch(new Vector3(-4.75f, 2.8f, -1.0f), Quaternion.Euler(0,  90f, 0), root, brass, flame);
        BuildWallTorch(new Vector3( 4.75f, 2.8f,  2.5f), Quaternion.Euler(0, -90f, 0), root, brass, flame);
        BuildWallTorch(new Vector3( 4.75f, 2.8f, -1.0f), Quaternion.Euler(0, -90f, 0), root, brass, flame);
        BuildWallTorch(new Vector3( 0.0f,  2.8f,  5.75f), Quaternion.Euler(0,180f, 0), root, brass, flame);
        BuildWallTorch(new Vector3(-4.75f, 2.8f, -4.0f), Quaternion.Euler(0,  90f, 0), root, brass, flame);
        BuildWallTorch(new Vector3( 4.75f, 2.8f, -4.0f), Quaternion.Euler(0, -90f, 0), root, brass, flame);

        // Füll-Licht
        AddLight("AmbFill", root, new Vector3(0, 4f, 0), LightType.Directional,
            new Color(0.85f, 0.65f, 0.35f), 0.35f, 0f, rotation: Quaternion.Euler(90f, 0f, 0f));
    }

    private void BuildChandelier(Vector3 pos, Transform root, Material brass, Material wax, Material flame)
    {
        var g = new GameObject("Chandelier"); g.transform.position = pos; g.transform.SetParent(root);
        Box("Chain", new Vector3(0, 0.4f, 0), new Vector3(0.03f, 0.8f, 0.03f), brass, g.transform, col: false);
        Cyl("Ring",  new Vector3(0, 0,    0), new Vector3(0.70f, 0.04f, 0.70f), brass, g.transform);
        Cyl("Inner", new Vector3(0, 0,    0), new Vector3(0.54f, 0.045f,0.54f), M(new Color(0.12f,0.08f,0.04f)), g.transform);
        for (int i = 0; i < 6; i++)
        {
            float a  = i * 60f * Mathf.Deg2Rad;
            float cx = Mathf.Sin(a) * 0.30f;
            float cz = Mathf.Cos(a) * 0.30f;
            BuildCandle(new Vector3(cx, -0.02f, cz), g.transform, wax, flame, brass);
            AddLight($"CL_{i}", g.transform, new Vector3(cx, 0.15f, cz),
                LightType.Point, new Color(1f, 0.70f, 0.32f), 1.2f, 5f, shadows: LightShadows.Soft);
        }
    }

    private void BuildWallTorch(Vector3 pos, Quaternion rot, Transform root, Material brass, Material flame)
    {
        var g = new GameObject("WallTorch"); g.transform.position = pos; g.transform.rotation = rot; g.transform.SetParent(root);
        Box("Bracket", new Vector3(0, 0, -0.12f), new Vector3(0.06f,0.06f,0.24f), brass, g.transform, col: false);
        Cyl("Cup",     new Vector3(0, 0,    0   ), new Vector3(0.10f,0.06f,0.10f), brass, g.transform);
        Cyl("Flame",   new Vector3(0, 0.07f, 0  ), new Vector3(0.06f,0.09f,0.06f), flame, g.transform);
        AddLight("TL", g.transform, new Vector3(0, 0.18f, 0),
            LightType.Point, new Color(1f, 0.60f, 0.18f), 1.8f, 6.5f, shadows: LightShadows.Soft);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Spieler
    // ═══════════════════════════════════════════════════════════════════════

    private GameObject AddPlayer(Scene scene)
    {
        var idleModel    = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Big Yahu standing.fbx");
        var runningModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Big Yahu jogging.fbx");
        var playerMat    = AssetDatabase.LoadAssetAtPath<Material>("Assets/Big Yahu/Big Yahu material.mat");

        var character = new GameObject("BigYahu") { tag = "Player" };
        character.transform.position = new Vector3(0f, 0f, -4.8f);

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
                    r.sharedMaterial = playerMat;
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
        AnimationClip src = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { src = c; break; }
        if (src == null) return;

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);

        var loop = Object.Instantiate(src);
        loop.name = stateName + "_Loop";
        var s = AnimationUtility.GetAnimationClipSettings(loop);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(loop, s);
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
    // Helios NPC + Buch-Puzzle
    // ═══════════════════════════════════════════════════════════════════════

    private void AddHeliosSetup(Scene scene)
    {
        // Materialien für den Thresen-Bereich
        var dark      = M(new Color(0.18f, 0.11f, 0.06f), 0.01f, 0.10f);
        var mid       = M(new Color(0.26f, 0.17f, 0.08f), 0.01f, 0.12f);
        var light     = M(new Color(0.36f, 0.24f, 0.12f), 0.01f, 0.14f);
        var brass     = M(new Color(0.52f, 0.40f, 0.14f), 0.55f, 0.35f);
        var parchment = M(new Color(0.80f, 0.73f, 0.56f), 0.01f, 0.08f);
        var wax       = M(new Color(0.90f, 0.86f, 0.70f), 0.01f, 0.06f);
        var flame     = Emit(new Color(0.9f, 0.7f, 0.2f), new Color(1f, 0.65f, 0.1f), 3f);
        var leather   = M(new Color(0.16f, 0.09f, 0.04f), 0.02f, 0.08f);
        var bRed      = M(new Color(0.42f, 0.08f, 0.06f), 0.01f, 0.12f);
        var bGreen    = M(new Color(0.10f, 0.26f, 0.12f), 0.01f, 0.12f);
        var bBlue     = M(new Color(0.08f, 0.12f, 0.32f), 0.01f, 0.12f);
        var bBrown    = M(new Color(0.30f, 0.16f, 0.07f), 0.01f, 0.10f);

        var envRoot = GameObject.Find("Environment")?.transform
                    ?? new GameObject("Environment").transform;
        BuildThresenArea(envRoot, dark, mid, light, brass, parchment, wax, flame, leather,
                         bRed, bGreen, bBlue, bBrown);

        // ── Helios instanziieren ──────────────────────────────────────────────
        const string fbxPath = "Assets/Big Yahu/Helios plotting.fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        GameObject helios;
        if (prefab != null)
        {
            helios = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(helios, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        else
        {
            helios = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Debug.LogWarning("[Level3] 'Helios plotting.fbx' nicht gefunden – Capsule als Platzhalter.");
        }
        helios.name = "Helios";

        // Skalierung: Zielgröße ~1.7m (gleich wie Big Yahu)
        var bounds = GetBounds(helios);
        float h     = bounds.size.y;
        float scale = (h > 0.1f) ? (1.7f / h) : 1f;
        helios.transform.localScale = Vector3.one * scale;

        // Direkt vor dem Spieler-Spawn, vor dem Wandregal
        helios.transform.position = new Vector3(0f, 0f, -3.0f);
        helios.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // schaut zur Kamera/Eingang

        // Material zuweisen
        var heliosMat = CreateHeliosMaterial();
        foreach (var r in helios.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterial = heliosMat;

        // Animation auf Loop
        SetupHeliosAnimation(helios, fbxPath);

        // Spot auf Helios
        var spotGO = new GameObject("HeliosSpot");
        spotGO.transform.position = new Vector3(0f, 4.5f, -3.0f);
        var spot = spotGO.AddComponent<Light>();
        spot.type      = LightType.Spot;
        spot.color     = new Color(0.95f, 0.88f, 0.65f);
        spot.intensity = 2.5f;
        spot.range     = 6f;
        spot.spotAngle = 35f;
        spot.shadows   = LightShadows.Soft;
        spotGO.transform.LookAt(helios.transform.position + Vector3.up * 0.9f);
        SceneManager.MoveGameObjectToScene(spotGO, scene);

        // ── Ausgangs-Trigger (inaktiv bis Bibel gewählt) ──────────────────────
        var exitGO  = new GameObject("ExitTrigger");
        exitGO.transform.position = new Vector3(0f, 1.2f, 5.5f);
        var exitCol = exitGO.AddComponent<BoxCollider>();
        exitCol.isTrigger = true;
        exitCol.size = new Vector3(6f, 2.4f, 0.5f);
        var ltt = exitGO.AddComponent<LevelTransitionTrigger>();
        ltt.targetScene = "Level4";
        exitGO.SetActive(false);
        SceneManager.MoveGameObjectToScene(exitGO, scene);

        // ── Hinweis-Canvas (E-Taste) ──────────────────────────────────────────
        var hintGO = BuildHintUI(scene);

        // ── Buch-UI Canvas ────────────────────────────────────────────────────
        var bookUI = BuildBookUI(scene);

        // ── HeliosInteraction Trigger ─────────────────────────────────────────
        var interGO = new GameObject("HeliosInteraction");
        interGO.transform.position = new Vector3(0f, 1.0f, -3.8f);
        var interCol = interGO.AddComponent<BoxCollider>();
        interCol.isTrigger = true;
        interCol.size = new Vector3(2.5f, 2.2f, 2.0f);
        var interaction = interGO.AddComponent<HeliosInteraction>();
        interaction.bookUI        = bookUI;
        interaction.exitTriggerGO = exitGO;
        interaction.hintGO        = hintGO;
        SceneManager.MoveGameObjectToScene(interGO,   scene);
        SceneManager.MoveGameObjectToScene(helios,    scene);
    }

    private void SetupHeliosAnimation(GameObject helios, string fbxPath)
    {
        var modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (modelImporter != null && modelImporter.animationType != ModelImporterAnimationType.Legacy)
        {
            modelImporter.animationType = ModelImporterAnimationType.Legacy;
            modelImporter.SaveAndReimport();
        }

        AnimationClip src = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { src = c; break; }

        if (src == null) { Debug.LogWarning("[Level3] Kein AnimationClip in Helios-FBX."); return; }

        const string clipPath = "Assets/Big Yahu/Helios_Plot_Loop.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);

        var loop    = Object.Instantiate(src);
        loop.name   = "Helios_Plot_Loop";
        loop.legacy = true;
        var cfg = AnimationUtility.GetAnimationClipSettings(loop);
        cfg.loopTime = cfg.loopBlend = true;
        AnimationUtility.SetAnimationClipSettings(loop, cfg);
        AssetDatabase.CreateAsset(loop, clipPath);
        AssetDatabase.SaveAssets();
        loop = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

        var anim = helios.GetComponentInChildren<Animation>(true) ?? helios.AddComponent<Animation>();
        anim.AddClip(loop, "Plot");
        anim.clip              = loop;
        anim.playAutomatically = true;
        anim.wrapMode          = WrapMode.Loop;
        anim.Play("Plot");

        var existingAnimator = helios.GetComponentInChildren<Animator>(true);
        if (existingAnimator != null)
            existingAnimator.runtimeAnimatorController = null;
    }

    // ── E-Taste Hinweis ──────────────────────────────────────────────────────

    private GameObject BuildHintUI(Scene scene)
    {
        var canvasGO = new GameObject("HintCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode =
            UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var panel     = MakeUIPanel(canvasGO.transform, "HintPanel",
            new Vector2(0.3f, 0.12f), new Vector2(0.7f, 0.22f));
        var panelImg  = panel.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.65f);

        var txtGO = MakeUIPanel(panel.transform, "HintText", Vector2.zero, Vector2.one);
        var txt   = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text      = "[ E ]  Mit Helios sprechen";
        txt.fontSize  = 26;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color     = new Color(0.95f, 0.88f, 0.62f);

        canvasGO.SetActive(false);
        SceneManager.MoveGameObjectToScene(canvasGO, scene);
        return canvasGO;
    }

    // ── Buch-Auswahl Canvas ──────────────────────────────────────────────────

    private BookSelectionUI BuildBookUI(Scene scene)
    {
        var canvasGO = new GameObject("BookSelectionCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode =
            UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var bg = MakeUIBox(canvasGO.transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.78f);

        var titleGO  = MakeUIPanel(canvasGO.transform, "Title", new Vector2(0.1f, 0.68f), new Vector2(0.9f, 0.82f));
        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text      = "Helios reicht dir drei Bücher...";
        titleTxt.fontSize  = 36;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color     = new Color(0.92f, 0.85f, 0.62f);

        var buttons = new UnityEngine.UI.Button[3];
        var labels  = new TextMeshProUGUI[3];
        float[] btnX = { 0.10f, 0.38f, 0.66f };
        for (int i = 0; i < 3; i++)
        {
            var btnGO  = MakeUIPanel(canvasGO.transform, $"BookBtn_{i}",
                new Vector2(btnX[i], 0.35f), new Vector2(btnX[i] + 0.26f, 0.65f));
            var btnImg = btnGO.AddComponent<UnityEngine.UI.Image>();
            btnImg.color = new Color(0.22f, 0.14f, 0.07f);
            buttons[i]   = btnGO.AddComponent<UnityEngine.UI.Button>();

            var lbl = MakeUIPanel(btnGO.transform, "Label", Vector2.zero, Vector2.one);
            var txt = lbl.AddComponent<TextMeshProUGUI>();
            txt.fontSize  = 24;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color     = new Color(0.90f, 0.84f, 0.68f);
            labels[i]     = txt;
        }

        var fbGO  = MakeUIPanel(canvasGO.transform, "Feedback", new Vector2(0.1f, 0.25f), new Vector2(0.9f, 0.35f));
        var fbTxt = fbGO.AddComponent<TextMeshProUGUI>();
        fbTxt.fontSize  = 28;
        fbTxt.alignment = TextAlignmentOptions.Center;
        fbTxt.color     = Color.white;

        var uiGO = new GameObject("BookSelectionUI");
        var ui   = uiGO.AddComponent<BookSelectionUI>();
        ui.overlayCanvas = canvas;
        ui.bookButtons   = buttons;
        ui.bookLabels    = labels;
        ui.feedbackText  = fbTxt;

        SceneManager.MoveGameObjectToScene(canvasGO, scene);
        SceneManager.MoveGameObjectToScene(uiGO,     scene);
        return ui;
    }

    private GameObject MakeUIBox(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                  Vector2 offsetMin, Vector2 offsetMax)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        return go;
    }

    private GameObject MakeUIPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        => MakeUIBox(parent, name, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

    // ═══════════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════════

    private Bounds GetBounds(GameObject go)
    {
        var rens = go.GetComponentsInChildren<Renderer>();
        if (rens.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        var b = rens[0].bounds;
        foreach (var r in rens) b.Encapsulate(r.bounds);
        return b;
    }

    private void AddBackgroundMusic(Scene scene)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Big Yahu/Untitled.mp3");
        if (clip == null) { Debug.LogWarning("[Music] Untitled.mp3 nicht gefunden."); return; }
        var go  = new GameObject("BackgroundMusic");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip; src.loop = true; src.playOnAwake = true;
        src.volume = 0.6f; src.spatialBlend = 0f;
        go.AddComponent<BackgroundMusic>();
        SceneManager.MoveGameObjectToScene(go, scene);
    }
}
