using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Checks all level scenes on editor start and before Play. If any scene
/// file is empty (git pull can blank them), the corresponding builder is
/// invoked automatically and the previously active scene is restored.
///
/// Empty heuristic: a scene saved with NewSceneSetup.EmptyScene is ~3.5 KB.
/// Threshold: 5 KB.
///
/// The rebuild is always deferred via EditorApplication.delayCall so that
/// it runs AFTER Unity has fully settled in edit mode. Rebuilding during
/// ExitingEditMode caused two problems:
///   1. EditorSceneManager.SaveScene did not flush to disk reliably.
///   2. NewScene(Single) inside Build() briefly left the editor with no
///      camera, producing "No cameras rendering" in the Game view.
/// </summary>
[InitializeOnLoad]
public static class EnsureLevel2Built
{
    private const long MinPopulatedSize = 5000;

    private static readonly (string path, System.Action buildFn)[] Levels =
    {
        ("Assets/Scenes/Level1.unity", BuildLevel1PrisonCell.BuildSilent),
        ("Assets/Scenes/Level2.unity", BuildLevel2MaintenanceRoom.BuildSilent),
        ("Assets/Scenes/Level3.unity", BuildLevel3Library.BuildSilent),
        ("Assets/Scenes/Level4.unity", BuildLevel4Computer.BuildSilent),
        ("Assets/Scenes/Level5.unity", BuildLevel5Workshop.BuildSilent),
        ("Assets/Scenes/Level6.unity", BuildLevel6FinalGate.BuildSilent),
    };

    static EnsureLevel2Built()
    {
        EditorApplication.delayCall            += CheckAndBuildAll;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    // ── Play-mode guard ───────────────────────────────────────────────────────
    // Cancel Play immediately if any scene is empty, then defer the actual
    // rebuild to a delayCall so it runs in a clean edit-mode frame.

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        var empty = FindEmptyLevels();
        if (empty.Length == 0) return;

        // Grab the scene path before cancelling – the active scene is still correct here.
        string returnPath = EditorSceneManager.GetActiveScene().path;

        EditorApplication.isPlaying = false;
        Debug.LogWarning("[EnsureAllLevelsBuilt] Leere Level-Szene(n) gefunden – baue nach Edit-Mode-Restore neu.");

        // Defer so the rebuild runs after Unity has fully returned to edit mode.
        EditorApplication.delayCall += () => RebuildAndRestore(returnPath);
    }

    // ── Editor-start guard (delayCall) ────────────────────────────────────────

    private static void CheckAndBuildAll()
    {
        if (Application.isPlaying) return;

        var empty = FindEmptyLevels();
        if (empty.Length == 0) return;

        string returnPath = EditorSceneManager.GetActiveScene().path;

        if (EditorSceneManager.GetActiveScene().isDirty && !string.IsNullOrEmpty(returnPath))
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        else if (string.IsNullOrEmpty(returnPath))
            returnPath = null;

        RebuildAndRestore(returnPath);
    }

    // ── Shared rebuild logic ──────────────────────────────────────────────────

    private static void RebuildAndRestore(string returnPath)
    {
        bool anyFailed = false;

        foreach (var (path, buildFn) in FindEmptyLevels())
        {
            Debug.LogWarning($"[EnsureAllLevelsBuilt] {path} ist leer – baue automatisch.");
            try
            {
                buildFn();
                AssetDatabase.SaveAssets();

                long sizeAfter = File.Exists(path) ? new FileInfo(path).Length : 0;
                if (sizeAfter < MinPopulatedSize)
                {
                    Debug.LogError($"[EnsureAllLevelsBuilt] Rebuild von {path} hat keine Daten erzeugt " +
                                   $"({sizeAfter} Bytes). Bitte manuell via Tools → Build Level X ausführen.");
                    anyFailed = true;
                }
                else
                {
                    Debug.Log($"[EnsureAllLevelsBuilt] {path} erfolgreich gebaut ({sizeAfter / 1024} KB).");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EnsureAllLevelsBuilt] Auto-Build fehlgeschlagen für {path}:\n{ex}\n" +
                               "Bitte manuell über Tools → Build Level X ausführen.");
                anyFailed = true;
            }
        }

        // Restore active scene before showing any dialog (avoids camera-less editor state).
        if (!string.IsNullOrEmpty(returnPath) && File.Exists(returnPath))
            EditorSceneManager.OpenScene(returnPath, OpenSceneMode.Single);

        if (anyFailed)
            EditorUtility.DisplayDialog("Level-Build fehlgeschlagen",
                "Mindestens eine Szene konnte nicht automatisch gebaut werden.\n" +
                "Bitte manuell über Tools → Build Level X ausführen und erneut Play drücken.", "OK");
        else
            EditorUtility.DisplayDialog("Level(s) neu gebaut",
                "Leere Level-Szene(n) wurden automatisch neu gebaut.\nBitte erneut Play drücken.", "OK");
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static (string path, System.Action buildFn)[] FindEmptyLevels()
    {
        var result = new System.Collections.Generic.List<(string, System.Action)>();
        foreach (var (path, buildFn) in Levels)
        {
            if (!File.Exists(path)) continue;
            if (new FileInfo(path).Length < MinPopulatedSize)
                result.Add((path, buildFn));
        }
        return result.ToArray();
    }
}
