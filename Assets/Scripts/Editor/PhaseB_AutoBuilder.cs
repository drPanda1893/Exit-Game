using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Editor-Helfer-Klasse für Level 3 / Phase B (Farbrätsel).
///
/// <para>
/// Bietet zwei Menüpunkte unter <c>Window → Custom Tools</c>
/// (vorher <c>Helios</c> — der eigene Reiter wurde zugunsten der
/// Standard-Unity-Menüstruktur entfernt):
/// <list type="bullet">
///   <item><c>Build Phase B</c> — baut Canvas + 4 Color-Buttons + Feedback-Text
///         und verkabelt das <see cref="Level3_ColorPuzzle"/>-Script via
///         <see cref="SerializedObject"/>.</item>
///   <item><c>Polish Phase B UI</c> — bügelt visuelle und Setup-Probleme aus:
///         feste Buttongröße + Glow + Shadow, Dialog-Panel-Höhe, DialogText-Centering,
///         BookshelfContainer ausblenden, Stray-Buttons / „New Text" löschen,
///         Helios-Portrait zuweisen, Speaker-Label gelb, Big-Yahu-Rigidbody-Constraints.</item>
/// </list>
/// </para>
///
/// <para>
/// Das Polish-Tool ist <b>idempotent</b>: mehrfaches Ausführen erzeugt keine
/// Dubletten und überschreibt nur, was wirklich anders ist.
/// </para>
/// </summary>
public static class PhaseB_AutoBuilder
{
    /// <summary>Name des Wurzel-GameObjects, das die Phase-B-Hierarchie trägt.</summary>
    private const string ROOT_NAME       = "PhaseB_ColorCode";

    /// <summary>Name des Canvas-Childs unter <see cref="ROOT_NAME"/>.</summary>
    private const string CANVAS_NAME     = "ColorPuzzleCanvas";

    /// <summary>Name des Panels, das die vier Farb-Buttons als HorizontalLayoutGroup hält.</summary>
    private const string BUTTON_PANEL    = "ButtonPanel";

    /// <summary>Name des Feedback-TextMeshPro-Objects ("✓ Korrekt" / "✗ Falsch").</summary>
    private const string FEEDBACK_NAME   = "FeedbackText";

    /// <summary>Feste Pixel-Größe für jeden Color-Button (quadratisch).</summary>
    private const float  BUTTON_SIZE     = 100f;

    /// <summary>Abstand in Pixeln zwischen den Color-Buttons in der HorizontalLayoutGroup.</summary>
    private const float  BUTTON_SPACING  = 20f;

    /// <summary>Asset-Pfad für die einmalig generierte Glow-Sprite-Textur.</summary>
    private const string GLOW_ASSET_PATH = "Assets/Generated/PhaseB_Glow.png";

    /// <summary>Namen aller Phase-B-Buttons. Wird in <see cref="PolishPhaseBUI"/> iteriert.</summary>
    private static readonly string[] BUTTON_NAMES =
        { "RedButton", "GreenButton", "BlueButton", "YellowButton" };

