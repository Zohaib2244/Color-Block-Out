using DG.Tweening;
using UnityEngine;

/// <summary>
/// Scene-level presentation for whatever level the <see cref="CatPuzzleController"/>
/// currently holds: the spawn/despawn tween, the timer and the completion hand-off.
/// </summary>
public sealed class LevelManager : MonoBehaviour
{
    [SerializeField] private CatPuzzleController puzzle;
    [SerializeField] private float spawnDuration = 0.75f;
    private bool timerStarted;

    private void Awake()
    {
        if (puzzle == null) puzzle = CatPuzzleController.Instance;
        if (puzzle == null) puzzle = FindFirstObjectByType<CatPuzzleController>();
    }

    private void OnEnable()
    {
        if (puzzle == null) return;
        puzzle.LevelBound.AddListener(OnLevelBound);
        puzzle.HoleMoved.AddListener(OnHoleMoved);
        puzzle.PuzzleCompleted.AddListener(OnPuzzleCompleted);
    }

    private void OnDisable()
    {
        if (puzzle == null) return;
        puzzle.LevelBound.RemoveListener(OnLevelBound);
        puzzle.HoleMoved.RemoveListener(OnHoleMoved);
        puzzle.PuzzleCompleted.RemoveListener(OnPuzzleCompleted);
    }

    /// <summary>The countdown starts on the player's first move, not on load.</summary>
    private void OnHoleMoved(CatHole hole) => BeginLevelTimer();

    private void OnLevelBound(CatLevelInstance level)
    {
        timerStarted = false;
        SpawnLevel(level);
    }

    /// <summary>Starts the countdown on the player's first interaction.</summary>
    public void BeginLevelTimer()
    {
        if (timerStarted || puzzle == null || puzzle.CurrentLevel == null) return;
        if (GameUIManager.Instance == null || GameUIManager.Instance.LevelScreen == null) return;
        GameUIManager.Instance.LevelScreen.StartLevelTime(puzzle.CurrentLevel.levelTime);
        timerStarted = true;
    }

    private void OnPuzzleCompleted()
    {
        if (GameUIManager.Instance != null && GameUIManager.Instance.LevelScreen != null) GameUIManager.Instance.LevelScreen.StopTimer();
        DespawnLevel(puzzle != null ? puzzle.ActiveLevel : null);
    }

    private void SpawnLevel(CatLevelInstance level)
    {
        if (level == null) return;
        level.transform.localScale = Vector3.zero;
        level.transform.DOScale(Vector3.one, spawnDuration).SetEase(Ease.OutBack);
    }

    private void DespawnLevel(CatLevelInstance level)
    {
        if (level == null) return;
        level.transform.DOScale(Vector3.zero, spawnDuration).SetEase(Ease.InBack);
    }
}
