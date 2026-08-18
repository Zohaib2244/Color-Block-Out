using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a <see cref="CatLevelData"/> asset into the scene and reads it back out again.
/// The editor tool and runtime level loading both go through here, so what a designer
/// sees while authoring is exactly what ships.
/// </summary>
public static class CatLevelBuilder
{
    public const string CatsContainerName = "Cats";
    public const string HolesContainerName = "Holes";

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
        Transform cats = CreateContainer(root.transform, CatsContainerName, config != null ? config.catHeight : 0.03f);
        Transform holes = CreateContainer(root.transform, HolesContainerName, config != null ? config.holeHeight : 0.03f);
        instance.Initialize(level, grid, cats, holes);

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

    public static void PlaceCat(CatPiece cat, Vector2Int cell, GridManager grid, CatPuzzleConfig config = null)
    {
        if (cat == null) return;
        config = CatPuzzleConfig.Resolve(config);
        cat.SetGridPosition(cell);
        if (grid == null) return;
        Vector3 position = grid.GridToWorldPosition(cell);
        position.y = grid.transform.position.y + (config != null ? config.catHeight : 0.03f);
        cat.transform.position = position;
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
            Vector2Int cell = instance.Grid != null ? instance.Grid.WorldToGridPosition(cat.transform.position) : cat.GridPosition;
            cat.SetGridPosition(cell);
            target.cats.Add(new CatPlacement { color = cat.Color, cell = cell });
        }

        target.holes = new List<CatHolePlacement>();
        foreach (CatHole hole in instance.Holes)
        {
            if (hole == null) continue;
            Vector2Int origin = instance.Grid != null ? instance.Grid.WorldToGridPosition(hole.transform.position) : hole.OriginCell;
            hole.SetOriginCell(origin);
            target.holes.Add(CatHoleBuilder.Capture(hole));
        }

        if (instance.Source != null && instance.Source != target) target.grid = instance.Source.grid;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(target);
#endif
    }

    private static Transform CreateContainer(Transform parent, string name, float height)
    {
        GameObject container = new GameObject(name);
        GridBuilder.RegisterCreated(container, $"Create {name}");
        container.transform.SetParent(parent, false);
        container.transform.localPosition = new Vector3(0f, height, 0f);
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;
        return container.transform;
    }
}
