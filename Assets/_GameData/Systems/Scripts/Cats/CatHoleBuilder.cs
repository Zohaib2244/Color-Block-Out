using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Turns a set of connected cells into a hole object. Each cell picks the piece
/// prefab that matches how it connects to its neighbours (Isolated, End Cap,
/// Straight, Corner, One Side or Middle) and is rotated to line the openings up.
/// </summary>
public static class CatHoleBuilder
{
    private static readonly Vector2Int[] Steps =
    {
        Vector2Int.up,    // Direction.Up
        Vector2Int.right, // Direction.Right
        Vector2Int.down,  // Direction.Down
        Vector2Int.left   // Direction.Left
    };

    /// <summary>Spawns the hole described by <paramref name="placement"/> under <paramref name="parent"/>.</summary>
    public static CatHole Build(CatHolePlacement placement, GridManager grid, Transform parent, CatPuzzleConfig config = null)
    {
        if (placement == null || grid == null) return null;
        config = CatPuzzleConfig.Resolve(config);
        List<Vector2Int> offsets = Normalise(placement.offsets);

        GameObject root = new GameObject($"Hole_{placement.color}_{placement.origin.x}_{placement.origin.y}");
        GridBuilder.RegisterCreated(root, "Create Hole");
        root.transform.SetParent(parent, false);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        CatHole hole = root.AddComponent<CatHole>();
        hole.Configure(placement.color, placement.origin, offsets);
        root.AddComponent<CatHoleDragHandler>();

        BuildPieces(hole, grid, config);
        MoveTo(hole, placement.origin, grid, config);
        return hole;
    }

    /// <summary>Rebuilds the piece meshes and colliders for a hole that already exists.</summary>
    public static void BuildPieces(CatHole hole, GridManager grid, CatPuzzleConfig config = null)
    {
        if (hole == null) return;
        config = CatPuzzleConfig.Resolve(config);
        CatHoleConfiguration holeConfiguration = config != null ? config.holeConfiguration : null;
        if (holeConfiguration == null)
        {
            Debug.LogError("CatPuzzleConfig has no CatHoleConfiguration assigned, holes cannot be built.");
            return;
        }

        GridBuilder.DestroyChildren(hole.transform);
        foreach (BoxCollider existing in hole.GetComponents<BoxCollider>())
        {
            if (Application.isPlaying) Object.Destroy(existing); else Object.DestroyImmediate(existing);
        }

        float spacing = grid != null ? grid.GetCellSize() : 1f;
        HashSet<Vector2Int> shape = new HashSet<Vector2Int>(hole.Offsets);
        Material material = GameConstants.GetGateColorMaterial(hole.Color);

        foreach (Vector2Int offset in hole.Offsets)
        {
            List<Direction> connections = GetConnections(offset, shape);
            CatHoleType type = GetHoleType(connections);
            CatHolePrefabData data = holeConfiguration.GetData(type);
            if (data == null || data.prefab == null)
            {
                Debug.LogWarning($"No prefab configured for hole type {type}.");
                continue;
            }

            GameObject piece = GridBuilder.InstantiatePrefab(data.prefab, hole.transform);
            piece.name = $"{type}_{offset.x}_{offset.y}";
            piece.transform.localPosition = new Vector3(offset.x * spacing, 0f, offset.y * spacing);
            piece.transform.localRotation = Quaternion.Euler(0f, GetQuarterTurns(data, connections) * 90f, 0f);

            if (material != null)
                foreach (MeshRenderer renderer in piece.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.sharedMaterial = material;

            // One collider per covered cell keeps the whole shape draggable from any piece.
            BoxCollider collider = hole.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(offset.x * spacing, 0f, offset.y * spacing);
            collider.size = new Vector3(spacing, spacing * 0.5f, spacing);
        }
    }

    /// <summary>Places the hole on a cell and keeps its logical origin in sync.</summary>
    public static void MoveTo(CatHole hole, Vector2Int origin, GridManager grid, CatPuzzleConfig config = null)
    {
        if (hole == null) return;
        config = CatPuzzleConfig.Resolve(config);
        hole.SetOriginCell(origin);
        if (grid == null) return;
        Vector3 position = grid.GridToWorldPosition(origin);
        position.y = grid.transform.position.y + (config != null ? config.holeHeight : 0.03f);
        hole.transform.position = position;
    }

    /// <summary>Shifts offsets so the lowest cell sits at (0,0), the shape's anchor.</summary>
    public static List<Vector2Int> Normalise(IEnumerable<Vector2Int> cells)
    {
        List<Vector2Int> list = cells != null ? cells.Distinct().ToList() : new List<Vector2Int>();
        if (list.Count == 0) return new List<Vector2Int> { Vector2Int.zero };
        int minX = list.Min(cell => cell.x);
        int minY = list.Min(cell => cell.y);
        Vector2Int anchor = new Vector2Int(minX, minY);
        return list.Select(cell => cell - anchor).OrderBy(cell => cell.y).ThenBy(cell => cell.x).ToList();
    }

    /// <summary>Splits a painted selection into separate placements, one per connected island.</summary>
    public static List<CatHolePlacement> SplitIntoPlacements(IEnumerable<Vector2Int> cells, BlockColorTypes color)
    {
        List<CatHolePlacement> placements = new List<CatHolePlacement>();
        HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(cells);

        while (remaining.Count > 0)
        {
            Vector2Int seed = remaining.First();
            List<Vector2Int> island = new List<Vector2Int>();
            Queue<Vector2Int> pending = new Queue<Vector2Int>();
            pending.Enqueue(seed);
            remaining.Remove(seed);

            while (pending.Count > 0)
            {
                Vector2Int current = pending.Dequeue();
                island.Add(current);
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int neighbour = current + step;
                    if (remaining.Remove(neighbour)) pending.Enqueue(neighbour);
                }
            }

            Vector2Int origin = new Vector2Int(island.Min(cell => cell.x), island.Min(cell => cell.y));
            placements.Add(new CatHolePlacement
            {
                color = color,
                origin = origin,
                offsets = Normalise(island)
            });
        }
        return placements;
    }

