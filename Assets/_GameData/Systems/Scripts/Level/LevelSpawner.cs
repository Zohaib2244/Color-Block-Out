using UnityEngine;

/// <summary>
/// Builds a level and hands it over. Everything about getting a <see cref="CatLevelData"/> into
/// the scene lives here: the grid, the cats, the holes and their colours. Once it is standing
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

    /// <summary>
    /// Clears whatever is loaded and builds the level in the given file in its place. Levels ship as
    /// JSON, so this is the usual way in; the parse uses this spawner's own config for the palette.
    /// </summary>
    public CatLevelInstance Spawn(TextAsset levelJson)
    {
        if (levelJson == null)
        {
            Debug.LogError("No level file to spawn.");
            return null;
        }

        CatLevelJson.LevelResult result = CatLevelJson.ParseLevel(levelJson.text, levelJson.name, config);
        if (!result.Succeeded)
        {
            Debug.LogError($"Level '{levelJson.name}' could not be loaded:\n{result.Describe()}", levelJson);
            return null;
        }

        // Built even when it validates badly - see LevelData.Get for why that beats refusing it.
        if (result.HasErrors) Debug.LogError($"Level '{levelJson.name}' has problems that will break it:\n{result.Describe()}", levelJson);
        else if (result.issues.Count > 0) Debug.LogWarning($"Level '{levelJson.name}' loaded with warnings:\n{result.Describe()}", levelJson);
        return Spawn(result.level);
    }

    /// <summary>Clears whatever is loaded and builds the given level in its place.</summary>
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
