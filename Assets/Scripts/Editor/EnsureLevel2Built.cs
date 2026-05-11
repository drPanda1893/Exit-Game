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
        ("Assets/Scenes/Level5.unity", BuildLevel5Breadboard.BuildSilent),
        ("Assets/Scenes/Level6.unity", BuildLevel6FinalGate.BuildSilent),
    };

    static EnsureLevel2Built()
    {
        EditorApplication.delayCall            += CheckAndBuildAll;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        var emptyLevels = FindEmptyLevels();
        if (emptyLevels.Length == 0) return;

        // Save active scene path before any BuildSilent() call changes it.
        var activeScene = EditorSceneManager.GetActiveScene();
        string returnPath = activeScene.path;

        EditorApplication.isPlaying = false;

        foreach (var (path, buildFn) in emptyLevels)
        {
            Debug.LogWarning($"[EnsureAllLevelsBuilt] {path} war leer – baue jetzt neu.");
            try
            {
                buildFn();
                AssetDatabase.SaveAssets();
                Debug.Log($"[EnsureAllLevelsBuilt] {path} neu gebaut.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EnsureAllLevelsBuilt] Auto-Build fehlgeschlagen für {path}: {ex.Message}");
            }
        }

        EditorUtility.DisplayDialog("Level(s) neu gebaut",
            "Leere Level-Szene(n) wurden automatisch neu gebaut.\nBitte erneut Play drücken.", "OK");

        // Restore the previously active scene so Play starts from the right scene.
        if (!string.IsNullOrEmpty(returnPath) && File.Exists(returnPath))
            EditorSceneManager.OpenScene(returnPath, OpenSceneMode.Single);
    }

    private static void CheckAndBuildAll()
    {
        if (Application.isPlaying) return;

        var emptyLevels = FindEmptyLevels();
        if (emptyLevels.Length == 0) return;

        var activeScene  = EditorSceneManager.GetActiveScene();
        string returnPath = activeScene.path;

        if (activeScene.isDirty && !string.IsNullOrEmpty(returnPath))
            EditorSceneManager.SaveScene(activeScene);
        else if (string.IsNullOrEmpty(returnPath))
            returnPath = null;

        foreach (var (path, buildFn) in emptyLevels)
        {
            Debug.LogWarning($"[EnsureAllLevelsBuilt] {path} ist leer – baue automatisch.");
            try
            {
                buildFn();
                AssetDatabase.SaveAssets();
                Debug.Log($"[EnsureAllLevelsBuilt] {path} automatisch erstellt.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EnsureAllLevelsBuilt] Auto-Build fehlgeschlagen für {path}: {ex.Message}\n" +
                               "Bitte manuell über Tools → Build Level X ausführen.");
            }
        }

        if (returnPath != null && File.Exists(returnPath))
            EditorSceneManager.OpenScene(returnPath, OpenSceneMode.Single);
    }

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
