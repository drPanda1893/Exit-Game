using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Baut Level 6 – Das finale Gefängnistor.
/// Menü: Tools → Build Level 6 Final Gate
///
/// Layout:
///   - Atmosphärischer Nachthimmel-Hintergrund
///   - Massives Gefängnistor mit Ketten und Schloss (Mitte)
///   - Bunsenbrenner-Button (groß, unten, rot-orange)
///   - Temperatur-Bar (blau → rot)
///   - Win-Overlay (zunächst versteckt) – "FREIHEIT!"
/// </summary>
public class BuildLevel6FinalGate : EditorWindow
{
    [MenuItem("Tools/Build Level 6 Final Gate")]
    public static void ShowWindow() => GetWindow<BuildLevel6FinalGate>("Level 6 Builder");

    void OnGUI()
    {
        GUILayout.Label("Level 6 – Das finale Gefängnistor", EditorStyles.boldLabel);
        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Das letzte Level: Bunsenbrenner-Button halten → Schloss erhitzt sich → " +
            "bei 100 % bricht es auf → Big Yahu ist frei!\n" +
            "Win-Screen zeigt sich direkt in dieser Szene.",
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

        // Kamera
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.01f, 0.03f);
        cam.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        SceneManager.MoveGameObjectToScene(camGO, scene);

