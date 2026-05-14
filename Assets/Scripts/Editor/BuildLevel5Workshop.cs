using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Baut Level 5 – Werkstatt-Schuppen (3D Top-Down Szene).
///
/// Ablauf in der Szene:
///   1. Spieler spawnt vor dem geschlossenen Schuppen.
///   2. [E] an der Schuppentür → Breadboard-Overlay (Schaltkreis-Puzzle).
///   3. Puzzle gelöst → Tür schwingt auf.
///   4. Spieler geht in den Schuppen → [E] am Bunsenbrenner → Pickup.
///   5. Exit-Marker draußen wird aktiv → Spieler läuft hin → Level 6 wird geladen.
///
/// Menü: Tools → Build Level 5 Workshop
/// </summary>
public class BuildLevel5Workshop : EditorWindow
{
    [MenuItem("Tools/Build Level 5 Workshop")]
    public static void ShowWindow() => GetWindow<BuildLevel5Workshop>("Level 5 Builder");

    // Tile-Typen für das Breadboard-Puzzle (5x5, row-major, row 0 = oben)
    private static readonly int[] TILE_TYPES =
    {
        0, 0, 2, 1, 2,
        0, 0, 1, 0, 1,
        3, 1, 2, 0, 4,
        0, 0, 0, 0, 0,
        0, 0, 0, 0, 0,
    };
    private static readonly int[] START_ROT =
    {
        0, 0, 2, 0, 1,
        0, 0, 1, 0, 1,
        0, 0, 1, 0, 0,
        0, 0, 0, 0, 0,
        0, 0, 0, 0, 0,
    };

    void OnGUI()
    {
        GUILayout.Label("Level 5 – Werkstatt-Schuppen (3D)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Top-Down 3D Szene. Spieler vor verschlossenem Schuppen.\n" +
            "Tür [E] → Breadboard-Puzzle → Tür auf → Bunsenbrenner holen → raus → Level 6.",
            MessageType.Info);
        GUILayout.Space(10);
        if (GUILayout.Button("Level 5 bauen", GUILayout.Height(36)))
            Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Hauptaufbau
    // ═══════════════════════════════════════════════════════════════════════
    void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level5.unity");

        // Kamera – Top-Down Follow
        var camGO = new GameObject("Main Camera");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
        cam.farClipPlane    = 60f;
        cam.tag             = "MainCamera";
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 11f, -4f);
        camGO.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
        var camFollow = camGO.AddComponent<TopDownCameraFollow>();
        camFollow.fixedWorldPosition = Vector3.zero;
        camFollow.height     = 11f;
        camFollow.pitchAngle = 70f;
        SceneManager.MoveGameObjectToScene(camGO, scene);

        // GameManager
        var gmGO = new GameObject("GameManager");
        var gm   = gmGO.AddComponent<GameManager>();
        var gmSo = new SerializedObject(gm);
        var levelNames = gmSo.FindProperty("levelSceneNames");
        levelNames.arraySize = 6;
        levelNames.GetArrayElementAtIndex(0).stringValue = "Level1";
        levelNames.GetArrayElementAtIndex(1).stringValue = "Level2";
        levelNames.GetArrayElementAtIndex(2).stringValue = "Level3";
        levelNames.GetArrayElementAtIndex(3).stringValue = "Level4";
        levelNames.GetArrayElementAtIndex(4).stringValue = "Level5";
        levelNames.GetArrayElementAtIndex(5).stringValue = "Level6";
        gmSo.ApplyModifiedPropertiesWithoutUndo();
        SceneManager.MoveGameObjectToScene(gmGO, scene);

        // EventSystem (für Maus-Klicks auf Breadboard-Tiles)
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();
        SceneManager.MoveGameObjectToScene(esGO, scene);

