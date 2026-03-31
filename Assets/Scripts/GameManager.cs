using UnityEngine;
using System;

/// <summary>
/// Singleton – verwaltet den Level-Fortschritt und schaltet UI-Panels um.
/// Jedes Level bekommt ein eigenes Panel als Kind des Haupt-Canvas.
/// levelPanels[0] = Level 1, levelPanels[1] = Level 2, usw.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level Panels (Index 0 = Level 1)")]
    [SerializeField] private GameObject[] levelPanels;
    [SerializeField] private GameObject winScreen;

    public int CurrentLevel { get; private set; }

    public event Action<int> OnLevelLoaded;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        LoadLevel(1);
    }

    public void LoadLevel(int level)
    {
        CurrentLevel = level;

        foreach (var panel in levelPanels)
            if (panel) panel.SetActive(false);

        if (winScreen) winScreen.SetActive(false);

        if (level >= 1 && level <= levelPanels.Length)
        {
            levelPanels[level - 1].SetActive(true);
        }
        else if (level > levelPanels.Length)
        {
            if (winScreen) winScreen.SetActive(true);
        }

        OnLevelLoaded?.Invoke(level);
        Debug.Log($"[GameManager] Level {level} geladen.");
    }

    public void CompleteCurrentLevel() => LoadLevel(CurrentLevel + 1);

    public void RestartGame() => LoadLevel(1);
}