    public static List<Direction> GetConnections(Vector2Int cell, ICollection<Vector2Int> shape)
    {
        List<Direction> connections = new List<Direction>();
        for (int i = 0; i < Steps.Length; i++)
            if (shape.Contains(cell + Steps[i])) connections.Add((Direction)i);
        return connections;
    }

    public static CatHoleType GetHoleType(List<Direction> connections)
    {
        switch (connections.Count)
        {
            case 0: return CatHoleType.Isolated;
            case 1: return CatHoleType.EndCap;
            case 2:
                bool opposite = (connections.Contains(Direction.Up) && connections.Contains(Direction.Down))
                             || (connections.Contains(Direction.Left) && connections.Contains(Direction.Right));
                return opposite ? CatHoleType.Straight : CatHoleType.Corner;
            case 3: return CatHoleType.OneSide;
            default: return CatHoleType.Middle;
        }
    }

    /// <summary>Finds the clockwise quarter turn that lines the prefab openings up with the real neighbours.</summary>
    public static int GetQuarterTurns(CatHolePrefabData data, List<Direction> connections)
    {
        if (data == null || data.defaultOpenings == null || connections.Count == 0 || connections.Count >= 4) return 0;
        HashSet<Direction> actual = new HashSet<Direction>(connections);
        for (int turns = 0; turns < 4; turns++)
        {
            HashSet<Direction> rotated = new HashSet<Direction>(data.defaultOpenings.Select(direction => (Direction)(((int)direction + turns) % 4)));
            if (rotated.SetEquals(actual)) return turns;
        }
        return 0;
    }

    /// <summary>Reads a hole back out of the scene, picking up any move the designer made.</summary>
    public static CatHolePlacement Capture(CatHole hole) => hole == null ? null : new CatHolePlacement
    {
        color = hole.Color,
        origin = hole.OriginCell,
        offsets = new List<Vector2Int>(hole.Offsets)
    };
}
