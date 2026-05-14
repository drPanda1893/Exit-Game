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

    public static void BuildSilent()
    {
        var w = CreateInstance<BuildLevel6FinalGate>();
        try
        {
            typeof(BuildLevel6FinalGate)
                .GetMethod("Build", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(w, null);
        }
        catch (System.Reflection.TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        finally { DestroyImmediate(w); }
    }

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

        // ── Kamera (Top-Down Follow, pitched – damit der Horizont sichtbar ist) ──
        var camGO = new GameObject("Main Camera");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.45f, 0.65f, 0.85f);   // helles Himmelblau – kein Schwarz mehr
        cam.farClipPlane    = 80f;
        cam.nearClipPlane   = 0.1f;
        cam.tag             = "MainCamera";
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 11f, -8f);
        camGO.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
        var follow = camGO.AddComponent<TopDownCameraFollow>();
        follow.fixedWorldPosition = Vector3.zero;   // explizit Follow-Modus
        follow.height     = 11f;
        follow.pitchAngle = 65f;
        SceneManager.MoveGameObjectToScene(camGO, scene);

        // ── Umgebung ─────────────────────────────────────────────────────────
        var root = new GameObject("Environment");
        SceneManager.MoveGameObjectToScene(root, scene);
        var gateBarsGO = BuildEnvironment(root.transform);

        // ── Spieler ──────────────────────────────────────────────────────────
        var player = AddPlayer(scene, new Vector3(0f, 0f, -5f));
        if (player != null) follow.SetTarget(player.transform);

        // ── Beleuchtung ─ Drinnen warm, draußen Tageslicht ────────────────────
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.40f, 0.42f, 0.46f);

        // Sonne (Directional) – sorgt für ein durchgehend helles Bild,
        // auch wenn alle Point-Lichter aus wären.
        var sunGO = new GameObject("Sun");
        sunGO.transform.SetParent(root.transform);
        sunGO.transform.rotation = Quaternion.Euler(55f, 25f, 0f);
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1.00f, 0.96f, 0.82f);
        sun.intensity = 1.1f;
        sun.shadows   = LightShadows.Soft;

        AddLight("SpawnLight", root.transform, new Vector3(0f, 3.5f, -4.5f),
            LightType.Point, new Color(1.00f, 0.85f, 0.55f), 1.4f, 9f, LightShadows.None);
        AddLight("CorridorLight", root.transform, new Vector3(0f, 4f, -1f),
            LightType.Point, new Color(0.95f, 0.85f, 0.65f), 1.6f, 10f, LightShadows.None);
        AddLight("GateGlow", root.transform, new Vector3(0f, 3.5f, 2.5f),
            LightType.Point, new Color(1.0f, 0.95f, 0.85f), 1.8f, 9f, LightShadows.None);

        // ── Gate-Trigger ──────────────────────────────────────────────────────
        var gateTrigger = BuildGateTrigger(root.transform);

        // ── Rene Redo (Freiheit hinter dem Tor) ───────────────────────────────
        var reneHintGO = BuildReneHintUI(scene);
        BuildReneRedo(root.transform, scene, reneHintGO);

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

        var arduinoGO = new GameObject("ArduinoBridge");
        arduinoGO.AddComponent<ArduinoBridge>();
        SceneManager.MoveGameObjectToScene(arduinoGO, scene);

        // ── UI ────────────────────────────────────────────────────────────────
        BuildUI(scene, gateTrigger, gateBarsGO);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level6] Finales Gefängnistor fertig gebaut.");
    }

    // =========================================================================
    // 3D Umgebung
    // =========================================================================

    /// <summary>Baut den Korridor + das Eisentor + die Freiheit dahinter.
    /// Kein Dach, damit die Top-Down-Kamera den Innenraum sieht.</summary>
    GameObject BuildEnvironment(Transform root)
    {
        var concreteMat = M(new Color(0.32f, 0.30f, 0.28f), 0.01f, 0.05f);
        var stoneMat    = M(new Color(0.28f, 0.27f, 0.25f), 0.00f, 0.03f);
        var ironMat     = M(new Color(0.20f, 0.19f, 0.18f), 0.60f, 0.35f);
        var rustMat     = M(new Color(0.35f, 0.18f, 0.06f), 0.40f, 0.10f);

        // ── Innen-Korridor (z -7 … +2) ───────────────────────────────────────
        Box("Boden",      new Vector3(0f, -0.08f, -2.5f), new Vector3(6f, 0.16f, 16f), concreteMat, root);
        Box("Wand_Links", new Vector3(-3f, 2.5f, -2.5f),  new Vector3(0.22f, 5f, 16f), stoneMat,    root);
        Box("Wand_Rechts",new Vector3( 3f, 2.5f, -2.5f),  new Vector3(0.22f, 5f, 16f), stoneMat,    root);
        // KEINE Decke – würde die Top-Down-Sicht ins Innere blockieren.
        // Stattdessen Querbalken als Atmosphäre:
        for (int b = 0; b < 4; b++)
            Box($"Balken_{b}", new Vector3(0f, 4.7f, -7f + b * 2.5f), new Vector3(6f, 0.18f, 0.18f),
                M(new Color(0.18f, 0.13f, 0.07f)), root, col: false);

        // Rückwand am Spieler-Start (z=-7)
        Box("EW_Links",   new Vector3(-2f, 2.5f, -7f),  new Vector3(2f, 5f, 0.22f),  stoneMat, root);
        Box("EW_Rechts",  new Vector3( 2f, 2.5f, -7f),  new Vector3(2f, 5f, 0.22f),  stoneMat, root);
        Box("EW_Top",     new Vector3( 0f, 3.8f, -7f),  new Vector3(6f, 2.4f, 0.22f),stoneMat, root);

        // ── Tor-Rahmen (Pfeiler + Sturz) ──────────────────────────────────────
        Box("Pfeiler_L",  new Vector3(-2.6f, 2.5f, 2f), new Vector3(1.0f, 5f, 0.6f), stoneMat, root);
        Box("Pfeiler_R",  new Vector3( 2.6f, 2.5f, 2f), new Vector3(1.0f, 5f, 0.6f), stoneMat, root);
        Box("Sturz",      new Vector3(0f, 4.5f, 2f),    new Vector3(6.44f, 0.8f, 0.6f), stoneMat, root);
        Box("Schloss",    new Vector3(0f, 1.4f, 1.76f), new Vector3(0.32f, 0.40f, 0.12f), rustMat, root, col: false);

        // ── Tor-Stäbe (Pivot oben am Sturz, klappen bei Sieg nach oben) ───────
        var bars = new GameObject("GateBars");
        bars.transform.SetParent(root);
        bars.transform.position = new Vector3(0f, 4.0f, 2f);   // Drehpunkt: oben am Sturz
        for (int i = 0; i < 5; i++)
        {
            float x = -1.6f + i * 0.8f;
            // Stäbe hängen vom Pivot nach unten (lokal y=-2 → Mitte des Stabs)
            Box($"Bar_{i}", new Vector3(x, 2.0f, 2f), new Vector3(0.10f, 4.0f, 0.10f), ironMat, bars.transform, col: false);
        }
        Box("Bar_H1", new Vector3(0f, 3.2f, 2f), new Vector3(3.4f, 0.10f, 0.10f), ironMat, bars.transform, col: false);
        Box("Bar_H2", new Vector3(0f, 1.0f, 2f), new Vector3(3.4f, 0.10f, 0.10f), ironMat, bars.transform, col: false);
        var barsCol = bars.AddComponent<BoxCollider>();
        barsCol.size   = new Vector3(4f, 4f, 0.2f);
        barsCol.center = new Vector3(0f, -2f, 0f);   // lokal: Mitte zwischen den Stäben (Pivot oben → Stäbe nach unten)

        // ── Atmosphäre im Korridor ────────────────────────────────────────────
        var debrisMat = M(new Color(0.20f, 0.16f, 0.10f));
        Box("Kiste_L",  new Vector3(-2.2f, 0.25f, -3f),   new Vector3(0.55f, 0.50f, 0.55f), debrisMat, root);
        Box("Kiste_R",  new Vector3( 2.3f, 0.22f, -1.5f), new Vector3(0.45f, 0.44f, 0.45f), debrisMat, root);
        Box("Stein",    new Vector3(-1.8f, 0.12f, 0.5f),  new Vector3(0.30f, 0.24f, 0.28f), stoneMat,  root, col: false);
        // Fackeln an den Pfeilern (warmes Licht im Korridor)
        var flameMat = Emit(new Color(1.0f, 0.55f, 0.10f), new Color(1.0f, 0.65f, 0.15f), 2.0f);
        Box("Fackel_L", new Vector3(-2.85f, 3.0f, -1f), new Vector3(0.18f, 0.30f, 0.18f), flameMat, root, col: false);
        Box("Fackel_R", new Vector3( 2.85f, 3.0f, -1f), new Vector3(0.18f, 0.30f, 0.18f), flameMat, root, col: false);

        // ════════════════════════════════════════════════════════════════════
        //   D R A U S S E N  —  Die Freiheit hinter dem Tor (z > 2)
        // ════════════════════════════════════════════════════════════════════
        BuildOutdoorFreedom(root);

        return bars;
    }

    /// <summary>Wiese, Weg, Bäume, Berge, Sonne – alles was hinter dem Tor zu sehen sein soll.</summary>
    void BuildOutdoorFreedom(Transform root)
    {
        var grassMat   = M(new Color(0.32f, 0.62f, 0.22f), 0f, 0.10f);
        var grassDark  = M(new Color(0.24f, 0.50f, 0.18f), 0f, 0.08f);
        var dirtMat    = M(new Color(0.50f, 0.36f, 0.22f), 0f, 0.10f);
        var trunkMat   = M(new Color(0.30f, 0.18f, 0.08f), 0f, 0.08f);
        var leavesMat  = M(new Color(0.22f, 0.55f, 0.20f), 0f, 0.06f);
        var leavesAlt  = M(new Color(0.28f, 0.62f, 0.22f), 0f, 0.06f);
        var mountainMat= M(new Color(0.42f, 0.48f, 0.55f), 0f, 0.05f);
        var hillMat    = M(new Color(0.30f, 0.55f, 0.25f), 0f, 0.06f);
        var skyMat     = Emit(new Color(0.55f, 0.78f, 0.95f),
                              new Color(0.65f, 0.85f, 1.0f), 0.6f);
        var sunMat     = Emit(new Color(1.0f, 0.92f, 0.55f),
                              new Color(1.0f, 0.95f, 0.65f), 3.0f);

        // Wiese (großes Plateau hinter dem Tor)
        Box("Wiese", new Vector3(0f, -0.05f, 14f), new Vector3(40f, 0.10f, 26f),
            grassMat, root);

        // Wiesen-Flecken für Tiefe
        for (int i = 0; i < 8; i++)
        {
            float x = -14f + i * 4f;
            Box($"Wiesenfleck_{i}", new Vector3(x + Random.Range(-1f,1f), 0.005f, 10f + Random.Range(0f, 4f)),
                new Vector3(2.2f, 0.02f, 2.0f), grassDark, root, col: false);
        }

        // Erdweg in der Mitte (Fortsetzung des Korridorbodens)
        Box("Weg", new Vector3(0f, 0.005f, 9f), new Vector3(2.6f, 0.02f, 8f), dirtMat, root, col: false);
        Box("Weg2", new Vector3(0f, 0.005f, 16f), new Vector3(2.2f, 0.02f, 6f), dirtMat, root, col: false);

        // Bäume (zwei Reihen entlang des Wegs)
        for (int i = 0; i < 6; i++)
        {
            float side = (i % 2 == 0) ? -1f : 1f;
            float xOff = side * (3.5f + Random.Range(0f, 1.5f));
            float z    = 6.5f + i * 2.8f + Random.Range(-0.5f, 0.5f);
            BuildTree(root, new Vector3(xOff, 0f, z), trunkMat,
                      (i % 2 == 0) ? leavesMat : leavesAlt);
        }
        // ein paar Bäume weiter hinten links/rechts
        BuildTree(root, new Vector3(-8f, 0f, 12f), trunkMat, leavesMat);
        BuildTree(root, new Vector3( 8f, 0f, 14f), trunkMat, leavesAlt);
        BuildTree(root, new Vector3(-11f, 0f, 18f), trunkMat, leavesAlt);
        BuildTree(root, new Vector3( 10f, 0f, 17f), trunkMat, leavesMat);

        // Hügel im Mittelgrund (sanfte Erhebungen)
        BuildHill(root, new Vector3(-9f, 0f, 22f), new Vector3(8f, 1.6f, 6f), hillMat);
        BuildHill(root, new Vector3( 9f, 0f, 22f), new Vector3(8f, 1.6f, 6f), hillMat);
        BuildHill(root, new Vector3( 0f, 0f, 26f), new Vector3(10f, 2.2f, 6f), hillMat);

        // Berge im Hintergrund (dahinter, größer)
        BuildHill(root, new Vector3(-14f, 0f, 33f), new Vector3(14f, 4.5f, 6f), mountainMat);
        BuildHill(root, new Vector3( 14f, 0f, 33f), new Vector3(14f, 4.0f, 6f), mountainMat);
        BuildHill(root, new Vector3(  0f, 0f, 36f), new Vector3(18f, 5.0f, 6f), mountainMat);

        // Himmels-Backdrop (große flache Fläche weit hinten, emissive damit sie immer hell ist)
        Box("Himmel_Hinten", new Vector3(0f, 15f, 40f), new Vector3(80f, 35f, 0.2f),
            skyMat, root, col: false);
        Box("Himmel_Links",  new Vector3(-22f, 15f, 18f), new Vector3(0.2f, 35f, 50f),
            skyMat, root, col: false);
        Box("Himmel_Rechts", new Vector3( 22f, 15f, 18f), new Vector3(0.2f, 35f, 50f),
            skyMat, root, col: false);

        // Sonne (leuchtende Kugel hoch im Hintergrund)
        var sunGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sunGO.name = "Sonne";
        sunGO.transform.SetParent(root);
        sunGO.transform.position = new Vector3(8f, 12f, 35f);
        sunGO.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
        UnityEngine.Object.DestroyImmediate(sunGO.GetComponent<Collider>());
        sunGO.GetComponent<Renderer>().sharedMaterial = sunMat;

        // Wolken (helle flache Flecken hoch oben)
        var cloudMat = Emit(new Color(0.95f, 0.95f, 0.98f),
                            new Color(1.0f, 1.0f, 1.0f), 0.4f);
        Box("Wolke1", new Vector3(-6f, 13f, 28f), new Vector3(5f, 0.6f, 2.5f), cloudMat, root, col: false);
        Box("Wolke2", new Vector3( 7f, 14f, 30f), new Vector3(6f, 0.6f, 3.0f), cloudMat, root, col: false);
        Box("Wolke3", new Vector3( 0f, 15f, 25f), new Vector3(4f, 0.5f, 2.0f), cloudMat, root, col: false);
    }

    /// <summary>Einfacher Baum: brauner Stamm + grüne Kugel-Kuppel als Laub.</summary>
    void BuildTree(Transform root, Vector3 pos, Material trunk, Material leaves)
    {
        var tree = new GameObject("Tree");
        tree.transform.SetParent(root);
        tree.transform.position = pos;

        Box("Stamm", pos + new Vector3(0f, 1.2f, 0f), new Vector3(0.40f, 2.4f, 0.40f), trunk, tree.transform, col: false);

        var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "Krone";
        crown.transform.SetParent(tree.transform);
        crown.transform.position = pos + new Vector3(0f, 3.0f, 0f);
        crown.transform.localScale = new Vector3(2.4f, 2.2f, 2.4f);
        UnityEngine.Object.DestroyImmediate(crown.GetComponent<Collider>());
        crown.GetComponent<Renderer>().sharedMaterial = leaves;

        // Zweite kleinere Krone obendrauf für mehr Form
        var crown2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown2.name = "Krone2";
        crown2.transform.SetParent(tree.transform);
        crown2.transform.position = pos + new Vector3(0f, 3.8f, 0f);
        crown2.transform.localScale = new Vector3(1.6f, 1.5f, 1.6f);
        UnityEngine.Object.DestroyImmediate(crown2.GetComponent<Collider>());
        crown2.GetComponent<Renderer>().sharedMaterial = leaves;
    }

    /// <summary>Sanfter Hügel/Berg als breite, flache Halbkugel.</summary>
    void BuildHill(Transform root, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Hill";
        go.transform.SetParent(root);
        go.transform.position   = pos + new Vector3(0f, scale.y * 0.0f, 0f);
        go.transform.localScale = new Vector3(scale.x, scale.y * 2f, scale.z);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    Material Emit(Color baseCol, Color glow, float intensity)
    {
        var m = new Material(Shader.Find("Standard")) { color = baseCol };
        m.SetFloat("_Metallic",   0f);
        m.SetFloat("_Glossiness", 0.1f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", glow * intensity);
        return m;
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
            new Vector2(0f,80f), new Vector2(460f,60f),
            new Vector2(0.5f,0f), new Color(0f,0f,0f,0.72f));
        promptGO.SetActive(false);
        AddPromptText(promptGO.transform, "[E]  Tor untersuchen");

        // Card-Panel entfernt – der Karten-Check laeuft jetzt komplett "silent":
        // einzige Anweisung an den Spieler steht auf dem LCD ("Karte um Schloss
        // zu oeffnen."). Heat-Panel oeffnet sich erst nach gueltigem Scan.

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
        hpTitleTMP.text      = "Wie bekomme ich dieses Schloss auf?";
        hpTitleTMP.fontSize  = 30f;
        hpTitleTMP.fontStyle = FontStyles.Bold;
        hpTitleTMP.color     = new Color(1f, 0.65f, 0.10f);
        hpTitleTMP.alignment = TextAlignmentOptions.Center;
        var hpTitleRT = hpTitle.GetComponent<RectTransform>();
        hpTitleRT.anchorMin = new Vector2(0f,1f); hpTitleRT.anchorMax = new Vector2(1f,1f);
        hpTitleRT.pivot = new Vector2(0.5f,1f);
        hpTitleRT.anchoredPosition = new Vector2(0f,-18f);
        hpTitleRT.sizeDelta = new Vector2(0f, 48f);

        // Instruction (zwei Zeilen: Föhn primär, Brenner-Button als Fallback)
        var instrGO = new GameObject("Instruction");
        instrGO.transform.SetParent(heatPanelGO.transform, false);
        var instrTMP = instrGO.AddComponent<TextMeshProUGUI>();
        instrTMP.text      = string.Empty;
        instrTMP.fontSize  = 22f;
        instrTMP.color     = new Color(0.85f, 0.85f, 0.85f);
        instrTMP.alignment = TextAlignmentOptions.Center;
        instrTMP.richText  = true;
        var instrRT = instrGO.GetComponent<RectTransform>();
        instrRT.anchorMin = new Vector2(0f,1f); instrRT.anchorMax = new Vector2(1f,1f);
        instrRT.pivot = new Vector2(0.5f,1f);
        instrRT.anchoredPosition = new Vector2(0f,-75f);
        instrRT.sizeDelta = new Vector2(0f, 64f);

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
        var sliderRT = sliderGO.transform is RectTransform sRT ? sRT : sliderGO.AddComponent<RectTransform>();
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
        var faRT = fillAreaGO.transform is RectTransform faExRT ? faExRT : fillAreaGO.AddComponent<RectTransform>();
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

        // Heat Button bewusst entfernt – Schloss erhitzt sich NUR ueber den
        // Arduino-Temperatursensor. Keine Maus-Fallback-Option mehr.

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
        // cardPanel/cardStatusText bleiben bewusst null – kein On-Screen-Hinweis.
        l6so.FindProperty("heatPanel").objectReferenceValue         = heatPanelGO;
        l6so.FindProperty("temperatureBar").objectReferenceValue    = slider;
        l6so.FindProperty("temperatureLabel").objectReferenceValue  = tempLabelTMP;
        l6so.FindProperty("statusText").objectReferenceValue        = statusTMP;
        // heatButton bleibt null – Schloss nur via Arduino-Sensor.
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

    // =========================================================================
    // Rene Redo – Schluss-NPC in der Freiheit
    // =========================================================================

    void BuildReneRedo(Transform envRoot, Scene scene, GameObject hintGO)
    {
        const string fbxPath = "Assets/Big Yahu/Rene Redo/Materials/Untitled@Talking.fbx";

        // FBX auf Generic – sonst kein Skinned-Mesh-Renderer im Editor
        var modelImp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (modelImp != null && modelImp.animationType != ModelImporterAnimationType.Generic)
        {
            modelImp.animationType = ModelImporterAnimationType.Generic;
            modelImp.SaveAndReimport();
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        GameObject rene;
        if (prefab != null)
        {
            rene = Object.Instantiate(prefab);
            rene.name = "ReneRedo";
        }
        else
        {
            rene = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rene.name = "ReneRedo";
            Debug.LogWarning("[Level6] 'Talking.fbx' nicht gefunden – Capsule als Platzhalter.");
        }
        rene.transform.SetParent(envRoot, false);

        foreach (Transform t in rene.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);
        foreach (var r in rene.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;

        // Skinned-Mesh-Renderer cullen sonst gerne weg, wenn die Bone-Bounds
        // nach Scale/Position nicht mehr passen. Damit ist Rene IMMER sichtbar.
        foreach (var smr in rene.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.updateWhenOffscreen = true;
            smr.localBounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
        }

        // Skalierung auf ~1.7 m (wie Big Yahu)
        var rens = rene.GetComponentsInChildren<Renderer>(true);
        float modelH = 1.7f;
        if (rens.Length > 0)
        {
            var b = rens[0].bounds;
            foreach (var r in rens) b.Encapsulate(r.bounds);
            if (b.size.y > 0.01f) modelH = b.size.y;
        }
        float scale = 1.7f / modelH;
        rene.transform.localScale = Vector3.one * scale;

        // Füße auf den Boden, in die Mitte des Wegs hinter dem Tor
        rens = rene.GetComponentsInChildren<Renderer>(true);
        float groundY = 0f;
        if (rens.Length > 0)
        {
            var b2 = rens[0].bounds;
            foreach (var r in rens) b2.Encapsulate(r.bounds);
            if (b2.min.y < 0f) groundY = -b2.min.y;
        }
        rene.transform.position = new Vector3(0f, groundY, 12f);
        // Rene steht mit dem RUECKEN zum Tor – sein Gesicht zeigt also in die
        // Freiheit (+Z). Cinematic-Kamera bleibt entsprechend in der Wiese hinter
        // ihm und schaut zurueck Richtung Tor (FaceAnchor.forward = +Z).
        //
        // Die FBX hat den Koerper intern leicht nach rechts gedreht – wir
        // korrigieren das mit einer kleinen Y-Drehung, damit er frontal in
        // die Kamera spricht und sein Ruecken parallel zum Tor steht.
        const float reneYawCorrection = 20f;
        rene.transform.rotation = Quaternion.Euler(0f, reneYawCorrection, 0f);

        // Materialien: das beim FBX-Import auto-extrahierte 'DefaultMaterial.mat' im
        // Unterordner ist initial leer (keine Texturen). Wir bestuecken es einmal
        // mit den Tripo-Maps – damit bleibt die FBX-Referenz erhalten und Rene
        // hat von da an die richtigen Farben. Notfalls: Override per Fallback-Mat.
        PopulateReneFbxMaterial();

        bool anyStillEmpty = false;
        foreach (var r in rene.GetComponentsInChildren<Renderer>(true))
        {
            var sm = r.sharedMaterial;
            if (sm == null || !sm.HasProperty("_MainTex") || sm.mainTexture == null)
            { anyStillEmpty = true; break; }
        }

        if (anyStillEmpty)
        {
            Debug.Log("[Level6] Rene-Material noch ohne Albedo – schreibe Fallback-Material.");
            var mat = CreateReneRedoMaterial();
            const string matSavePath = "Assets/Big Yahu/Rene Redo/ReneRedo_Runtime.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matSavePath) != null)
                AssetDatabase.DeleteAsset(matSavePath);
            AssetDatabase.CreateAsset(mat, matSavePath);
            AssetDatabase.SaveAssets();
            mat = AssetDatabase.LoadAssetAtPath<Material>(matSavePath);
            foreach (var r in rene.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = mat;
        }
        else
        {
            Debug.Log("[Level6] Rene-DefaultMaterial mit Tripo-Texturen bestueckt.");
        }

        // Animation Loop
        SetupReneAnimation(rene, fbxPath);

        // Warmes Spotlight auf Rene
        var spotGO = new GameObject("ReneSpot");
        spotGO.transform.SetParent(envRoot);
        spotGO.transform.position = new Vector3(0f, 4.5f, 12f);
        var spot = spotGO.AddComponent<Light>();
        spot.type      = LightType.Spot;
        spot.color     = new Color(1.00f, 0.92f, 0.70f);
        spot.intensity = 2.2f;
        spot.range     = 7f;
        spot.spotAngle = 45f;
        spot.shadows   = LightShadows.Soft;
        spotGO.transform.LookAt(rene.transform.position + Vector3.up * 1.0f);

        // Face-Anchor: leeres Child von Rene auf Kopfhoehe. Welt-Forward wird
        // FIX auf +Z gesetzt – Rene schaut in die Freiheit, Kamera steht
        // hinter ihm in der Wiese und schaut zurueck Richtung Tor.
        var faceAnchorGO = new GameObject("FaceAnchor");
        faceAnchorGO.transform.SetParent(rene.transform, false);
        faceAnchorGO.transform.localPosition = new Vector3(0f, 1.55f / scale, 0f);
        faceAnchorGO.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        // Trigger + Interaction
        var interGO = new GameObject("ReneRedoInteraction");
        interGO.transform.SetParent(envRoot);
        interGO.transform.position = new Vector3(0f, 1.0f, 12f);
        var col = interGO.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(3.0f, 2.2f, 3.0f);

        // AudioSource fuer Renes Stimme
        var src = interGO.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop        = false;
        src.spatialBlend = 0f;
        src.volume       = 1f;

        const string voicePath = "Assets/Big Yahu/Rene Redo/Rene Redo Dialog.mp3";
        var voiceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(voicePath);
        if (voiceClip == null)
            Debug.LogWarning($"[Level6] Voice-Clip '{voicePath}' nicht gefunden – Rene wird stumm bleiben.");

        var inter = interGO.AddComponent<ReneRedoInteraction>();
        var so = new SerializedObject(inter);
        so.FindProperty("hintGO").objectReferenceValue       = hintGO;
        so.FindProperty("audioSource").objectReferenceValue  = src;
        so.FindProperty("voiceClip").objectReferenceValue    = voiceClip;
        so.FindProperty("faceAnchor").objectReferenceValue   = faceAnchorGO.transform;
        so.FindProperty("loopCount").intValue                = 1;
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[Level6] Rene Redo in der Freiheit platziert.");
    }

    void PopulateReneFbxMaterial()
    {
        // Beim FBX-Import erstellt Unity 'DefaultMaterial.mat' im Materials-Unterordner.
        // Slot ist initial leer. Wir bestuecken ihn mit den 4 Tripo-Maps, die jetzt
        // direkt im gleichen Materials-Ordner liegen.
        const string matPath   = "Assets/Big Yahu/Rene Redo/Materials/DefaultMaterial.mat";
        const string texFolder = "Assets/Big Yahu/Rene Redo/Materials/";
        AssetDatabase.Refresh();

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Debug.LogWarning("[Level6] " + matPath + " nicht gefunden – ueberspringe Material-Populate.");
            return;
        }

        var albedo    = LoadTex(texFolder + "actionfigure3dmodel_basecolor");
        var normal    = LoadTex(texFolder + "actionfigure3dmodel_normal");
        var metallic  = LoadTex(texFolder + "actionfigure3dmodel_metallic");
        var roughness = LoadTex(texFolder + "actionfigure3dmodel_roughness");

        // Albedo
        if (albedo != null)
        {
            EnsureTextureSRGB(albedo, true);
            mat.mainTexture = albedo;
            mat.color = Color.white;
            Debug.Log("[Level6] DefaultMaterial._MainTex → " + AssetDatabase.GetAssetPath(albedo));
        }
        else
        {
            Debug.LogWarning("[Level6] basecolor.JPEG fehlt in " + texFolder);
        }

        // Normal
        if (normal != null)
        {
            var npath = AssetDatabase.GetAssetPath(normal);
            var imp   = AssetImporter.GetAtPath(npath) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(npath);
            }
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }

        // Metallic + Roughness zu einer Unity-konformen MetalGloss-Map kombinieren:
        // R=metallic, A=smoothness=(1-roughness). Standard-Shader braucht diese Form.
        if (metallic != null && roughness != null)
        {
            const string combinedPath = "Assets/Big Yahu/Rene Redo/Materials/ReneRedo_MetalGloss.png";
            var combined = BuildMetalGlossTexture(metallic, roughness, combinedPath);
            if (combined != null)
            {
                mat.SetTexture("_MetallicGlossMap", combined);
                mat.EnableKeyword("_METALLICGLOSSMAP");
                Debug.Log("[Level6] DefaultMaterial._MetallicGlossMap → " + combinedPath);
            }
        }
        else if (metallic != null)
        {
            EnsureTextureSRGB(metallic, false);
            mat.SetTexture("_MetallicGlossMap", metallic);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        }

        mat.SetFloat("_Metallic",     1f);   // Skaliert die R-Werte aus der Map
        mat.SetFloat("_Glossiness",   1f);   // Skaliert die Alpha-Werte (smoothness) aus der Map
        mat.SetFloat("_GlossMapScale", 1f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Kombiniert eine Tripo-Metallic-JPEG (RGB) mit einer Roughness-JPEG zu
    /// einer Unity-kompatiblen Metal-Gloss-Map (R=metallic, A=1-roughness).
    /// Wird als PNG neben den Quell-Texturen abgelegt und importiert.
    /// </summary>
    Texture2D BuildMetalGlossTexture(Texture2D metallic, Texture2D roughness, string outPath)
    {
        try
        {
            // Beide Quellen lesbar machen + linear importieren – nur einmal nötig.
            string mPath = AssetDatabase.GetAssetPath(metallic);
            string rPath = AssetDatabase.GetAssetPath(roughness);
            if (EnsureReadableLinear(mPath)) metallic  = AssetDatabase.LoadAssetAtPath<Texture2D>(mPath);
            if (EnsureReadableLinear(rPath)) roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(rPath);

            int w = Mathf.Min(metallic.width, roughness.width);
            int h = Mathf.Min(metallic.height, roughness.height);
            var mPx = metallic.GetPixels(0, 0, w, h);
            var rPx = roughness.GetPixels(0, 0, w, h);

            var combined = new Texture2D(w, h, TextureFormat.RGBA32, true, true);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                float m = mPx[i].r;
                float rough = rPx[i].r;
                px[i] = new Color(m, m, m, 1f - rough);
            }
            combined.SetPixels(px);
            combined.Apply(true, false);

            System.IO.File.WriteAllBytes(outPath, combined.EncodeToPNG());
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);

            var imp = AssetImporter.GetAtPath(outPath) as TextureImporter;
            if (imp != null)
            {
                imp.sRGBTexture = false;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Level6] MetalGloss-Combine fehlgeschlagen: " + ex.Message);
            return null;
        }
    }

    bool EnsureReadableLinear(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return false;
        bool changed = false;
        if (!imp.isReadable)  { imp.isReadable  = true;  changed = true; }
        if (imp.sRGBTexture)  { imp.sRGBTexture = false; changed = true; }
        if (changed) imp.SaveAndReimport();
        return changed;
    }

    Material CreateReneRedoMaterial()
    {
        const string folder    = "Assets/Big Yahu/Rene Redo/Rene Redo material/";
        const string altFolder = "Assets/Big Yahu/Rene Redo/Textures/";

        // Sicherheitshalber refreshen, damit gerade abgelegte JPEGs einen
        // Importer + .meta bekommen, bevor wir sie laden.
        AssetDatabase.Refresh();

        var mat = new Material(Shader.Find("Standard")) { name = "ReneRedo_Material" };
        mat.color = new Color(0.95f, 0.45f, 0.10f, 1f);

        // Nur Albedo + Normal werden genutzt – Metallic/Roughness liefern bei
        // diesem Charakter-Modell schlechte Ergebnisse (siehe Hinweis unten).
        var albedo = LoadTex(folder + "actionfigure3dmodel_basecolor");
        var normal = LoadTex(folder + "actionfigure3dmodel_normal");

        if (albedo == null) albedo = LoadTex(altFolder + "Rene_Albedo");
        if (normal == null) normal = LoadTex(altFolder + "Rene_Normal");

        if (albedo == null)
            Debug.LogWarning("[Level6] Keine Rene-Albedo gefunden – Fallback-Orange wird verwendet. " +
                             "Erwartet: " + folder + "actionfigure3dmodel_basecolor.JPEG");
        else
            Debug.Log("[Level6] Rene-Albedo geladen: " + AssetDatabase.GetAssetPath(albedo));

        if (albedo != null)
        {
            // Albedo-Map muss als sRGB importiert sein, sonst kommt die Farbe falsch raus.
            EnsureTextureSRGB(albedo, true);
            mat.mainTexture = albedo;
            mat.color = Color.white;
        }

        if (normal != null)
        {
            var path = AssetDatabase.GetAssetPath(normal);
            var imp  = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }

        // BEWUSST KEIN _MetallicGlossMap:
        // Die Tripo-"metallic"-JPEG bewertet die ganze Figur als hochmetallisch.
        // Ohne Reflection-Probe / Skybox spiegelt das nur eine dunkle Umgebung
        // wider → Rene wirkt komplett schwarz. Wir bleiben bei einem nicht-
        // metallischen Charakter-Material (Haut/Stoff) mit moderater Glossiness.
        // Roughness/Occlusion-Maps werden ebenfalls absichtlich ignoriert.

        mat.SetFloat("_Metallic",   0.0f);
        mat.SetFloat("_Glossiness", 0.3f);
        return mat;
    }

    void EnsureTextureSRGB(Texture2D tex, bool srgb)
    {
        if (tex == null) return;
        var path = AssetDatabase.GetAssetPath(tex);
        var imp  = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        if (imp.sRGBTexture == srgb) return;
        imp.sRGBTexture = srgb;
        imp.SaveAndReimport();
    }

    Texture2D LoadTex(string pathWithoutExt)
    {
        string[] exts = { ".png", ".PNG", ".jpg", ".JPG", ".jpeg", ".JPEG", ".tga", ".TGA", ".psd" };
        foreach (var e in exts)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(pathWithoutExt + e);
            if (t != null) return t;
        }
        return null;
    }

    void SetupReneAnimation(GameObject rene, string fbxPath)
    {
        AnimationClip src = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { src = c; break; }
        if (src == null) return;

        const string clipPath = "Assets/Big Yahu/Rene Redo/ReneRedo_Talk_Loop.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);
        var loop = Object.Instantiate(src);
        loop.name = "ReneRedo_Talk_Loop";
        var cfg = AnimationUtility.GetAnimationClipSettings(loop);
        cfg.loopTime = cfg.loopBlend = true;
        AnimationUtility.SetAnimationClipSettings(loop, cfg);
        AssetDatabase.CreateAsset(loop, clipPath);
        AssetDatabase.SaveAssets();
        loop = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

        const string ctrlPath = "Assets/Big Yahu/Rene Redo/ReneRedo_Talk.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null)
            AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm   = ctrl.layers[0].stateMachine;
        var st   = sm.AddState("Talk"); st.motion = loop; sm.defaultState = st;
        AssetDatabase.SaveAssets();

        var animator = rene.GetComponentInChildren<Animator>(true) ?? rene.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.enabled = true;

        foreach (var anim in rene.GetComponentsInChildren<Animation>(true))
            Object.DestroyImmediate(anim);
    }

    GameObject BuildReneHintUI(Scene scene)
    {
        var canvasGO = new GameObject("ReneHintCanvas");
        SceneManager.MoveGameObjectToScene(canvasGO, scene);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        var panelGO = new GameObject("HintPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);
        var prt = panelGO.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0f);
        prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot     = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 90f);
        prt.sizeDelta = new Vector2(360f, 64f);

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(panelGO.transform, false);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "[ E ] Mit Rene Redo sprechen";
        tmp.fontSize = 24f;
        tmp.color = new Color(1f, 0.88f, 0.45f);
        tmp.alignment = TextAlignmentOptions.Center;
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        canvasGO.SetActive(true);
        panelGO.SetActive(false);
        return panelGO;
    }

    // =========================================================================

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
