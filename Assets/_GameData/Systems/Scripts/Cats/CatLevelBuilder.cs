using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a <see cref="CatLevelData"/> into the scene and reads it back out again.
/// The editor tool and runtime level loading both go through here, so what a designer
/// sees while authoring is exactly what ships.
/// </summary>
public static class CatLevelBuilder
{
    /// <summary>Builds the grid, cats and holes of a level under <paramref name="parent"/>.</summary>
    public static CatLevelInstance Build(CatLevelData level, Transform parent, CatPuzzleConfig config)
    {
        if (level == null) return null;
        if (config == null)
        {
            Debug.LogError($"No CatPuzzleConfig supplied, level '{level.DisplayName}' cannot be built. Assign one on the LevelSpawner.");
            return null;
        }
        if (level.grid == null)
        {
            Debug.LogError($"Level '{level.DisplayName}' has no board, so it cannot be built.");
            return null;
        }

        GameObject root = new GameObject(level.DisplayName);
        GridBuilder.RegisterCreated(root, "Build Cat Level");
        root.transform.SetParent(parent, false);

        CatLevelInstance instance = root.AddComponent<CatLevelInstance>();
        GridManager grid = GridBuilder.Build(level.grid, root.transform, config);
        instance.Initialize(level, grid, grid.CatParent, grid.HoleParent);

        if (level.cats != null) foreach (CatPlacement placement in level.cats) SpawnCat(placement, instance, config);
        if (level.holes != null) foreach (CatHolePlacement placement in level.holes) SpawnHole(placement, instance, config);

        instance.RefreshContents();
        RestackCats(instance, config);
        return instance;
    }

    /// <summary>Adds one cat to a level that is already in the scene.</summary>
    public static CatPiece SpawnCat(CatPlacement placement, CatLevelInstance instance, CatPuzzleConfig config)
    {
        if (placement == null || instance == null) return null;
        if (config == null || config.catPrefab == null)
        {
            Debug.LogError("CatPuzzleConfig has no cat prefab assigned.");
            return null;
        }

        GameObject catObject = GridBuilder.InstantiatePrefab(config.catPrefab, instance.CatRoot);
        CatPiece cat = catObject.GetComponent<CatPiece>();
        if (cat == null) cat = catObject.AddComponent<CatPiece>();
        cat.Configure(placement.colorId, placement.cell, config.palette);
        catObject.name = $"Cat_{ColorName(config, placement.colorId)}_{placement.cell.x}_{placement.cell.y}";
        PlaceCat(cat, placement.cell, instance.Grid);
        instance.Register(cat);
        return cat;
    }

    public static string ColorName(CatPuzzleConfig config, int colorId) =>
        config != null && config.palette != null ? config.palette.GetName(colorId) : colorId.ToString();

    /// <summary>
    /// Re-tints everything in a level. Colour tints live in material property blocks, which are
    /// not saved with the scene, so a level that was built earlier and then reloaded needs this.
    /// </summary>
    public static void ApplyColors(CatLevelInstance instance, CatPuzzleConfig config)
    {
        if (instance == null || config == null || config.palette == null) return;
        foreach (CatPiece cat in instance.Cats) if (cat != null) cat.ApplyColor(config.palette);
        foreach (CatHole hole in instance.Holes) if (hole != null) hole.ApplyColor(config.palette);
    }

    /// <summary>Adds one hole to a level that is already in the scene.</summary>
    public static CatHole SpawnHole(CatHolePlacement placement, CatLevelInstance instance, CatPuzzleConfig config)
    {
        if (placement == null || instance == null) return null;
        CatHole hole = CatHoleBuilder.Build(placement, instance.Grid, instance.HoleRoot, config);
        instance.Register(hole);
        return hole;
    }

    /// <summary>Puts a cat on a cell. Height comes from the Cats parent, not from the cat.</summary>
    public static void PlaceCat(CatPiece cat, Vector2Int cell, GridManager grid)
    {
        if (cat == null) return;
        cat.SetGridPosition(cell);
        if (grid == null) return;
        cat.transform.localPosition = grid.CellToLocalPosition(cell);
    }

    /// <summary>
    /// Sorts cats sharing a cell into a vertical stack, so more than one cat can occupy the same
    /// cell as a puzzle element. Cats not sharing a cell with anyone else stay flush with the grid.
    /// Collected cats are skipped since they are on their way out of play.
    /// </summary>
    public static void RestackCats(IEnumerable<CatPiece> cats, GridManager grid, float stackHeight)
    {
        if (grid == null) return;

        Dictionary<Vector2Int, List<CatPiece>> byCell = new Dictionary<Vector2Int, List<CatPiece>>();
        foreach (CatPiece cat in cats)
        {
            if (cat == null || cat.IsCollected) continue;
            if (!byCell.TryGetValue(cat.GridPosition, out List<CatPiece> stack)) byCell[cat.GridPosition] = stack = new List<CatPiece>();
            stack.Add(cat);
        }

        foreach (KeyValuePair<Vector2Int, List<CatPiece>> entry in byCell)
        {
            Vector3 local = grid.CellToLocalPosition(entry.Key);
            for (int i = 0; i < entry.Value.Count; i++)
            {
                local.y = i * stackHeight;
                entry.Value[i].transform.localPosition = local;
            }
        }
    }

