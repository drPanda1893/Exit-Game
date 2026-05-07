using UnityEditor;
using UnityEngine;

/// <summary>
/// Trägt alle 6 Level-Szenen automatisch in die Build Settings ein.
/// Läuft einmalig wenn Unity das Projekt öffnet (InitializeOnLoad).
/// Kann auch manuell über Tools → Setup Build Scenes ausgeführt werden.
/// </summary>
[InitializeOnLoad]
public static class SetupBuildScenes
{
    static SetupBuildScenes()
    {
        EditorApplication.delayCall += Register;
    }

    [MenuItem("Tools/Setup Build Scenes")]
    public static void Register()
    {
        string[] required =
        {
            "Assets/Scenes/Level1.unity",
            "Assets/Scenes/Level2.unity",
            "Assets/Scenes/Level3.unity",
            "Assets/Scenes/Level4.unity",
            "Assets/Scenes/Level5.unity",
            "Assets/Scenes/Level6.unity",
        };

        var existing = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);

        bool changed = false;
        foreach (string path in required)
        {
            if (AssetDatabase.AssetPathToGUID(path) == "") continue;  // Datei existiert noch nicht

            bool alreadyIn = false;
            foreach (var s in existing)
                if (s.path == path) { alreadyIn = true; break; }

            if (!alreadyIn)
            {
                existing.Add(new EditorBuildSettingsScene(path, true));
                changed = true;
                Debug.Log($"[SetupBuildScenes] Szene hinzugefügt: {path}");
            }
        }

        if (changed)
            EditorBuildSettings.scenes = existing.ToArray();
    }
}
