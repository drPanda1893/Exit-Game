using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PatchLevel1KeypadBridge
{
    [MenuItem("Tools/Patch Level1 – Add KeypadBridge to NumpadPanel")]
    static void Patch()
    {
        string scenePath = "Assets/Scenes/Level1.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        GameObject numpadPanel = GameObject.Find("NumpadPanel");
        if (numpadPanel == null)
        {
            Debug.LogError("[Patch] NumpadPanel nicht gefunden.");
            return;
        }

        if (numpadPanel.GetComponent<Level1_KeypadBridge>() != null)
        {
            Debug.Log("[Patch] Level1_KeypadBridge ist bereits auf NumpadPanel vorhanden.");
            return;
        }

        Level1_KeypadBridge bridge = numpadPanel.AddComponent<Level1_KeypadBridge>();
        SerializedObject so = new SerializedObject(bridge);
        so.FindProperty("numpadController").objectReferenceValue =
            numpadPanel.GetComponent<NumpadController>();
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Patch] Level1_KeypadBridge erfolgreich zu NumpadPanel hinzugefügt.");
    }
}
