using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Baut Level 4 – Gefängnis-Schleich-Minigame.
/// Menü: Tools → Build Level 4 Computer
/// </summary>
public class BuildLevel4Computer : EditorWindow
{
    [MenuItem("Tools/Build Level 4 Computer")]
    public static void ShowWindow() => GetWindow<BuildLevel4Computer>("Level 4 Builder");

    void OnGUI()
    {
        GUILayout.Label("Level 4 – Gefängnishof Stealth", EditorStyles.boldLabel);
        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "High-End Stealth-UI: Gefängnishof, patrouillierende Wärter, Schuppen-Ziel.\n" +
            "Steuerung: WASD – Wärtern ausweichen – Schuppen erreichen.",
            MessageType.Info);
        GUILayout.Space(12);
        if (GUILayout.Button("Level 4 bauen", GUILayout.Height(36)))
            Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Haupt-Build
    // ═══════════════════════════════════════════════════════════════════════

    void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level4.unity");

        // Kamera
        var camGO = new GameObject("Main Camera");
        var cam   = camGO.gameObject.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
        cam.tag             = "MainCamera";
        camGO.gameObject.AddComponent<AudioListener>();
        SceneManager.MoveGameObjectToScene(camGO, scene);

        // GameManager und BigYahuDialogSystem werden NICHT in die Standalone-Szene eingefügt –
        // sie kommen via DontDestroyOnLoad aus dem vorherigen Level oder fehlen beim direkten Test
        // (Level4_StealthMinigame startet ohne Dialog-System trotzdem korrekt dank Null-Checks)

        // Canvas
        var canvasGO = new GameObject("Level4Canvas");
        var canvas   = canvasGO.gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = canvasGO.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.gameObject.AddComponent<GraphicRaycaster>();
        SceneManager.MoveGameObjectToScene(canvasGO, scene);

        // Vollbild-Hintergrund
        BuildAtmosphericBackground(canvasGO.transform);

        // Spielfeld + Stealth-Script
        var (playArea, stealth) = BuildPlayField(canvasGO.transform);

        // HUD über dem Spielfeld
        BuildHUD(canvasGO.transform, stealth);

