using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Registers all Level scenes in Unity Build Settings.
/// Run once via Tools > Setup Build Settings.
/// </summary>
public static class SetupBuildSettings
{
    private static readonly string[] SceneNames =
    {
        "Level1", "Level2", "Level3", "Level4", "Level5", "Level6"
    };

    [MenuItem("Tools/Setup Build Settings")]
    public static void Run()
    {
        var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        var existingPaths = new HashSet<string>();
        foreach (var s in existing) existingPaths.Add(s.path);

        int added = 0;
        foreach (string name in SceneNames)
        {
            string[] guids = AssetDatabase.FindAssets($"t:Scene {name}");
            string path = null;
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == name) { path = p; break; }
            }

            if (path == null)
            {
                Debug.LogWarning($"[BuildSettings] Scene not found: {name} – rebuild it first.");
                continue;
            }

            if (!existingPaths.Contains(path))
            {
                existing.Add(new EditorBuildSettingsScene(path, true));
                existingPaths.Add(path);
                added++;
                Debug.Log($"[BuildSettings] Added: {path}");
            }
            else
            {
                // Ensure it's enabled
                for (int i = 0; i < existing.Count; i++)
                {
                    if (existing[i].path == path && !existing[i].enabled)
                    {
                        existing[i] = new EditorBuildSettingsScene(path, true);
                        added++;
                    }
                }
            }
        }

        EditorBuildSettings.scenes = existing.ToArray();
        Debug.Log($"[BuildSettings] Done. {added} scene(s) added/enabled.");
        EditorUtility.DisplayDialog("Build Settings", $"Done. {added} scene(s) added/enabled.", "OK");
    }
}
