using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Loading and progression: which level is current, moving between them, and the screens that go
/// with each outcome. It asks <see cref="LevelSpawner"/> to put a level in the scene and listens to
/// <see cref="LevelManager"/> for how that level ended.
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Singleton
    public static GameManager Instance;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    #endregion

    [SerializeField] private LevelData levelData;
    [SerializeField] private LevelSpawner spawner;
    [SerializeField] private LevelManager levelManager;
    public UnityEvent onLevelLoaded;

    public int TotalLevels => levelData != null ? levelData.Count : 0;
    public CatLevelData CurrentLevel { get; private set; }
    public LevelState CurrentLevelState => levelManager != null ? levelManager.State : LevelState.None;

    private void Start()
    {
        GameConstants.InitializeGame();
        if (spawner == null) spawner = LevelSpawner.Instance != null ? LevelSpawner.Instance : FindFirstObjectByType<LevelSpawner>();
        if (levelManager == null) levelManager = LevelManager.Instance != null ? LevelManager.Instance : FindFirstObjectByType<LevelManager>();

        if (levelManager == null) return;
        levelManager.LevelCompleted.AddListener(OnLevelCompleted);
        levelManager.LevelFailed.AddListener(OnLevelFailed);
    }

    private void OnDestroy()
    {
        if (levelManager == null) return;
        levelManager.LevelCompleted.RemoveListener(OnLevelCompleted);
        levelManager.LevelFailed.RemoveListener(OnLevelFailed);
    }

    public void LoadLevel(int levelIndex)
    {
        if (levelData == null || levelData.Count == 0)
        {
            Debug.LogError("No level collection assigned to GameManager.");
            return;
        }
        if (spawner == null)
        {
            Debug.LogError("No LevelSpawner in the scene, a level cannot be built.");
            return;
        }

        CurrentLevel = levelData.Get(levelIndex);
        spawner.Spawn(CurrentLevel);
        if (GameUIManager.Instance != null) GameUIManager.Instance.ShowScreen(ScreenType.GamePlay);
        onLevelLoaded?.Invoke();
        FirebaseHandler.LogLevelEvent(FirebaseHandler.LevelState.Start, levelIndex + 1);
    }

    public void LoadNextLevel() => LoadLevel(GameConstants.CurrentLevelIndex);

    public void RetryLevel() => LoadLevel(GameConstants.CurrentLevelIndex);

    public void UnloadAllLevels()
    {
        if (spawner != null) spawner.Despawn();
        CurrentLevel = null;
    }

    private void OnLevelCompleted()
    {
        GameConstants.CurrentLevelIndex++;
        GameUIManager.Instance.ShowScreen(ScreenType.LevelCompleted);
        FirebaseHandler.LogLevelEvent(FirebaseHandler.LevelState.Complete, GameConstants.CurrentLevelIndex + 1);
    }

    private void OnLevelFailed()
    {
        GameUIManager.Instance.ShowScreen(ScreenType.GameOver);
        FirebaseHandler.LogLevelEvent(FirebaseHandler.LevelState.Fail, GameConstants.CurrentLevelIndex + 1);
    }
}
