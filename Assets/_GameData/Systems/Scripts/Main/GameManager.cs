using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    #region Singleton
    public static GameManager Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    [SerializeField] private LevelData levelData;
    public LevelState currentLevelState = LevelState.None;
    public UnityEvent onLevelLoaded;

    public int TotalLevels => levelData != null ? levelData.Count : 0;
    public CatLevelData CurrentLevel { get; private set; }

    void Start()
    {
        GameConstants.InitializeGame();
        onLevelLoaded.AddListener(ConfigureCamera);
    }

    public void LoadLevel(int levelIndex)
    {
        if (levelData == null || levelData.Count == 0)
        {
            Debug.LogError("No level collection assigned to GameManager.");
            return;
        }
        if (CatPuzzleController.Instance == null)
        {
            Debug.LogError("No CatPuzzleController in the scene, a level cannot be spawned.");
            return;
        }

        CurrentLevel = levelData.Get(levelIndex);
        CatPuzzleController.Instance.LoadLevel(CurrentLevel);
        currentLevelState = LevelState.InProgress;
        onLevelLoaded?.Invoke();
        FirebaseHandler.LogLevelEvent(FirebaseHandler.LevelState.Start, levelIndex + 1);
    }

    public void LoadNextLevel() => LoadLevel(GameConstants.CurrentLevelIndex);

    public void RetryLevel()
    {
        if (currentLevelState != LevelState.InProgress && currentLevelState != LevelState.Failed) return;
        GameUIManager.Instance.LevelScreen.StopTimer();
        LoadLevel(GameConstants.CurrentLevelIndex);
    }

    public void LevelFailed()
    {
        if (currentLevelState == LevelState.InProgress)
        {
            currentLevelState = LevelState.Failed;
            GameUIManager.Instance.ShowScreen(ScreenType.GameOver);
            FirebaseHandler.LogLevelEvent(FirebaseHandler.LevelState.Fail, GameConstants.CurrentLevelIndex + 1);
        }
    }

    public void LevelCompleted()
    {
        if (currentLevelState == LevelState.InProgress)
        {
            currentLevelState = LevelState.Completed;
            GameConstants.CurrentLevelIndex++;
            GameUIManager.Instance.ShowScreen(ScreenType.LevelCompleted);
            FirebaseHandler.LogLevelEvent(FirebaseHandler.LevelState.Complete, GameConstants.CurrentLevelIndex + 1);
        }
    }

    public void UnloadAllLevels()
    {
        if (CatPuzzleController.Instance != null) CatPuzzleController.Instance.ClearLevel();
        CurrentLevel = null;
        currentLevelState = LevelState.None;
    }

    void ConfigureCamera()
    {
        if (CurrentLevel == null || Camera.main == null) return;
        Camera.main.transform.position = CurrentLevel.cameraPosition;
        Camera.main.fieldOfView = CurrentLevel.cameraFOV;
    }
}
