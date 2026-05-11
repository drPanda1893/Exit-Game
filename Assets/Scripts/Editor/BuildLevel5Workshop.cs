using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Baut Level 5 – Werkstatt / Bunsenbrenner-Schaltkreis-Puzzle.
/// Menü: Tools → Build Level 5 Workshop
/// </summary>
public class BuildLevel5Workshop : EditorWindow
{
    [MenuItem("Tools/Build Level 5 Workshop")]
    public static void ShowWindow() => GetWindow<BuildLevel5Workshop>("Level 5 Builder");

    void OnGUI()
    {
        GUILayout.Label("Level 5 – Werkstatt: Bunsenbrenner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Schaltkreis-Rotations-Puzzle. Drehe Kabel-Segmente bis\n" +
            "Strom von der Quelle zum Bunsenbrenner fließt.",
            MessageType.Info);
        GUILayout.Space(10);
        if (GUILayout.Button("Level 5 bauen", GUILayout.Height(36)))
            Build();
    }

    // Tile-Typen (identisch mit Level5_Breadboard) – für Visualisierung
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

    // ═══════════════════════════════════════════════════════════════════
    void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level5.unity");

        // Kamera
        var camGO = new GameObject("Main Camera");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.05f, 0.04f);
        cam.tag             = "MainCamera";
        camGO.AddComponent<AudioListener>();

        // Canvas
        var canvasGO = new GameObject("Level5Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler   = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Hintergrund
        BuildBackground(canvasGO.transform);

        // Linkes Werkzeug-Panel
        BuildToolBoard(canvasGO.transform);

        // Rechtes Bunsenbrenner-Panel
        Image burnerFlame = null;
        BuildBurnerShowcase(canvasGO.transform, out burnerFlame);

        // Zentrales Spielfeld
        TextMeshProUGUI statusText = null;
        RectTransform[] tileRoots = null;
        BuildGamePanel(canvasGO.transform, out tileRoots, out statusText);

        // Level5_Breadboard Script
        var scriptGO = new GameObject("Level5Breadboard");
        scriptGO.transform.SetParent(canvasGO.transform, false);
        var breadboard = scriptGO.AddComponent<Level5_Breadboard>();
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;
        var type = typeof(Level5_Breadboard);
        type.GetField("tileRoots",   bf)?.SetValue(breadboard, tileRoots);
        type.GetField("statusText",  bf)?.SetValue(breadboard, statusText);
        type.GetField("burnerFlame", bf)?.SetValue(breadboard, burnerFlame);

        // Musik
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Big Yahu/Untitled.mp3");
        if (clip != null)
        {
            var music = new GameObject("Music");
            var src   = music.AddComponent<AudioSource>();
            src.clip = clip; src.loop = true; src.playOnAwake = true; src.volume = 0.4f;
            SceneManager.MoveGameObjectToScene(music, scene);
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level5] Werkstatt fertig gebaut.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Atmosphärischer Hintergrund (Werkstatt-Mauer)
    // ═══════════════════════════════════════════════════════════════════
    void BuildBackground(Transform parent)
    {
        var bg = MakePanel(parent, "BG", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.06f, 0.05f);

        // Ziegelwand-Muster (horizontale Reihen mit versetzten Steinen)
        Color brick  = new Color(0.18f, 0.10f, 0.07f);
        Color mortar = new Color(0.10f, 0.07f, 0.05f);
        for (int row = 0; row < 12; row++)
        {
            float y   = -480f + row * 90f;
            float off = (row % 2 == 0) ? 0f : 55f;
            for (int col = 0; col < 18; col++)
            {
                float x = -950f + off + col * 110f;
                MakeAnchored(bg, $"Brick_{row}_{col}", new Vector2(x, y), new Vector2(104f, 42f))
                    .gameObject.AddComponent<Image>().color = brick;
                // Fugen-Linie unten
                MakeAnchored(bg, $"Mortar_{row}_{col}", new Vector2(x, y - 22f), new Vector2(104f, 4f))
                    .gameObject.AddComponent<Image>().color = mortar;
            }
        }

        // Oberes Lichtband (Werkstatt-Deckenleuchten)
        for (int i = 0; i < 4; i++)
        {
            float lx = -660f + i * 440f;
            var lamp = MakeAnchored(bg, $"Lamp_{i}", new Vector2(lx, 490f), new Vector2(120f, 16f));
            lamp.gameObject.AddComponent<Image>().color = new Color(1f, 0.92f, 0.72f);
            // Lichtkegel nach unten
            MakeAnchored(bg, $"Glow_{i}", new Vector2(lx, 350f), new Vector2(260f, 280f))
                .gameObject.AddComponent<Image>().color = new Color(1f, 0.85f, 0.55f, 0.06f);
        }

        // Untere Boden-Platte
        MakeAnchored(bg, "Floor", new Vector2(0f, -490f), new Vector2(1920f, 60f))
            .gameObject.AddComponent<Image>().color = new Color(0.12f, 0.10f, 0.08f);
        // Boden-Reflexion
        MakeAnchored(bg, "FloorReflect", new Vector2(0f, -475f), new Vector2(1920f, 4f))
            .gameObject.AddComponent<Image>().color = new Color(0.40f, 0.32f, 0.18f, 0.4f);

        // Kupfer-Rohre (horizontal, Werkstatt-Atmosphäre)
        Color copper = new Color(0.55f, 0.28f, 0.08f);
        MakeAnchored(bg, "Pipe1", new Vector2(0f, 400f), new Vector2(1920f, 8f))
            .gameObject.AddComponent<Image>().color = copper;
        MakeAnchored(bg, "PipeFit1L", new Vector2(-900f, 400f), new Vector2(18f, 18f))
            .gameObject.AddComponent<Image>().color = new Color(copper.r * 0.7f, copper.g * 0.7f, copper.b * 0.7f);
        MakeAnchored(bg, "PipeFit1R", new Vector2( 900f, 400f), new Vector2(18f, 18f))
            .gameObject.AddComponent<Image>().color = new Color(copper.r * 0.7f, copper.g * 0.7f, copper.b * 0.7f);

        MakeAnchored(bg, "Pipe2", new Vector2(0f, -380f), new Vector2(1920f, 6f))
            .gameObject.AddComponent<Image>().color = new Color(copper.r * 0.8f, copper.g * 0.8f, copper.b * 0.8f);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Linkes Werkzeug-Panel (Werkzeugwand)
    // ═══════════════════════════════════════════════════════════════════
    void BuildToolBoard(Transform parent)
    {
        var board = MakeAnchored(parent, "ToolBoard", new Vector2(-820f, 0f), new Vector2(220f, 700f));
        board.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.09f, 0.06f);

        // Rand-Streifen
        MakeAnchored(board, "BorderR", new Vector2(108f, 0f), new Vector2(4f, 700f))
            .gameObject.AddComponent<Image>().color = new Color(0.35f, 0.22f, 0.08f);

        // Pegboard-Loch-Muster
        for (int r = 0; r < 14; r++)
        for (int c = 0; c < 5; c++)
        {
            float hx = -80f + c * 40f;
            float hy =  280f - r * 42f;
            MakeAnchored(board, $"Hole_{r}_{c}", new Vector2(hx, hy), new Vector2(6f, 6f))
                .gameObject.AddComponent<Image>().color = new Color(0.06f, 0.04f, 0.03f);
        }

        // Werkzeug-Silhouetten (Hammer, Schraubenschlüssel, Schraubenzieher, Zange, Lineal)
        BuildToolSilhouette(board, "Hammer",   new Vector2( 30f,  220f), new Vector2(16f, 90f), new Color(0.25f, 0.18f, 0.10f));
        BuildToolSilhouette(board, "HammerH",  new Vector2( 30f,  260f), new Vector2(44f, 18f), new Color(0.25f, 0.18f, 0.10f));
        BuildToolSilhouette(board, "Wrench",   new Vector2(-30f,  160f), new Vector2(14f, 80f), new Color(0.22f, 0.22f, 0.26f));
        BuildToolSilhouette(board, "WrenchH",  new Vector2(-30f,  196f), new Vector2(34f, 12f), new Color(0.22f, 0.22f, 0.26f));
        BuildToolSilhouette(board, "Screw",    new Vector2( 60f,   40f), new Vector2(10f, 100f),new Color(0.22f, 0.20f, 0.18f));
        BuildToolSilhouette(board, "ScrewT",   new Vector2( 60f,   90f), new Vector2(28f,  8f), new Color(0.22f, 0.20f, 0.18f));
        BuildToolSilhouette(board, "Ruler",    new Vector2(-50f,  -30f), new Vector2(8f,  130f),new Color(0.55f, 0.40f, 0.12f));
        BuildToolSilhouette(board, "RulerT",   new Vector2(-50f,   34f), new Vector2(30f,  6f), new Color(0.55f, 0.40f, 0.12f));
        BuildToolSilhouette(board, "Pliers",   new Vector2( 20f, -120f), new Vector2(12f,  90f),new Color(0.24f, 0.22f, 0.20f));
        BuildToolSilhouette(board, "PliersH",  new Vector2( 20f,  -76f), new Vector2(38f, 10f), new Color(0.24f, 0.22f, 0.20f));
        BuildToolSilhouette(board, "PliersTip",new Vector2(30f, -124f),  new Vector2(10f, 12f), new Color(0.20f, 0.18f, 0.16f));

        // Label
        AddLabel(board, "LBL", new Vector2(0f, -310f), new Vector2(200f, 22f),
            "WERKZEUGWAND", 11f, new Color(0.50f, 0.35f, 0.12f));
    }

    void BuildToolSilhouette(RectTransform parent, string name, Vector2 pos, Vector2 size, Color col)
    {
        MakeAnchored(parent, name, pos, size).gameObject.AddComponent<Image>().color = col;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Rechtes Bunsenbrenner-Panel
    // ═══════════════════════════════════════════════════════════════════
    void BuildBurnerShowcase(Transform parent, out Image burnerFlameOut)
    {
        burnerFlameOut = null;

        var panel = MakeAnchored(parent, "BurnerPanel", new Vector2(820f, 0f), new Vector2(220f, 700f));
        panel.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f);

        // Rand links
        MakeAnchored(panel, "BorderL", new Vector2(-108f, 0f), new Vector2(4f, 700f))
            .gameObject.AddComponent<Image>().color = new Color(0.35f, 0.22f, 0.08f);

        // Arbeitsplatten-Oberfläche
        var bench = MakeAnchored(panel, "Bench", new Vector2(0f, -160f), new Vector2(210f, 220f));
        bench.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.16f, 0.10f);
        MakeAnchored(bench, "BenchEdge", new Vector2(0f, 108f), new Vector2(210f, 6f))
            .gameObject.AddComponent<Image>().color = new Color(0.30f, 0.22f, 0.12f);

        // Gas-Leitung (Rohr zum Brenner)
        MakeAnchored(bench, "GasPipe", new Vector2(-60f, 60f), new Vector2(8f, 80f))
            .gameObject.AddComponent<Image>().color = new Color(0.35f, 0.32f, 0.28f);
        MakeAnchored(bench, "GasPipeH", new Vector2(-20f, 98f), new Vector2(88f, 8f))
            .gameObject.AddComponent<Image>().color = new Color(0.35f, 0.32f, 0.28f);
        // Hahn
        MakeAnchored(bench, "GasValve", new Vector2(-60f, 98f), new Vector2(16f, 16f))
            .gameObject.AddComponent<Image>().color = new Color(0.60f, 0.20f, 0.08f);
        MakeAnchored(bench, "ValveHandle", new Vector2(-60f, 107f), new Vector2(28f, 6f))
            .gameObject.AddComponent<Image>().color = new Color(0.70f, 0.25f, 0.08f);

        // === Bunsenbrenner-Körper ===
        // Fuß
        var foot = MakeAnchored(bench, "BurnerFoot", new Vector2(20f, -78f), new Vector2(50f, 10f));
        foot.gameObject.AddComponent<Image>().color = new Color(0.30f, 0.28f, 0.24f);
        // Rohr
        var tube = MakeAnchored(bench, "BurnerTube", new Vector2(20f, 10f), new Vector2(14f, 200f));
        tube.gameObject.AddComponent<Image>().color = new Color(0.32f, 0.30f, 0.26f);
        // Luftregulierung
        MakeAnchored(bench, "AirRing", new Vector2(20f, -30f), new Vector2(22f, 12f))
            .gameObject.AddComponent<Image>().color = new Color(0.40f, 0.36f, 0.30f);
        // Düse oben
        MakeAnchored(bench, "Nozzle", new Vector2(20f, 110f), new Vector2(18f, 14f))
            .gameObject.AddComponent<Image>().color = new Color(0.28f, 0.26f, 0.22f);

        // === Flamme (starts dunkel, leuchtet beim Lösen auf) ===
        var flame1 = MakeAnchored(bench, "Flame1", new Vector2(20f, 148f), new Vector2(20f, 52f));
        flame1.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.12f, 0.08f);
        var flame2 = MakeAnchored(bench, "Flame2", new Vector2(14f, 165f), new Vector2(12f, 30f));
        flame2.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.10f, 0.06f);
        var flame3 = MakeAnchored(bench, "Flame3", new Vector2(26f, 162f), new Vector2(10f, 24f));
        flame3.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.10f, 0.06f);
        // Das ist das Image, das vom Script hell-orange gemacht wird:
        burnerFlameOut = flame1.gameObject.GetComponent<Image>();

        // Labels
        AddLabel(panel, "BurnerLbl", new Vector2(0f, 210f), new Vector2(200f, 22f),
            "BUNSENBRENNER", 11f, new Color(0.55f, 0.35f, 0.10f));
        AddLabel(panel, "StatusLbl", new Vector2(0f, 185f), new Vector2(200f, 18f),
            "STATUS: INAKTIV", 9f, new Color(0.40f, 0.30f, 0.10f));
        AddLabel(panel, "BottomLbl", new Vector2(0f, -310f), new Vector2(200f, 22f),
            "GASDRUCK: 2.4 BAR", 9f, new Color(0.30f, 0.28f, 0.22f));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Zentrales Spielfeld
    // ═══════════════════════════════════════════════════════════════════
    void BuildGamePanel(Transform parent, out RectTransform[] tileRoots, out TextMeshProUGUI statusOut)
    {
        tileRoots = new RectTransform[25];
        statusOut = null;

        // Äußerer Stahlrahmen
        var outerFrame = MakeAnchored(parent, "OuterFrame", new Vector2(0f, 0f), new Vector2(680f, 740f));
        outerFrame.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.20f, 0.17f);
        // Eckbolzen
        Color bolt = new Color(0.40f, 0.35f, 0.22f);
        foreach (var (bx, by) in new[]{(-330f,-360f),(330f,-360f),(-330f,360f),(330f,360f)})
            MakeBolt(outerFrame, bx, by, bolt);

        // Innerer Rahmen
        var innerFrame = MakeAnchored(outerFrame, "InnerFrame", Vector2.zero, new Vector2(672f, 732f));
        innerFrame.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f);

        // ── Titelleiste ──────────────────────────────────────────────
        var titleBar = MakeAnchored(innerFrame, "TitleBar", new Vector2(0f, 335f), new Vector2(672f, 60f));
        titleBar.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.10f, 0.05f);
        MakeAnchored(titleBar, "AccL", new Vector2(-280f, 0f), new Vector2(2f, 44f))
            .gameObject.AddComponent<Image>().color = new Color(0.80f, 0.55f, 0.10f);
        MakeAnchored(titleBar, "AccR", new Vector2( 280f, 0f), new Vector2(2f, 44f))
            .gameObject.AddComponent<Image>().color = new Color(0.80f, 0.55f, 0.10f);
        AddLabel(titleBar, "Title", Vector2.zero, new Vector2(560f, 44f),
            "SCHALTKREIS-REPARATUR  —  LEVEL 05", 22f, new Color(0.92f, 0.78f, 0.30f));

        // ── Anleitung ────────────────────────────────────────────────
        AddLabel(innerFrame, "Hint", new Vector2(0f, 295f), new Vector2(600f, 24f),
            "WASD = Tile auswählen  |  ENTER / LEERTASTE = Drehen  |  Verbinde Quelle mit Brenner",
            12f, new Color(0.55f, 0.48f, 0.30f));

        // ── PCB-Board (Leiterplattenoptik) ────────────────────────────
        float tileSpacing = 80f;
        float tileSize    = 72f;

        var pcb = MakeAnchored(innerFrame, "PCB", new Vector2(0f, 30f), new Vector2(448f, 448f));
        pcb.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.09f, 0.04f);

        // PCB-Rand-Akzent
        MakeAnchored(pcb, "BorderT", new Vector2(0f,  222f), new Vector2(448f, 4f))
            .gameObject.AddComponent<Image>().color = new Color(0.18f, 0.38f, 0.14f);
        MakeAnchored(pcb, "BorderB", new Vector2(0f, -222f), new Vector2(448f, 4f))
            .gameObject.AddComponent<Image>().color = new Color(0.18f, 0.38f, 0.14f);
        MakeAnchored(pcb, "BorderL", new Vector2(-222f,  0f), new Vector2(4f, 448f))
            .gameObject.AddComponent<Image>().color = new Color(0.18f, 0.38f, 0.14f);
        MakeAnchored(pcb, "BorderR", new Vector2( 222f,  0f), new Vector2(4f, 448f))
            .gameObject.AddComponent<Image>().color = new Color(0.18f, 0.38f, 0.14f);

        // PCB-Trace-Linien (dekorativ)
        for (int i = 0; i < 5; i++)
        {
            float p = -160f + i * 80f;
            MakeAnchored(pcb, $"TraceH{i}", new Vector2(0f, p), new Vector2(440f, 1f))
                .gameObject.AddComponent<Image>().color = new Color(0.10f, 0.20f, 0.08f, 0.5f);
            MakeAnchored(pcb, $"TraceV{i}", new Vector2(p, 0f), new Vector2(1f, 440f))
                .gameObject.AddComponent<Image>().color = new Color(0.10f, 0.20f, 0.08f, 0.5f);
        }

        // ── Stromquelle-Anzeige (links vom Grid) ──────────────────────
        var srcPanel = MakeAnchored(innerFrame, "SrcPanel", new Vector2(-295f, 30f), new Vector2(80f, 90f));
        srcPanel.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.10f, 0.04f);
        // Batterie-Körper
        MakeAnchored(srcPanel, "BatBody", new Vector2(0f, 5f), new Vector2(32f, 52f))
            .gameObject.AddComponent<Image>().color = new Color(0.22f, 0.18f, 0.06f);
        MakeAnchored(srcPanel, "BatPos",  new Vector2(0f, 33f), new Vector2(16f, 10f))
            .gameObject.AddComponent<Image>().color = new Color(0.60f, 0.52f, 0.12f);
        MakeAnchored(srcPanel, "BatStripe1", new Vector2(0f,  8f), new Vector2(30f, 8f))
            .gameObject.AddComponent<Image>().color = new Color(0.55f, 0.45f, 0.10f);
        MakeAnchored(srcPanel, "BatStripe2", new Vector2(0f, -4f), new Vector2(30f, 8f))
            .gameObject.AddComponent<Image>().color = new Color(0.12f, 0.10f, 0.04f);
        // Anschluss-Pfeil
        MakeAnchored(srcPanel, "Arrow", new Vector2(30f, 5f), new Vector2(14f, 8f))
            .gameObject.AddComponent<Image>().color = new Color(0.95f, 0.65f, 0.05f);
        AddLabel(srcPanel, "Lbl", new Vector2(0f, -36f), new Vector2(78f, 18f),
            "QUELLE", 9f, new Color(0.80f, 0.60f, 0.12f));

        // ── Ziel-Anzeige (rechts vom Grid) ────────────────────────────
        var dstPanel = MakeAnchored(innerFrame, "DstPanel", new Vector2(295f, 30f), new Vector2(80f, 90f));
        dstPanel.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.07f, 0.05f);
        // Mini-Bunsenbrenner
        MakeAnchored(dstPanel, "MiniFoot",  new Vector2(0f, -22f), new Vector2(28f, 6f))
            .gameObject.AddComponent<Image>().color = new Color(0.32f, 0.28f, 0.24f);
        MakeAnchored(dstPanel, "MiniTube",  new Vector2(0f,   8f), new Vector2(8f, 56f))
            .gameObject.AddComponent<Image>().color = new Color(0.32f, 0.28f, 0.24f);
        MakeAnchored(dstPanel, "MiniFlame", new Vector2(0f,  38f), new Vector2(12f, 20f))
            .gameObject.AddComponent<Image>().color = new Color(0.25f, 0.15f, 0.06f);
        // Anschluss-Pfeil (links)
        MakeAnchored(dstPanel, "Arrow", new Vector2(-30f, 5f), new Vector2(14f, 8f))
            .gameObject.AddComponent<Image>().color = new Color(0.95f, 0.65f, 0.05f);
        AddLabel(dstPanel, "Lbl", new Vector2(0f, -36f), new Vector2(78f, 18f),
            "BRENNER", 9f, new Color(0.80f, 0.60f, 0.12f));

        // ── Tiles (5×5) ───────────────────────────────────────────────
        for (int r = 0; r < 5; r++)
        for (int c = 0; c < 5; c++)
        {
            int   idx  = r * 5 + c;
            float tx   = (c - 2) * tileSpacing;
            float ty   = (2 - r) * tileSpacing;
            var tileRT = BuildTile(pcb, idx, new Vector2(tx, ty), tileSize);
            tileRoots[idx] = tileRT;
        }

        // ── Status-Text (unten) ───────────────────────────────────────
        var statusGO  = new GameObject("StatusText");
        var statusRT  = statusGO.AddComponent<RectTransform>();
        statusRT.SetParent(innerFrame, false);
        statusRT.anchorMin = new Vector2(0f, 0f);
        statusRT.anchorMax = new Vector2(1f, 0f);
        statusRT.offsetMin = new Vector2(10f, 8f);
        statusRT.offsetMax = new Vector2(-10f, 38f);
        var stxt = statusGO.AddComponent<TextMeshProUGUI>();
        stxt.text = string.Empty; stxt.fontSize = 18f;
        stxt.alignment = TextAlignmentOptions.Center;
        stxt.color = new Color(0.90f, 0.82f, 0.30f);
        statusOut = stxt;

        // ── Untere Info-Leiste ─────────────────────────────────────────
        var botBar = MakeAnchored(innerFrame, "BotBar", new Vector2(0f, -330f), new Vector2(672f, 50f));
        botBar.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.05f);
        AddLabel(botBar, "Info", new Vector2(0f, 0f), new Vector2(650f, 36f),
            "WERKSTATT  —  ELEKTRISCHES SCHALTKREIS-SYSTEM  —  SICHERHEITSSTUFE 5",
            11f, new Color(0.35f, 0.28f, 0.14f));
    }

    // ─────────────────────────────────────────────────────────────────
    // Einzelnes Tile bauen
    // ─────────────────────────────────────────────────────────────────
    RectTransform BuildTile(RectTransform parent, int idx, Vector2 pos, float size)
    {
        int type = TILE_TYPES[idx];
        float half  = size * 0.5f;
        float pipeW = 13f;
        float pipeH = half + 1f;   // from center to edge

        // Tile-Root
        var root = MakeAnchored(parent, $"Tile_{idx / 5}_{idx % 5}", pos, new Vector2(size, size));

        // Hintergrund (je nach Typ)
        Color bgCol = type == 0 ? new Color(0.03f, 0.07f, 0.03f)
                    : type == 3 ? new Color(0.14f, 0.09f, 0.03f)
                    : type == 4 ? new Color(0.08f, 0.07f, 0.04f)
                    :             new Color(0.06f, 0.12f, 0.06f);
        root.gameObject.AddComponent<Image>().color = bgCol;

        // Tile-Rand
        Color borderCol = type == 0 ? new Color(0.06f, 0.11f, 0.06f) : new Color(0.14f, 0.28f, 0.10f);
        MakeAnchored(root, "BorderT", new Vector2(0f,  half - 1f), new Vector2(size, 2f)).gameObject.AddComponent<Image>().color = borderCol;
        MakeAnchored(root, "BorderB", new Vector2(0f, -half + 1f), new Vector2(size, 2f)).gameObject.AddComponent<Image>().color = borderCol;
        MakeAnchored(root, "BorderL", new Vector2(-half + 1f, 0f), new Vector2(2f, size)).gameObject.AddComponent<Image>().color = borderCol;
        MakeAnchored(root, "BorderR", new Vector2( half - 1f, 0f), new Vector2(2f, size)).gameObject.AddComponent<Image>().color = borderCol;

        // Auswahl-Rahmen (gelb, zunächst inaktiv)
        var selFrame = MakeAnchored(root, "SelFrame", Vector2.zero, new Vector2(size - 2f, size - 2f));
        selFrame.gameObject.AddComponent<Image>().color = new Color(0.95f, 0.90f, 0.15f, 0f);
        MakeAnchored(selFrame, "SelBT", new Vector2(0f,  half - 3f), new Vector2(size - 4f, 3f)).gameObject.AddComponent<Image>().color = new Color(0.95f, 0.92f, 0.15f);
        MakeAnchored(selFrame, "SelBB", new Vector2(0f, -half + 3f), new Vector2(size - 4f, 3f)).gameObject.AddComponent<Image>().color = new Color(0.95f, 0.92f, 0.15f);
        MakeAnchored(selFrame, "SelBL", new Vector2(-half + 3f, 0f), new Vector2(3f, size - 4f)).gameObject.AddComponent<Image>().color = new Color(0.95f, 0.92f, 0.15f);
        MakeAnchored(selFrame, "SelBR", new Vector2( half - 3f, 0f), new Vector2(3f, size - 4f)).gameObject.AddComponent<Image>().color = new Color(0.95f, 0.92f, 0.15f);
        selFrame.gameObject.SetActive(false);

        if (type == 0) return root;  // Leeres Tile – fertig

        // Interior (rotierbar) – enthält Pipe-Segmente
        var interior = MakeAnchored(root, "Interior", Vector2.zero, new Vector2(size, size));
        // Startrotation anwenden
        int rot = START_ROT[idx];
        interior.localRotation = Quaternion.Euler(0, 0, -90f * rot);

        Color pipeColor = new Color(0.22f, 0.26f, 0.32f); // neutral, wird zur Laufzeit gefärbt

        // Center-Knoten (immer vorhanden, wenn Verbindungen)
        MakeAnchored(interior, "PipeC", Vector2.zero, new Vector2(pipeW, pipeW))
            .gameObject.AddComponent<Image>().color = pipeColor;

        switch (type)
        {
            case 1: // Gerade (N+S in Basis)
                MakeAnchored(interior, "PipeN", new Vector2(0f,  pipeH * 0.5f), new Vector2(pipeW, pipeH)).gameObject.AddComponent<Image>().color = pipeColor;
                MakeAnchored(interior, "PipeS", new Vector2(0f, -pipeH * 0.5f), new Vector2(pipeW, pipeH)).gameObject.AddComponent<Image>().color = pipeColor;
                break;

            case 2: // Ecke (N+E in Basis)
                MakeAnchored(interior, "PipeN", new Vector2(0f,  pipeH * 0.5f), new Vector2(pipeW, pipeH)).gameObject.AddComponent<Image>().color = pipeColor;
                MakeAnchored(interior, "PipeE", new Vector2(pipeH * 0.5f, 0f),  new Vector2(pipeH, pipeW)).gameObject.AddComponent<Image>().color = pipeColor;
                break;

            case 3: // Quelle – exits East (Orange, fix)
            {
                Color srcCol = new Color(0.95f, 0.65f, 0.05f);
                MakeAnchored(interior, "PipeE", new Vector2(pipeH * 0.5f, 0f), new Vector2(pipeH, pipeW)).gameObject.AddComponent<Image>().color = srcCol;
                // Batterie-Minus-Symbol
                MakeAnchored(interior, "Sym1", new Vector2(-10f, 4f),  new Vector2(16f, 3f)).gameObject.AddComponent<Image>().color = new Color(0.70f, 0.50f, 0.10f);
                MakeAnchored(interior, "Sym2", new Vector2(-10f, -4f), new Vector2(10f, 3f)).gameObject.AddComponent<Image>().color = new Color(0.70f, 0.50f, 0.10f);
                break;
            }
            case 4: // Ziel – receives North (dunkler Brenner-Stub)
            {
                Color dstCol = new Color(0.35f, 0.20f, 0.06f);
                MakeAnchored(interior, "PipeN", new Vector2(0f, pipeH * 0.5f), new Vector2(pipeW, pipeH)).gameObject.AddComponent<Image>().color = dstCol;
                // Mini-Flammen-Symbol
                MakeAnchored(interior, "Flame", new Vector2(0f, -12f), new Vector2(14f, 22f)).gameObject.AddComponent<Image>().color = new Color(0.20f, 0.12f, 0.05f);
                break;
            }
        }

        // Klick-Handler (Button auf Root damit Maus-Klick auch dreht)
        if (type != 0 && type != 3 && type != 4)
        {
            int capturedIdx = idx;
            int capturedRow = idx / 5, capturedCol = idx % 5;
            var btn = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.onClick.AddListener(() =>
            {
                var script = Object.FindFirstObjectByType<Level5_Breadboard>();
                if (script != null) script.RotateTile(capturedRow, capturedCol);
            });
        }

        return root;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    void MakeBolt(Transform parent, float x, float y, Color col)
    {
        var b = MakeAnchored(parent, $"Bolt_{x}_{y}", new Vector2(x, y), new Vector2(14f, 14f));
        b.gameObject.AddComponent<Image>().color = col;
        MakeAnchored(b, "i", Vector2.zero, new Vector2(7f, 7f))
            .gameObject.AddComponent<Image>().color = new Color(col.r * 1.5f, col.g * 1.5f, col.b * 1.3f);
    }

    void AddLabel(RectTransform parent, string name, Vector2 pos, Vector2 size,
                  string text, float fs, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
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
        rt.offsetMin = offsetMin; rt.offsetMax  = offsetMax;
        return rt;
    }

    RectTransform MakeAnchored(RectTransform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    RectTransform MakeAnchored(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }
}
