using UnityEngine;

public class CharacterSpawner : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Ziehe 'Big Yahu jogging.fbx' aus dem Project-Fenster hier rein")]
    private GameObject characterPrefab;

    void Start()
    {
        SpawnCharacter();
    }

    private void SpawnCharacter()
    {
        if (characterPrefab == null)
        {
            Debug.LogWarning("⚠️ CharacterSpawner: Kein Prefab zugewiesen! Ziehe 'Big Yahu jogging.fbx' in das 'Character Prefab' Feld im Inspector.");
            return;
        }

        GameObject character = Instantiate(characterPrefab, Vector3.zero, Quaternion.identity);
        character.name = "BigYahu";
        character.transform.localScale = Vector3.one;
        Debug.Log("✓ BigYahu gespawnt!");
    }
}
