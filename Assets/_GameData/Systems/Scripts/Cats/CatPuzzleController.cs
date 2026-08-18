using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The single scene-level rules object for the cat puzzle. Levels are spawned as
/// <see cref="CatLevelInstance"/> objects that bind themselves here, so the controller
/// outlives every level and nothing in a level asset points back at the scene.
/// </summary>
public sealed class CatPuzzleController : MonoBehaviour
{
    public static CatPuzzleController Instance { get; private set; }

    [SerializeField] private Transform levelRoot;
    [SerializeField] private CatPuzzleConfig config;
    [SerializeField] private CatLevelInstance activeLevel;

    public UnityEvent<CatPiece, CatHole> CatCollected = new UnityEvent<CatPiece, CatHole>();
    public UnityEvent<CatHole> HoleMoved = new UnityEvent<CatHole>();
    public UnityEvent<CatHole> HoleCompleted = new UnityEvent<CatHole>();
    public UnityEvent<CatLevelInstance> LevelBound = new UnityEvent<CatLevelInstance>();
    public UnityEvent PuzzleCompleted = new UnityEvent();

    public CatLevelInstance ActiveLevel => activeLevel;
    public CatLevelData CurrentLevel => activeLevel != null ? activeLevel.Source : null;
    public GridManager GridManager => activeLevel != null ? activeLevel.Grid : null;
    public CatPuzzleConfig Config => config != null ? config : CatPuzzleConfig.Instance;
    public Transform LevelRoot => levelRoot != null ? levelRoot : transform;

    private IReadOnlyList<CatPiece> Cats => activeLevel != null ? activeLevel.Cats : System.Array.Empty<CatPiece>();
    private IReadOnlyList<CatHole> Holes => activeLevel != null ? activeLevel.Holes : System.Array.Empty<CatHole>();

