using UnityEngine;
using DG.Tweening;

/// <summary>Level lifecycle for the cat puzzle.</summary>
public sealed class LevelManager : MonoBehaviour
{
    [SerializeField] private CatPuzzleController puzzle;
    [SerializeField] private int levelTime = 60;
    [SerializeField] private Vector3 cameraPosition;
    [SerializeField] private float cameraFOV;
    private bool timerStarted;

    private void Start()
    {
        if (puzzle == null) puzzle = GetComponentInChildren<CatPuzzleController>();
        if (puzzle != null) puzzle.PuzzleCompleted.AddListener(OnPuzzleCompleted);
        GameUIManager.Instance.ShowScreen(ScreenType.GamePlay);
        SpawnLevel();
    }

    private void OnDestroy()
    {
        if (puzzle != null) puzzle.PuzzleCompleted.RemoveListener(OnPuzzleCompleted);
    }

    public void BeginLevelTimer()
    {
        if (timerStarted || GameUIManager.Instance == null || GameUIManager.Instance.LevelScreen == null) return;
        GameUIManager.Instance.LevelScreen.StartLevelTime(levelTime);
        timerStarted = true;
    }

    private void OnPuzzleCompleted()
    {
        if (GameUIManager.Instance != null && GameUIManager.Instance.LevelScreen != null) GameUIManager.Instance.LevelScreen.StopTimer();
        DespawnLevel();
        DOVirtual.DelayedCall(0.75f, () => GameManager.Instance.LevelCompleted());
    }

    private void SpawnLevel() { transform.localScale = Vector3.zero; transform.DOScale(Vector3.one, 0.75f).SetEase(Ease.OutBack); }
    private void DespawnLevel() { transform.DOScale(Vector3.zero, 0.75f).SetEase(Ease.InBack); }
    public (Vector3 position, float fov) GetCameraProperties() => (cameraPosition, cameraFOV);

#if UNITY_EDITOR
    [ContextMenu("Set Camera Properties")]
    private void SetCameraProperties()
    {
        if (Camera.main == null) return;
        cameraPosition = Camera.main.transform.position; cameraFOV = Camera.main.fieldOfView;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void ConfigureLevel()
    {
        if (puzzle == null) puzzle = GetComponentInChildren<CatPuzzleController>();
        if (puzzle != null && puzzle.GridManager != null) transform.position = puzzle.GridManager.GetGridCentrePosition();
        SetCameraProperties();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void RenameLevel(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        gameObject.name = newName;
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }

    public void MoveCameraToPosition()
    {
        if (Camera.main != null) { Camera.main.transform.position = cameraPosition; Camera.main.fieldOfView = cameraFOV; }
    }
#endif
}