        // Canvas
        var canvasGO = new GameObject("Level6Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        SceneManager.MoveGameObjectToScene(canvasGO, scene);

        // EventSystem
        var evGO = new GameObject("EventSystem");
        evGO.AddComponent<EventSystem>();
        evGO.AddComponent<StandaloneInputModule>();
        SceneManager.MoveGameObjectToScene(evGO, scene);

        BuildBackground(canvasGO.transform);

        var (lockImg, gateClosedVis, gateOpenVis) = BuildGate(canvasGO.transform);

        BuildHUD(canvasGO.transform);

        var winOverlayRT = BuildWinOverlay(canvasGO.transform, out Button restartBtn);

        BuildPuzzleControls(canvasGO.transform, lockImg, gateClosedVis, gateOpenVis,
            winOverlayRT.gameObject, restartBtn);

        AddBackgroundMusic(scene);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Level6] Finales Gefängnistor fertig gebaut.");
    }

    // =========================================================================
    // Hintergrund – dunkle Nacht mit Scheinwerfer-Effekt
    // =========================================================================

    void BuildBackground(Transform parent)
    {
        var bg = MakePanel(parent, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.01f, 0.04f);

        // Sternenhimmel (kleine weiße Punkte)
        for (int i = 0; i < 80; i++)
        {
            float x = Random.Range(-900f, 900f);
            float y = Random.Range(-200f, 520f);
            float s = Random.Range(2f, 5f);
            float brightness = Random.Range(0.5f, 1f);
            Anchored(bg, $"Star_{i}", new Vector2(x, y), new Vector2(s, s))
                .gameObject.AddComponent<Image>().color = new Color(brightness, brightness, brightness, brightness);
        }

        // Scheinwerfer-Kegel (oben links und rechts, auf Tor gerichtet)
        BuildSpotlight(bg, new Vector2(-500f,  420f), new Vector2(120f, 520f), -20f);
        BuildSpotlight(bg, new Vector2( 500f,  420f), new Vector2(120f, 520f),  20f);

        // Boden-Schatten (unten dunkel)
        Anchored(bg, "GroundShadow", new Vector2(0, -460f), new Vector2(1920f, 180f))
            .gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        // Seitliche Wände
        Anchored(bg, "WallL", new Vector2(-680f, -100f), new Vector2(300f, 700f))
            .gameObject.AddComponent<Image>().color = new Color(0.07f, 0.06f, 0.05f);
        Anchored(bg, "WallR", new Vector2( 680f, -100f), new Vector2(300f, 700f))
            .gameObject.AddComponent<Image>().color = new Color(0.07f, 0.06f, 0.05f);

        // Mauer-Steinmuster (Wand links)
        BuildBrickPattern(bg, new Vector2(-680f, -100f), 6, 9);
        BuildBrickPattern(bg, new Vector2( 680f, -100f), 6, 9);
    }

    void BuildSpotlight(Transform parent, Vector2 pos, Vector2 size, float angle)
    {
        var go = Anchored(parent, $"Spotlight_{pos.x}", pos, size);
        var img = go.gameObject.AddComponent<Image>();
        img.color = new Color(0.9f, 0.85f, 0.6f, 0.04f);
        go.localRotation = Quaternion.Euler(0, 0, angle);
    }

    void BuildBrickPattern(Transform parent, Vector2 center, int cols, int rows)
    {
        Color brick  = new Color(0.09f, 0.07f, 0.06f);
        Color mortar = new Color(0.05f, 0.04f, 0.03f);
        float bw = 44f, bh = 22f;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            float ox = (r % 2 == 0) ? 0f : bw * 0.5f;
            float x  = center.x - cols * bw * 0.5f + c * bw + ox + bw * 0.5f;
            float y  = center.y - rows * bh * 0.5f + r * bh + bh * 0.5f;
            var b = Anchored(parent, $"Brick_{center.x}_{r}_{c}", new Vector2(x, y),
                new Vector2(bw - 2f, bh - 2f));
            b.gameObject.AddComponent<Image>().color = brick;
        }
    }

    // =========================================================================
    // Tor-Visual
    // =========================================================================

    (Image lockImg, GameObject closedVis, GameObject openVis) BuildGate(Transform parent)
    {
        // ── Geschlossenes Tor ─────────────────────────────────────────────────
        var closedRoot = new GameObject("GateClosed");
        closedRoot.transform.SetParent(parent, false);
        var closedRT = closedRoot.AddComponent<RectTransform>();
        closedRT.anchorMin = new Vector2(0.5f, 0.5f);
        closedRT.anchorMax = new Vector2(0.5f, 0.5f);
        closedRT.pivot     = new Vector2(0.5f, 0.5f);
        closedRT.anchoredPosition = new Vector2(0, 80f);
        closedRT.sizeDelta        = new Vector2(440f, 520f);

        // Tor-Rahmen
        var frameOuter = Anchored(closedRoot.transform, "FrameOuter", Vector2.zero, new Vector2(440f, 520f));
        frameOuter.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.16f, 0.12f);
        var frameInner = Anchored(closedRoot.transform, "FrameInner", Vector2.zero, new Vector2(420f, 500f));
        frameInner.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.09f, 0.07f);

        // Eckbolzen
        Color boltCol = new Color(0.28f, 0.24f, 0.18f);
        foreach (var (bx, by) in new[]{(-200f,-230f),(200f,-230f),(-200f,230f),(200f,230f)})
            BuildBolt(closedRoot.transform, bx, by, boltCol);

        // Vertikale Gitterstäbe (7 Stück)
        Color barCol = new Color(0.22f, 0.20f, 0.15f);
        Color barShine = new Color(0.32f, 0.28f, 0.22f);
        for (int i = 0; i < 7; i++)
        {
            float xOff = -180f + i * 60f;
            var bar = Anchored(closedRoot.transform, $"Bar_{i}", new Vector2(xOff, 0), new Vector2(22f, 490f));
            bar.gameObject.AddComponent<Image>().color = barCol;
            // Glanzstreifen
            Anchored(bar, "Shine", new Vector2(-6f, 0), new Vector2(4f, 490f))
                .gameObject.AddComponent<Image>().color = barShine;
        }

        // Horizontale Querriegel (3 Stück)
        for (int i = 0; i < 3; i++)
        {
            float yOff = -150f + i * 150f;
            var bar = Anchored(closedRoot.transform, $"HBar_{i}", new Vector2(0, yOff), new Vector2(400f, 20f));
            bar.gameObject.AddComponent<Image>().color = barCol;
        }

        // Kette links
        BuildChain(closedRoot.transform, new Vector2(-165f, 0), 380f);
        // Kette rechts
        BuildChain(closedRoot.transform, new Vector2( 165f, 0), 380f);

        // Schloss in der Mitte
        var lockRoot = Anchored(closedRoot.transform, "Lock", Vector2.zero, new Vector2(80f, 90f));
        lockRoot.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.28f, 0.15f);

        // Schloss-Bogen
        var arch = Anchored(lockRoot, "Arch", new Vector2(0, 30f), new Vector2(44f, 40f));
        arch.gameObject.AddComponent<Image>().color = new Color(0.28f, 0.22f, 0.12f);
        Anchored(arch, "ArchHole", Vector2.zero, new Vector2(22f, 28f))
            .gameObject.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.03f);

        // Schlüsselloch
        Anchored(lockRoot, "Keyhole", new Vector2(0, -8f), new Vector2(14f, 20f))
            .gameObject.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.03f);

        // Image-Referenz für den Script
        var lockImg = lockRoot.gameObject.GetComponent<Image>();

        // ── Offenes Tor (zunächst deaktiviert) ───────────────────────────────
        var openRoot = new GameObject("GateOpen");
        openRoot.transform.SetParent(parent, false);
        var openRT = openRoot.AddComponent<RectTransform>();
        openRT.anchorMin = new Vector2(0.5f, 0.5f);
        openRT.anchorMax = new Vector2(0.5f, 0.5f);
        openRT.pivot     = new Vector2(0.5f, 0.5f);
        openRT.anchoredPosition = new Vector2(0, 80f);
        openRT.sizeDelta        = new Vector2(440f, 520f);

        // Zwei halb-geöffnete Torflügel
        BuildOpenGateWing(openRoot.transform, -130f, -1f);   // linker Flügel (aufgeschwungen links)
        BuildOpenGateWing(openRoot.transform,  130f,  1f);   // rechter Flügel

        // Freiheits-Leuchten hinter dem Tor
        Anchored(openRoot.transform, "FreedomGlow", Vector2.zero, new Vector2(300f, 300f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.95f, 0.6f, 0.12f);
        Anchored(openRoot.transform, "FreedomGlowInner", Vector2.zero, new Vector2(180f, 180f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.98f, 0.8f, 0.20f);

        openRoot.SetActive(false);

        return (lockImg, closedRoot, openRoot);
    }

    void BuildChain(Transform parent, Vector2 center, float height)
    {
        Color link = new Color(0.30f, 0.26f, 0.18f);
        int count = 8;
        float spacing = height / count;
        for (int i = 0; i < count; i++)
        {
            float y = center.y - height * 0.5f + i * spacing;
            bool horiz = (i % 2 == 0);
            var w = horiz ? new Vector2(18f, 8f) : new Vector2(8f, 18f);
            Anchored(parent, $"Chain_{center.x}_{i}", new Vector2(center.x, y), w)
                .gameObject.AddComponent<Image>().color = link;
        }
    }

    void BuildOpenGateWing(Transform parent, float xCenter, float skewSign)
    {
        Color wingCol = new Color(0.18f, 0.16f, 0.12f);
        // Torflügel leicht schräg (simuliert durch leichte x-Verschiebung der Stäbe)
        for (int i = 0; i < 3; i++)
        {
            float xOff = xCenter - 50f + i * 50f + skewSign * 20f;
            var bar = Anchored(parent, $"WingBar_{xCenter}_{i}", new Vector2(xOff, 0),
                new Vector2(18f, 490f));
            bar.gameObject.AddComponent<Image>().color = wingCol;
        }
        // Querriegel
        for (int i = 0; i < 3; i++)
        {
            float y = -150f + i * 150f;
            var bar = Anchored(parent, $"WingHBar_{xCenter}_{i}", new Vector2(xCenter + skewSign * 10f, y),
                new Vector2(160f, 14f));
            bar.gameObject.AddComponent<Image>().color = wingCol;
        }
    }

    void BuildBolt(Transform parent, float x, float y, Color col)
    {
        var b = Anchored(parent, $"Bolt_{x}_{y}", new Vector2(x, y), new Vector2(16f, 16f));
        b.gameObject.AddComponent<Image>().color = col;
        Anchored(b, "Inner", Vector2.zero, new Vector2(8f, 8f))
            .gameObject.AddComponent<Image>().color = new Color(col.r * 1.4f, col.g * 1.4f, col.b * 1.4f);
    }

    // =========================================================================
    // Puzzle-Steuerung (Bunsenbrenner)
    // =========================================================================

    void BuildPuzzleControls(Transform parent,
        Image lockImg, GameObject closedVis, GameObject openVis,
        GameObject winOverlay, Button restartBtn)
    {
        // Hintergrund-Leiste unten
        var controlBG = Anchored(parent, "ControlBG", new Vector2(0, -280f), new Vector2(700f, 340f));
        controlBG.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.05f, 0.04f, 0.92f);

        // Rahmen
        Anchored(controlBG, "Border", Vector2.zero, new Vector2(698f, 338f))
            .gameObject.AddComponent<Image>().color = new Color(0.20f, 0.16f, 0.10f);

        // Status-Text
        var statusGO  = Anchored(controlBG, "StatusText", new Vector2(0, 120f), new Vector2(640f, 35f));
        var statusTxt = statusGO.gameObject.AddComponent<TextMeshProUGUI>();
        statusTxt.text = string.Empty;
        statusTxt.fontSize = 20f;
        statusTxt.alignment = TextAlignmentOptions.Center;
        statusTxt.color = new Color(1f, 0.8f, 0.3f);
        statusTxt.fontStyle = FontStyles.Bold;

        // Temperatur-Label (über der Bar)
        var tempLblGO  = Anchored(controlBG, "TempLbl", new Vector2(-240f, 68f), new Vector2(120f, 30f));
        var tempLblTxt = tempLblGO.gameObject.AddComponent<TextMeshProUGUI>();
        tempLblTxt.text = "TEMPERATUR";
        tempLblTxt.fontSize = 12f;
        tempLblTxt.color = new Color(0.7f, 0.5f, 0.3f);
        tempLblTxt.alignment = TextAlignmentOptions.Left;

        // Temperatur-Bar
        var sliderGO = new GameObject("TempBar");
        sliderGO.transform.SetParent(controlBG, false);
        var sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRT.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRT.pivot     = new Vector2(0.5f, 0.5f);
        sliderRT.anchoredPosition = new Vector2(0, 55f);
        sliderRT.sizeDelta        = new Vector2(560f, 30f);
        var slider = sliderGO.AddComponent<Slider>();

        // Bar-Hintergrund
        var bgRect = Anchored(sliderGO.transform, "Background", Vector2.zero, new Vector2(560f, 30f));
        bgRect.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.05f);
        slider.targetGraphic = bgRect.GetComponent<Image>();

        // Fill-Area
        var fillArea = Anchored(bgRect, "Fill Area", Vector2.zero, new Vector2(554f, 28f));
        var fill     = Anchored(fillArea, "Fill", Vector2.zero, new Vector2(554f, 28f));
        var fillImg  = fill.gameObject.AddComponent<Image>();
        fillImg.color = new Color(0.25f, 0.55f, 1f);
        slider.fillRect = fill;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = 0f;
        slider.interactable = false;

        // Prozentanzeige
        var pctGO  = Anchored(controlBG, "PctText", new Vector2(320f, 55f), new Vector2(80f, 30f));
        var pctTxt = pctGO.gameObject.AddComponent<TextMeshProUGUI>();
        pctTxt.text = "0 %";
        pctTxt.fontSize = 18f;
        pctTxt.alignment = TextAlignmentOptions.Left;
        pctTxt.color = new Color(0.9f, 0.7f, 0.4f);
        pctTxt.fontStyle = FontStyles.Bold;

        // Bunsenbrenner-Button
        var btnGO = Anchored(controlBG, "HeatButton", new Vector2(0, -40f), new Vector2(500f, 70f));
        var btnBG = btnGO.gameObject.AddComponent<Image>();
        btnBG.color = new Color(0.55f, 0.15f, 0.05f);
        var btn = btnGO.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnBG;

        // Button-Zustands-Farben
        var colors = btn.colors;
        colors.normalColor      = new Color(0.55f, 0.15f, 0.05f);
        colors.highlightedColor = new Color(0.75f, 0.22f, 0.07f);
        colors.pressedColor     = new Color(0.90f, 0.35f, 0.10f);
        colors.disabledColor    = new Color(0.25f, 0.20f, 0.15f);
        btn.colors = colors;

        // Flammen-Akzentlinie oben am Button
        Anchored(btnGO, "AccentTop", new Vector2(0, 33f), new Vector2(500f, 4f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.5f, 0f);
        Anchored(btnGO, "AccentBot", new Vector2(0, -33f), new Vector2(500f, 4f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.5f, 0f);

        // Button-Label
        var btnLbl = Anchored(btnGO, "Label", Vector2.zero, new Vector2(480f, 60f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        btnLbl.text = "BUNSENBRENNER HALTEN";
        btnLbl.fontSize = 26f;
        btnLbl.alignment = TextAlignmentOptions.Center;
        btnLbl.color = new Color(1f, 0.85f, 0.6f);
        btnLbl.fontStyle = FontStyles.Bold;

        // Hinweis unter dem Button
        var hintTxt = Anchored(controlBG, "HintText", new Vector2(0, -100f), new Vector2(600f, 30f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        hintTxt.text = "Gedrückt halten → Temperatur steigt   |   Loslassen → kühlt ab";
        hintTxt.fontSize = 14f;
        hintTxt.alignment = TextAlignmentOptions.Center;
        hintTxt.color = new Color(0.55f, 0.42f, 0.30f);

        // Level6_FinalGate-Script aufsetzen
        var scriptGO = new GameObject("Level6_FinalGate");
        scriptGO.transform.SetParent(parent, false);
        scriptGO.AddComponent<RectTransform>();
        var script = scriptGO.AddComponent<Level6_FinalGate>();

        var so = new SerializedObject(script);
        so.FindProperty("temperatureBar").objectReferenceValue   = slider;
        so.FindProperty("temperatureLabel").objectReferenceValue = pctTxt;
        so.FindProperty("statusText").objectReferenceValue       = statusTxt;
        so.FindProperty("heatButton").objectReferenceValue       = btn;
        so.FindProperty("lockImage").objectReferenceValue        = lockImg;
        so.FindProperty("gateClosedVisual").objectReferenceValue = closedVis;
        so.FindProperty("gateOpenVisual").objectReferenceValue   = openVis;
        so.FindProperty("winOverlay").objectReferenceValue       = winOverlay;
        so.FindProperty("restartButton").objectReferenceValue    = restartBtn;
        so.ApplyModifiedProperties();
    }

    // =========================================================================
    // Win-Overlay
    // =========================================================================

    RectTransform BuildWinOverlay(Transform parent, out Button restartBtn)
    {
        // Vollbild-Overlay (dunkel, zunächst inaktiv)
        var overlay = MakePanel(parent, "WinOverlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
        overlay.gameObject.SetActive(false);

        // Sterne / Partikel-Look
        for (int i = 0; i < 40; i++)
        {
            float x = Random.Range(-800f, 800f);
            float y = Random.Range(-400f, 400f);
            float s = Random.Range(3f, 8f);
            float h = Random.Range(0.8f, 1f);
            Anchored(overlay, $"WinStar_{i}", new Vector2(x, y), new Vector2(s, s))
                .gameObject.AddComponent<Image>().color = new Color(1f, h, 0.4f, h);
        }

        // Leuchtkranz
        Anchored(overlay, "Halo", new Vector2(0, 60f), new Vector2(500f, 500f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.9f, 0.4f, 0.06f);
        Anchored(overlay, "HaloInner", new Vector2(0, 60f), new Vector2(300f, 300f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.95f, 0.6f, 0.10f);

        // Schloss-offen-Symbol (großes "🔓"-Ersatz via Text)
        var lockSymGO  = Anchored(overlay, "LockSymbol", new Vector2(0, 220f), new Vector2(120f, 120f));
        var lockSymTxt = lockSymGO.gameObject.AddComponent<TextMeshProUGUI>();
        lockSymTxt.text = "FREI";
        lockSymTxt.fontSize = 30f;
        lockSymTxt.alignment = TextAlignmentOptions.Center;
        lockSymTxt.color = new Color(1f, 0.9f, 0.3f);
        lockSymTxt.fontStyle = FontStyles.Bold;

        // Haupttitel
        var titleGO  = Anchored(overlay, "Title", new Vector2(0, 110f), new Vector2(900f, 110f));
        var titleTxt = titleGO.gameObject.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "WIR SIND FREI!";
        titleTxt.fontSize = 80f;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.88f, 0.3f);
        titleTxt.fontStyle = FontStyles.Bold;

        // Untertitel
        var subGO  = Anchored(overlay, "Subtitle", new Vector2(0, 20f), new Vector2(800f, 60f));
        var subTxt = subGO.gameObject.AddComponent<TextMeshProUGUI>();
        subTxt.text = "Big Yahu hat das Gefängnis verlassen!";
        subTxt.fontSize = 32f;
        subTxt.alignment = TextAlignmentOptions.Center;
        subTxt.color = new Color(0.85f, 0.75f, 0.55f);

        // Trennlinie
        Anchored(overlay, "Divider", new Vector2(0, -40f), new Vector2(500f, 2f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.8f, 0.3f, 0.5f);

        // Danke-Text
        var thanksGO  = Anchored(overlay, "Thanks", new Vector2(0, -80f), new Vector2(700f, 45f));
        var thanksTxt = thanksGO.gameObject.AddComponent<TextMeshProUGUI>();
        thanksTxt.text = "Herzlichen Glückwunsch! Du hast alle 6 Level gemeistert.";
        thanksTxt.fontSize = 22f;
        thanksTxt.alignment = TextAlignmentOptions.Center;
        thanksTxt.color = new Color(0.70f, 0.65f, 0.55f);

        // Restart-Button
        var restartGO  = Anchored(overlay, "RestartButton", new Vector2(0, -190f), new Vector2(360f, 60f));
        var restartBG  = restartGO.gameObject.AddComponent<Image>();
        restartBG.color = new Color(0.20f, 0.16f, 0.08f);
        restartBtn = restartGO.gameObject.AddComponent<Button>();
        restartBtn.targetGraphic = restartBG;

        var rColors = restartBtn.colors;
        rColors.normalColor      = new Color(0.20f, 0.16f, 0.08f);
        rColors.highlightedColor = new Color(0.35f, 0.28f, 0.14f);
        rColors.pressedColor     = new Color(0.50f, 0.40f, 0.20f);
        restartBtn.colors = rColors;

        // Rahmen
        Anchored(restartGO, "BorderT", new Vector2(0,  28f), new Vector2(360f, 2f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.8f, 0.3f, 0.6f);
        Anchored(restartGO, "BorderB", new Vector2(0, -28f), new Vector2(360f, 2f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.8f, 0.3f, 0.6f);

        var restartLbl = Anchored(restartGO, "Label", Vector2.zero, new Vector2(340f, 50f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        restartLbl.text = "NOCHMAL SPIELEN";
        restartLbl.fontSize = 22f;
        restartLbl.alignment = TextAlignmentOptions.Center;
        restartLbl.color = new Color(1f, 0.88f, 0.4f);
        restartLbl.fontStyle = FontStyles.Bold;

        return overlay;
    }

    // =========================================================================
    // HUD
    // =========================================================================

    void BuildHUD(Transform parent)
    {
        // Oben
        var top = Anchored(parent, "TopHUD", new Vector2(0, 495f), new Vector2(1920f, 70f));
        top.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.02f, 0.92f);

        Anchored(top, "AccentL", new Vector2(-200f, 0), new Vector2(2f, 50f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.5f, 0.1f, 0.8f);
        Anchored(top, "AccentR", new Vector2( 200f, 0), new Vector2(2f, 50f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.5f, 0.1f, 0.8f);

        var titleTxt = Anchored(top, "Title", Vector2.zero, new Vector2(550f, 50f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "GEFAENGNISAUSBRUCH — DAS FINALE TOR";
        titleTxt.fontSize = 26f;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.75f, 0.35f);
        titleTxt.fontStyle = FontStyles.Bold;

        var levelLbl = Anchored(top, "LevelLbl", new Vector2(-700f, 0), new Vector2(200f, 40f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        levelLbl.text = "LEVEL 06";
        levelLbl.fontSize = 20f;
        levelLbl.alignment = TextAlignmentOptions.Left;
        levelLbl.color = new Color(1f, 0.5f, 0.2f, 0.8f);

        BuildBadge(top, "FINAL", new Vector2(750f, 0), new Color(1f, 0.4f, 0.1f));
        BuildBadge(top, "AUSBRUCH", new Vector2(870f, 0), new Color(0.9f, 0.8f, 0.2f));

        // Unten
        var bot = Anchored(parent, "BotHUD", new Vector2(0, -495f), new Vector2(1920f, 60f));
        bot.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.01f, 0.92f);

        var hintTxt = Anchored(bot, "Hint", new Vector2(-200f, 0), new Vector2(800f, 40f))
            .gameObject.AddComponent<TextMeshProUGUI>();
        hintTxt.text = "Werkzeug: Bunsenbrenner  |  Ziel: Schloss schmelzen  |  Freiheit!";
        hintTxt.fontSize = 16f;
        hintTxt.alignment = TextAlignmentOptions.Center;
        hintTxt.color = new Color(0.6f, 0.45f, 0.25f);

        // Seitenleisten
        BuildSidePanel(parent, -870f);
        BuildSidePanel(parent,  870f);
    }

    void BuildBadge(Transform parent, string label, Vector2 pos, Color col)
    {
        var badge = Anchored(parent, $"Badge_{label}", pos, new Vector2(130f, 30f));
        badge.gameObject.AddComponent<Image>().color = new Color(col.r * 0.25f, col.g * 0.25f, col.b * 0.25f, 0.85f);
        Anchored(badge, "BL", new Vector2(-63f, 0), new Vector2(2f, 30f)).gameObject.AddComponent<Image>().color = col;
        Anchored(badge, "BR", new Vector2( 63f, 0), new Vector2(2f, 30f)).gameObject.AddComponent<Image>().color = col;
        var txt = Anchored(badge, "Txt", Vector2.zero, new Vector2(126f, 26f)).gameObject.AddComponent<TextMeshProUGUI>();
        txt.text = label; txt.fontSize = 13f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = col; txt.fontStyle = FontStyles.Bold;
    }

    void BuildSidePanel(Transform parent, float x)
    {
        var panel = Anchored(parent, $"Side_{x}", new Vector2(x, 0), new Vector2(100f, 700f));
        panel.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.02f, 0.85f);

        float sign = x < 0 ? 1f : -1f;
        Anchored(panel, "Glow", new Vector2(sign * 48f, 0), new Vector2(2f, 700f))
            .gameObject.AddComponent<Image>().color = new Color(1f, 0.4f, 0.1f, 0.3f);

        var textGO = new GameObject("SideText");
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.SetParent(panel, false);
        textRT.sizeDelta = new Vector2(600f, 24f);
        textRT.anchoredPosition = Vector2.zero;
        textRT.localRotation = Quaternion.Euler(0, 0, 90f);
        var txt = textGO.AddComponent<TextMeshProUGUI>();
        txt.text = "DAS FINALE TOR  *  SICHERHEITSSTUFE MAXIMUM  *  DAS FINALE TOR";
        txt.fontSize = 10f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.35f, 0.25f, 0.12f);
        txt.fontStyle = FontStyles.Bold;
    }

    // =========================================================================
    // Hintergrundmusik
    // =========================================================================

    void AddBackgroundMusic(Scene scene)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Big Yahu/Untitled.mp3");
        if (clip == null) return;
        var go = new GameObject("BackgroundMusic");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip; src.loop = true; src.playOnAwake = true; src.volume = 0.5f;
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    // =========================================================================
    // UI-Helfer
    // =========================================================================

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

    RectTransform Anchored(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return rt;
    }
}
