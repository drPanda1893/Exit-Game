using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unsichtbare Trigger-Zone: Betritt der Spieler sie, wird die nächste Szene geladen.
/// Wird im Editor hinter die geöffnete Tür platziert.
/// </summary>
public class LevelTransitionTrigger : MonoBehaviour
{
    public string targetScene = "Level2";

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            SceneManager.LoadScene(targetScene);
    }
}
