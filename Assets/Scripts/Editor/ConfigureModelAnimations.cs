using UnityEditor;
using UnityEngine;

public class ConfigureModelAnimations : AssetPostprocessor
{
    // This method is called by Unity when a model is imported or reimported.
    void OnPostprocessModel(GameObject g)
    {
        ModelImporter modelImporter = assetImporter as ModelImporter;
        if (modelImporter == null) return;

        // Check if this is the model we want to configure.
        if (!assetPath.Contains("Big Yahu jogging.fbx"))
        {
            return;
        }

        // Get the animation clips defined in the model importer.
        // If it's the first import or there are no overrides, this will be empty.
        var existingClips = modelImporter.clipAnimations;

        // If no clips are configured, get the clips that Unity detected by default.
        // This is safer because it doesn't assume we have overrides yet.
        if (existingClips == null || existingClips.Length == 0)
        {
            existingClips = modelImporter.defaultClipAnimations;
        }

        // Ensure we have clips to work with.
        if (existingClips.Length == 0)
        {
            Debug.LogWarning($"Could not find any animation clips in '{assetPath}' to configure.");
            return;
        }
        
        // To be safe, we create a completely new array for the settings.
        // This avoids modifying Unity's internal arrays directly.
        ModelImporterClipAnimation[] newClips = new ModelImporterClipAnimation[existingClips.Length];

        for (int i = 0; i < existingClips.Length; i++)
        {
            // Copy the existing settings for each clip.
            newClips[i] = existingClips[i];

            // Set the loop time on the main animation clip.
            // For a simple FBX, we assume this is the first and only clip.
            newClips[i].loopTime = true;
        }

        // Apply the new clip animation settings.
        modelImporter.clipAnimations = newClips;

        Debug.Log($"✓ Ensured that animation clips in '{assetPath}' are set to loop.");
    }
}