    // ══════════════════════════════════════════════════════════
    // Menüpunkt 1: Build Phase B
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Menüpunkt <c>Window → Custom Tools → Build Phase B</c>:
    /// Baut die komplette Phase-B-Hierarchie auf und verkabelt das
    /// <see cref="Level3_ColorPuzzle"/>-Script. Idempotent — wiederholtes
    /// Ausführen erzeugt keine Dubletten.
    /// </summary>
    [MenuItem("Window/Custom Tools/Build Phase B")]
    public static void BuildPhaseB()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("Phase B", "Keine aktive Scene gefunden.", "OK");
            return;
        }

        var root = FindOrCreateRoot(scene);
        var canvasGO = EnsureCanvas(root);
        var buttonPanel = EnsureButtonPanel(canvasGO.transform);

        var redBtn    = EnsureColorButton(buttonPanel, "RedButton",    "Red",    new Color(0.86f, 0.18f, 0.18f), 0);
        var greenBtn  = EnsureColorButton(buttonPanel, "GreenButton",  "Green",  new Color(0.20f, 0.72f, 0.30f), 1);
        var blueBtn   = EnsureColorButton(buttonPanel, "BlueButton",   "Blue",   new Color(0.20f, 0.40f, 1.00f), 2);
        var yellowBtn = EnsureColorButton(buttonPanel, "YellowButton", "Yellow", new Color(0.96f, 0.85f, 0.18f), 3);

        var feedback = EnsureFeedbackText(canvasGO.transform);

        var puzzle = root.GetComponent<Level3_ColorPuzzle>();
        if (puzzle == null) puzzle = Undo.AddComponent<Level3_ColorPuzzle>(root);

        WireSerializedFields(puzzle, redBtn, greenBtn, blueBtn, yellowBtn, feedback);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        Debug.Log($"[PhaseB_AutoBuilder] '{ROOT_NAME}' aufgebaut: 4 Buttons + Level3_ColorPuzzle verkabelt.");
    }

    // ══════════════════════════════════════════════════════════
    // Menüpunkt 2: Polish Phase B UI
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Menüpunkt <c>Window → Custom Tools → Polish Phase B UI</c>:
    /// All-in-One-Cleanup für die Phase-B-Scene. Führt nacheinander aus:
    /// <list type="number">
    ///   <item>Horizontale Button-Reihe mit fester Größe + Spacing.</item>
    ///   <item>Glow-Sprite + Shadow auf jeden Color-Button.</item>
    ///   <item>DialogPanel-Mindesthöhe + DialogText vertikal zentriert.</item>
    ///   <item>BookshelfContainer ausblenden (Phase A).</item>
    ///   <item>Aggressive Löschung aller Stray-Objekte mit exaktem Namen
    ///         „New Text" oder „Button" außerhalb der Phase-B- und Dialog-Roots.</item>
    ///   <item>DialogSystem_Manager: Helios-Portrait + gelbe Speaker-Farbe.</item>
    ///   <item>Big-Yahu-Rigidbody: Rotation X/Z + Position Y einfrieren.</item>
    ///   <item>Re-Wiring der <see cref="Level3_ColorPuzzle"/>-SerializeFields.</item>
    /// </list>
    /// </summary>
    [MenuItem("Window/Custom Tools/Polish Phase B UI")]
    public static void PolishPhaseBUI()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("Polish Phase B", "Keine aktive Scene gefunden.", "OK");
            return;
        }

        Transform rootT = null;
        foreach (var go in scene.GetRootGameObjects())
        {
            rootT = FindRecursive(go.transform, ROOT_NAME);
            if (rootT != null) break;
        }

        if (rootT == null)
        {
            EditorUtility.DisplayDialog("Polish Phase B",
                $"'{ROOT_NAME}' nicht gefunden. Bitte zuerst 'Window → Custom Tools → Build Phase B' ausführen.", "OK");
            return;
        }

        var buttonPanel = FindRecursive(rootT, BUTTON_PANEL);
        if (buttonPanel == null)
        {
            EditorUtility.DisplayDialog("Polish Phase B",
                $"'{BUTTON_PANEL}' nicht gefunden — Build Phase B erneut ausführen.", "OK");
            return;
        }

        ApplyHorizontalRowLayout(buttonPanel);

        var glow = GetOrCreateGlowSprite();

        foreach (var btnName in BUTTON_NAMES)
        {
            var btnT = FindRecursive(buttonPanel, btnName);
            if (btnT == null) continue;
            PolishButton(btnT, glow);
        }

        // Dialog-System aufpolieren (Panel-Höhe, DialogText-Centering)
        PolishDialogSystem(scene);

        // BookshelfContainer ausblenden, damit Phase-B-Buttons in der Mitte sichtbar werden
        DeactivateBookshelfContainer(scene);

        // Aggressive Löschung: Stray-„Button"- und „New Text"-Objekte komplett entfernen
        DeleteStrayObjects(scene, rootT);

        // Helios-Portrait + gelbe Speaker-Farbe auf den DialogSystem_Manager pushen
        ConfigureDialogSystemManager(scene);

        // Big Yahu Rigidbody → Rotation auf X/Z einfrieren (kippt nicht mehr um)
        ConfigureBigYahuRigidbody(scene);

        // Re-Wiring: stellt sicher, dass das Puzzle-Script alle Buttons referenziert
        var puzzle = rootT.GetComponent<Level3_ColorPuzzle>();
        if (puzzle != null)
        {
            var redBtn    = FindRecursive(buttonPanel, "RedButton")?.GetComponent<Button>();
            var greenBtn  = FindRecursive(buttonPanel, "GreenButton")?.GetComponent<Button>();
            var blueBtn   = FindRecursive(buttonPanel, "BlueButton")?.GetComponent<Button>();
            var yellowBtn = FindRecursive(buttonPanel, "YellowButton")?.GetComponent<Button>();
            var feedback  = FindRecursive(rootT, FEEDBACK_NAME)?.GetComponent<TextMeshProUGUI>();
            WireSerializedFields(puzzle, redBtn, greenBtn, blueBtn, yellowBtn, feedback);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = rootT.gameObject;

        Debug.Log("[PhaseB_AutoBuilder] Polish abgeschlossen: Layout, Glow, Cleanup, Dialog, Big Yahu.");
    }

    // ══════════════════════════════════════════════════════════
    // Polish-Helfer: Layout & Buttons
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Konfiguriert das Button-Panel als horizontale Reihe mit festen Kindgrößen
    /// und 20px Spacing.
    /// </summary>
    /// <param name="buttonPanel">Transform des Button-Panels.</param>
    private static void ApplyHorizontalRowLayout(Transform buttonPanel)
    {
        var hlg = buttonPanel.GetComponent<HorizontalLayoutGroup>()
                  ?? buttonPanel.gameObject.AddComponent<HorizontalLayoutGroup>();

        hlg.spacing                = BUTTON_SPACING;
        hlg.padding                = new RectOffset(20, 20, 20, 20);
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
    }

    /// <summary>
    /// Polish-Pass für einen einzelnen Color-Button: feste Größe via
    /// <see cref="LayoutElement"/>, Glow-Sprite, Shadow-Komponente.
    /// </summary>
    /// <param name="btnT">Transform des Buttons.</param>
    /// <param name="glow">Vor-generierter Glow-Sprite (radialer Falloff).</param>
    private static void PolishButton(Transform btnT, Sprite glow)
    {
        var le = btnT.GetComponent<LayoutElement>() ?? btnT.gameObject.AddComponent<LayoutElement>();
        le.minWidth        = BUTTON_SIZE;
        le.minHeight       = BUTTON_SIZE;
        le.preferredWidth  = BUTTON_SIZE;
        le.preferredHeight = BUTTON_SIZE;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;

        var rect = btnT.GetComponent<RectTransform>();
        if (rect != null) rect.sizeDelta = new Vector2(BUTTON_SIZE, BUTTON_SIZE);

        var img = btnT.GetComponent<Image>();
        if (img != null && glow != null)
        {
            img.sprite = glow;
            img.type   = Image.Type.Simple;
            img.preserveAspect = false;
        }

        var shadow = btnT.GetComponent<Shadow>() ?? btnT.gameObject.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(3f, -3f);
    }

    /// <summary>
    /// Findet das DialogPanel und hebt die Mindesthöhe auf 300 an.
    /// Justiert anschließend den DialogText: vertikal zentriertes
    /// TMP-Alignment + großzügige Top/Bottom-Insets, sodass der Text
    /// mittig im schwarzen Balken sitzt.
    /// </summary>
    /// <param name="scene">Aktive Scene mit Dialog-System.</param>
    private static void PolishDialogSystem(Scene scene)
    {
        var dialogPanel = FindGOInScene(scene, "DialogPanel");
        if (dialogPanel != null)
        {
            var rect = dialogPanel.GetComponent<RectTransform>();
            if (rect != null)
            {
                var size = rect.sizeDelta;
                if (size.y < 300f) size.y = 300f;
                rect.sizeDelta = size;
            }
        }

        // Beide möglichen Namen abdecken (Code nutzt 'DialogText', GDD-Brief erwähnt 'DialogueText').
        var dialogTextGO = FindGOInScene(scene, "DialogText") ?? FindGOInScene(scene, "DialogueText");
        if (dialogTextGO != null)
        {
            var tmp = dialogTextGO.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                Undo.RecordObject(tmp, "Polish DialogText alignment");
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                EditorUtility.SetDirty(tmp);

                var rt = tmp.rectTransform;
                Undo.RecordObject(rt, "Polish DialogText offsets");
                var offsetMax = rt.offsetMax;
                var offsetMin = rt.offsetMin;
                if (offsetMax.y > -60f) offsetMax.y = -60f;
                if (offsetMin.y <  40f) offsetMin.y =  40f;
                rt.offsetMax = offsetMax;
                rt.offsetMin = offsetMin;

                var pos = rt.anchoredPosition;
                if (pos.y > -10f) pos.y = -10f;
                rt.anchoredPosition = pos;

                EditorUtility.SetDirty(rt);
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    // Polish-Helfer: Aggressives Cleanup
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Versteckt das BookshelfContainer-GameObject (Phase A), falls vorhanden.
    /// </summary>
    /// <param name="scene">Aktive Scene.</param>
    /// <remarks>
    /// Im Gegensatz zu <see cref="DeleteStrayObjects"/> wird hier nicht gelöscht,
    /// da das Bücherregal in Phase A noch gebraucht wird.
    /// </remarks>
    private static void DeactivateBookshelfContainer(Scene scene)
    {
        var bookshelf = FindGOInScene(scene, "BookshelfContainer");
        if (bookshelf != null && bookshelf.activeSelf)
        {
            Undo.RecordObject(bookshelf, "Deactivate BookshelfContainer");
            bookshelf.SetActive(false);
            Debug.Log("[PhaseB_AutoBuilder] 'BookshelfContainer' deaktiviert.");
        }
    }

    /// <summary>
    /// Löscht ALLE GameObjects mit exaktem Namen <c>"New Text"</c> oder
    /// <c>"Button"</c>, die NICHT zu PhaseB_ColorCode oder zum Dialog-System
    /// gehören. Verwendet <see cref="Undo.DestroyObjectImmediate"/>, damit
    /// die Aktion via Ctrl+Z rückgängig gemacht werden kann.
    /// </summary>
    /// <param name="scene">Aktive Scene.</param>
    /// <param name="phaseBRoot">Wurzel der Phase-B-Hierarchie (geschützt).</param>
    /// <remarks>
    /// Sammelt erst alle Treffer in einer Liste, bevor gelöscht wird —
    /// vermeidet Iteration über eine sich verändernde Hierarchie.
    /// </remarks>
    private static void DeleteStrayObjects(Scene scene, Transform phaseBRoot)
    {
        var dialogRootGO = FindGOInScene(scene, "BigYahuDialogSystem");
        var dialogRoot   = dialogRootGO != null ? dialogRootGO.transform : null;

        var toDelete = new List<GameObject>();
        foreach (var go in scene.GetRootGameObjects())
            CollectStrayObjects(go.transform, phaseBRoot, dialogRoot, toDelete);

        int killed = 0;
        foreach (var go in toDelete)
        {
            if (go == null) continue;
            Undo.DestroyObjectImmediate(go);
            killed++;
        }

        if (killed > 0)
            Debug.Log($"[PhaseB_AutoBuilder] Hausputz: {killed} Stray-GameObjects gelöscht (Name 'Button' oder 'New Text').");
    }

    /// <summary>
    /// Rekursive Hilfsmethode: sammelt GameObjects mit exaktem Namen
    /// <c>"Button"</c> oder <c>"New Text"</c>, sofern sie nicht zu
    /// <paramref name="phaseBRoot"/> oder <paramref name="dialogRoot"/> gehören.
    /// </summary>
    /// <param name="t">Aktuell besuchte Transform.</param>
    /// <param name="phaseBRoot">Geschützte Phase-B-Wurzel.</param>
    /// <param name="dialogRoot">Geschützte Dialog-System-Wurzel.</param>
    /// <param name="sink">Liste, in die Treffer geschrieben werden.</param>
    private static void CollectStrayObjects(Transform t,
                                             Transform phaseBRoot,
                                             Transform dialogRoot,
                                             List<GameObject> sink)
    {
        // 'New Text' ist IMMER ein Unity-Default-Stub — nirgendwo legitim, immer löschen.
        if (t.name == "New Text")
        {
            sink.Add(t.gameObject);
            return;
        }

        // 'Button' nur löschen, wenn nicht im Phase-B- oder Dialog-Tree
        // (Phase-B-Buttons heißen RedButton/GreenButton/etc., nicht exakt 'Button').
        if (t.name == "Button")
        {
            bool inPhaseB = phaseBRoot != null && IsDescendantOf(t, phaseBRoot);
            bool inDialog = dialogRoot != null && IsDescendantOf(t, dialogRoot);

            if (!inPhaseB && !inDialog)
            {
                sink.Add(t.gameObject);
                return;
            }
        }

        for (int i = 0; i < t.childCount; i++)
            CollectStrayObjects(t.GetChild(i), phaseBRoot, dialogRoot, sink);
    }

    /// <summary>
    /// Prüft, ob <paramref name="child"/> ein Nachfahre von
    /// <paramref name="ancestor"/> ist (oder identisch).
    /// </summary>
    /// <param name="child">Zu prüfende Transform.</param>
    /// <param name="ancestor">Vorfahr-Transform.</param>
    /// <returns>True, falls child == ancestor oder einer der Eltern.</returns>
    private static bool IsDescendantOf(Transform child, Transform ancestor)
    {
        var t = child;
        while (t != null)
        {
            if (t == ancestor) return true;
            t = t.parent;
        }
        return false;
    }

    /// <summary>
    /// Konfiguriert den <c>BigYahuDialogSystem</c> (alias DialogSystem_Manager):
    /// <list type="bullet">
    ///   <item><c>heliosPortrait</c> wird automatisch zugewiesen, falls noch leer.</item>
    ///   <item><c>speakerLabel.color</c> wird hart auf Gelb gesetzt
    ///         (auch ohne Play-Mode sichtbar).</item>
    /// </list>
    /// </summary>
    /// <param name="scene">Aktive Scene mit Dialog-System.</param>
    private static void ConfigureDialogSystemManager(Scene scene)
    {
        var dialogRoot = FindGOInScene(scene, "BigYahuDialogSystem");
        if (dialogRoot == null)
        {
            Debug.LogWarning("[PhaseB_AutoBuilder] Kein BigYahuDialogSystem in der Scene – Helios-Setup übersprungen.");
            return;
        }

        var ds = dialogRoot.GetComponent<BigYahuDialogSystem>();
        if (ds == null)
        {
            Debug.LogWarning("[PhaseB_AutoBuilder] DialogSystem-GameObject ohne Komponente – nichts zu konfigurieren.");
            return;
        }

        var so = new SerializedObject(ds);

        // 1) Helios-Portrait automatisch zuweisen, falls noch keins gesetzt ist.
        var portraitProp = so.FindProperty("heliosPortrait");
        if (portraitProp != null && portraitProp.objectReferenceValue == null)
        {
            var sprite = FindHeliosSprite();
            if (sprite != null)
            {
                portraitProp.objectReferenceValue = sprite;
                Debug.Log($"[PhaseB_AutoBuilder] Helios-Portrait zugewiesen: {AssetDatabase.GetAssetPath(sprite)}");
            }
            else
            {
                Debug.LogWarning("[PhaseB_AutoBuilder] Kein Helios-Sprite gefunden – heliosPortrait bleibt leer.");
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // 2) SpeakerLabel-Farbe direkt im Editor auf Gelb setzen (auch ohne Play-Mode).
        var speakerLabelProp = new SerializedObject(ds).FindProperty("speakerLabel");
        if (speakerLabelProp != null && speakerLabelProp.objectReferenceValue is TextMeshProUGUI label)
        {
            Undo.RecordObject(label, "Set SpeakerLabel Yellow");
            label.color = Color.yellow;
            EditorUtility.SetDirty(label);
        }
    }

    /// <summary>
    /// Sucht das Helios-Portrait im Projekt mit folgender Priorität:
    /// <list type="number">
    ///   <item>Sprite namens <c>"Helios_Portrait"</c> (exakt).</item>
    ///   <item>Beliebiges Sprite mit „helios" im Namen.</item>
    ///   <item>Sprite-Sub-Assets in einer Helios-Textur (z.B. FBX-Extrakt).</item>
    /// </list>
    /// </summary>
    /// <returns>Das gefundene Sprite oder <c>null</c>, falls nichts passt.</returns>
    private static Sprite FindHeliosSprite()
    {
        // 1) Exakter Name 'Helios_Portrait'
        var guids = AssetDatabase.FindAssets("Helios_Portrait t:Sprite");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileName, "Helios_Portrait", System.StringComparison.OrdinalIgnoreCase))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) return sprite;
            }
        }

        // 2) Generisch: irgendein Sprite mit 'helios' im Namen
        guids = AssetDatabase.FindAssets("helios t:Sprite");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
        }

        // 3) Sub-Assets in Texturen (FBX-Extrakte)
        guids = AssetDatabase.FindAssets("helios t:Texture2D");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite s) return s;
            }
        }
        return null;
    }

    /// <summary>
    /// Friert Big Yahus Rigidbody so ein, dass er nicht umkippt und nicht
    /// durch Gravitation den Boden verlässt — bleibt aber via WASD über
    /// <see cref="PlayerController"/> auf X/Z bewegbar, falls die Komponente
    /// dranhängt.
    /// </summary>
    /// <param name="scene">Aktive Scene.</param>
    /// <remarks>
    /// Setzt:
    /// <list type="bullet">
    ///   <item><c>FreezeRotationX | FreezeRotationZ</c> — kippsicher.</item>
    ///   <item><c>FreezePositionY</c> — kein Fallen / Hüpfen, X/Z bleibt frei für WASD.</item>
    /// </list>
    /// PlayerController bleibt unangetastet — falls Big Yahu der Spielcharakter ist,
    /// funktioniert WASD weiterhin.
    /// </remarks>
    private static void ConfigureBigYahuRigidbody(Scene scene)
    {
        var bigYahu = FindGOInScene(scene, "Big Yahu")
                      ?? FindGOInScene(scene, "BigYahu")
                      ?? FindGOInScene(scene, "Big_Yahu");

        if (bigYahu == null)
        {
            Debug.LogWarning("[PhaseB_AutoBuilder] Kein 'Big Yahu'-GameObject gefunden – Rigidbody-Setup übersprungen.");
            return;
        }

        var rb = bigYahu.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"[PhaseB_AutoBuilder] '{bigYahu.name}' hat keinen Rigidbody – Constraints nicht gesetzt.");
            return;
        }

        Undo.RecordObject(rb, "Freeze BigYahu Rotation X/Z + Position Y");
        rb.constraints = rb.constraints
                         | RigidbodyConstraints.FreezeRotationX
                         | RigidbodyConstraints.FreezeRotationZ
                         | RigidbodyConstraints.FreezePositionY;
        EditorUtility.SetDirty(rb);
        Debug.Log($"[PhaseB_AutoBuilder] '{bigYahu.name}' Rigidbody: Rotation X/Z + Position Y eingefroren — X/Z bleibt für WASD frei.");
    }

    // ══════════════════════════════════════════════════════════
    // Glow-Sprite-Generator
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Erzeugt (falls nötig) einen weichen radialen Glow-Sprite mit
    /// Smoothstep-Falloff und gibt ihn zurück. Wird unter
    /// <see cref="GLOW_ASSET_PATH"/> persistiert und beim erneuten Aufruf
    /// aus dem AssetDatabase geladen.
    /// </summary>
    /// <returns>Der Glow-Sprite (immer != null nach erstem Aufruf).</returns>
    private static Sprite GetOrCreateGlowSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(GLOW_ASSET_PATH);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Generated"))
            AssetDatabase.CreateFolder("Assets", "Generated");

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // smoothstep für weichen Falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        System.IO.File.WriteAllBytes(GLOW_ASSET_PATH, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(GLOW_ASSET_PATH);

        var importer = (TextureImporter)AssetImporter.GetAtPath(GLOW_ASSET_PATH);
        if (importer != null)
        {
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled       = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(GLOW_ASSET_PATH);
    }

    // ══════════════════════════════════════════════════════════
    // Hierarchie-Aufbau (Build-Pass)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Sucht das <see cref="ROOT_NAME"/>-GameObject in der Scene oder erzeugt
    /// es neu. Neu erzeugte Roots werden Undo-fähig registriert.
    /// </summary>
    /// <param name="scene">Aktive Scene.</param>
    /// <returns>Wurzel-GameObject der Phase-B-Hierarchie.</returns>
    private static GameObject FindOrCreateRoot(Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            var found = FindRecursive(go.transform, ROOT_NAME);
            if (found != null) return found.gameObject;
        }

        var root = new GameObject(ROOT_NAME);
        SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Create PhaseB Root");
        return root;
    }

    /// <summary>
    /// Tiefen-Suche nach einer Transform mit dem angegebenen Namen.
    /// </summary>
    /// <param name="t">Start-Transform.</param>
    /// <param name="name">Gesuchter Name (case-sensitive).</param>
    /// <returns>Erste passende Transform oder <c>null</c>.</returns>
    private static Transform FindRecursive(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var found = FindRecursive(t.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Stellt sicher, dass unter <paramref name="parent"/> ein Canvas mit
    /// ScreenSpaceOverlay + ScaleWithScreenSize + GraphicRaycaster existiert.
    /// </summary>
    /// <param name="parent">Eltern-GameObject (Phase-B-Root).</param>
    /// <returns>Das Canvas-GameObject.</returns>
    private static GameObject EnsureCanvas(GameObject parent)
    {
        var existing = FindRecursive(parent.transform, CANVAS_NAME);
        if (existing != null) return existing.gameObject;

        var canvasGO = new GameObject(CANVAS_NAME);
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
        canvasGO.transform.SetParent(parent.transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        return canvasGO;
    }

    /// <summary>
    /// Stellt sicher, dass unter <paramref name="canvas"/> ein
    /// <see cref="BUTTON_PANEL"/>-Panel mit dunklem Hintergrund und
    /// HorizontalLayoutGroup existiert.
    /// </summary>
    /// <param name="canvas">Canvas-Transform.</param>
    /// <returns>Das ButtonPanel als Transform.</returns>
    private static Transform EnsureButtonPanel(Transform canvas)
    {
        var existing = FindRecursive(canvas, BUTTON_PANEL);
        if (existing != null) return existing;

        var panel = MakeRect(canvas, BUTTON_PANEL,
            new Vector2(0.18f, 0.30f), new Vector2(0.82f, 0.55f));

        var img = panel.gameObject.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.45f);

        var hlg = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 24f;
        hlg.padding                = new RectOffset(24, 24, 24, 24);
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;

        return panel;
    }

    /// <summary>
    /// Erzeugt oder aktualisiert einen einzelnen Color-Button mit Image,
    /// Button-Komponente, Color-Tints und Label-TMP.
    /// </summary>
    /// <param name="parent">ButtonPanel-Transform.</param>
    /// <param name="name">GameObject-Name (z.B. "RedButton").</param>
    /// <param name="label">Anzeige-Text auf dem Button (z.B. "Red").</param>
    /// <param name="tint">Tint-Farbe für Image und Button-Color-States.</param>
    /// <param name="siblingIndex">Position innerhalb der HorizontalLayoutGroup.</param>
    /// <returns>Die Button-Komponente.</returns>
    private static Button EnsureColorButton(Transform parent, string name, string label,
                                             Color tint, int siblingIndex)
    {
        var existing = FindRecursive(parent, name);
        Transform btnRect;

        if (existing != null)
        {
            btnRect = existing;
        }
        else
        {
            btnRect = MakeRect(parent, name, Vector2.zero, Vector2.one);
            Undo.RegisterCreatedObjectUndo(btnRect.gameObject, $"Create {name}");
        }

        btnRect.SetSiblingIndex(siblingIndex);

        var img = btnRect.GetComponent<Image>() ?? btnRect.gameObject.AddComponent<Image>();
        img.color = tint;

        var btn = btnRect.GetComponent<Button>() ?? btnRect.gameObject.AddComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = tint;
        cb.highlightedColor = Color.Lerp(tint, Color.white, 0.25f);
        cb.pressedColor     = Color.Lerp(tint, Color.black, 0.30f);
        btn.colors = cb;

        var lblRect = FindRecursive(btnRect, "Label");
        if (lblRect == null)
            lblRect = MakeRect(btnRect, "Label", Vector2.zero, Vector2.one);

        var tmp = lblRect.GetComponent<TextMeshProUGUI>()
                  ?? lblRect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.fontStyle = FontStyles.Bold;

        return btn;
    }

    /// <summary>
    /// Erzeugt oder aktualisiert das Feedback-Label (zeigt „✓ Korrekt" /
    /// „✗ Falsch") unter dem Canvas.
    /// </summary>
    /// <param name="canvas">Canvas-Transform.</param>
    /// <returns>Die TextMeshProUGUI-Komponente.</returns>
    private static TextMeshProUGUI EnsureFeedbackText(Transform canvas)
    {
        var existing = FindRecursive(canvas, FEEDBACK_NAME);
        Transform rect = existing != null
            ? existing
            : MakeRect(canvas, FEEDBACK_NAME, new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.26f));

        var tmp = rect.GetComponent<TextMeshProUGUI>()
                  ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = string.Empty;
        tmp.fontSize  = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = new Color(0.95f, 0.92f, 0.78f);

        return tmp;
    }

    // ══════════════════════════════════════════════════════════
    // Inspector-Verkabelung via SerializedObject
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Schreibt die Button- und Feedback-Referenzen in die SerializeFields
    /// des Puzzle-Skripts. Nutzt <see cref="SerializedObject"/>, damit die
    /// Inspector-Sicht persistiert.
    /// </summary>
    /// <param name="puzzle">Ziel-Komponente.</param>
    /// <param name="red">Roter Button.</param>
    /// <param name="green">Grüner Button.</param>
    /// <param name="blue">Blauer Button.</param>
    /// <param name="yellow">Gelber Button.</param>
    /// <param name="feedback">Feedback-Label.</param>
    private static void WireSerializedFields(Level3_ColorPuzzle puzzle,
                                              Button red, Button green, Button blue, Button yellow,
                                              TextMeshProUGUI feedback)
    {
        var so = new SerializedObject(puzzle);
        so.FindProperty("redButton").objectReferenceValue    = red;
        so.FindProperty("greenButton").objectReferenceValue  = green;
        so.FindProperty("blueButton").objectReferenceValue   = blue;
        so.FindProperty("yellowButton").objectReferenceValue = yellow;
        so.FindProperty("feedbackText").objectReferenceValue = feedback;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ══════════════════════════════════════════════════════════
    // Allgemeine Utilities
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Sucht ein GameObject mit dem angegebenen Namen scene-weit
    /// (über alle Root-Objekte hinweg).
    /// </summary>
    /// <param name="scene">Aktive Scene.</param>
    /// <param name="name">Gesuchter Name (case-sensitive).</param>
    /// <returns>Erstes passendes GameObject oder <c>null</c>.</returns>
    private static GameObject FindGOInScene(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            var t = FindRecursive(go.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    /// <summary>
    /// Erzeugt ein neues GameObject mit RectTransform unter
    /// <paramref name="parent"/>. Anchors werden gesetzt; Offsets bleiben Null.
    /// </summary>
    /// <param name="parent">Eltern-Transform.</param>
    /// <param name="name">GameObject-Name.</param>
    /// <param name="anchorMin">Untere-linke Anchor-Koordinate.</param>
    /// <param name="anchorMax">Obere-rechte Anchor-Koordinate.</param>
    /// <returns>Die neue Transform (mit RectTransform-Komponente).</returns>
    private static Transform MakeRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
}