        // Globales Licht – außen, leicht abendlich
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.20f, 0.22f, 0.25f);
        var sunGO = new GameObject("Sun");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.9f, 0.85f, 0.70f);
        sun.intensity = 0.7f;
        sunGO.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
        SceneManager.MoveGameObjectToScene(sunGO, scene);

        // Umgebung
        var root = new GameObject("Environment");
        SceneManager.MoveGameObjectToScene(root, scene);

        BuildOutdoor(root.transform);
        var shedParts = BuildShed(root.transform);
        var interior  = BuildInteriorWorkbench(root.transform);

        // Spieler – steht direkt vor dem Schuppen (südlich der Tür)
        var player = AddPlayer(scene, new Vector3(0f, 0f, -2.2f));
        if (player != null) camFollow.SetTarget(player.transform);

        // Trigger-Zonen (proximity)
        var doorSpot    = AddSpotTrigger(scene, root.transform,
                          new Vector3(0f, 1f, 2.8f), new Vector3(3.0f, 2.0f, 2.0f), "DoorSpot");
        var leverSpot   = AddSpotTrigger(scene, root.transform,
                          shedParts.leverPos, new Vector3(1.5f, 2.0f, 1.5f), "LeverSpot");
        var brennerSpot = AddSpotTrigger(scene, root.transform,
                          interior.brennerPos + new Vector3(0f, 0f, -1.0f),
                          new Vector3(2.2f, 2.0f, 1.6f), "BrennerSpot");
        var exitSpot    = AddSpotTrigger(scene, root.transform,
                          new Vector3(0f, 1f, -5.4f), new Vector3(3.0f, 2.0f, 1.4f), "ExitSpot");

        // Prompt-Canvases
        var doorPrompt    = BuildPromptCanvas(scene, "[ E ]  Schaltung reparieren",
                            new Color(0.55f, 0.85f, 1.0f));
        var leverPrompt   = BuildPromptCanvas(scene, "[ E ]  Hebel umlegen",
                            new Color(0.55f, 0.95f, 0.40f));
        var brennerPrompt = BuildPromptCanvas(scene, "[ E ]  Bunsenbrenner aufnehmen",
                            new Color(1.0f, 0.65f, 0.20f));
        var exitPrompt    = BuildPromptCanvas(scene, "Zum Hof zurück …",
                            new Color(0.75f, 0.95f, 0.65f));

        // Exit-Marker (3D)
        var exitMarker = BuildExitMarker(root.transform, new Vector3(0f, 0.05f, -5.4f));

        // Breadboard-Overlay (Canvas, inaktiv)
        var (breadboardCanvas, breadboardScript) = BuildBreadboardOverlay(scene);

        // Scene-Flow Komponente
        var flowGO = new GameObject("Level5_SceneFlow");
        var flow   = flowGO.AddComponent<Level5_SceneFlow>();
        flow.doorSpot         = doorSpot;
        flow.doorPrompt       = doorPrompt;
        flow.doorObject       = shedParts.door;
        flow.breadboardCanvas = breadboardCanvas;
        flow.breadboard       = breadboardScript;
        flow.leverSpot        = leverSpot;
        flow.leverPrompt      = leverPrompt;
        flow.leverHandle      = shedParts.leverHandle;
        flow.brennerSpot      = brennerSpot;
        flow.brennerPrompt    = brennerPrompt;
        flow.brennerObject    = interior.brennerObject;
        flow.brennerGlow      = interior.brennerGlow;
        flow.exitSpot         = exitSpot;
        flow.exitMarker       = exitMarker;
        flow.exitPrompt       = exitPrompt;
        flow.nextScene        = "Level6";
        SceneManager.MoveGameObjectToScene(flowGO, scene);

        // Hintergrundmusik
        AddBackgroundMusic(scene);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level5] Werkstatt-Schuppen fertig.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Materialien
    // ═══════════════════════════════════════════════════════════════════════

    Material M(Color c, float metal = 0f, float smooth = 0.25f)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Metallic",   metal);
        m.SetFloat("_Glossiness", smooth);
        return m;
    }

    Material Emit(Color c, Color glow, float intensity = 1.5f)
    {
        var m = M(c);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", glow * intensity);
        return m;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3D Hilfen
    // ═══════════════════════════════════════════════════════════════════════

    GameObject Box(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent,
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

    GameObject Cyl(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent,
                   Quaternion? rot = null, bool col = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        if (rot.HasValue) go.transform.rotation = rot.Value;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!col) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    void AddLight(string name, Transform parent, Vector3 worldPos, LightType type,
                  Color color, float intensity, float range, bool enabled = true,
                  float spotAngle = 60f, LightShadows shadows = LightShadows.None)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = worldPos;
        var l = go.AddComponent<Light>();
        l.type      = type;
        l.color     = color;
        l.intensity = intensity;
        l.range     = range;
        l.spotAngle = spotAngle;
        l.shadows   = shadows;
        l.enabled   = enabled;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Außenwelt (Vorplatz vor dem Schuppen)
    // ═══════════════════════════════════════════════════════════════════════

    void BuildOutdoor(Transform root)
    {
        var gravel   = M(new Color(0.18f, 0.18f, 0.18f), 0f, 0.05f);
        var gravel2  = M(new Color(0.22f, 0.21f, 0.20f), 0f, 0.06f);
        var concrete = M(new Color(0.35f, 0.35f, 0.34f), 0f, 0.15f);
        var fence    = M(new Color(0.30f, 0.27f, 0.22f), 0.30f, 0.20f);

        // Boden – vor dem Schuppen, reicht bis an die Schuppen-Vorderwand
        Box("Ground", new Vector3(0f, -0.05f, -1.5f), new Vector3(18f, 0.1f, 11f), gravel, root);

        // Schachbrettmuster für Detail
        for (int x = -4; x <= 4; x++)
        for (int z = -3; z <= 1; z++)
        {
            var m = ((x + z) % 2 == 0) ? gravel : gravel2;
            Box($"Slab_{x}_{z}", new Vector3(x * 1.0f, 0.01f, z * 1.0f - 2f),
                new Vector3(0.95f, 0.02f, 0.95f), m, root, col: false);
        }

        // Betonweg von Spawn zum Schuppen
        Box("Path", new Vector3(0f, 0.02f, -1f), new Vector3(1.6f, 0.02f, 5f), concrete, root, col: false);

        // Außenzaun an den Seiten
        for (int z = -7; z <= -2; z++)
        {
            Box($"FenceL_{z}",  new Vector3(-8f, 0.8f, z), new Vector3(0.08f, 1.6f, 0.08f), fence, root, col: false);
            Box($"FenceR_{z}",  new Vector3( 8f, 0.8f, z), new Vector3(0.08f, 1.6f, 0.08f), fence, root, col: false);
        }
        // Stacheldraht-Andeutung
        Box("FenceTopL", new Vector3(-8f, 1.6f, -4.5f), new Vector3(0.06f, 0.02f, 5.5f), fence, root, col: false);
        Box("FenceTopR", new Vector3( 8f, 1.6f, -4.5f), new Vector3(0.06f, 0.02f, 5.5f), fence, root, col: false);

        // Außen-Begrenzungs-Collider (verhindert dass Spieler vom Weg läuft)
        var leftBorder  = new GameObject("BorderL");
        leftBorder.transform.SetParent(root);
        leftBorder.transform.position = new Vector3(-8.5f, 1f, -2f);
        var lc = leftBorder.AddComponent<BoxCollider>();
        lc.size = new Vector3(0.5f, 3f, 10f);

        var rightBorder = new GameObject("BorderR");
        rightBorder.transform.SetParent(root);
        rightBorder.transform.position = new Vector3(8.5f, 1f, -2f);
        var rc = rightBorder.AddComponent<BoxCollider>();
        rc.size = new Vector3(0.5f, 3f, 10f);

        var southBorder = new GameObject("BorderS");
        southBorder.transform.SetParent(root);
        southBorder.transform.position = new Vector3(0f, 1f, -7f);
        var sc = southBorder.AddComponent<BoxCollider>();
        sc.size = new Vector3(18f, 3f, 0.5f);

        // Atmosphäre: kleine Lampe vor dem Schuppen
        var lamp = M(new Color(0.25f, 0.20f, 0.10f), 0.4f, 0.3f);
        Box("LampPost", new Vector3(-3f, 1.5f, -1f), new Vector3(0.1f, 3f, 0.1f), lamp, root);
        Cyl("LampHead", new Vector3(-3f, 3.0f, -1f), new Vector3(0.4f, 0.1f, 0.4f),
            Emit(new Color(0.9f, 0.7f, 0.3f), new Color(1f, 0.7f, 0.2f), 2f), root);
        AddLight("Lamp", root, new Vector3(-3f, 2.8f, -1f),
            LightType.Point, new Color(1f, 0.75f, 0.30f), 1.4f, 6f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Schuppen (verschlossenes Holzgebäude mit Tür)
    // ═══════════════════════════════════════════════════════════════════════

    struct ShedParts
    {
        public GameObject door;         // Tür-Pivot zum Drehen
        public Transform  interior;     // Mittelpunkt-Transform des Innenraums
        public GameObject leverHandle;  // LeverPivot – wird nach Puzzle-Lösung gedreht
        public Vector3    leverPos;     // Weltposition des Hebels (für Trigger-Platzierung)
    }

    ShedParts BuildShed(Transform root)
    {
        var planks      = M(new Color(0.32f, 0.20f, 0.10f), 0.02f, 0.10f);
        var planksDark  = M(new Color(0.20f, 0.13f, 0.07f), 0.02f, 0.08f);
        var roofMat     = M(new Color(0.18f, 0.14f, 0.10f), 0.05f, 0.08f);
        var iron        = M(new Color(0.18f, 0.18f, 0.18f), 0.45f, 0.30f);
        var brass       = M(new Color(0.55f, 0.42f, 0.15f), 0.55f, 0.35f);
        var interiorFloor = M(new Color(0.20f, 0.17f, 0.12f), 0.0f, 0.08f);

        // Innen-Boden (sichtbar wenn Tür auf ist)
        Box("ShedFloor", new Vector3(0f, 0.005f, 6.5f), new Vector3(7f, 0.01f, 5f),
            interiorFloor, root, col: false);

        // Hintere Wand
        Box("ShedBackWall",  new Vector3(0f, 1.6f, 9f), new Vector3(7.2f, 3.2f, 0.20f), planks, root);
        // Linke Wand
        Box("ShedLeftWall",  new Vector3(-3.5f, 1.6f, 6.5f), new Vector3(0.20f, 3.2f, 5.2f), planks, root);
        // Rechte Wand
        Box("ShedRightWall", new Vector3( 3.5f, 1.6f, 6.5f), new Vector3(0.20f, 3.2f, 5.2f), planks, root);

        // Vordere Wand mit Türöffnung in der Mitte
        // Türöffnung ist 1.6m breit, also linke + rechte Front-Seitenpaneele
        Box("ShedFrontL",    new Vector3(-2.55f, 1.6f, 4f), new Vector3(1.9f, 3.2f, 0.20f), planks, root);
        Box("ShedFrontR",    new Vector3( 2.55f, 1.6f, 4f), new Vector3(1.9f, 3.2f, 0.20f), planks, root);
        // Türsturz oben (Breite = Türöffnung 3.2 m)
        Box("ShedLintel",    new Vector3(0f, 2.85f, 4f), new Vector3(3.2f, 0.7f, 0.22f), planksDark, root);

        // KEIN Dach – würde die Top-Down-Sicht ins Innere blockieren.
        // Nur Querbalken als Atmosphäre.
        Box("ShedBeam1", new Vector3(0f, 3.20f, 5f),   new Vector3(7.2f, 0.12f, 0.12f), planksDark, root, col: false);
        Box("ShedBeam2", new Vector3(0f, 3.20f, 6.5f), new Vector3(7.2f, 0.12f, 0.12f), planksDark, root, col: false);
        Box("ShedBeam3", new Vector3(0f, 3.20f, 8f),   new Vector3(7.2f, 0.12f, 0.12f), planksDark, root, col: false);

        // Schild über der Tür
        Box("ShedSign",      new Vector3(0f, 3.10f, 3.92f), new Vector3(1.6f, 0.5f, 0.05f), planksDark, root, col: false);
        AddSignText(root, new Vector3(0f, 3.10f, 3.87f), "WERKSTATT");

        // ── Tür (geschlossen, schwenkt nach innen auf) ──────────────────────
        // Pivot am linken Türrahmen-Pfosten (Scharnier) – Drehung um Y öffnet die Tür.
        // Öffnungsbreite = 3.2 m (ShedFrontL rechte Kante x=-1.6 bis ShedFrontR linke Kante x=1.6).
        var doorPivot = new GameObject("ShedDoorPivot");
        doorPivot.transform.SetParent(root);
        doorPivot.transform.position = new Vector3(-1.6f, 1.2f, 4f);

        // Türblatt – deckt die gesamte 3.2 m-Öffnung ab.
        Box("ShedDoor", new Vector3(0f, 1.2f, 4f), new Vector3(3.2f, 2.4f, 0.10f),
            planksDark, doorPivot.transform, col: true);
        // Querbretter (Breite an Türblatt angepasst)
        Box("DoorPlank1", new Vector3(0f, 1.5f, 3.94f), new Vector3(3.1f, 0.05f, 0.02f),
            brass, doorPivot.transform, col: false);
        Box("DoorPlank2", new Vector3(0f, 0.9f, 3.94f), new Vector3(3.1f, 0.05f, 0.02f),
            brass, doorPivot.transform, col: false);
        // Türknauf rechts (verschoben an rechte Türkante)
        Cyl("DoorKnob", new Vector3(1.3f, 1.2f, 3.92f), new Vector3(0.10f, 0.04f, 0.10f),
            brass, doorPivot.transform, rot: Quaternion.Euler(90f, 0f, 0f), col: false);

        // ── Breadboard-Terminal an der Tür (visueller Hinweis) ──────────────
        var terminalMat = Emit(new Color(0.08f, 0.10f, 0.16f),
                               new Color(0.30f, 0.50f, 1.0f), 1.2f);
        Box("DoorTerminal", new Vector3(1.4f, 1.4f, 3.88f), new Vector3(0.5f, 0.4f, 0.05f),
            terminalMat, root, col: false);
        // Kleine Lichter am Terminal
        Box("TermLight1", new Vector3(1.25f, 1.5f, 3.85f), new Vector3(0.05f, 0.05f, 0.02f),
            Emit(new Color(0.20f, 0.85f, 0.30f), new Color(0.4f, 1f, 0.4f), 1.8f), root, col: false);
        Box("TermLight2", new Vector3(1.55f, 1.5f, 3.85f), new Vector3(0.05f, 0.05f, 0.02f),
            Emit(new Color(0.85f, 0.20f, 0.20f), new Color(1f, 0.4f, 0.4f), 1.8f), root, col: false);

        // Innenbeleuchtung
        AddLight("ShedInteriorLight", root, new Vector3(0f, 2.9f, 7f),
            LightType.Point, new Color(0.95f, 0.85f, 0.65f), 1.3f, 7f);

        var interior = new GameObject("ShedInteriorAnchor");
        interior.transform.SetParent(root);
        interior.transform.position = new Vector3(0f, 0f, 7f);

        // ── Hebel links neben der Tür ──────────────────────────────────────
        // Sockel (fest, kein Collider nötig)
        Box("LeverBase", new Vector3(-1.6f, 0.55f, 3.7f),
            new Vector3(0.22f, 1.1f, 0.22f), iron, root);
        // Pivot-Punkt = Basis des beweglichen Griffs
        var leverPivot = new GameObject("LeverPivot");
        leverPivot.transform.SetParent(root);
        leverPivot.transform.position = new Vector3(-1.6f, 1.1f, 3.7f);
        // Griff-Stab (hängt vom Pivot nach oben – Weltpos y=1.55 = Mitte des Stabs)
        Cyl("LeverHandle", new Vector3(-1.6f, 1.55f, 3.7f),
            new Vector3(0.09f, 0.45f, 0.09f), iron, leverPivot.transform,
            rot: Quaternion.identity, col: false);
        // Handgriff-Querstab oben
        Box("LeverGrip", new Vector3(-1.6f, 1.95f, 3.7f),
            new Vector3(0.22f, 0.09f, 0.14f), brass, leverPivot.transform, col: false);
        // Kleines Kontrollicht am Sockel (immer sichtbar, Atmosphäre)
        Box("LeverLight", new Vector3(-1.6f, 1.12f, 3.64f),
            new Vector3(0.08f, 0.08f, 0.02f),
            Emit(new Color(0.15f, 0.75f, 0.25f), new Color(0.30f, 1.0f, 0.35f), 2f),
            root, col: false);

        return new ShedParts
        {
            door        = doorPivot,
            interior    = interior.transform,
            leverHandle = leverPivot,
            leverPos    = new Vector3(-1.6f, 1.1f, 3.7f),
        };
    }

    void AddSignText(Transform root, Vector3 worldPos, string text)
    {
        // TextMeshPro 3D-Text als Schild
        var go = new GameObject("SignText");
        go.transform.SetParent(root);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = 1.0f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.95f, 0.78f, 0.30f);
        var rt = go.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(2f, 0.6f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Innenraum-Werkbank mit Bunsenbrenner
    // ═══════════════════════════════════════════════════════════════════════

    struct InteriorParts
    {
        public GameObject brennerObject;
        public Light      brennerGlow;
        public Vector3    brennerPos;
    }

    InteriorParts BuildInteriorWorkbench(Transform root)
    {
        var woodMid   = M(new Color(0.26f, 0.17f, 0.08f), 0.01f, 0.12f);
        var woodDark  = M(new Color(0.18f, 0.11f, 0.06f), 0.01f, 0.10f);
        var metal     = M(new Color(0.32f, 0.30f, 0.26f), 0.4f, 0.40f);
        var brass     = M(new Color(0.55f, 0.42f, 0.15f), 0.55f, 0.35f);
        var flameMat  = Emit(new Color(1.0f, 0.55f, 0.10f),
                             new Color(1.0f, 0.65f, 0.15f), 2.5f);

        // Werkbank an der Rückwand des Schuppens (Inneres: z ≈ 4..9)
        // Box() setzt Weltpositionen, daher alle z-Coords um 8.4 (Bench-Weltpos) versetzen.
        const float bz = 8.4f;

        var bench = new GameObject("Workbench");
        bench.transform.SetParent(root);
        bench.transform.position = new Vector3(0f, 0f, bz);

        Box("BenchTop",   new Vector3(0f,   0.85f, bz       ), new Vector3(3.0f, 0.10f, 0.9f), woodMid,  bench.transform);
        Box("BenchLegFL", new Vector3(-1.3f, 0.45f, bz - 0.35f), new Vector3(0.10f, 0.85f, 0.10f), woodDark, bench.transform);
        Box("BenchLegFR", new Vector3( 1.3f, 0.45f, bz - 0.35f), new Vector3(0.10f, 0.85f, 0.10f), woodDark, bench.transform);
        Box("BenchLegBL", new Vector3(-1.3f, 0.45f, bz + 0.35f), new Vector3(0.10f, 0.85f, 0.10f), woodDark, bench.transform);
        Box("BenchLegBR", new Vector3( 1.3f, 0.45f, bz + 0.35f), new Vector3(0.10f, 0.85f, 0.10f), woodDark, bench.transform);

        // Werkzeug-Deko links auf der Bank
        Box("Hammer",      new Vector3(-0.9f, 0.92f, bz - 0.05f), new Vector3(0.32f, 0.05f, 0.08f), metal, bench.transform, col: false);
        Box("Wrench",      new Vector3(-1.0f, 0.93f, bz + 0.20f), new Vector3(0.45f, 0.04f, 0.06f), metal, bench.transform, col: false);
        Box("Screwdriver", new Vector3(-0.5f, 0.92f, bz + 0.30f), new Vector3(0.25f, 0.03f, 0.03f), brass, bench.transform, col: false);

        // ── BUNSENBRENNER ──────────────────────────────────────────────────
        // Weltposition des Brenners auf der Werkbank
        Vector3 brennerPos = new Vector3(0.5f, 0.92f, bz);
        var brenner = new GameObject("BunsenBrenner");
        brenner.transform.SetParent(bench.transform);
        brenner.transform.position = brennerPos;

        float bwx = brennerPos.x, bwy = brennerPos.y, bwz = brennerPos.z;
        Cyl("Foot",    new Vector3(bwx, bwy + 0.03f, bwz), new Vector3(0.18f, 0.025f, 0.18f),
            metal, brenner.transform, rot: Quaternion.identity, col: false);
        Cyl("Tube",    new Vector3(bwx, bwy + 0.27f, bwz), new Vector3(0.06f, 0.25f,  0.06f),
            metal, brenner.transform, rot: Quaternion.identity, col: false);
        Cyl("AirRing", new Vector3(bwx, bwy + 0.10f, bwz), new Vector3(0.08f, 0.02f,  0.08f),
            brass, brenner.transform, rot: Quaternion.identity, col: false);
        Cyl("Nozzle",  new Vector3(bwx, bwy + 0.52f, bwz), new Vector3(0.05f, 0.03f,  0.05f),
            brass, brenner.transform, rot: Quaternion.identity, col: false);
        Cyl("Flame",   new Vector3(bwx, bwy + 0.70f, bwz), new Vector3(0.05f, 0.12f,  0.05f),
            flameMat, brenner.transform, rot: Quaternion.identity, col: false);

        // Glow-Spot leuchtet den Brenner an, sobald die Tür offen ist
        var glowGO = new GameObject("BrennerGlow");
        glowGO.transform.SetParent(brenner.transform);
        glowGO.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var glow = glowGO.AddComponent<Light>();
        glow.type      = LightType.Point;
        glow.color     = new Color(1.0f, 0.65f, 0.20f);
        glow.intensity = 2.0f;
        glow.range     = 3.0f;
        glow.shadows   = LightShadows.None;
        glow.enabled   = false;   // wird via SceneFlow aktiviert

        // Werkzeugwand hinter der Bank
        var pegboard = M(new Color(0.20f, 0.13f, 0.07f), 0.02f, 0.10f);
        Box("PegBoard", new Vector3(0f, 1.8f, bz + 0.42f), new Vector3(2.8f, 1.0f, 0.05f),
            pegboard, bench.transform, col: false);

        return new InteriorParts
        {
            brennerObject = brenner,
            brennerGlow   = glow,
            brennerPos    = brennerPos,
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Exit-Marker (leuchtendes Quad auf dem Boden)
    // ═══════════════════════════════════════════════════════════════════════

    GameObject BuildExitMarker(Transform root, Vector3 pos)
    {
        var marker = new GameObject("ExitMarker");
        marker.transform.SetParent(root);
        marker.transform.position = pos;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "MarkerQuad";
        quad.transform.SetParent(marker.transform, false);
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale    = new Vector3(2.5f, 1.4f, 1f);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.GetComponent<Renderer>().sharedMaterial =
            Emit(new Color(0.20f, 0.85f, 0.30f), new Color(0.40f, 1.0f, 0.40f), 2f);

        var markerLight = new GameObject("MarkerLight");
        markerLight.transform.SetParent(marker.transform);
        markerLight.transform.localPosition = new Vector3(0f, 1f, 0f);
        var l = markerLight.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = new Color(0.45f, 0.95f, 0.40f);
        l.intensity = 1.5f;
        l.range     = 4f;

        marker.SetActive(false);   // wird vom SceneFlow nach Pickup eingeschaltet
        return marker;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Trigger-Spot
    // ═══════════════════════════════════════════════════════════════════════

    DustyWallSpot AddSpotTrigger(Scene scene, Transform parent,
                                  Vector3 pos, Vector3 size, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = size;
        return go.AddComponent<DustyWallSpot>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Prompt-Canvas ([E] Hinweis unten am Bildschirm)
    // ═══════════════════════════════════════════════════════════════════════

    GameObject BuildPromptCanvas(Scene scene, string text, Color color)
    {
        var canvasGO = new GameObject($"Prompt_{text.Substring(0, System.Math.Min(text.Length, 10))}");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        var panel    = MakeUIPanel(canvasGO.transform, "Panel",
            new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.22f));
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.65f);

        var txtGO = MakeUIPanel(panel.transform, "Text", Vector2.zero, Vector2.one);
        var txt   = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text      = text;
        txt.fontSize  = 26;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color     = color;

        canvasGO.SetActive(false);
        SceneManager.MoveGameObjectToScene(canvasGO, scene);
        return canvasGO;
    }

    GameObject MakeUIPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return go;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Breadboard-Overlay-Canvas (komplettes Puzzle-UI, initial inaktiv)
    // ═══════════════════════════════════════════════════════════════════════

    (GameObject canvasGO, Level5_Breadboard script) BuildBreadboardOverlay(Scene scene)
    {
        var canvasGO = new GameObject("BreadboardCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12;
        var scaler   = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        BuildBreadboardBackground(canvasGO.transform);
        BuildToolBoard(canvasGO.transform);
        Button        leverButton    = null;
        RectTransform leverHandleUI  = null;
        BuildHudLever(canvasGO.transform, out leverButton, out leverHandleUI);

        TextMeshProUGUI statusText = null;
        RectTransform[] tileRoots = null;
        BuildGamePanel(canvasGO.transform, out tileRoots, out statusText);

        // Logik-Komponente auf eigenem Child (damit es im hidden Canvas mitgeht)
        var scriptGO = new GameObject("Level5Breadboard");
        scriptGO.transform.SetParent(canvasGO.transform, false);
        var breadboard = scriptGO.AddComponent<Level5_Breadboard>();
        var so   = new SerializedObject(breadboard);
        so.FindProperty("statusText").objectReferenceValue    = statusText;
        so.FindProperty("leverButton").objectReferenceValue   = leverButton;
        so.FindProperty("leverHandleUI").objectReferenceValue = leverHandleUI;
        var tileRootsProp = so.FindProperty("tileRoots");
        tileRootsProp.arraySize = tileRoots.Length;
        for (int i = 0; i < tileRoots.Length; i++)
            tileRootsProp.GetArrayElementAtIndex(i).objectReferenceValue = tileRoots[i];
        // Im Overlay-Modus KEIN Auto-Fade!
        so.FindProperty("autoTransitionAfterSolve").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        canvasGO.SetActive(false);
        SceneManager.MoveGameObjectToScene(canvasGO, scene);
        return (canvasGO, breadboard);
    }

    // ─── Hintergrund-Ziegelwand ────────────────────────────────────────────
    void BuildBreadboardBackground(Transform parent)
    {
        var bg = MakePanel(parent, "BG", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.95f);

        Color brick  = new Color(0.18f, 0.10f, 0.07f);
        for (int row = 0; row < 12; row++)
        {
            float y   = -480f + row * 90f;
            float off = (row % 2 == 0) ? 0f : 55f;
            for (int col = 0; col < 18; col++)
            {
                float x = -950f + off + col * 110f;
                MakeAnchored(bg, $"Brick_{row}_{col}", new Vector2(x, y), new Vector2(104f, 42f))
                    .gameObject.AddComponent<Image>().color = brick;
            }
        }
    }

    void BuildToolBoard(Transform parent)
    {
        var board = MakeAnchored(parent, "ToolBoard", new Vector2(-820f, 0f), new Vector2(220f, 700f));
        board.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.09f, 0.06f);

        for (int r = 0; r < 14; r++)
        for (int c = 0; c < 5; c++)
        {
            float hx = -80f + c * 40f;
            float hy =  280f - r * 42f;
            MakeAnchored(board, $"Hole_{r}_{c}", new Vector2(hx, hy), new Vector2(6f, 6f))
                .gameObject.AddComponent<Image>().color = new Color(0.06f, 0.04f, 0.03f);
        }

        AddLabel(board, "LBL", new Vector2(0f, -310f), new Vector2(200f, 22f),
            "WERKZEUGWAND", 11f, new Color(0.50f, 0.35f, 0.12f));
    }

    void BuildHudLever(Transform parent, out Button leverButtonOut, out RectTransform leverHandleUIOut)
    {
        var panel = MakeAnchored(parent, "LeverPanel", new Vector2(820f, 0f), new Vector2(220f, 700f));
        panel.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f);

        AddLabel(panel, "LeverLbl", new Vector2(0f, 270f), new Vector2(200f, 26f),
            "TOR-HEBEL", 13f, new Color(0.55f, 0.35f, 0.10f));
        AddLabel(panel, "InstrLbl", new Vector2(0f, 245f), new Vector2(200f, 18f),
            "KLICK ZUM ÖFFNEN", 9f, new Color(0.40f, 0.30f, 0.10f));

        // Gehäuse-Hintergrund
        var housing = MakeAnchored(panel, "LeverHousing", new Vector2(0f, 40f), new Vector2(90f, 230f));
        housing.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.15f, 0.11f);

        // Sockel
        MakeAnchored(panel, "LeverBaseBar", new Vector2(0f, -65f), new Vector2(70f, 18f))
            .gameObject.AddComponent<Image>().color = new Color(0.28f, 0.24f, 0.18f);

        // ── Hebel-Handle (rotiert beim Klick) ─────────────────────────────
        // pivot = (0.5, 0) → Drehpunkt am unteren Ende des Stabs
        var pivotGO = new GameObject("LeverHandleUI");
        pivotGO.transform.SetParent(panel, false);
        var pivotRT = pivotGO.AddComponent<RectTransform>();
        pivotRT.anchorMin         = pivotRT.anchorMax = new Vector2(0.5f, 0.5f);
        pivotRT.pivot             = new Vector2(0.5f, 0f);   // Drehpunkt unten
        pivotRT.anchoredPosition  = new Vector2(0f, -55f);   // Fußpunkt in Panel-Koords
        pivotRT.sizeDelta         = new Vector2(22f, 130f);
        pivotGO.AddComponent<Image>().color = new Color(0.45f, 0.38f, 0.28f);
        leverHandleUIOut = pivotRT;

        // Griffstück oben am Handle
        var gripGO = new GameObject("Grip");
        gripGO.transform.SetParent(pivotGO.transform, false);
        var gripRT = gripGO.AddComponent<RectTransform>();
        gripRT.anchorMin = gripRT.anchorMax = gripRT.pivot = new Vector2(0.5f, 0.5f);
        gripRT.anchoredPosition = new Vector2(0f, 60f);   // lokal = Spitze des Stabs
        gripRT.sizeDelta = new Vector2(60f, 24f);
        gripGO.AddComponent<Image>().color = new Color(0.65f, 0.48f, 0.18f);

        // ── Button (transparent, überdeckt Gehäuse-Bereich) ───────────────
        var btnGO = new GameObject("LeverButton");
        btnGO.transform.SetParent(panel, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = btnRT.anchorMax = btnRT.pivot = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = new Vector2(0f, 40f);
        btnRT.sizeDelta = new Vector2(160f, 280f);
        btnGO.AddComponent<Image>().color = Color.clear;
        var btn = btnGO.AddComponent<Button>();
        var cols = btn.colors;
        cols.normalColor      = Color.clear;
        cols.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        cols.pressedColor     = new Color(1f, 1f, 1f, 0.18f);
        cols.disabledColor    = Color.clear;
        btn.colors = cols;
        leverButtonOut = btn;
    }

    void BuildGamePanel(Transform parent, out RectTransform[] tileRoots, out TextMeshProUGUI statusOut)
    {
        tileRoots = new RectTransform[25];
        statusOut = null;

        var outerFrame = MakeAnchored(parent, "OuterFrame", new Vector2(0f, 0f), new Vector2(680f, 740f));
        outerFrame.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.20f, 0.17f);

        var innerFrame = MakeAnchored(outerFrame, "InnerFrame", Vector2.zero, new Vector2(672f, 732f));
        innerFrame.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f);

        var titleBar = MakeAnchored(innerFrame, "TitleBar", new Vector2(0f, 335f), new Vector2(672f, 60f));
        titleBar.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.10f, 0.05f);
        AddLabel(titleBar, "Title", Vector2.zero, new Vector2(560f, 44f),
            "SCHALTKREIS REPARIEREN  —  WERKSTATT-SCHUPPEN", 22f,
            new Color(0.92f, 0.78f, 0.30f));

        AddLabel(innerFrame, "Hint", new Vector2(0f, 295f), new Vector2(600f, 24f),
            "WASD = Tile auswählen  |  ENTER / LEERTASTE = Drehen  |  Verbinde Quelle mit Brenner",
            12f, new Color(0.55f, 0.48f, 0.30f));

        float tileSpacing = 80f;
        float tileSize    = 72f;

        var pcb = MakeAnchored(innerFrame, "PCB", new Vector2(0f, 30f), new Vector2(448f, 448f));
        pcb.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.09f, 0.04f);

        for (int r = 0; r < 5; r++)
        for (int c = 0; c < 5; c++)
        {
            int idx  = r * 5 + c;
            float tx = (c - 2) * tileSpacing;
            float ty = (2 - r) * tileSpacing;
            tileRoots[idx] = BuildTile(pcb, idx, new Vector2(tx, ty), tileSize);
        }

        var statusGO  = new GameObject("StatusText");
        var statusRT  = statusGO.AddComponent<RectTransform>();
        statusRT.SetParent(innerFrame, false);
        statusRT.anchorMin = new Vector2(0f, 0f); statusRT.anchorMax = new Vector2(1f, 0f);
        statusRT.offsetMin = new Vector2(10f, 8f); statusRT.offsetMax = new Vector2(-10f, 38f);
        var stxt = statusGO.AddComponent<TextMeshProUGUI>();
        stxt.text = string.Empty; stxt.fontSize = 18f;
        stxt.alignment = TextAlignmentOptions.Center;
        stxt.color = new Color(0.90f, 0.82f, 0.30f);
        statusOut = stxt;
    }

    RectTransform BuildTile(RectTransform parent, int idx, Vector2 pos, float size)
    {
        int   type  = TILE_TYPES[idx];
        float half  = size * 0.5f;
        float pipeW = 13f;
        float pipeH = half + 1f;

        var root = MakeAnchored(parent, $"Tile_{idx / 5}_{idx % 5}", pos, new Vector2(size, size));

        Color bgCol = type == 0 ? new Color(0.03f, 0.07f, 0.03f)
                    : type == 3 ? new Color(0.14f, 0.09f, 0.03f)
                    : type == 4 ? new Color(0.08f, 0.07f, 0.04f)
                    :             new Color(0.06f, 0.12f, 0.06f);
        root.gameObject.AddComponent<Image>().color = bgCol;

        var selFrame = MakeAnchored(root, "SelFrame", Vector2.zero, new Vector2(size - 2f, size - 2f));
        var sfImg    = selFrame.gameObject.AddComponent<Image>();
        sfImg.color  = new Color(0.95f, 0.92f, 0.15f, 0f);
        var sfOutline = selFrame.gameObject.AddComponent<Outline>();
        sfOutline.effectColor    = new Color(0.95f, 0.92f, 0.15f);
        sfOutline.effectDistance = new Vector2(3f, 3f);
        selFrame.gameObject.SetActive(false);

        if (type == 0) return root;

        var interior = MakeAnchored(root, "Interior", Vector2.zero, new Vector2(size, size));
        int rot = START_ROT[idx];
        interior.localRotation = Quaternion.Euler(0, 0, -90f * rot);

        Color pipeColor = new Color(0.22f, 0.26f, 0.32f);

        MakeAnchored(interior, "PipeC", Vector2.zero, new Vector2(pipeW, pipeW))
            .gameObject.AddComponent<Image>().color = pipeColor;

        switch (type)
        {
            case 1:
                MakeAnchored(interior, "PipeN", new Vector2(0f,  pipeH * 0.5f), new Vector2(pipeW, pipeH))
                    .gameObject.AddComponent<Image>().color = pipeColor;
                MakeAnchored(interior, "PipeS", new Vector2(0f, -pipeH * 0.5f), new Vector2(pipeW, pipeH))
                    .gameObject.AddComponent<Image>().color = pipeColor;
                break;
            case 2:
                MakeAnchored(interior, "PipeN", new Vector2(0f, pipeH * 0.5f), new Vector2(pipeW, pipeH))
                    .gameObject.AddComponent<Image>().color = pipeColor;
                MakeAnchored(interior, "PipeE", new Vector2(pipeH * 0.5f, 0f), new Vector2(pipeH, pipeW))
                    .gameObject.AddComponent<Image>().color = pipeColor;
                break;
            case 3:
                MakeAnchored(interior, "PipeE", new Vector2(pipeH * 0.5f, 0f), new Vector2(pipeH, pipeW))
                    .gameObject.AddComponent<Image>().color = new Color(0.95f, 0.65f, 0.05f);
                break;
            case 4:
                MakeAnchored(interior, "PipeN", new Vector2(0f, pipeH * 0.5f), new Vector2(pipeW, pipeH))
                    .gameObject.AddComponent<Image>().color = new Color(0.35f, 0.20f, 0.06f);
                break;
        }

        if (type != 0 && type != 3 && type != 4)
        {
            int capturedRow = idx / 5, capturedCol = idx % 5;
            var btn = root.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                var script = Object.FindFirstObjectByType<Level5_Breadboard>();
                if (script != null) script.RotateTile(capturedRow, capturedCol);
            });
        }
        return root;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UI Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════════

    void AddLabel(RectTransform parent, string name, Vector2 pos, Vector2 size,
                  string text, float fs, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = fs;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = col; txt.fontStyle = FontStyles.Bold;
    }

    RectTransform MakePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        return rt;
    }

    RectTransform MakeAnchored(RectTransform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    RectTransform MakeAnchored(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Spieler (BigYahu, gleicher Aufbau wie Level 3)
    // ═══════════════════════════════════════════════════════════════════════

    GameObject AddPlayer(Scene scene, Vector3 spawnPos)
    {
        var idleModel    = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Big Yahu standing.fbx");
        var runningModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Big Yahu jogging.fbx");
        var playerMat    = AssetDatabase.LoadAssetAtPath<Material>("Assets/Big Yahu/Big Yahu material.mat");

        var character = new GameObject("BigYahu") { tag = "Player" };
        character.transform.position = spawnPos;
        character.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

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

    void SetupLoopController(GameObject instance, string fbxPath, string clipPath,
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

    void AddBackgroundMusic(Scene scene)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Big Yahu/Untitled.mp3");
        if (clip == null) return;
        var go  = new GameObject("BackgroundMusic");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip; src.loop = true; src.playOnAwake = true;
        src.volume = 0.5f; src.spatialBlend = 0f;
        go.AddComponent<BackgroundMusic>();
        SceneManager.MoveGameObjectToScene(go, scene);
    }
}