    public bool HasLevel => activeLevel != null;
    public bool IsComplete => HasLevel && Cats.All(cat => cat == null || cat.IsCollected);
    public int GridWidth => GridManager != null ? GridManager.GetGridWidth() : 0;
    public int GridHeight => GridManager != null ? GridManager.GetGridLength() : 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // A level built in the editor is already sitting under the root; adopt it and
        // announce it so presentation behaves the same as a level loaded at runtime.
        if (activeLevel == null) activeLevel = LevelRoot.GetComponentInChildren<CatLevelInstance>(true);
        if (activeLevel == null) return;
        activeLevel.RefreshContents();
        LevelBound.Invoke(activeLevel);
    }

    #region Level lifecycle
    /// <summary>Clears whatever is loaded and spawns the given level asset.</summary>
    public CatLevelInstance LoadLevel(CatLevelData level)
    {
        ClearLevel();
        if (level == null) return null;
        CatLevelInstance instance = CatLevelBuilder.Build(level, LevelRoot, Config);
        BindLevel(instance);
        return instance;
    }

    /// <summary>Called by a level as it comes to life. The controller keeps only one at a time.</summary>
    public void BindLevel(CatLevelInstance instance)
    {
        if (instance == null || activeLevel == instance) return;
        activeLevel = instance;
        activeLevel.RefreshContents();
        LevelBound.Invoke(activeLevel);
    }

    public void UnbindLevel(CatLevelInstance instance)
    {
        if (activeLevel == instance) activeLevel = null;
    }

    public void ClearLevel()
    {
        activeLevel = null;
        GridBuilder.DestroyChildren(LevelRoot);
    }
    #endregion

    #region Grid helpers
    /// <summary>Cell position in grid space, which is what content parented under the grid uses.</summary>
    public Vector3 CellToLocal(Vector2Int cell)
    {
        GridManager grid = GridManager;
        return grid != null ? grid.CellToLocalPosition(cell) : new Vector3(cell.x, 0f, cell.y);
    }

    public Vector3 GridToWorld(Vector2Int cell, float y)
    {
        GridManager grid = GridManager;
        if (grid == null) return new Vector3(cell.x, y, cell.y);
        Vector3 world = grid.GridToWorldPosition(cell);
        world.y = y;
        return world;
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        GridManager grid = GridManager;
        if (grid == null) return new Vector2Int(Mathf.RoundToInt(worldPosition.x), Mathf.RoundToInt(worldPosition.z));
        return grid.WorldToGridPosition(worldPosition);
    }

    public bool IsPlayable(Vector2Int cell)
    {
        GridManager grid = GridManager;
        return grid == null ? cell.x >= 0 && cell.y >= 0 : grid.IsCellPlayable(cell);
    }
    #endregion

    #region Hole movement
    /// <summary>
    /// True when every cell the shape would cover from <paramref name="origin"/> is free. The
    /// shape is what defines the limits: the board edge and walls, holes of any colour, and cats
    /// this hole cannot swallow. Cats of the hole's own colour are not obstacles — it eats them.
    /// </summary>
    public bool CanPlaceHole(CatHole hole, Vector2Int origin)
    {
        if (hole == null || !hole.IsActive) return false;
        foreach (Vector2Int cell in hole.CellsAt(origin))
        {
            if (!IsPlayable(cell)) return false;
            if (IsBlockedByCat(hole, cell)) return false;
            if (IsBlockedByHole(hole, cell)) return false;
        }
        return true;
    }

    private bool IsBlockedByCat(CatHole hole, Vector2Int cell)
    {
        foreach (CatPiece cat in Cats)
        {
            if (cat == null || cat.IsCollected || cat.GridPosition != cell) continue;
            if (cat.Color != hole.Color) return true;
        }
        return false;
    }

    private bool IsBlockedByHole(CatHole hole, Vector2Int cell)
    {
        foreach (CatHole other in Holes)
        {
            if (other == null || other == hole || !other.IsActive) continue;
            if (other.Covers(cell)) return true;
        }
        return false;
    }

    /// <summary>
    /// Walks the hole one cell at a time toward <paramref name="target"/> and stops where the
    /// shape would clip something, so a hole can never jump an obstacle. When the dominant axis
    /// is blocked the other one is tried, which lets the shape slide along a wall instead of
    /// sticking. Returns the furthest origin actually reachable.
    /// </summary>
    public Vector2Int SlideHole(CatHole hole, Vector2Int from, Vector2Int target)
    {
        if (hole == null || !hole.IsActive) return from;

        Vector2Int current = from;
        int remaining = Mathf.Abs(target.x - from.x) + Mathf.Abs(target.y - from.y);
        while (current != target && remaining-- > 0)
        {
            Vector2Int delta = target - current;
            Vector2Int stepX = new Vector2Int(System.Math.Sign(delta.x), 0);
            Vector2Int stepY = new Vector2Int(0, System.Math.Sign(delta.y));
            bool xFirst = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            Vector2Int primary = xFirst ? stepX : stepY;
            Vector2Int secondary = xFirst ? stepY : stepX;

            if (primary != Vector2Int.zero && CanPlaceHole(hole, current + primary)) { current += primary; continue; }
            if (secondary != Vector2Int.zero && CanPlaceHole(hole, current + secondary)) { current += secondary; continue; }
            break;
        }
        return current;
    }

    /// <summary>Moves a hole and resolves any cats it now sits on.</summary>
    public bool TryMoveHole(CatHole hole, Vector2Int origin)
    {
        if (!CanPlaceHole(hole, origin)) return false;
        CatHoleBuilder.MoveTo(hole, origin, GridManager, Config);
        HoleMoved.Invoke(hole);
        ResolveHole(hole);
        return true;
    }

    public void ResolveAllHoles()
    {
        foreach (CatHole hole in Holes.ToArray()) ResolveHole(hole);
    }

    private void ResolveHole(CatHole hole)
    {
        if (hole == null || !hole.IsActive || activeLevel == null) return;

        bool collectedAny = false;
        foreach (CatPiece cat in Cats.ToArray())
        {
            if (cat == null || cat.IsCollected || cat.Color != hole.Color) continue;
            if (!hole.Covers(cat.GridPosition)) continue;
            cat.Collect();
            collectedAny = true;
            CatCollected.Invoke(cat, hole);
        }
        if (!collectedAny) return;

        bool colorComplete = Cats.All(cat => cat == null || cat.Color != hole.Color || cat.IsCollected);
        if (colorComplete)
        {
            foreach (CatHole sameColor in Holes.Where(candidate => candidate != null && candidate.IsActive && candidate.Color == hole.Color).ToArray())
            {
                sameColor.Complete();
                HoleCompleted.Invoke(sameColor);
            }
        }

        if (IsComplete) PuzzleCompleted.Invoke();
    }
    #endregion
}
