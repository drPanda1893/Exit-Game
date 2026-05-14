using UnityEngine;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// Singleton – verwaltet den Level-Fortschritt.
///
/// Panel-Modus: levelPanels[] gesetzt → alle Level als UI-Panels in einer Szene.
/// Szenen-Modus: levelPanels leer  → jedes Level eine eigene Szene (Standard).
///
/// Im Szenen-Modus wird CurrentLevel automatisch aus dem geladenen Szenennamen
/// abgeleitet – kein manueller Start()-Aufruf nötig.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Panel-Modus (optional – alle Level in einer Szene)")]
    [SerializeField] private GameObject[] levelPanels;
    [SerializeField] private GameObject winScreen;

    [Header("Szenen-Modus (Standard – Index 0 = Level 1)")]
    [SerializeField] private string[] levelSceneNames =
        { "Level1", "Level2", "Level3", "Level4", "Level5", "Level6" };

    public int CurrentLevel { get; private set; }
    public event Action<int> OnLevelLoaded;

    private float startRealTime = -1f;
    private float elapsedAtWin  = -1f;

    public float ElapsedSeconds
    {
        get
        {
            if (GameTimerLcd.Instance != null) return GameTimerLcd.Instance.ElapsedSeconds;
            if (elapsedAtWin >= 0) return elapsedAtWin;
            return startRealTime >= 0 ? Time.realtimeSinceStartup - startRealTime : 0f;
        }
    }

    public void StopTimer()
    {
        if (elapsedAtWin < 0) elapsedAtWin = ElapsedSeconds;
        GameTimerLcd.Instance?.Stop();
    }

    void EnsureTimerComponent()
    {
        if (GameTimerLcd.Instance == null) gameObject.AddComponent<GameTimerLcd>();
    }

    bool PanelMode => levelPanels != null && levelPanels.Length > 0;

    // -------------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureTimerComponent();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        // Panel-Modus: Level-1-Panel beim ersten Start aktivieren.
        // Szenen-Modus: nichts tun – OnSceneLoaded setzt CurrentLevel automatisch.
        if (PanelMode && CurrentLevel == 0)
            LoadLevel(1);
    }

    // Wird nach jedem SceneManager.LoadScene aufgerufen (auch beim ersten Laden).
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (PanelMode) return;

        for (int i = 0; i < levelSceneNames.Length; i++)
        {
            if (levelSceneNames[i] == scene.name)
            {
                CurrentLevel = i + 1;
                if (CurrentLevel == 1)
                {
                    startRealTime = Time.realtimeSinceStartup;
                    elapsedAtWin  = -1f;
                    EnsureTimerComponent();
                    GameTimerLcd.Instance?.Begin();
                }
                OnLevelLoaded?.Invoke(CurrentLevel);
                Debug.Log($"[GameManager] Szene '{scene.name}' → Level {CurrentLevel}");
                return;
            }
        }
    }

    // -------------------------------------------------------------------------

    public void LoadLevel(int level)
    {
        CurrentLevel = level;
        int idx = level - 1;

        if (PanelMode)
        {
            foreach (var panel in levelPanels)
                if (panel) panel.SetActive(false);
            if (winScreen) winScreen.SetActive(false);

            if (idx >= 0 && idx < levelPanels.Length && levelPanels[idx] != null)
                levelPanels[idx].SetActive(true);
            else if (winScreen)
                winScreen.SetActive(true);

            OnLevelLoaded?.Invoke(level);
            Debug.Log($"[GameManager] Panel Level {level} aktiviert.");
            return;
        }

        // Szenen-Modus
        if (levelSceneNames != null && idx >= 0 && idx < levelSceneNames.Length)
        {
            Debug.Log($"[GameManager] Lade Szene: {levelSceneNames[idx]}");
            SceneManager.LoadScene(levelSceneNames[idx]);
        }
        else
        {
            // Kein weiteres Level definiert – Level-Script zeigt eigenen Win-Screen.
            Debug.Log("[GameManager] Alle Level abgeschlossen.");
        }

        OnLevelLoaded?.Invoke(level);
    }

    public void CompleteCurrentLevel() => LoadLevel(CurrentLevel + 1);

    public void RestartGame()
    {
        startRealTime = -1f;
        elapsedAtWin  = -1f;
        GameTimerLcd.Instance?.ResetTimer();
        LoadLevel(1);
    }
}
