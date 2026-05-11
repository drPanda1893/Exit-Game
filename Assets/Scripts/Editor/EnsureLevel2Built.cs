using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Prüft, ob Level2.unity leer ist und ruft ggf. den Builder automatisch auf.
/// Triggert sowohl beim Editor-Start als auch beim Drücken von Play, damit
/// der Spieler beim Übergang von Level 1 nach Level 2 niemals in einer leeren
/// Szene landet ("No cameras rendering" / hängendes Spiel).
///
/// Heuristik: eine leere Unity-Szene (NewSceneSetup.EmptyScene + SaveScene)
/// liegt bei ~3.5 KB / ~125 Zeilen. Die gebaute Szene ist um Größenordnungen
/// größer (>50 KB). Schwelle: 5 KB.
/// </summary>
[InitializeOnLoad]
public static class EnsureLevel2Built
{
    private const string ScenePath = "Assets/Scenes/Level2.unity";
    private const long   MinPopulatedSize = 5000;

    static EnsureLevel2Built()
    {
        EditorApplication.delayCall            += CheckAndBuild;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        if (!File.Exists(ScenePath)) return;
        if (new FileInfo(ScenePath).Length >= MinPopulatedSize) return;

        // Level2 ist leer → Play stoppen, neu bauen, Play danach wieder starten.
        EditorApplication.isPlaying = false;
        Debug.LogWarning("[EnsureLevel2Built] Level2.unity war leer – baue jetzt neu, bitte erneut Play drücken.");

        try
        {
            BuildLevel2MaintenanceRoom.BuildSilent();
            AssetDatabase.SaveAssets();
            Debug.Log("[EnsureLevel2Built] Level2.unity neu gebaut – Play erneut drücken.");
            EditorUtility.DisplayDialog("Level 2 neu gebaut",
                "Level2.unity war leer und wurde automatisch neu gebaut.\nBitte erneut Play drücken.", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EnsureLevel2Built] Auto-Build fehlgeschlagen: {ex.Message}\n" +
                           "Bitte manuell über Tools → Level 2 → Rebuild Now ausführen.");
        }
    }

    private static void CheckAndBuild()
    {
        if (Application.isPlaying) return;
        if (!File.Exists(ScenePath)) return;
        if (new FileInfo(ScenePath).Length >= MinPopulatedSize) return;

        Debug.LogWarning("[EnsureLevel2Built] Level2.unity ist leer – baue Wartungsraum automatisch.");

        // Aktive Szene merken (und vorher speichern falls dirty), damit wir nach
        // dem Build dorthin zurückkehren können.
        var activeScene  = EditorSceneManager.GetActiveScene();
        string returnPath = activeScene.path;
        bool   savedReturn = false;

        if (activeScene.isDirty && !string.IsNullOrEmpty(returnPath))
            savedReturn = EditorSceneManager.SaveScene(activeScene);
        else if (string.IsNullOrEmpty(returnPath))
            returnPath = null; // unbenannte Szene – nicht zurückkehren

        try
        {
            BuildLevel2MaintenanceRoom.BuildSilent();
            AssetDatabase.SaveAssets();
            Debug.Log("[EnsureLevel2Built] Level2.unity automatisch erstellt.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EnsureLevel2Built] Auto-Build fehlgeschlagen: {ex.Message}\n" +
                           "Bitte manuell über Tools → Level 2 → Rebuild Now (1-Klick) ausführen.");
        }

        if (returnPath != null && returnPath != ScenePath && File.Exists(returnPath))
        {
            EditorSceneManager.OpenScene(returnPath, OpenSceneMode.Single);
            if (savedReturn) Debug.Log($"[EnsureLevel2Built] Zurück zu {returnPath}.");
        }
    }
}