    /// <summary>
    /// Restacks and refreshes the pile badges together, since a badge always needs to reflect
    /// whatever the restack just produced. Use the lower-level overload directly only when there is
    /// no <see cref="CatLevelInstance"/>/config on hand to refresh badges with.
    /// </summary>
    public static void RestackCats(CatLevelInstance instance, CatPuzzleConfig config)
    {
        if (instance == null) return;
        RestackCats(instance.Cats, instance.Grid, config != null ? config.catStackHeight : 0f);
        RefreshPileBadges(instance, config);
    }

    /// <summary>
    /// Shows how many cats are stacked on each cell that has two or more, at the bottom-left corner
    /// of whichever one is currently on top. A gate cell (see <see cref="GateCells"/>) always shows
    /// one too, even for a single cat, since that count is the whole point of a gate's queue.
    /// Recomputed after every restack, since both the top cat and its height change as a pile is
    /// worked down.
    /// </summary>
    public static void RefreshPileBadges(CatLevelInstance instance, CatPuzzleConfig config)
    {
        if (instance == null || instance.Grid == null) return;
        GridManager grid = instance.Grid;
        float cellSize = grid.GetCellSize();
        Vector3 cornerOffset = new Vector3(-cellSize * 0.5f, config != null ? config.countBadgeHeight : 0f, -cellSize * 0.5f);
        HashSet<Vector2Int> gateCells = GateCells(grid);

        Dictionary<Vector2Int, CatPiece> topByCell = new Dictionary<Vector2Int, CatPiece>();
        Dictionary<Vector2Int, int> countByCell = new Dictionary<Vector2Int, int>();
        foreach (CatPiece cat in instance.Cats)
        {
            if (cat == null || cat.IsCollected) continue;
            countByCell.TryGetValue(cat.GridPosition, out int count);
            countByCell[cat.GridPosition] = count + 1;
            if (!topByCell.TryGetValue(cat.GridPosition, out CatPiece current) || cat.transform.localPosition.y > current.transform.localPosition.y)
                topByCell[cat.GridPosition] = cat;
        }

        bool ShouldShow(Vector2Int cell, int count) => count >= 2 || gateCells.Contains(cell);

        // A cell that no longer earns a badge - emptied out, or a non-gate pile worked down to one
        // cat - loses it.
        foreach (Vector2Int cell in new List<Vector2Int>(instance.PileBadges.Keys))
            if (!countByCell.TryGetValue(cell, out int count) || !ShouldShow(cell, count)) instance.RemovePileBadge(cell);

        foreach (KeyValuePair<Vector2Int, int> entry in countByCell)
        {
            if (!ShouldShow(entry.Key, entry.Value)) continue;

            if (!instance.TryGetPileBadge(entry.Key, out CatCountBadge badge) || badge == null)
            {
                badge = CatCountBadgeBuilder.Spawn(instance.CatRoot, config);
                if (badge == null) continue;
                instance.SetPileBadge(entry.Key, badge);
            }

            CatPiece top = topByCell[entry.Key];
            badge.transform.localPosition = top.transform.localPosition + cornerOffset;
            badge.SetCount(entry.Value);
            if (config != null && config.palette != null) badge.SetColor(config.palette.GetColor(top.ColorId));
        }
    }

    /// <summary>
    /// Cells a gate used to seal, borrowed from the wall openings <c>CatLevelJson.ApplyGates</c>
    /// leaves behind - a gate cell and an opened edge are 1:1, so nothing extra needs to be tracked
    /// just to remember which piles came from a gate.
    /// </summary>
    private static HashSet<Vector2Int> GateCells(GridManager grid)
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        List<GridData.WallOpening> openings = grid.SavedGridData != null ? grid.SavedGridData.wallOpenings : null;
        if (openings == null) return cells;
        foreach (GridData.WallOpening opening in openings) cells.Add(new Vector2Int(opening.x, opening.z));
        return cells;
    }

    /// <summary>Writes the scene back into a level, including holes the designer dragged around.</summary>
    public static void Capture(CatLevelInstance instance, CatLevelData target)
    {
        if (instance == null || target == null) return;
        instance.RefreshContents();

        target.cats = new List<CatPlacement>();
        foreach (CatPiece cat in instance.Cats)
        {
            if (cat == null) continue;
            Vector2Int cell = instance.Grid != null ? instance.Grid.LocalPositionToCell(cat.transform.localPosition) : cat.GridPosition;
            cat.SetGridPosition(cell);
            target.cats.Add(new CatPlacement { colorId = cat.ColorId, cell = cell });
        }

        target.holes = new List<CatHolePlacement>();
        foreach (CatHole hole in instance.Holes)
        {
            if (hole == null) continue;
            Vector2Int origin = instance.Grid != null ? instance.Grid.LocalPositionToCell(hole.transform.localPosition) : hole.OriginCell;
            hole.SetOriginCell(origin);
            target.holes.Add(CatHoleBuilder.Capture(hole));
        }

        // The board is the level's own, so a capture that came from another level takes a copy.
        if (target.grid == null && instance.Source != null && instance.Source.grid != null) target.grid = instance.Source.grid.Clone();
    }
}
