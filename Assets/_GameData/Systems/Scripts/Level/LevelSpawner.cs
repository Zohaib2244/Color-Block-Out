using UnityEngine;

/// <summary>
/// Builds a level and hands it over. Everything about getting a <see cref="CatLevelData"/> asset
/// into the scene lives here: the grid, the cats, the holes and their colours. Once it is standing
/// up the finished <see cref="CatLevelInstance"/> goes to <see cref="CatPuzzleController"/> for the
/// rules and to <see cref="LevelManager"/>, which owns it from then on.
/// </summary>
public sealed class LevelSpawner : MonoBehaviour
{
    public static LevelSpawner Instance { get; private set; }

    [SerializeField] private CatPuzzleConfig config;
    [Tooltip("Levels are spawned under here. Defaults to this object.")]
    [SerializeField] private Transform levelRoot;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private CatPuzzleController puzzle;

    public CatPuzzleConfig Config => config;
    public Transform LevelRoot => levelRoot != null ? levelRoot : transform;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (levelManager == null) levelManager = FindFirstObjectByType<LevelManager>();
        if (puzzle == null) puzzle = CatPuzzleController.Instance != null ? CatPuzzleController.Instance : FindFirstObjectByType<CatPuzzleController>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // A level built in the editor is already sitting under the root. Adopt it so testing by
        // pressing Play behaves exactly like loading one at runtime.
        if (levelManager != null && levelManager.ActiveLevel != null) return;
        CatLevelInstance existing = LevelRoot.GetComponentInChildren<CatLevelInstance>(true);
        if (existing != null) HandOver(existing);
    }

    /// <summary>Clears whatever is loaded and builds the given level asset in its place.</summary>
    public CatLevelInstance Spawn(CatLevelData level)
    {
        Despawn();
        if (level == null) return null;

        CatLevelInstance instance = CatLevelBuilder.Build(level, LevelRoot, config);
        if (instance == null) return null;

        HandOver(instance);
        return instance;
    }

    public void Despawn()
    {
        if (puzzle != null) puzzle.ClearLevel();
        if (levelManager != null) levelManager.EndLevel();
        GridBuilder.DestroyChildren(LevelRoot);
    }

    private void HandOver(CatLevelInstance instance)
    {
        instance.RefreshContents();
        // Tints live in material property blocks, which a scene reload drops.
        CatLevelBuilder.ApplyColors(instance, config);

        if (puzzle != null) puzzle.SetLevel(instance);
        if (levelManager != null) levelManager.BeginLevel(instance);
    }
}
