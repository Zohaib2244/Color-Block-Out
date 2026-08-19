using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Owns the level that is currently being played and the state it is in. It is handed a finished
/// level by <see cref="LevelSpawner"/>, watches the puzzle rules for the moment it is solved or
/// run out of time, and reports that upward. <see cref="GameManager"/> decides what happens next.
/// </summary>
public sealed class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [SerializeField] private CatPuzzleController puzzle;
    [SerializeField] private float spawnDuration = 0.75f;

    [Tooltip("Held after the puzzle is solved so the despawn animation can play.")]
    [SerializeField] private float completeDelay = 0.75f;

    public UnityEvent<CatLevelInstance> LevelStarted = new UnityEvent<CatLevelInstance>();
    public UnityEvent LevelCompleted = new UnityEvent();
    public UnityEvent LevelFailed = new UnityEvent();

    private bool timerStarted;

    public CatLevelInstance ActiveLevel { get; private set; }
    public LevelState State { get; private set; } = LevelState.None;
    public CatLevelData CurrentLevel => ActiveLevel != null ? ActiveLevel.Source : null;
    public bool IsPlaying => State == LevelState.InProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (puzzle == null) puzzle = CatPuzzleController.Instance != null ? CatPuzzleController.Instance : FindFirstObjectByType<CatPuzzleController>();
    }

    private void OnEnable()
    {
        if (puzzle == null) return;
        puzzle.HoleMoved.AddListener(OnHoleMoved);
        puzzle.PuzzleCompleted.AddListener(OnPuzzleSolved);
    }

    private void OnDisable()
    {
        if (puzzle == null) return;
        puzzle.HoleMoved.RemoveListener(OnHoleMoved);
        puzzle.PuzzleCompleted.RemoveListener(OnPuzzleSolved);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Takes ownership of a freshly spawned level.</summary>
    public void BeginLevel(CatLevelInstance instance)
    {
        if (instance == null) return;
        ActiveLevel = instance;
        State = LevelState.InProgress;
        timerStarted = false;

        instance.transform.localScale = Vector3.zero;
        instance.transform.DOScale(Vector3.one, spawnDuration).SetEase(Ease.OutBack);
        LevelStarted.Invoke(instance);
    }

    /// <summary>Drops the current level without judging it, for a reload or a return to menu.</summary>
    public void EndLevel()
    {
        StopTimer();
        ActiveLevel = null;
        State = LevelState.None;
        timerStarted = false;
    }

    /// <summary>The countdown starts on the player's first move, not on load.</summary>
    private void OnHoleMoved(CatHole hole) => BeginTimer();

    public void BeginTimer()
    {
        if (timerStarted || !IsPlaying || CurrentLevel == null) return;
        if (GameUIManager.Instance == null || GameUIManager.Instance.LevelScreen == null) return;
        GameUIManager.Instance.LevelScreen.StartLevelTime(CurrentLevel.levelTime);
        timerStarted = true;
    }

    private void OnPuzzleSolved()
    {
        if (!IsPlaying) return;
        State = LevelState.Completed;
        StopTimer();
        Despawn();
        DOVirtual.DelayedCall(completeDelay, () => LevelCompleted.Invoke());
    }

    /// <summary>Called when the countdown runs out.</summary>
    public void Fail()
    {
        if (!IsPlaying) return;
        State = LevelState.Failed;
        StopTimer();
        LevelFailed.Invoke();
    }

    private void Despawn()
    {
        if (ActiveLevel == null) return;
        ActiveLevel.transform.DOScale(Vector3.zero, spawnDuration).SetEase(Ease.InBack);
    }

    private void StopTimer()
    {
        if (GameUIManager.Instance != null && GameUIManager.Instance.LevelScreen != null)
            GameUIManager.Instance.LevelScreen.StopTimer();
    }
}
