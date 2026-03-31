using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class BuildLevel1PrisonCell : EditorWindow
{
    private int barCount = 24;

    [MenuItem("Tools/Build Level 1 Prison Cell (Detailed)")]
    public static void ShowWindow() => GetWindow<BuildLevel1PrisonCell>("Cell Builder");

    void OnGUI()
    {
        GUILayout.Label("Level 1 Builder", EditorStyles.boldLabel);
        GUILayout.Space(10);
        barCount = EditorGUILayout.IntSlider("Gitterstab-Anzahl", barCount, 10, 50);
        GUILayout.Space(20);
        if (GUILayout.Button("Gefängniszelle bauen", GUILayout.Height(30)))
            BuildLevel1();
    }

    // ─── Scene Setup ─────────────────────────────────────────────────────────

    private void BuildLevel1()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level1.unity");
        Debug.Log("Baue Level 1 Gefängniszelle...");

        GameObject environmentRoot = new GameObject("Environment");
        SceneManager.MoveGameObjectToScene(environmentRoot, scene);

        var (numpadPanel, hintText) = BuildUI(scene);
        GameObject exitDoor = BuildCellEnvironment(environmentRoot.transform, numpadPanel, hintText);

        // Puzzle-Handler auf NumpadPanel: prüft Code 1642 und öffnet Tür
        CellPuzzleHandler handler = numpadPanel.AddComponent<CellPuzzleHandler>();
        if (exitDoor != null)
            handler.exitDoor = exitDoor.GetComponent<DoorController>();

        // Kamera zuerst erstellen — garantiert vorhanden auch wenn BigYahu-Setup fehlschlägt
        GameObject camGO = new GameObject("Main Camera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(0.04f, 0.04f, 0.06f);
        cam.farClipPlane     = 30f;
        cam.nearClipPlane    = 0.1f;
        camGO.AddComponent<AudioListener>();
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0, 11f, 0);
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        TopDownCameraFollow follow = camGO.AddComponent<TopDownCameraFollow>();
        SceneManager.MoveGameObjectToScene(camGO, scene);

        GameObject bigYahu = AddBigYahuToScene(scene);
        if (bigYahu != null) follow.SetTarget(bigYahu.transform);

        // Weak directional fill — ensures the cell is never pitch-black
        GameObject fillGO = new GameObject("FillLight");
        fillGO.transform.rotation = Quaternion.Euler(45f, -55f, 0f);
        Light fill = fillGO.AddComponent<Light>();
        fill.type      = LightType.Directional;
        fill.color     = new Color(0.50f, 0.60f, 1f);
        fill.intensity = 0.28f;
        fill.shadows   = LightShadows.None;
        SceneManager.MoveGameObjectToScene(fillGO, scene);

        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.15f, 0.15f, 0.20f);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Level 1 fertig gebaut.");
    }

    // ─── Materials ───────────────────────────────────────────────────────────

    private Material CreateMaterial(Color color, float metallic = 0f, float smoothness = 0.25f)
    {
        Material mat = new(Shader.Find("Standard")) { color = color };
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", smoothness);
        return mat;
    }

    private Material CreateEmissiveMaterial(Color color, Color emission, float intensity = 1f)
    {
        Material mat = CreateMaterial(color, 0f, 0.1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emission * intensity);
        return mat;
    }

    private Material CreateTransparentMaterial(Color color, Color emission, float emissionIntensity = 0.4f)
    {
        Material mat = new(Shader.Find("Standard")) { color = color };
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emission * emissionIntensity);
        return mat;
    }

    // ─── Environment ─────────────────────────────────────────────────────────

    private GameObject BuildCellEnvironment(Transform root, GameObject numpadPanel, TextMeshProUGUI hintText)
    {
        GameObject exitDoorGO = null;
        Material concreteMat     = CreateMaterial(new Color(0.30f, 0.28f, 0.25f), 0.04f, 0.08f);
        Material darkConcreteMat = CreateMaterial(new Color(0.18f, 0.16f, 0.14f), 0.02f, 0.04f);
        Material darkMetalMat    = CreateMaterial(new Color(0.12f, 0.12f, 0.13f), 0.88f, 0.50f);
        Material floorMat        = CreateMaterial(new Color(0.22f, 0.20f, 0.18f), 0.02f, 0.08f);

        // ── Floor ──────────────────────────────────────────────────────────────
        CreateSolid("Floor", new Vector3(0, -0.1f, 0), new Vector3(5, 0.2f, 5), floorMat, root);

        // Floor tile grout lines (decorative)
        Material groutMat = CreateMaterial(new Color(0.13f, 0.12f, 0.11f), 0f, 0.03f);
        for (int i = -2; i <= 2; i++)
        {
            CreateDecor("GroutX", new Vector3(i * 1.0f, 0.005f, 0), new Vector3(0.025f, 0.005f, 5f), groutMat, root);
            CreateDecor("GroutZ", new Vector3(0, 0.005f, i * 1.0f), new Vector3(5f, 0.005f, 0.025f), groutMat, root);
        }

        // Floor drain (center-right)
        CreateDrain(new Vector3(0.8f, 0.001f, 0.5f), root);

        // Safety line painted on floor (yellow stripe near bars)
        Material yellowMat = CreateMaterial(new Color(0.70f, 0.60f, 0.08f), 0f, 0.2f);
        CreateDecor("SafetyLine", new Vector3(0, 0.003f, -1.9f), new Vector3(4.6f, 0.003f, 0.06f), yellowMat, root);

        // ── Walls ──────────────────────────────────────────────────────────────
        CreateSolid("BackWall",  new Vector3( 0,    2.5f,  2.5f), new Vector3(5f,   5f, 0.3f), concreteMat, root);
        CreateSolid("LeftWall",  new Vector3(-2.5f, 2.5f,  0f  ), new Vector3(0.3f, 5f, 5f  ), concreteMat, root);
        // Rechte Wand – mit Türöffnung bei z=0 (1.4 m breit, 2.4 m hoch)
        CreateSolid("RightWall_Front",  new Vector3(2.5f, 2.5f, -1.6f), new Vector3(0.3f, 5f,  1.8f), concreteMat, root);
        CreateSolid("RightWall_Back",   new Vector3(2.5f, 2.5f,  1.6f), new Vector3(0.3f, 5f,  1.8f), concreteMat, root);
        CreateSolid("RightWall_Lintel", new Vector3(2.5f, 3.7f,  0f  ), new Vector3(0.3f, 2.6f, 1.4f), concreteMat, root);

        // Tür (schiebt sich bei korrektem Code in -Z-Richtung hinter den vorderen Wandabschnitt)
        Material doorMat = CreateMaterial(new Color(0.22f, 0.17f, 0.12f), 0.12f, 0.22f);
        exitDoorGO = new GameObject("ExitDoor");
        exitDoorGO.transform.position = new Vector3(2.35f, 1.2f, 0f);
        exitDoorGO.transform.SetParent(root);
        GameObject doorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorPanel.transform.SetParent(exitDoorGO.transform, false);
        doorPanel.transform.localPosition = Vector3.zero;
        doorPanel.transform.localScale    = new Vector3(0.15f, 2.4f, 1.4f);
        doorPanel.GetComponent<Renderer>().material = doorMat;
        DoorController doorCtrl = exitDoorGO.AddComponent<DoorController>();
        doorCtrl.openDistance = 1.4f;
        doorCtrl.openDuration = 1.2f;

        // Stone block mortar lines
        AddMortarLines(root, darkConcreteMat);

        // Wall cracks (decorative — thin slanted slivers)
        AddWallCracks(root);

        // Metal door frame around bar opening
        AddBarFrame(darkMetalMat, root);

        // ── NO CEILING (top-down camera must see inside) ────────────────────
        // Exposed beams only — visible as dark stripes from above
        Material beamMat = CreateMaterial(new Color(0.15f, 0.13f, 0.11f), 0.02f, 0.04f);
        for (float bx = -1.5f; bx <= 1.5f; bx += 1.5f)
            CreateDecor("Beam", new Vector3(bx, 4.85f, 0), new Vector3(0.22f, 0.18f, 5.0f), beamMat, root);
        // Top-edge trim on walls (gives the "ceiling edge" impression without blocking camera)
        CreateDecor("TopTrimBack",  new Vector3(0,      5.05f, 2.35f), new Vector3(4.95f, 0.1f, 0.3f),  darkConcreteMat, root);
        CreateDecor("TopTrimLeft",  new Vector3(-2.35f, 5.05f, 0),     new Vector3(0.3f,  0.1f, 4.95f), darkConcreteMat, root);
        CreateDecor("TopTrimRight", new Vector3( 2.35f, 5.05f, 0),     new Vector3(0.3f,  0.1f, 4.95f), darkConcreteMat, root);

        // ── Iron bars ─────────────────────────────────────────────────────────
        CreateIronBars("CellBars", new Vector3(0, 0, -2.35f), 4.8f, 5f, barCount, darkMetalMat, root);

        // ── Window ─────────────────────────────────────────────────────────────
        CreateWindow(new Vector3(0, 3.5f, 2.45f), root);

        // ── Furniture ─────────────────────────────────────────────────────────
        CreateDetailedBed(new Vector3(-1.6f, 0f, 1.4f), root);
        CreateDetailedToilet(new Vector3(1.8f, 0f, 1.8f), root, numpadPanel, hintText);
        CreateBucket(new Vector3(1.28f, 0f, 1.22f), root);
        CreateDetailedTable(new Vector3(1.1f, 0f, -1.2f), root);
        CreateStool(new Vector3(0.25f, 0f, -1.2f), root);

        // ── Chain lamp ────────────────────────────────────────────────────────
        CreateChainLamp(new Vector3(0f, 5f, 0f), root);

        // ── Accessories ───────────────────────────────────────────────────────
        CreateWallShelf(new Vector3(-2.3f, 2.6f, -0.55f), root);
        CreateFoldedJumpsuit(new Vector3(-1.55f, 0.57f, 0.5f), root);
        CreateNewspaper(new Vector3(1.1f, 0.74f, -1.55f), root);
        CreateToothbrush(new Vector3(1.78f, 0.97f, 1.55f), root);

        // ── Pipes ─────────────────────────────────────────────────────────────
        Material pipeMat = CreateMaterial(new Color(0.30f, 0.26f, 0.22f), 0.62f, 0.28f);
        CreatePipeV(new Vector3(2.28f, 2.5f,  1.85f), 3.0f,  0.055f, pipeMat, root);
        CreatePipeH(new Vector3(1.1f,  4.70f,  2.28f), 2.4f, 0.048f, pipeMat, root);
        CreatePipeH(new Vector3(-0.9f, 4.58f, -2.22f), 2.0f, 0.048f, pipeMat, root);
        // Pipe elbow (cube connector)
        CreateDecor("PipeElbow", new Vector3(2.28f, 4.70f, 2.28f),
            new Vector3(0.11f, 0.11f, 0.11f), pipeMat, root);

        // ── Tally marks & graffiti ────────────────────────────────────────────
        CreateTallyMarks(new Vector3(-0.85f, 2.1f, 2.34f), root);

        // Loose stone on floor
        GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        stone.name = "Stone";
        stone.transform.position = new Vector3(-1.45f, 0.12f, -0.1f);
        stone.transform.localScale = new Vector3(0.27f, 0.17f, 0.22f);
        stone.GetComponent<Renderer>().material = CreateMaterial(new Color(0.42f, 0.39f, 0.35f), 0f, 0.08f);
        DestroyImmediate(stone.GetComponent<Collider>());
        MeshCollider sc = stone.AddComponent<MeshCollider>();
        sc.convex = true;
        stone.tag = "Stone";
        stone.transform.SetParent(root);

        return exitDoorGO;
    }

    // Creates a primitive with collider (structural / solid)
    private GameObject CreateSolid(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material = mat;
        go.transform.SetParent(parent);
        return go;
    }

    // Creates a primitive WITHOUT collider (decorative)
    private GameObject CreateDecor(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent,
                                   Quaternion? rot = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        if (rot.HasValue) go.transform.rotation = rot.Value;
        go.GetComponent<Renderer>().material = mat;
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent);
        return go;
    }

    // ─── Ceiling ─────────────────────────────────────────────────────────────

    // ─── Mortar lines ────────────────────────────────────────────────────────

    private void AddMortarLines(Transform root, Material mortarMat)
    {
        float[] hs = { 0.48f, 0.92f, 1.36f, 1.80f, 2.24f, 2.68f, 3.12f, 3.56f, 4.00f, 4.44f, 4.88f };

        foreach (float h in hs)
        {
            // Back wall
            CreateDecor("M_Back",  new Vector3(0,     h, 2.34f), new Vector3(4.85f, 0.035f, 0.015f), mortarMat, root);
            // Left wall
            CreateDecor("M_Left",  new Vector3(-2.34f, h, 0),   new Vector3(0.015f, 0.035f, 4.85f), mortarMat, root);
            // Right wall
            CreateDecor("M_Right", new Vector3(2.34f,  h, 0),   new Vector3(0.015f, 0.035f, 4.85f), mortarMat, root);
        }
    }

    // ─── Tally marks ─────────────────────────────────────────────────────────

    private void CreateTallyMarks(Vector3 pos, Transform root)
    {
        GameObject group = new GameObject("TallyMarks");
        group.transform.position = pos;
        group.transform.SetParent(root);

        Material scratchMat = CreateMaterial(new Color(0.50f, 0.46f, 0.41f), 0f, 0.04f);

        for (int i = 0; i < 4; i++)
        {
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.transform.SetParent(group.transform);
            mark.transform.localPosition = new Vector3(i * 0.09f - 0.135f, 0f, 0f);
            mark.transform.localScale = new Vector3(0.018f, 0.26f, 0.015f);
            mark.GetComponent<Renderer>().material = scratchMat;
            DestroyImmediate(mark.GetComponent<Collider>());
        }

        // Diagonal across all four
        GameObject diag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        diag.transform.SetParent(group.transform);
        diag.transform.localPosition = new Vector3(0.02f, 0f, 0f);
        diag.transform.localRotation = Quaternion.Euler(0f, 0f, 63f);
        diag.transform.localScale = new Vector3(0.018f, 0.40f, 0.015f);
        diag.GetComponent<Renderer>().material = scratchMat;
        DestroyImmediate(diag.GetComponent<Collider>());
    }

    // ─── Floor drain ─────────────────────────────────────────────────────────

    private void CreateDrain(Vector3 pos, Transform root)
    {
        Material grateMat = CreateMaterial(new Color(0.18f, 0.17f, 0.16f), 0.85f, 0.40f);
        // Drain recess
        CreateDecor("DrainBase", pos, new Vector3(0.20f, 0.002f, 0.20f), grateMat, root);
        // Grate bars (3 strips)
        for (int i = -1; i <= 1; i++)
            CreateDecor("DrainBar", pos + new Vector3(i * 0.06f, 0.004f, 0),
                new Vector3(0.018f, 0.004f, 0.18f), grateMat, root);
    }

    // ─── Bar door frame ──────────────────────────────────────────────────────

    private void AddBarFrame(Material mat, Transform root)
    {
        // Vertical posts on left and right of the bar opening
        CreateSolid("BarFrameLeft",  new Vector3(-2.35f, 2.5f, -2.35f), new Vector3(0.12f, 5f, 0.12f), mat, root);
        CreateSolid("BarFrameRight", new Vector3( 2.35f, 2.5f, -2.35f), new Vector3(0.12f, 5f, 0.12f), mat, root);
        // Header beam above bars
        CreateDecor("BarFrameTop", new Vector3(0, 5.05f, -2.35f), new Vector3(4.85f, 0.15f, 0.15f), mat, root);
        // Threshold at floor
        CreateDecor("BarFrameBot", new Vector3(0, 0.04f, -2.35f), new Vector3(4.85f, 0.08f, 0.12f), mat, root);
    }

    // ─── Wall cracks ─────────────────────────────────────────────────────────

    private void AddWallCracks(Transform root)
    {
        Material crackMat = CreateMaterial(new Color(0.12f, 0.11f, 0.10f), 0f, 0.02f);
        // Crack 1 — back wall lower right
        CreateDecor("Crack1a", new Vector3(1.0f, 0.8f, 2.34f),
            new Vector3(0.012f, 0.35f, 0.012f), crackMat, root,
            Quaternion.Euler(0f, 0f, 12f));
        CreateDecor("Crack1b", new Vector3(1.08f, 0.62f, 2.34f),
            new Vector3(0.010f, 0.20f, 0.010f), crackMat, root,
            Quaternion.Euler(0f, 0f, -8f));
        // Crack 2 — left wall mid
        CreateDecor("Crack2", new Vector3(-2.34f, 1.5f, 0.3f),
            new Vector3(0.012f, 0.28f, 0.012f), crackMat, root,
            Quaternion.Euler(0f, 0f, 20f));
    }

    // ─── Folded jumpsuit (near bed) ──────────────────────────────────────────

    private void CreateFoldedJumpsuit(Vector3 pos, Transform root)
    {
        // Orange prison jumpsuit, folded flat on the mattress
        Material jumpMat = CreateMaterial(new Color(0.75f, 0.38f, 0.05f), 0f, 0.06f);
        Material darkMat = CreateMaterial(new Color(0.55f, 0.27f, 0.03f), 0f, 0.05f);

        GameObject g = new GameObject("Jumpsuit");
        g.transform.position = pos;
        g.transform.SetParent(root);

        // Main fold (body of suit)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(g.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        body.transform.localScale    = new Vector3(0.38f, 0.04f, 0.28f);
        body.GetComponent<Renderer>().material = jumpMat;
        DestroyImmediate(body.GetComponent<Collider>());

        // Sleeve visible at side
        GameObject sleeve = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sleeve.transform.SetParent(g.transform);
        sleeve.transform.localPosition = new Vector3(0.22f, 0.02f, -0.04f);
        sleeve.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
        sleeve.transform.localScale    = new Vector3(0.22f, 0.025f, 0.10f);
        sleeve.GetComponent<Renderer>().material = jumpMat;
        DestroyImmediate(sleeve.GetComponent<Collider>());

        // Dark stripe detail
        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.transform.SetParent(g.transform);
        stripe.transform.localPosition = new Vector3(0f, 0.022f, 0f);
        stripe.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        stripe.transform.localScale    = new Vector3(0.06f, 0.008f, 0.28f);
        stripe.GetComponent<Renderer>().material = darkMat;
        DestroyImmediate(stripe.GetComponent<Collider>());
    }

    // ─── Newspaper on table ──────────────────────────────────────────────────

    private void CreateNewspaper(Vector3 pos, Transform root)
    {
        Material paperMat  = CreateMaterial(new Color(0.80f, 0.78f, 0.70f), 0f, 0.05f);
        Material printMat  = CreateMaterial(new Color(0.40f, 0.38f, 0.35f), 0f, 0.04f);

        GameObject g = new GameObject("Newspaper");
        g.transform.position = pos;
        g.transform.SetParent(root);

        // Paper sheet (slightly angled, realistic)
        GameObject sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sheet.transform.SetParent(g.transform);
        sheet.transform.localPosition = Vector3.zero;
        sheet.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
        sheet.transform.localScale    = new Vector3(0.32f, 0.005f, 0.24f);
        sheet.GetComponent<Renderer>().material = paperMat;
        DestroyImmediate(sheet.GetComponent<Collider>());

        // "Print lines" on paper (3 dark strips)
        for (int i = -1; i <= 1; i++)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.transform.SetParent(g.transform);
            line.transform.localPosition = new Vector3(0, 0.004f, i * 0.07f);
            line.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
            line.transform.localScale    = new Vector3(0.26f, 0.003f, 0.018f);
            line.GetComponent<Renderer>().material = printMat;
            DestroyImmediate(line.GetComponent<Collider>());
        }
    }

    // ─── Toothbrush on toilet tank ───────────────────────────────────────────

    private void CreateToothbrush(Vector3 pos, Transform root)
    {
        Material handleMat  = CreateMaterial(new Color(0.15f, 0.35f, 0.65f), 0f, 0.3f);
        Material bristleMat = CreateMaterial(new Color(0.90f, 0.90f, 0.88f), 0f, 0.1f);

        GameObject g = new GameObject("Toothbrush");
        g.transform.position = pos;
        g.transform.SetParent(root);

        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handle.transform.SetParent(g.transform);
        handle.transform.localPosition = Vector3.zero;
        handle.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
        handle.transform.localScale    = new Vector3(0.022f, 0.012f, 0.15f);
        handle.GetComponent<Renderer>().material = handleMat;
        DestroyImmediate(handle.GetComponent<Collider>());

        GameObject bristle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bristle.transform.SetParent(g.transform);
        bristle.transform.localPosition = new Vector3(0.01f, 0.008f, 0.06f);
        bristle.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
        bristle.transform.localScale    = new Vector3(0.018f, 0.01f, 0.04f);
        bristle.GetComponent<Renderer>().material = bristleMat;
        DestroyImmediate(bristle.GetComponent<Collider>());
    }

    // ─── Iron bars ───────────────────────────────────────────────────────────

    private void CreateIronBars(string name, Vector3 startPos, float width, float height,
                                int count, Material metalMat, Transform parent)
    {
        GameObject barGroup = new GameObject(name);
        barGroup.transform.position = startPos;
        barGroup.transform.SetParent(parent);

        // Bottom frame
        GameObject bf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bf.transform.SetParent(barGroup.transform);
        bf.transform.localPosition = new Vector3(0, 0.1f, 0);
        bf.transform.localScale = new Vector3(width, 0.18f, 0.18f);
        bf.GetComponent<Renderer>().material = metalMat;

        // Top frame
        GameObject tf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tf.transform.SetParent(barGroup.transform);
        tf.transform.localPosition = new Vector3(0, height - 0.1f, 0);
        tf.transform.localScale = new Vector3(width, 0.18f, 0.18f);
        tf.GetComponent<Renderer>().material = metalMat;

        // Mid cross-bar
        GameObject mf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mf.transform.SetParent(barGroup.transform);
        mf.transform.localPosition = new Vector3(0, height * 0.5f, 0);
        mf.transform.localScale = new Vector3(width, 0.10f, 0.14f);
        mf.GetComponent<Renderer>().material = metalMat;

        float startX = -width / 2f + 0.2f;
        float spacing = (width - 0.4f) / (count - 1);
        for (int i = 0; i < count; i++)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.transform.SetParent(barGroup.transform);
            bar.transform.localPosition = new Vector3(startX + i * spacing, height / 2f, 0);
            bar.transform.localScale = new Vector3(0.065f, height / 2f, 0.065f);
            bar.GetComponent<Renderer>().material = metalMat;
        }
    }

    // ─── Window ──────────────────────────────────────────────────────────────

    private void CreateWindow(Vector3 pos, Transform parent)
    {
        GameObject group = new GameObject("Window");
        group.transform.position = pos;
        group.transform.SetParent(parent);

        Material frameMat = CreateMaterial(new Color(0.16f, 0.15f, 0.14f), 0.1f, 0.08f);
        Material metalMat = CreateMaterial(new Color(0.09f, 0.08f, 0.07f), 0.92f, 0.52f);

        // Concrete frame
        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.transform.SetParent(group.transform);
        frame.transform.localPosition = Vector3.zero;
        frame.transform.localScale = new Vector3(1.4f, 1.0f, 0.4f);
        frame.GetComponent<Renderer>().material = frameMat;
        DestroyImmediate(frame.GetComponent<Collider>());

        // Dark glass
        GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glass.transform.SetParent(group.transform);
        glass.transform.localPosition = new Vector3(0, 0, 0.1f);
        glass.transform.localScale = new Vector3(1.2f, 0.8f, 0.04f);
        glass.GetComponent<Renderer>().material = CreateMaterial(new Color(0.03f, 0.05f, 0.11f), 0f, 0.95f);
        DestroyImmediate(glass.GetComponent<Collider>());

        // Window bars
        for (int i = -1; i <= 1; i++)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.transform.SetParent(group.transform);
            bar.transform.localPosition = new Vector3(i * 0.3f, 0, -0.08f);
            bar.transform.localScale = new Vector3(0.048f, 0.4f, 0.048f);
            bar.GetComponent<Renderer>().material = metalMat;
            DestroyImmediate(bar.GetComponent<Collider>());
        }

        // Moonlight
        GameObject moonGO = new GameObject("MoonLight");
        moonGO.transform.SetParent(group.transform);
        moonGO.transform.localPosition = new Vector3(0, 0, 2f);
        moonGO.transform.localRotation = Quaternion.Euler(25, 180, 0);
        Light ml = moonGO.AddComponent<Light>();
        ml.type = LightType.Directional;
        ml.color = new Color(0.50f, 0.62f, 1f);
        ml.intensity = 0.55f;
        ml.shadows = LightShadows.Soft;

        // Light shaft (semi-transparent emissive volume)
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "LightShaft";
        shaft.transform.SetParent(group.transform);
        shaft.transform.localPosition  = new Vector3(0, -1.3f, -1.6f);
        shaft.transform.localRotation  = Quaternion.Euler(22f, 0f, 0f);
        shaft.transform.localScale     = new Vector3(0.85f, 2.8f, 0.85f);
        shaft.GetComponent<Renderer>().material = CreateTransparentMaterial(
            new Color(0.72f, 0.86f, 1f, 0.07f),
            new Color(0.45f, 0.65f, 1f), 0.12f);
        DestroyImmediate(shaft.GetComponent<Collider>());
    }

    // ─── Bed ─────────────────────────────────────────────────────────────────

    private void CreateDetailedBed(Vector3 pos, Transform parent)
    {
        GameObject g = new GameObject("Bed");
        g.transform.position = pos;
        g.transform.SetParent(parent);

        Material metalMat    = CreateMaterial(new Color(0.20f, 0.20f, 0.22f), 0.78f, 0.38f);
        Material mattressMat = CreateMaterial(new Color(0.62f, 0.59f, 0.52f), 0f,   0.05f);
        Material blanketMat  = CreateMaterial(new Color(0.28f, 0.25f, 0.22f), 0f,   0.07f);
        Material pillowMat   = CreateMaterial(new Color(0.72f, 0.70f, 0.65f), 0f,   0.06f);

        // Frame
        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.transform.SetParent(g.transform);
        frame.transform.localPosition = new Vector3(0, 0.30f, 0);
        frame.transform.localScale    = new Vector3(1.2f, 0.08f, 2.2f);
        frame.GetComponent<Renderer>().material = metalMat;

        // Legs (4 corners)
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.transform.SetParent(g.transform);
                leg.transform.localPosition = new Vector3(x * 0.54f, 0.15f, z * 1.04f);
                leg.transform.localScale    = new Vector3(0.06f, 0.15f, 0.06f);
                leg.GetComponent<Renderer>().material = metalMat;
            }

        // Headboard slab
        GameObject hb = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hb.transform.SetParent(g.transform);
        hb.transform.localPosition = new Vector3(0, 0.72f, 1.06f);
        hb.transform.localScale    = new Vector3(1.18f, 0.82f, 0.09f);
        hb.GetComponent<Renderer>().material = metalMat;

        // Headboard top rail
        GameObject hbRail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hbRail.transform.SetParent(g.transform);
        hbRail.transform.localPosition = new Vector3(0, 1.10f, 1.06f);
        hbRail.transform.localScale    = new Vector3(1.18f, 0.07f, 0.13f);
        hbRail.GetComponent<Renderer>().material = metalMat;

        // Footboard
        GameObject fb = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fb.transform.SetParent(g.transform);
        fb.transform.localPosition = new Vector3(0, 0.56f, -1.06f);
        fb.transform.localScale    = new Vector3(1.18f, 0.52f, 0.08f);
        fb.GetComponent<Renderer>().material = metalMat;

        // Mattress
        GameObject mattress = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mattress.transform.SetParent(g.transform);
        mattress.transform.localPosition = new Vector3(0, 0.42f, 0.02f);
        mattress.transform.localScale    = new Vector3(1.1f, 0.13f, 2.0f);
        mattress.GetComponent<Renderer>().material = mattressMat;

        // Blanket main
        Material blanketCodeMat = CreateMaterial(new Color(0.22f, 0.20f, 0.18f), 0f, 0.07f);
        GameObject blanket = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blanket.transform.SetParent(g.transform);
        blanket.transform.localPosition = new Vector3(0, 0.52f, -0.08f);
        blanket.transform.localScale    = new Vector3(1.04f, 0.09f, 1.72f);
        blanket.GetComponent<Renderer>().material = blanketCodeMat;

        // "66A" — Häftlingsnummer groß auf der Decke aufgedruckt (Top-Down sichtbar)
        GameObject codeGO = new GameObject("BlanketCode_66A");
        codeGO.transform.SetParent(g.transform);
        codeGO.transform.localPosition = new Vector3(0f, 0.572f, -0.08f); // knapp über Decke
        codeGO.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);  // nach oben zeigend, korrekt ausgerichtet
        TextMeshPro tmp = codeGO.AddComponent<TextMeshPro>();
        tmp.text            = "66A";
        tmp.fontStyle       = FontStyles.Bold;
        tmp.alignment       = TextAlignmentOptions.Center;
        tmp.color           = new Color(0.68f, 0.60f, 0.45f); // verblasstes Aufdruckbeige
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin     = 0.5f;
        tmp.fontSizeMax     = 2.8f;
        RectTransform codeRect = codeGO.GetComponent<RectTransform>();
        codeRect.sizeDelta = new Vector2(0.92f, 0.40f);

        // Blanket bunched section (looks rumpled)
        GameObject bunch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bunch.transform.SetParent(g.transform);
        bunch.transform.localPosition = new Vector3(0.08f, 0.60f, -0.52f);
        bunch.transform.localRotation = Quaternion.Euler(4f, 3f, 0f);
        bunch.transform.localScale    = new Vector3(0.82f, 0.15f, 0.52f);
        bunch.GetComponent<Renderer>().material = blanketMat;

        // Pillow
        GameObject pillow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillow.transform.SetParent(g.transform);
        pillow.transform.localPosition = new Vector3(0, 0.52f, 0.76f);
        pillow.transform.localRotation = Quaternion.Euler(0f, 5f, 0f);
        pillow.transform.localScale    = new Vector3(0.78f, 0.09f, 0.37f);
        pillow.GetComponent<Renderer>().material = pillowMat;

        // Solid collider (whole bed)
        BoxCollider bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.40f, 0);
        bc.size   = new Vector3(1.2f, 0.80f, 2.2f);
    }

    // ─── Toilet ──────────────────────────────────────────────────────────────

    private void CreateDetailedToilet(Vector3 pos, Transform parent,
                                      GameObject numpadPanel, TextMeshProUGUI hintText)
    {
        GameObject g = new GameObject("Toilet");
        g.transform.position = pos;
        g.transform.SetParent(parent);

        Material ceramicMat = CreateMaterial(new Color(0.76f, 0.76f, 0.80f), 0.05f, 0.72f);
        Material metalMat   = CreateMaterial(new Color(0.52f, 0.52f, 0.55f), 0.88f, 0.62f);

        // Bowl base
        GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.transform.SetParent(g.transform);
        baseObj.transform.localPosition = new Vector3(0, 0.22f, 0);
        baseObj.transform.localScale    = new Vector3(0.32f, 0.22f, 0.4f);
        baseObj.GetComponent<Renderer>().material = ceramicMat;

        // Bowl rim
        GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.transform.SetParent(g.transform);
        rim.transform.localPosition = new Vector3(0, 0.44f, 0);
        rim.transform.localScale    = new Vector3(0.36f, 0.028f, 0.43f);
        rim.GetComponent<Renderer>().material = ceramicMat;

        // Tank body
        GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tank.transform.SetParent(g.transform);
        tank.transform.localPosition = new Vector3(0, 0.72f, 0.27f);
        tank.transform.localScale    = new Vector3(0.48f, 0.42f, 0.21f);
        tank.GetComponent<Renderer>().material = ceramicMat;

        // Tank lid
        GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.transform.SetParent(g.transform);
        lid.transform.localPosition = new Vector3(0, 0.94f, 0.27f);
        lid.transform.localScale    = new Vector3(0.50f, 0.04f, 0.23f);
        lid.GetComponent<Renderer>().material = ceramicMat;

        // Pipe
        GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipe.transform.SetParent(g.transform);
        pipe.transform.localPosition = new Vector3(0, 0.52f, 0.21f);
        pipe.transform.localScale    = new Vector3(0.05f, 0.21f, 0.05f);
        pipe.GetComponent<Renderer>().material = metalMat;

        // Solid collider
        BoxCollider solid = g.AddComponent<BoxCollider>();
        solid.isTrigger = false;
        solid.center    = new Vector3(0, 0.47f, 0.1f);
        solid.size      = new Vector3(0.60f, 0.94f, 0.65f);

        // Trigger collider (interaction zone)
        BoxCollider trigger = g.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center    = new Vector3(0, 1f, 0);
        trigger.size      = new Vector3(2f, 2f, 2f);

        ToiletInteraction interaction = g.AddComponent<ToiletInteraction>();
        interaction.numpad   = numpadPanel?.GetComponent<NumpadController>();
        interaction.hintText = hintText;
    }

    // ─── Bucket ──────────────────────────────────────────────────────────────

    private void CreateBucket(Vector3 pos, Transform parent)
    {
        GameObject g = new GameObject("Bucket");
        g.transform.position = pos;
        g.transform.SetParent(parent);

        Material mat = CreateMaterial(new Color(0.28f, 0.25f, 0.22f), 0.62f, 0.28f);

        // Body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.transform.SetParent(g.transform);
        body.transform.localPosition = new Vector3(0, 0.14f, 0);
        body.transform.localScale    = new Vector3(0.17f, 0.14f, 0.17f);
        body.GetComponent<Renderer>().material = mat;
        DestroyImmediate(body.GetComponent<Collider>());

        // Rim
        GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.transform.SetParent(g.transform);
        rim.transform.localPosition = new Vector3(0, 0.29f, 0);
        rim.transform.localScale    = new Vector3(0.19f, 0.018f, 0.19f);
        rim.GetComponent<Renderer>().material = mat;
        DestroyImmediate(rim.GetComponent<Collider>());

        // Handle
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handle.transform.SetParent(g.transform);
        handle.transform.localPosition = new Vector3(0.11f, 0.32f, 0);
        handle.transform.localRotation = Quaternion.Euler(0, 0, 38f);
        handle.transform.localScale    = new Vector3(0.018f, 0.14f, 0.018f);
        handle.GetComponent<Renderer>().material = mat;
        DestroyImmediate(handle.GetComponent<Collider>());
    }

    // ─── Table ───────────────────────────────────────────────────────────────

    private void CreateDetailedTable(Vector3 pos, Transform parent)
    {
        GameObject g = new GameObject("Table");
        g.transform.position = pos;
        g.transform.SetParent(parent);

        Material woodMat  = CreateMaterial(new Color(0.33f, 0.21f, 0.11f), 0f,   0.14f);
        Material metalMat = CreateMaterial(new Color(0.38f, 0.36f, 0.33f), 0.52f, 0.28f);
        Material clayMat  = CreateMaterial(new Color(0.52f, 0.46f, 0.36f), 0f,   0.08f);

        // Table top
        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.transform.SetParent(g.transform);
        top.transform.localPosition = new Vector3(0, 0.70f, 0);
        top.transform.localScale    = new Vector3(1.2f, 0.06f, 0.8f);
        top.GetComponent<Renderer>().material = woodMat;

        // Legs
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.transform.SetParent(g.transform);
                leg.transform.localPosition = new Vector3(x * 0.53f, 0.35f, z * 0.36f);
                leg.transform.localScale    = new Vector3(0.05f, 0.35f, 0.05f);
                leg.GetComponent<Renderer>().material = woodMat;
            }

        // Metal food tray on table
        GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tray.transform.SetParent(g.transform);
        tray.transform.localPosition = new Vector3(-0.05f, 0.742f, 0.04f);
        tray.transform.localScale    = new Vector3(0.54f, 0.022f, 0.37f);
        tray.GetComponent<Renderer>().material = metalMat;
        DestroyImmediate(tray.GetComponent<Collider>());

        // Bowl/plate on tray
        GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bowl.transform.SetParent(g.transform);
        bowl.transform.localPosition = new Vector3(-0.10f, 0.758f, 0.04f);
        bowl.transform.localScale    = new Vector3(0.15f, 0.022f, 0.15f);
        bowl.GetComponent<Renderer>().material = clayMat;
        DestroyImmediate(bowl.GetComponent<Collider>());

        // Tin cup
        GameObject cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cup.transform.SetParent(g.transform);
        cup.transform.localPosition = new Vector3(0.22f, 0.760f, 0.04f);
        cup.transform.localScale    = new Vector3(0.068f, 0.068f, 0.068f);
        cup.GetComponent<Renderer>().material = metalMat;
        DestroyImmediate(cup.GetComponent<Collider>());

        // Solid collider
        BoxCollider bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.37f, 0);
        bc.size   = new Vector3(1.2f, 0.75f, 0.8f);
    }

    // ─── Stool ───────────────────────────────────────────────────────────────

    private void CreateStool(Vector3 pos, Transform parent)
    {
        GameObject g = new GameObject("Stool");
        g.transform.position = pos;
        g.transform.SetParent(parent);

        Material woodMat = CreateMaterial(new Color(0.33f, 0.21f, 0.11f), 0f, 0.14f);

        GameObject seat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        seat.transform.SetParent(g.transform);
        seat.transform.localPosition = new Vector3(0, 0.42f, 0);
        seat.transform.localScale    = new Vector3(0.32f, 0.05f, 0.32f);
        seat.GetComponent<Renderer>().material = woodMat;

        GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leg.transform.SetParent(g.transform);
        leg.transform.localPosition = new Vector3(0, 0.21f, 0);
        leg.transform.localScale    = new Vector3(0.07f, 0.21f, 0.07f);
        leg.GetComponent<Renderer>().material = woodMat;

        BoxCollider bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.22f, 0);
        bc.size   = new Vector3(0.35f, 0.45f, 0.35f);
    }

    // ─── Chain lamp ──────────────────────────────────────────────────────────

    private void CreateChainLamp(Vector3 pos, Transform parent)
    {
        GameObject g = new GameObject("ChainLamp");
        g.transform.position = pos;
        g.transform.SetParent(parent);

        Material chainMat = CreateMaterial(new Color(0.14f, 0.13f, 0.12f), 0.92f, 0.45f);
        Material shadeMat = CreateMaterial(new Color(0.11f, 0.10f, 0.09f), 0.72f, 0.38f);

        // Ceiling mount
        GameObject mount = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mount.transform.SetParent(g.transform);
        mount.transform.localPosition = Vector3.zero;
        mount.transform.localScale    = new Vector3(0.10f, 0.035f, 0.10f);
        mount.GetComponent<Renderer>().material = chainMat;
        DestroyImmediate(mount.GetComponent<Collider>());

        // Chain links
        for (int i = 0; i < 6; i++)
        {
            GameObject link = GameObject.CreatePrimitive(PrimitiveType.Cube);
            link.transform.SetParent(g.transform);
            link.transform.localPosition = new Vector3(0, -0.09f - i * 0.08f, 0);
            link.transform.localRotation = (i % 2 == 0)
                ? Quaternion.identity
                : Quaternion.Euler(0, 90, 0);
            link.transform.localScale = new Vector3(0.028f, 0.065f, 0.045f);
            link.GetComponent<Renderer>().material = chainMat;
            DestroyImmediate(link.GetComponent<Collider>());
        }

        // Shade (outer cylinder)
        float shadeY = -0.68f;
        GameObject shade = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shade.transform.SetParent(g.transform);
        shade.transform.localPosition = new Vector3(0, shadeY, 0);
        shade.transform.localScale    = new Vector3(0.54f, 0.16f, 0.54f);
        shade.GetComponent<Renderer>().material = shadeMat;
        DestroyImmediate(shade.GetComponent<Collider>());

        // Shade inner (warm golden interior)
        GameObject shadeIn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shadeIn.transform.SetParent(g.transform);
        shadeIn.transform.localPosition = new Vector3(0, shadeY - 0.04f, 0);
        shadeIn.transform.localScale    = new Vector3(0.44f, 0.12f, 0.44f);
        shadeIn.GetComponent<Renderer>().material =
            CreateMaterial(new Color(0.58f, 0.50f, 0.36f), 0.28f, 0.35f);
        DestroyImmediate(shadeIn.GetComponent<Collider>());

        // Bulb
        GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.transform.SetParent(g.transform);
        bulb.transform.localPosition = new Vector3(0, shadeY, 0);
        bulb.transform.localScale    = new Vector3(0.11f, 0.11f, 0.11f);
        bulb.GetComponent<Renderer>().material =
            CreateEmissiveMaterial(new Color(1f, 0.96f, 0.78f), new Color(1f, 0.88f, 0.55f), 3.5f);
        DestroyImmediate(bulb.GetComponent<Collider>());

        // Point light (ambient fill for whole cell)
        GameObject ptGO = new GameObject("PointLight");
        ptGO.transform.SetParent(g.transform);
        ptGO.transform.localPosition = new Vector3(0, shadeY - 0.12f, 0);
        Light pt = ptGO.AddComponent<Light>();
        pt.type      = LightType.Point;
        pt.color     = new Color(1f, 0.86f, 0.60f);
        pt.intensity = 3.0f;
        pt.range     = 9f;
        pt.shadows   = LightShadows.Soft;

        // Spot light (focused cone downward)
        GameObject spotGO = new GameObject("SpotLight");
        spotGO.transform.SetParent(g.transform);
        spotGO.transform.localPosition = new Vector3(0, shadeY - 0.12f, 0);
        spotGO.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Light spot = spotGO.AddComponent<Light>();
        spot.type      = LightType.Spot;
        spot.spotAngle = 70f;
        spot.range     = 7f;
        spot.intensity = 3.8f;
        spot.color     = new Color(1f, 0.90f, 0.68f);
        spot.shadows   = LightShadows.Soft;
    }

    // ─── Wall shelf ──────────────────────────────────────────────────────────

    private void CreateWallShelf(Vector3 pos, Transform root)
    {
        GameObject g = new GameObject("WallShelf");
        g.transform.position = pos;
        g.transform.SetParent(root);

        Material woodMat  = CreateMaterial(new Color(0.30f, 0.19f, 0.10f), 0f,   0.13f);
        Material metalMat = CreateMaterial(new Color(0.24f, 0.22f, 0.20f), 0.72f, 0.32f);
        Material bookMat  = CreateMaterial(new Color(0.38f, 0.14f, 0.11f), 0f,   0.08f);

        // Shelf plank (sticks out from wall in +X direction)
        GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plank.transform.SetParent(g.transform);
        plank.transform.localPosition = new Vector3(0.28f, 0, 0);
        plank.transform.localScale    = new Vector3(0.06f, 0.045f, 0.62f);
        plank.GetComponent<Renderer>().material = woodMat;
        DestroyImmediate(plank.GetComponent<Collider>());

        // Bracket (angled support)
        GameObject bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bracket.transform.SetParent(g.transform);
        bracket.transform.localPosition = new Vector3(0.14f, -0.14f, 0.1f);
        bracket.transform.localRotation = Quaternion.Euler(0, 0, 18f);
        bracket.transform.localScale    = new Vector3(0.032f, 0.28f, 0.032f);
        bracket.GetComponent<Renderer>().material = metalMat;
        DestroyImmediate(bracket.GetComponent<Collider>());

        // Book
        GameObject book = GameObject.CreatePrimitive(PrimitiveType.Cube);
        book.transform.SetParent(g.transform);
        book.transform.localPosition = new Vector3(0.30f, 0.055f, -0.18f);
        book.transform.localScale    = new Vector3(0.055f, 0.11f, 0.085f);
        book.GetComponent<Renderer>().material = bookMat;
        DestroyImmediate(book.GetComponent<Collider>());

        // Tin cup on shelf
        GameObject cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cup.transform.SetParent(g.transform);
        cup.transform.localPosition = new Vector3(0.30f, 0.05f, 0.14f);
        cup.transform.localScale    = new Vector3(0.048f, 0.05f, 0.048f);
        cup.GetComponent<Renderer>().material = metalMat;
        DestroyImmediate(cup.GetComponent<Collider>());
    }

    // ─── Pipes ───────────────────────────────────────────────────────────────

    private void CreatePipeV(Vector3 center, float height, float radius, Material mat, Transform parent)
    {
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        p.name = "Pipe_V";
        p.transform.position   = center;
        p.transform.localScale = new Vector3(radius * 2, height * 0.5f, radius * 2);
        p.GetComponent<Renderer>().material = mat;
        DestroyImmediate(p.GetComponent<Collider>());
        p.transform.SetParent(parent);
    }

    private void CreatePipeH(Vector3 center, float length, float radius, Material mat, Transform parent)
    {
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        p.name = "Pipe_H";
        p.transform.position      = center;
        p.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        p.transform.localScale    = new Vector3(radius * 2, length * 0.5f, radius * 2);
        p.GetComponent<Renderer>().material = mat;
        DestroyImmediate(p.GetComponent<Collider>());
        p.transform.SetParent(parent);
    }

    // ─── Character ───────────────────────────────────────────────────────────

    private GameObject AddBigYahuToScene(Scene scene)
    {
        GameObject idleModel    = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Big Yahu standing.fbx");
        GameObject runningModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Big Yahu jogging.fbx");
        Material mat            = AssetDatabase.LoadAssetAtPath<Material>("Assets/Big Yahu/Big Yahu material.mat");

        GameObject character = new("BigYahu") { tag = "Player" };
        character.transform.position = Vector3.zero;

        if (idleModel != null && runningModel != null)
        {
            GameObject idle = (GameObject)PrefabUtility.InstantiatePrefab(idleModel);
            idle.name = "IdleModel";
            idle.transform.SetParent(character.transform, false);
            idle.SetActive(true);
            try { SetupStandingAnimation(idle); }
            catch (System.Exception e) { Debug.LogWarning("SetupStandingAnimation fehlgeschlagen: " + e.Message); }

            GameObject running = (GameObject)PrefabUtility.InstantiatePrefab(runningModel);
            running.name = "RunningModel";
            running.transform.SetParent(character.transform, false);
            running.SetActive(false);
            try { SetupRunAnimation(running); }
            catch (System.Exception e) { Debug.LogWarning("SetupRunAnimation fehlgeschlagen: " + e.Message); }

            if (mat != null)
                foreach (Renderer r in character.GetComponentsInChildren<Renderer>(true))
                    r.material = mat;
        }
        else
        {
            Debug.LogWarning("Big Yahu Modelle nicht gefunden. Platzhalter-Kapsel wird verwendet.");
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(character.transform, false);
            capsule.transform.localPosition = new Vector3(0, 1f, 0);
            capsule.GetComponent<Renderer>().material.color = Color.red;
        }

        CapsuleCollider col = character.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.3f;
        col.center = new Vector3(0, 0.9f, 0);

        Rigidbody rb = character.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezePositionY
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;

        character.AddComponent<CharacterAnimator>();
        character.AddComponent<PlayerController>();

        SceneManager.MoveGameObjectToScene(character, scene);
        Debug.Log("BigYahu hinzugefuegt (Tag: Player).");
        return character;
    }

    private void SetupRunAnimation(GameObject runningInstance)
    {
        Object[] fbxAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Big Yahu/Big Yahu jogging.fbx");
        AnimationClip sourceClip = null;
        foreach (Object asset in fbxAssets)
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            { sourceClip = clip; break; }

        if (sourceClip == null)
        {
            Debug.LogWarning("Keine AnimationClip in 'Big Yahu jogging.fbx' gefunden.");
            return;
        }

        const string clipPath = "Assets/Big Yahu/BigYahu_Run_Loop.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);

        AnimationClip loopClip = Object.Instantiate(sourceClip);
        loopClip.name = "BigYahu_Run_Loop";
        AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(loopClip);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(loopClip, s);
        AssetDatabase.CreateAsset(loopClip, clipPath);

        const string controllerPath = "Assets/Big Yahu/BigYahu_Run.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState runState = sm.AddState("Run");
        runState.motion = loopClip;
        sm.defaultState = runState;
        AssetDatabase.SaveAssets();

        Animator animator = runningInstance.GetComponent<Animator>() ?? runningInstance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
    }

    private void SetupStandingAnimation(GameObject standingInstance)
    {
        Object[] fbxAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Big Yahu/Big Yahu standing.fbx");
        AnimationClip sourceClip = null;
        foreach (Object asset in fbxAssets)
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            { sourceClip = clip; break; }

        if (sourceClip == null)
        {
            Debug.LogWarning("Keine AnimationClip in 'Big Yahu standing.fbx' gefunden.");
            return;
        }

        const string clipPath = "Assets/Big Yahu/BigYahu_Stand_Loop.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);

        AnimationClip loopClip = Object.Instantiate(sourceClip);
        loopClip.name = "BigYahu_Stand_Loop";
        AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(loopClip);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(loopClip, s);
        AssetDatabase.CreateAsset(loopClip, clipPath);

        const string controllerPath = "Assets/Big Yahu/BigYahu_Stand.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState standState = sm.AddState("Stand");
        standState.motion = loopClip;
        sm.defaultState = standState;
        AssetDatabase.SaveAssets();

        Animator animator = standingInstance.GetComponent<Animator>() ?? standingInstance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
    }

    // ─── UI ──────────────────────────────────────────────────────────────────

    private (GameObject numpadPanel, TextMeshProUGUI hintText) BuildUI(Scene scene)
    {
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        SceneManager.MoveGameObjectToScene(eventSystem, scene);

        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        SceneManager.MoveGameObjectToScene(canvasGO, scene);

        TextMeshProUGUI hintText = CreateHintText(canvasGO.transform);

        // Numpad panel
        GameObject numpadPanel = new GameObject("NumpadPanel");
        numpadPanel.transform.SetParent(canvasGO.transform, false);
        numpadPanel.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        RectTransform numpadRect = numpadPanel.GetComponent<RectTransform>();
        numpadRect.sizeDelta       = new Vector2(350, 500);
        numpadRect.anchoredPosition = Vector2.zero;
        NumpadController numpadCtrl = numpadPanel.AddComponent<NumpadController>();
        numpadPanel.SetActive(false);

        // Display
        GameObject displayBg = new GameObject("DisplayBackground");
        displayBg.transform.SetParent(numpadPanel.transform, false);
        displayBg.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 1f);
        RectTransform displayRect = displayBg.GetComponent<RectTransform>();
        displayRect.anchorMin       = new Vector2(0.5f, 1f);
        displayRect.anchorMax       = new Vector2(0.5f, 1f);
        displayRect.pivot           = new Vector2(0.5f, 1f);
        displayRect.anchoredPosition = new Vector2(0, -20);
        displayRect.sizeDelta       = new Vector2(300, 80);

        GameObject displayTextGO = new GameObject("Text");
        displayTextGO.transform.SetParent(displayBg.transform, false);
        TextMeshProUGUI displayText = displayTextGO.AddComponent<TextMeshProUGUI>();
        displayText.text      = "____";
        displayText.fontSize  = 50;
        displayText.alignment = TextAlignmentOptions.Center;
        displayText.color     = Color.green;
        RectTransform dtr = displayTextGO.GetComponent<RectTransform>();
        dtr.anchorMin = Vector2.zero;
        dtr.anchorMax = Vector2.one;
        dtr.sizeDelta = Vector2.zero;
        numpadCtrl.displayText = displayText;

        // Button grid
        GameObject gridGO = new GameObject("ButtonGrid");
        gridGO.transform.SetParent(numpadPanel.transform, false);
        RectTransform gridRect = gridGO.AddComponent<RectTransform>();
        gridRect.anchorMin        = new Vector2(0.5f, 0f);
        gridRect.anchorMax        = new Vector2(0.5f, 0f);
        gridRect.pivot            = new Vector2(0.5f, 0f);
        gridRect.anchoredPosition = new Vector2(0, 20);
        gridRect.sizeDelta        = new Vector2(300, 360);
        GridLayoutGroup grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize       = new Vector2(90, 80);
        grid.spacing        = new Vector2(10, 10);
        grid.childAlignment = TextAnchor.UpperCenter;

        string[] buttons  = { "7", "8", "9", "4", "5", "6", "1", "2", "3", "ENT", "0", "DEL" };
        Color deleteColor = new Color(0.6f, 0.2f, 0.2f);
        Color enterColor  = new Color(0.15f, 0.55f, 0.15f);
        Color digitColor  = new Color(0.3f, 0.3f, 0.3f);

        foreach (string btn in buttons)
        {
            Color btnColor = btn == "DEL" ? deleteColor : btn == "ENT" ? enterColor : digitColor;
            CreateButton(btn, gridGO.transform, btnColor, numpadCtrl);
        }

        return (numpadPanel, hintText);
    }

    private TextMeshProUGUI CreateHintText(Transform canvasTransform)
    {
        GameObject hintBg = new GameObject("InteractionHint");
        hintBg.transform.SetParent(canvasTransform, false);
        hintBg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        RectTransform rect = hintBg.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0f);
        rect.anchorMax        = new Vector2(0.5f, 0f);
        rect.pivot            = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0, 40);
        rect.sizeDelta        = new Vector2(500, 50);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(hintBg.transform, false);
        TextMeshProUGUI hintText = textGO.AddComponent<TextMeshProUGUI>();
        hintText.text      = "";
        hintText.fontSize  = 22;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color     = Color.white;
        RectTransform tr = textGO.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.sizeDelta = Vector2.zero;
        return hintText;
    }

    private void CreateButton(string text, Transform parent, Color bgColor, NumpadController controller)
    {
        GameObject btnGO = new GameObject("Button_" + text);
        btnGO.transform.SetParent(parent, false);
        btnGO.AddComponent<Image>().color = bgColor;
        Button btn = btnGO.AddComponent<Button>();
        UnityEventTools.AddStringPersistentListener(btn.onClick, controller.ButtonPressed, text);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        TextMeshProUGUI txt = textGO.AddComponent<TextMeshProUGUI>();
        txt.text      = text;
        txt.fontSize  = 36;
        txt.color     = Color.white;
        txt.alignment = TextAlignmentOptions.Center;
        RectTransform tr = textGO.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.sizeDelta = Vector2.zero;
    }
}