        // Hintergrundmusik
        AddBackgroundMusic(scene);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level4] Gefängnishof fertig gebaut.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Atmosphärischer Hintergrund
    // ═══════════════════════════════════════════════════════════════════════

    void BuildAtmosphericBackground(Transform parent)
    {
        // Vollbild-Dunkelheit
        var bg = MakePanel(parent, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.08f);

        // Gitternetz-Effekt (Gefängnisatmosphäre)
        var grid = MakePanel(bg.transform, "Grid", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var gridImg = grid.gameObject.AddComponent<Image>();
        gridImg.color = new Color(0.08f, 0.10f, 0.14f, 0.6f);

        // Horizontale Gitterlinien
        for (int i = 0; i < 12; i++)
        {
            float y = -540f + i * 90f;
            var line = MakeAnchored(bg.transform, $"GridH_{i}", new Vector2(0f, y), new Vector2(1920f, 1f));
            line.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.20f, 0.3f);
        }
        // Vertikale Gitterlinien
        for (int i = 0; i < 22; i++)
        {
            float x = -960f + i * 88f;
            var line = MakeAnchored(bg.transform, $"GridV_{i}", new Vector2(x, 0f), new Vector2(1f, 1080f));
            line.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.20f, 0.3f);
        }

        // Vignette (vier dunkle Ecken-Overlays)
        Color vigCol = new Color(0f, 0f, 0f, 0.55f);
        MakeAnchored(bg.transform, "VigTop",    new Vector2(0, 440f),  new Vector2(1920f, 200f)).gameObject.AddComponent<Image>().color = vigCol;
        MakeAnchored(bg.transform, "VigBottom", new Vector2(0, -440f), new Vector2(1920f, 200f)).gameObject.AddComponent<Image>().color = vigCol;
        MakeAnchored(bg.transform, "VigLeft",   new Vector2(-860f, 0), new Vector2(200f, 1080f)).gameObject.AddComponent<Image>().color = vigCol;
        MakeAnchored(bg.transform, "VigRight",  new Vector2( 860f, 0), new Vector2(200f, 1080f)).gameObject.AddComponent<Image>().color = vigCol;

        // Scanlines-Overlay
        var scan = MakePanel(bg.transform, "Scanlines", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        scan.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Spielfeld
    // ═══════════════════════════════════════════════════════════════════════

    (RectTransform playArea, Level4_StealthMinigame stealth) BuildPlayField(Transform parent)
    {
        // Äußerer Zierrahmen (Stahl-Optik)
        var outerFrame = MakeAnchored(parent, "OuterFrame", Vector2.zero, new Vector2(962f, 722f));
        outerFrame.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.22f, 0.28f);

        // Innerer Rahmen (dunkel)
        var innerFrame = MakeAnchored(outerFrame, "InnerFrame", Vector2.zero, new Vector2(954f, 714f));
        innerFrame.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f);

        // Eckbolzen (4 Ecken)
        Color boltCol = new Color(0.35f, 0.38f, 0.45f);
        foreach (var (bx, by) in new[]{(-465f,-345f),(465f,-345f),(-465f,345f),(465f,345f)})
            MakeBolt(innerFrame, bx, by, boltCol);

        // Spielfeld-Boden (Beton-Optik)
        var floor = MakeAnchored(innerFrame, "Floor", Vector2.zero, new Vector2(940f, 700f));
        floor.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.13f, 0.17f);

        // Boden-Steinfliesen-Muster
        BuildFloorTiles(floor.transform);

        // Gefängnismauer-Rand (innere Borte)
        BuildWallBorder(floor.transform);

        // Hindernisse (Betonblöcke)
        BuildObstacles(floor.transform);

        // === Spieler ===
        var playerGO = new GameObject("Player");
        var playerRT = playerGO.gameObject.AddComponent<RectTransform>();
        playerRT.SetParent(floor.transform, false);
        playerRT.sizeDelta        = new Vector2(28f, 28f);
        playerRT.anchoredPosition = new Vector2(-330f, -250f);
        BuildPlayerIcon(playerRT);

        // === Wärter ===
        var guards     = new List<RectTransform>();
        var guardData  = new (Vector2 pos, Vector2 dir)[]
        {
            (new Vector2(  0f,  220f), Vector2.right),
            (new Vector2(-180f,  20f), Vector2.up),
            (new Vector2( 220f, -80f), Vector2.right),
            (new Vector2( 50f, -180f), Vector2.up),
        };
        for (int i = 0; i < guardData.Length; i++)
        {
            var gGO = new GameObject($"Guard_{i}");
            var gRT = gGO.gameObject.AddComponent<RectTransform>();
            gRT.SetParent(floor.transform, false);
            gRT.sizeDelta        = new Vector2(32f, 32f);
            gRT.anchoredPosition = guardData[i].pos;
            BuildGuardIcon(gRT, i);
            guards.Add(gRT);
        }

        // === Ziel (Schuppen) ===
        var goalGO = new GameObject("Goal_Schuppen");
        var goalRT = goalGO.gameObject.AddComponent<RectTransform>();
        goalRT.SetParent(floor.transform, false);
        goalRT.sizeDelta        = new Vector2(48f, 48f);
        goalRT.anchoredPosition = new Vector2(350f, 260f);
        BuildGoalIcon(goalRT);

        // === Status-Text im Spielfeld ===
        var statusGO  = new GameObject("StatusText");
        var statusRT  = statusGO.gameObject.AddComponent<RectTransform>();
        statusRT.SetParent(floor.transform, false);
        statusRT.anchorMin        = new Vector2(0f, 0f);
        statusRT.anchorMax        = new Vector2(1f, 0f);
        statusRT.offsetMin        = new Vector2(10f, 6f);
        statusRT.offsetMax        = new Vector2(-10f, 32f);
        var statusTxt = statusGO.gameObject.AddComponent<TextMeshProUGUI>();
        statusTxt.text      = string.Empty;
        statusTxt.fontSize  = 18f;
        statusTxt.alignment = TextAlignmentOptions.Center;
        statusTxt.color     = new Color(0.9f, 0.9f, 0.7f);

        // === Level4_StealthMinigame ===
        var scriptGO = new GameObject("StealthMinigame");
        var scriptRT = scriptGO.gameObject.AddComponent<RectTransform>();
        scriptRT.SetParent(floor.transform, false);
        scriptRT.anchorMin = Vector2.zero; scriptRT.anchorMax = Vector2.one;
        scriptRT.offsetMin = Vector2.zero; scriptRT.offsetMax  = Vector2.zero;

        var stealth = scriptGO.gameObject.AddComponent<Level4_StealthMinigame>();
        stealth.GetType().GetField("player",     System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(stealth, playerRT);
        stealth.GetType().GetField("goal",       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(stealth, goalRT);
        stealth.GetType().GetField("statusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(stealth, statusTxt);
        stealth.GetType().GetField("playArea",   System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(stealth, floor);
        stealth.GetType().GetField("guards",     System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(stealth, guards);

        return (floor, stealth);
    }

    void BuildFloorTiles(Transform parent)
    {
        // Betonfliesen-Schachbrettmuster (leicht abwechselnd)
        Color c1 = new Color(0.11f, 0.13f, 0.17f);
        Color c2 = new Color(0.13f, 0.15f, 0.19f);
        int cols = 19, rows = 14;
        float tw = 940f / cols, th = 700f / rows;
        for (int x = 0; x < cols; x++)
        for (int y = 0; y < rows; y++)
        {
            float px = -470f + x * tw + tw * 0.5f;
            float py = -350f + y * th + th * 0.5f;
            var t = MakeAnchored(parent, $"Tile_{x}_{y}", new Vector2(px, py), new Vector2(tw - 1f, th - 1f));
            t.gameObject.AddComponent<Image>().color = ((x + y) % 2 == 0) ? c1 : c2;
        }
    }

    void BuildWallBorder(Transform parent)
    {
        // Innenrand – Mauer mit Zinnen-Optik
        Color wallCol  = new Color(0.18f, 0.20f, 0.26f);
        Color crownCol = new Color(0.22f, 0.25f, 0.32f);
        float w = 940f, h = 700f;

        // Rahmen-Streifen
        MakeAnchored(parent, "WallTop",    new Vector2(0,     h*0.5f-8),  new Vector2(w, 16f)).gameObject.AddComponent<Image>().color = wallCol;
        MakeAnchored(parent, "WallBottom", new Vector2(0,    -h*0.5f+8),  new Vector2(w, 16f)).gameObject.AddComponent<Image>().color = wallCol;
        MakeAnchored(parent, "WallLeft",   new Vector2(-w*0.5f+8, 0),     new Vector2(16f, h)).gameObject.AddComponent<Image>().color = wallCol;
        MakeAnchored(parent, "WallRight",  new Vector2( w*0.5f-8, 0),     new Vector2(16f, h)).gameObject.AddComponent<Image>().color = wallCol;

        // Zinnen oben
        for (int i = 0; i < 24; i++)
        {
            float cx = -460f + i * 40f;
            MakeAnchored(parent, $"CrownT_{i}", new Vector2(cx, h*0.5f-4),  new Vector2(18f, 16f)).gameObject.AddComponent<Image>().color = crownCol;
        }
        // Zinnen unten
        for (int i = 0; i < 24; i++)
        {
            float cx = -460f + i * 40f;
            MakeAnchored(parent, $"CrownB_{i}", new Vector2(cx, -h*0.5f+4), new Vector2(18f, 16f)).gameObject.AddComponent<Image>().color = crownCol;
        }
    }

    void BuildObstacles(Transform parent)
    {
        // Betonblöcke – bilden Gänge für das Stealth-Spiel
        Color blockColor  = new Color(0.22f, 0.24f, 0.30f);
        Color blockShadow = new Color(0.14f, 0.15f, 0.19f);
        Color blockLight  = new Color(0.28f, 0.31f, 0.38f);

        var blocks = new (Vector2 pos, Vector2 size, string name)[]
        {
            // Horizontale Mauern
            (new Vector2(-100f,  170f), new Vector2(220f, 30f), "BlockH1"),
            (new Vector2( 130f,  -30f), new Vector2(180f, 30f), "BlockH2"),
            (new Vector2(-220f, -100f), new Vector2(160f, 30f), "BlockH3"),
            (new Vector2(  60f, -200f), new Vector2(200f, 30f), "BlockH4"),
            (new Vector2( 280f,  100f), new Vector2(30f,  170f), "BlockV1"),
            (new Vector2(-300f,   60f), new Vector2(30f,  160f), "BlockV2"),
            (new Vector2(  10f,   50f), new Vector2(30f,  180f), "BlockV3"),
            // Einzelne Blöcke als Deckung
            (new Vector2(-180f,  240f), new Vector2(50f,  50f), "Cover1"),
            (new Vector2( 200f, -240f), new Vector2(50f,  50f), "Cover2"),
            (new Vector2(-350f, -200f), new Vector2(50f,  50f), "Cover3"),
            (new Vector2( 350f,  -80f), new Vector2(50f,  50f), "Cover4"),
        };

        foreach (var (pos, size, name) in blocks)
        {
            var block = MakeAnchored(parent, name, pos, size);
            block.gameObject.AddComponent<Image>().color = blockColor;

            // Highlight oben und links (3D-Effekt)
            MakeAnchored(block, "Light", new Vector2(-size.x*0.5f + 2f, size.y*0.5f - 2f),
                new Vector2(size.x, 3f)).gameObject.AddComponent<Image>().color = blockLight;
            MakeAnchored(block, "Shadow", new Vector2(size.x*0.5f - 2f, -size.y*0.5f + 2f),
                new Vector2(3f, size.y)).gameObject.AddComponent<Image>().color = blockShadow;

            // Fugen-Muster auf dem Block
            if (size.x > 60f)
            {
                for (int fi = 1; fi < (int)(size.x / 40f); fi++)
                {
                    float gx = -size.x*0.5f + fi * 40f;
                    MakeAnchored(block, $"Fuge_{fi}", new Vector2(gx, 0), new Vector2(1f, size.y * 0.8f))
                        .gameObject.AddComponent<Image>().color = blockShadow;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Icons
    // ═══════════════════════════════════════════════════════════════════════

    void BuildPlayerIcon(RectTransform parent)
    {
        // Äußerer Glowing-Ring (Cyan)
        var glow = MakeAnchored(parent, "Glow", Vector2.zero, new Vector2(38f, 38f));
        var glowImg = glow.gameObject.AddComponent<Image>();
        glowImg.color = new Color(0.2f, 0.9f, 0.9f, 0.25f);

        // Körper
        var body = MakeAnchored(parent, "Body", Vector2.zero, new Vector2(26f, 26f));
        body.gameObject.AddComponent<Image>().color = new Color(0.3f, 1.0f, 0.9f);

        // Gefängnisstreifen (3 dunkle Horizontalstreifen)
        Color stripe = new Color(0.05f, 0.30f, 0.28f);
        MakeAnchored(body, "S1", new Vector2(0,  6f), new Vector2(26f, 4f)).gameObject.AddComponent<Image>().color = stripe;
        MakeAnchored(body, "S2", new Vector2(0,  0f), new Vector2(26f, 4f)).gameObject.AddComponent<Image>().color = stripe;
        MakeAnchored(body, "S3", new Vector2(0, -6f), new Vector2(26f, 4f)).gameObject.AddComponent<Image>().color = stripe;

        // Pfeil (Blickrichtung, nach oben)
        var arrow = MakeAnchored(parent, "Arrow", new Vector2(0, 18f), new Vector2(10f, 10f));
        arrow.gameObject.AddComponent<Image>().color = new Color(0.3f, 1.0f, 0.9f, 0.7f);
    }

    void BuildGuardIcon(RectTransform parent, int index)
    {
        Color[] guardColors =
        {
            new Color(0.95f, 0.20f, 0.15f),
            new Color(0.95f, 0.45f, 0.10f),
            new Color(0.85f, 0.15f, 0.30f),
            new Color(0.75f, 0.10f, 0.50f),
        };
        Color col = guardColors[index % guardColors.Length];

        // Gefahren-Aura
        var aura = MakeAnchored(parent, "Aura", Vector2.zero, new Vector2(52f, 52f));
        aura.gameObject.AddComponent<Image>().color = new Color(col.r, col.g, col.b, 0.15f);

        // Körper
        var body = MakeAnchored(parent, "Body", Vector2.zero, new Vector2(30f, 30f));
        body.gameObject.AddComponent<Image>().color = col;

        // Warnsymbol (!)
        var bang = MakeAnchored(body, "Bang", new Vector2(0, 2f), new Vector2(6f, 14f));
        bang.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 0.2f);
        var bangDot = MakeAnchored(body, "BangDot", new Vector2(0, -8f), new Vector2(6f, 6f));
        bangDot.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 0.2f);

        // Helm-Balken oben
        var helm = MakeAnchored(parent, "Helm", new Vector2(0, 12f), new Vector2(32f, 8f));
        helm.gameObject.AddComponent<Image>().color = new Color(col.r * 0.6f, col.g * 0.6f, col.b * 0.6f);

        // Sichtbereich-Indikator (kleiner Kegel-Simulator, vorwärts)
        var vision = MakeAnchored(parent, "Vision", new Vector2(0, 28f), new Vector2(20f, 24f));
        vision.gameObject.AddComponent<Image>().color = new Color(col.r, col.g, col.b, 0.20f);
    }

    void BuildGoalIcon(RectTransform parent)
    {
        // Glowing-Halo
        var halo = MakeAnchored(parent, "Halo", Vector2.zero, new Vector2(68f, 68f));
        halo.gameObject.AddComponent<Image>().color = new Color(1f, 0.85f, 0.1f, 0.18f);

        // Schuppen-Körper
        var body = MakeAnchored(parent, "Body", new Vector2(0, -4f), new Vector2(42f, 34f));
        body.gameObject.AddComponent<Image>().color = new Color(0.65f, 0.48f, 0.12f);

        // Dach
        var roofBase = MakeAnchored(parent, "Roof", new Vector2(0, 14f), new Vector2(48f, 14f));
        roofBase.gameObject.AddComponent<Image>().color = new Color(0.45f, 0.30f, 0.08f);

        // Tür
        var door = MakeAnchored(body, "Door", new Vector2(0, -4f), new Vector2(12f, 20f));
        door.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.22f, 0.06f);

        // Highlight
        var shine = MakeAnchored(parent, "Shine", new Vector2(-10f, 6f), new Vector2(8f, 22f));
        shine.gameObject.AddComponent<Image>().color = new Color(1f, 0.95f, 0.5f, 0.35f);

        // "ZIEL"-Label
        var lbl    = MakeAnchored(parent, "Label", new Vector2(0, -38f), new Vector2(70f, 18f));
        var lblTxt = lbl.gameObject.AddComponent<TextMeshProUGUI>();
        lblTxt.text      = "ZIEL";
        lblTxt.fontSize  = 13f;
        lblTxt.alignment = TextAlignmentOptions.Center;
        lblTxt.color     = new Color(1f, 0.90f, 0.3f);
        lblTxt.fontStyle = FontStyles.Bold;
    }

    void MakeBolt(Transform parent, float x, float y, Color col)
    {
        var b = MakeAnchored(parent, $"Bolt_{x}_{y}", new Vector2(x, y), new Vector2(12f, 12f));
        b.gameObject.AddComponent<Image>().color = col;
        MakeAnchored(b, "Inner", Vector2.zero, new Vector2(6f, 6f)).gameObject.AddComponent<Image>().color =
            new Color(col.r * 1.4f, col.g * 1.4f, col.b * 1.4f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HUD
    // ═══════════════════════════════════════════════════════════════════════

    void BuildHUD(Transform parent, Level4_StealthMinigame stealth)
    {
        // ── Titel-Leiste oben ──────────────────────────────────────────────
        var topBar = MakeAnchored(parent, "TopBar", new Vector2(0, 490f), new Vector2(1920f, 70f));
        topBar.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.12f, 0.92f);

        // Linke Trennlinie (leuchtend)
        MakeAnchored(topBar, "AccentL", new Vector2(-200f, 0), new Vector2(2f, 50f))
            .gameObject.AddComponent<Image>().color = new Color(0.3f, 0.8f, 1f, 0.8f);
        MakeAnchored(topBar, "AccentR", new Vector2( 200f, 0), new Vector2(2f, 50f))
            .gameObject.AddComponent<Image>().color = new Color(0.3f, 0.8f, 1f, 0.8f);

        var title    = MakeAnchored(topBar, "Title", Vector2.zero, new Vector2(500f, 50f));
        var titleTxt = title.gameObject.AddComponent<TextMeshProUGUI>();
        titleTxt.text      = "GEFÄNGNISAUSBRUCH — PHASE 4";
        titleTxt.fontSize  = 26f;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color     = new Color(0.85f, 0.92f, 1f);
        titleTxt.fontStyle = FontStyles.Bold;

        // Linke Info: Levelbezeichnung
        var levelLbl    = MakeAnchored(topBar, "LevelLbl", new Vector2(-700f, 0), new Vector2(280f, 40f));
        var levelTxt    = levelLbl.gameObject.AddComponent<TextMeshProUGUI>();
        levelTxt.text      = "LEVEL 04";
        levelTxt.fontSize  = 20f;
        levelTxt.alignment = TextAlignmentOptions.Left;
        levelTxt.color     = new Color(0.4f, 0.7f, 1f, 0.8f);

        // Rechts: Status-Badges
        BuildStatusBadge(topBar.transform, "STEALTH", new Vector2(750f, 0), new Color(0.2f, 0.8f, 0.3f));
        BuildStatusBadge(topBar.transform, "WÄRTER: 4", new Vector2(860f, 0), new Color(0.9f, 0.3f, 0.2f));

        // ── Untere Info-Leiste ─────────────────────────────────────────────
        var botBar = MakeAnchored(parent, "BottomBar", new Vector2(0, -490f), new Vector2(1920f, 60f));
        botBar.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.10f, 0.92f);

        // Steuerungshinweise
        var ctrlTxt = MakeAnchored(botBar, "Controls", new Vector2(-400f, 0), new Vector2(700f, 40f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        ctrlTxt.text      = "[ W A S D ]  Bewegen   —   Wärtern ausweichen   —   Schuppen erreichen";
        ctrlTxt.fontSize  = 16f;
        ctrlTxt.alignment = TextAlignmentOptions.Center;
        ctrlTxt.color     = new Color(0.6f, 0.7f, 0.8f);

        // Rechts: Spieler-Legende
        var legTxt = MakeAnchored(botBar, "Legend", new Vector2(600f, 0), new Vector2(500f, 40f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        legTxt.text      = "[CYAN] = Du   [ROT] = Waerter   [GOLD] = Ziel";
        legTxt.fontSize  = 15f;
        legTxt.alignment = TextAlignmentOptions.Right;
        legTxt.color     = new Color(0.5f, 0.6f, 0.7f);

        // ── Seitliche Dekor-Panels ─────────────────────────────────────────
        BuildSidePanel(parent, -860f);
        BuildSidePanel(parent,  860f);
    }

    void BuildStatusBadge(Transform parent, string label, Vector2 pos, Color col)
    {
        var badge = MakeAnchored(parent, $"Badge_{label}", pos, new Vector2(130f, 30f));
        var bg    = badge.gameObject.AddComponent<Image>();
        bg.color  = new Color(col.r * 0.3f, col.g * 0.3f, col.b * 0.3f, 0.8f);

        MakeAnchored(badge, "Border_L", new Vector2(-63f, 0), new Vector2(2f, 30f))
            .gameObject.AddComponent<Image>().color = col;
        MakeAnchored(badge, "Border_R", new Vector2( 63f, 0), new Vector2(2f, 30f))
            .gameObject.AddComponent<Image>().color = col;

        var txt    = MakeAnchored(badge, "Text", Vector2.zero, new Vector2(126f, 26f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        txt.text      = label;
        txt.fontSize  = 13f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color     = col;
        txt.fontStyle = FontStyles.Bold;
    }

    void BuildSidePanel(Transform parent, float x)
    {
        var panel = MakeAnchored(parent, $"SidePanel_{x}", new Vector2(x, 0), new Vector2(120f, 700f));
        panel.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.10f, 0.85f);

        // Leuchtstreifen
        float sign = x < 0 ? 1f : -1f;
        MakeAnchored(panel, "Glow", new Vector2(sign * 58f, 0), new Vector2(2f, 700f))
            .gameObject.AddComponent<Image>().color = new Color(0.2f, 0.5f, 1f, 0.4f);

        // Vertikale "GEFÄNGNIS"-Beschriftung
        var textGO = new GameObject("SideText");
        var textRT = textGO.gameObject.AddComponent<RectTransform>();
        textRT.SetParent(panel, false);
        textRT.sizeDelta        = new Vector2(600f, 30f);
        textRT.anchoredPosition = Vector2.zero;
        textRT.localRotation    = Quaternion.Euler(0, 0, 90f);
        var txt = textGO.gameObject.AddComponent<TextMeshProUGUI>();
        txt.text      = "GEFÄNGNISHOF  ·  SICHERHEITSSTUFE MAXIMUM  ·  GEFÄNGNISHOF";
        txt.fontSize  = 11f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color     = new Color(0.25f, 0.35f, 0.55f);
        txt.fontStyle = FontStyles.Bold;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Hintergrundmusik
    // ═══════════════════════════════════════════════════════════════════════

    void AddBackgroundMusic(Scene scene)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Big Yahu/Untitled.mp3");
        if (clip == null) return;
        var go  = new GameObject("BackgroundMusic");
        var src = go.gameObject.AddComponent<AudioSource>();
        src.clip = clip; src.loop = true; src.playOnAwake = true; src.volume = 0.5f;
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UI-Helfer
    // ═══════════════════════════════════════════════════════════════════════

    RectTransform MakePanel(Transform parent, string name,
                             Vector2 anchorMin, Vector2 anchorMax,
                             Vector2 offsetMin, Vector2 offsetMax)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt   = go.gameObject.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax  = offsetMax;
        return rt;
    }

    RectTransform MakeAnchored(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.gameObject.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;
        return rt;
    }
}
