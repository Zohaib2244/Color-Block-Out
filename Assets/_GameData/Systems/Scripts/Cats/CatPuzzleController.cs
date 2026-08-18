using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Rules for the cat-and-hole game, independent of block physics.</summary>
public sealed class CatPuzzleController : MonoBehaviour
{
    public static CatPuzzleController Instance { get; private set; }

    [Header("Board")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private CatLevelData levelToLoad;
    [SerializeField] private Transform spawnedContent;
    [SerializeField] private List<CatPiece> cats = new List<CatPiece>();
    [SerializeField] private List<CatHole> holes = new List<CatHole>();

    public UnityEvent<CatPiece, CatHole> CatCollected = new UnityEvent<CatPiece, CatHole>();
    public UnityEvent<CatHole> HoleCompleted = new UnityEvent<CatHole>();
    public UnityEvent PuzzleCompleted = new UnityEvent();

    public IReadOnlyList<CatPiece> Cats => cats;
    public IReadOnlyList<CatHole> Holes => holes;
    public GridManager GridManager => gridManager;
    public CatLevelData CurrentLevel => levelToLoad;
    public int GridWidth => gridManager != null ? gridManager.GetGridWidth() : width;
    public int GridHeight => gridManager != null ? gridManager.GetGridLength() : height;
    public bool IsComplete => cats.All(cat => cat == null || cat.IsCollected);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (gridManager == null) gridManager = GetComponentInParent<GridManager>();
        if (cats.Count == 0) cats = GetComponentsInChildren<CatPiece>(true).ToList();
        if (holes.Count == 0) holes = GetComponentsInChildren<CatHole>(true).ToList();
    }

    private void Start()
    {
        if (levelToLoad != null) LoadLevel(levelToLoad);
        else SyncVisualsToGrid();
    }

    public void SetGridManager(GridManager manager) => gridManager = manager;

    public void LoadLevel(CatLevelData level)
    {
        if (level == null) return;
        levelToLoad = level;
        if (gridManager != null && level.grid != null) gridManager.ApplyGridData(level.grid);
        ClearSpawnedContent();
        cats.Clear();
        holes.Clear();

        if (spawnedContent == null)
        {
            GameObject root = new GameObject("Runtime Level Content");
            root.transform.SetParent(transform, false);
            spawnedContent = root.transform;
        }

        if (level.catPrefab != null)
        foreach (CatPlacement placement in level.cats)
        {
            GameObject instance = Instantiate(level.catPrefab, spawnedContent);
            CatPiece cat = instance.GetComponent<CatPiece>() ?? instance.AddComponent<CatPiece>();
            cat.Configure(placement.color, placement.gridPosition);
            cat.SetColor(placement.color);
            cats.Add(cat);
        }

        CatHoleConfiguration configuration = level.holeConfiguration;
        if (configuration != null)
        foreach (CatHolePlacement placement in level.holes)
        {
            GameObject prefab = configuration.GetPrefab(placement.holeType);
            if (prefab == null) continue;
            GameObject instance = Instantiate(prefab, spawnedContent);
            CatHole hole = instance.GetComponent<CatHole>() ?? instance.AddComponent<CatHole>();
            hole.Configure(placement.color, placement.gridPosition);
            instance.transform.rotation = Quaternion.Euler(0f, placement.rotationQuarterTurns * 90f, 0f);
            if (instance.GetComponent<CatHoleDragHandler>() == null) instance.AddComponent<CatHoleDragHandler>();
            holes.Add(hole);
        }
        SyncVisualsToGrid();
    }

    private void ClearSpawnedContent()
    {
        if (spawnedContent == null) return;
        for (int i = spawnedContent.childCount - 1; i >= 0; i--)
        {
            GameObject child = spawnedContent.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }

    public void SyncVisualsToGrid()
    {
        if (gridManager == null) return;
        foreach (CatPiece cat in cats)
            if (cat != null) cat.transform.position = GridToWorld(cat.GridPosition, cat.transform.position.y);
        foreach (CatHole hole in holes)
            if (hole != null) hole.transform.position = GridToWorld(hole.GridPosition, hole.transform.position.y);
    }

    public Vector3 GridToWorld(Vector2Int position, float y)
    {
        if (gridManager == null) return new Vector3(position.x, y, position.y);
        Vector3 world = gridManager.GridToWorldPosition(position);
        world.y = y;
        return world;
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        if (gridManager == null) return new Vector2Int(Mathf.RoundToInt(worldPosition.x), Mathf.RoundToInt(worldPosition.z));
        return gridManager.WorldToGridPosition(worldPosition);
    }

    public bool IsInside(Vector2Int position) =>
        position.x >= 0 && position.x < GridWidth && position.y >= 0 && position.y < GridHeight;

    /// <summary>Moves a hole and resolves a matching cat on its destination cell.</summary>
    public bool TryMoveHole(CatHole hole, Vector2Int destination)
    {
        if (hole == null || !hole.IsActive || !IsInside(destination)) return false;
        if (holes.Any(other => other != null && other != hole && other.IsActive && other.GridPosition == destination)) return false;

        hole.SetGridPosition(destination);
        hole.transform.position = GridToWorld(destination, hole.transform.position.y);
        ResolveHole(hole);
        return true;
    }

    /// <summary>Validates a drag preview without changing puzzle state.</summary>
    public bool TryPreviewHoleDestination(CatHole hole, Vector2Int destination)
    {
        if (hole == null || !hole.IsActive || !IsInside(destination)) return false;
        return !holes.Any(other => other != null && other != hole && other.IsActive && other.GridPosition == destination);
    }

    public void ResolveAllHoles()
    {
        foreach (CatHole hole in holes.ToArray()) ResolveHole(hole);
    }

    private void ResolveHole(CatHole hole)
    {
        if (hole == null || !hole.IsActive) return;
        CatPiece cat = cats.FirstOrDefault(candidate => candidate != null && !candidate.IsCollected && candidate.GridPosition == hole.GridPosition);
        if (cat == null || cat.Color != hole.Color) return;

        cat.Collect();
        CatCollected.Invoke(cat, hole);

        bool colorComplete = cats.All(candidate => candidate == null || candidate.Color != hole.Color || candidate.IsCollected);
        if (colorComplete)
        {
            foreach (CatHole sameColorHole in holes.Where(candidate => candidate != null && candidate.IsActive && candidate.Color == hole.Color))
            {
                sameColorHole.Complete();
                HoleCompleted.Invoke(sameColorHole);
            }
        }

        if (IsComplete) PuzzleCompleted.Invoke();
    }
}
