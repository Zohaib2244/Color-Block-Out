using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a <see cref="CatLevelData"/> asset into the scene and reads it back out again.
/// The editor tool and runtime level loading both go through here, so what a designer
/// sees while authoring is exactly what ships.
/// </summary>
public static class CatLevelBuilder
{
    /// <summary>Builds the grid, cats and holes of a level under <paramref name="parent"/>.</summary>
    public static CatLevelInstance Build(CatLevelData level, Transform parent, CatPuzzleConfig config = null)
    {
        if (level == null) return null;
        config = CatPuzzleConfig.Resolve(config);
        if (level.grid == null)
        {
            Debug.LogError($"Level '{level.name}' has no GridData assigned.");
            return null;
        }

        GameObject root = new GameObject(string.IsNullOrEmpty(level.levelName) ? level.name : level.levelName);
        GridBuilder.RegisterCreated(root, "Build Cat Level");
        root.transform.SetParent(parent, false);

        CatLevelInstance instance = root.AddComponent<CatLevelInstance>();
        GridManager grid = GridBuilder.Build(level.grid, root.transform, config);
        instance.Initialize(level, grid, grid.CatParent, grid.HoleParent);

        foreach (CatPlacement placement in level.cats) SpawnCat(placement, instance, config);
        foreach (CatHolePlacement placement in level.holes) SpawnHole(placement, instance, config);

        instance.RefreshContents();
        return instance;
    }

    /// <summary>Adds one cat to a level that is already in the scene.</summary>
    public static CatPiece SpawnCat(CatPlacement placement, CatLevelInstance instance, CatPuzzleConfig config = null)
    {
        config = CatPuzzleConfig.Resolve(config);
        if (placement == null || instance == null) return null;
        if (config == null || config.catPrefab == null)
        {
            Debug.LogError("CatPuzzleConfig has no cat prefab assigned.");
            return null;
        }

        GameObject catObject = GridBuilder.InstantiatePrefab(config.catPrefab, instance.CatRoot);
        CatPiece cat = catObject.GetComponent<CatPiece>();
        if (cat == null) cat = catObject.AddComponent<CatPiece>();
        cat.Configure(placement.color, placement.cell);
        catObject.name = $"Cat_{placement.color}_{placement.cell.x}_{placement.cell.y}";
        PlaceCat(cat, placement.cell, instance.Grid, config);
        instance.Register(cat);
        return cat;
    }

    /// <summary>Adds one hole to a level that is already in the scene.</summary>
    public static CatHole SpawnHole(CatHolePlacement placement, CatLevelInstance instance, CatPuzzleConfig config = null)
    {
        config = CatPuzzleConfig.Resolve(config);
        if (placement == null || instance == null) return null;
        CatHole hole = CatHoleBuilder.Build(placement, instance.Grid, instance.HoleRoot, config);
        instance.Register(hole);
        return hole;
    }

    /// <summary>Puts a cat on a cell. Height comes from the Cats parent, not from the cat.</summary>
    public static void PlaceCat(CatPiece cat, Vector2Int cell, GridManager grid, CatPuzzleConfig config = null)
    {
        if (cat == null) return;
        cat.SetGridPosition(cell);
        if (grid == null) return;
        cat.transform.localPosition = grid.CellToLocalPosition(cell);
    }

    /// <summary>Writes the scene back into the level asset, including holes the designer dragged around.</summary>
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
            target.cats.Add(new CatPlacement { color = cat.Color, cell = cell });
        }

        target.holes = new List<CatHolePlacement>();
        foreach (CatHole hole in instance.Holes)
        {
            if (hole == null) continue;
            Vector2Int origin = instance.Grid != null ? instance.Grid.LocalPositionToCell(hole.transform.localPosition) : hole.OriginCell;
            hole.SetOriginCell(origin);
            target.holes.Add(CatHoleBuilder.Capture(hole));
        }

        if (instance.Source != null && instance.Source != target) target.grid = instance.Source.grid;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(target);
#endif
    }
}
