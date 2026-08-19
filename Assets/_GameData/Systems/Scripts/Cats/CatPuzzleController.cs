using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The rules of the cat puzzle: what a hole is allowed to do and what happens to the cats it
/// reaches. It owns no lifecycle — <see cref="LevelSpawner"/> builds a level and
/// <see cref="LevelManager"/> holds it; this only ever operates on the level it is handed.
/// </summary>
public sealed class CatPuzzleController : MonoBehaviour
{
    public static CatPuzzleController Instance { get; private set; }

    [SerializeField] private CatLevelInstance activeLevel;

    public UnityEvent<CatPiece, CatHole> CatCollected = new UnityEvent<CatPiece, CatHole>();
    public UnityEvent<CatHole> HoleMoved = new UnityEvent<CatHole>();
    public UnityEvent<CatHole> HoleCompleted = new UnityEvent<CatHole>();
    public UnityEvent PuzzleCompleted = new UnityEvent();

    public CatLevelInstance ActiveLevel => activeLevel;
    public CatLevelData CurrentLevel => activeLevel != null ? activeLevel.Source : null;
    public GridManager GridManager => activeLevel != null ? activeLevel.Grid : null;

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

    /// <summary>The level these rules apply to. Set by the spawner as a level comes up.</summary>
    public void SetLevel(CatLevelInstance instance)
    {
        activeLevel = instance;
        if (activeLevel != null) activeLevel.RefreshContents();
    }

    public void ClearLevel() => activeLevel = null;

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
            if (cat.ColorId != hole.ColorId) return true;
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
    public Vector2Int SlideHole(CatHole hole, Vector2Int from, Vector2Int target, List<Vector2Int> path = null)
    {
        path?.Clear();
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

            if (primary != Vector2Int.zero && CanPlaceHole(hole, current + primary)) current += primary;
            else if (secondary != Vector2Int.zero && CanPlaceHole(hole, current + secondary)) current += secondary;
            else break;

            path?.Add(current);
        }
        return current;
    }

    /// <summary>Moves a hole and resolves any cats it now sits on.</summary>
    public bool TryMoveHole(CatHole hole, Vector2Int origin)
    {
        if (!CanPlaceHole(hole, origin)) return false;
        CatHoleBuilder.MoveTo(hole, origin, GridManager);
        HoleMoved.Invoke(hole);
        ResolveHole(hole);
        return true;
    }

    public void ResolveAllHoles()
    {
        foreach (CatHole hole in Holes.ToArray()) ResolveHole(hole);
    }

    private void ResolveHole(CatHole hole) => CollectCatsUnder(hole, hole != null ? hole.OriginCell : Vector2Int.zero);

    /// <summary>
    /// Eats every matching cat the shape covers from <paramref name="origin"/>. Called while the
    /// hole is still being dragged, so a cat is taken the moment the hole reaches it rather than
    /// on release. Each cat hops to a hole cell no other cat is using before it disappears.
    /// </summary>
    public void CollectCatsUnder(CatHole hole, Vector2Int origin)
    {
        if (hole == null || !hole.IsActive || activeLevel == null) return;

        List<Vector2Int> holeCells = hole.CellsAt(origin).ToList();
        HashSet<Vector2Int> taken = new HashSet<Vector2Int>();
        foreach (CatPiece cat in Cats)
            if (cat != null && !cat.IsCollected && holeCells.Contains(cat.GridPosition)) taken.Add(cat.GridPosition);

        bool collectedAny = false;
        foreach (CatPiece cat in Cats.ToArray())
        {
            if (cat == null || cat.IsCollected || cat.ColorId != hole.ColorId) continue;
            if (!holeCells.Contains(cat.GridPosition)) continue;

            taken.Remove(cat.GridPosition);
            Vector2Int exit = PickExitCell(holeCells, taken, cat.GridPosition);
            taken.Add(exit);

            cat.SetGridPosition(exit);
            cat.CollectInto(CellToLocal(exit), HoleSurfaceY());
            collectedAny = true;
            CatCollected.Invoke(cat, hole);
        }
        if (!collectedAny) return;

        bool colorComplete = Cats.All(cat => cat == null || cat.ColorId != hole.ColorId || cat.IsCollected);
        if (colorComplete)
        {
            foreach (CatHole sameColor in Holes.Where(candidate => candidate != null && candidate.IsActive && candidate.ColorId == hole.ColorId).ToArray())
            {
                sameColor.Complete();
                HoleCompleted.Invoke(sameColor);
            }
        }

        if (IsComplete) PuzzleCompleted.Invoke();
    }

    /// <summary>Closest hole cell nobody else is heading for, so cats never stack up on the way out.</summary>
    private static Vector2Int PickExitCell(List<Vector2Int> holeCells, HashSet<Vector2Int> taken, Vector2Int from)
    {
        Vector2Int best = from;
        int bestDistance = int.MaxValue;
        foreach (Vector2Int cell in holeCells)
        {
            if (taken.Contains(cell)) continue;
            int distance = Mathf.Abs(cell.x - from.x) + Mathf.Abs(cell.y - from.y);
            if (distance >= bestDistance) continue;
            best = cell;
            bestDistance = distance;
        }
        return best;
    }

    /// <summary>Where the hole surface sits in the Cats parent's space, which cats fall through.</summary>
    private float HoleSurfaceY()
    {
        GridData data = GridManager != null ? GridManager.SavedGridData : null;
        return data != null ? data.holeParentHeight - data.catParentHeight : 0f;
    }
    #endregion
}
