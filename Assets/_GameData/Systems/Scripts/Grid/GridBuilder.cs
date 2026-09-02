using UnityEngine;

/// <summary>
/// Builds the cells and walls described by a <see cref="GridData"/> asset.
/// Shared by the Grid Creator tool, the Cat Level editor and runtime level spawning,
/// so a grid always looks the same no matter who asked for it.
/// </summary>
public static class GridBuilder
{
    private const string CellsContainerName = "GridCells";
    private const string WallsContainerName = "Walls";
    private const string CatsContainerName = "Cats";
    private const string HolesContainerName = "Holes";

    // Container heights live on the config, so these only stand in for a build with no config.
    private const float DefaultCellHeight = 0f;
    private const float DefaultWallHeight = 0f;
    private const float DefaultCatHeight = 0.178f;
    private const float DefaultHoleHeight = 0.08f;

    private enum Side { North = 0, East = 1, South = 2, West = 3 }

    /// <summary>
    /// Spawns a "Grid" object under <paramref name="parent"/> and fills it with the board.
    /// When <paramref name="centreOnParent"/> is true the board is offset so the parent
    /// sits at the middle of the board, which keeps scale tweens looking correct.
    /// </summary>
    public static GridManager Build(GridData data, Transform parent, CatPuzzleConfig config, bool centreOnParent = true)
    {
        if (data == null) return null;
        data.EnsureArrays();

        GameObject gridObject = new GameObject("Grid");
        RegisterCreated(gridObject, "Create Grid");
        gridObject.transform.SetParent(parent, false);
        gridObject.transform.localRotation = Quaternion.identity;
        gridObject.transform.localScale = Vector3.one;
        gridObject.transform.localPosition = centreOnParent
            ? new Vector3(-(data.gridWidth - 1) * data.cellSize * 0.5f, 0f, -(data.gridLength - 1) * data.cellSize * 0.5f)
            : Vector3.zero;

        GridManager manager = gridObject.AddComponent<GridManager>();

        // The four containers belong to the grid, so everything under them shares its cell space.
        // How high each one sits is project wide and comes from the config, not from the board, so
        // every grid in the game stacks the same way.
        Transform cells = CreateContainer(gridObject.transform, CellsContainerName, config != null ? config.cellParentHeight : DefaultCellHeight);
        Transform walls = CreateContainer(gridObject.transform, WallsContainerName, config != null ? config.wallParentHeight : DefaultWallHeight);
        Transform cats = CreateContainer(gridObject.transform, CatsContainerName, config != null ? config.catParentHeight : DefaultCatHeight);
        Transform holes = CreateContainer(gridObject.transform, HolesContainerName, config != null ? config.holeParentHeight : DefaultHoleHeight);

        for (int x = 0; x < data.gridWidth; x++)
            for (int z = 0; z < data.gridLength; z++)
                if (data.IsPlayable(x, z)) CreateCell(cells, x, z, data, config);

        BuildWalls(walls, data, config);

        manager.CellParent = cells;
        manager.WallParent = walls;
        manager.CatParent = cats;
        manager.HoleParent = holes;
        manager.ApplyGridData(data);
        return manager;
    }

    #region Cells
    private static void CreateCell(Transform parent, int x, int z, GridData data, CatPuzzleConfig config)
    {
        GameObject cell;
        if (config != null && config.cellPrefab != null)
        {
            cell = InstantiatePrefab(config.cellPrefab, parent);
        }
        else
        {
            cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cell.transform.SetParent(parent, false);
            cell.transform.localScale = new Vector3(data.cellSize * 0.9f, 0.1f, data.cellSize * 0.9f);
            RegisterCreated(cell, "Create Grid Cell");
        }

        cell.name = $"Cell_{x}_{z}";
        cell.transform.localPosition = new Vector3(x * data.cellSize, 0f, z * data.cellSize);
        cell.transform.localRotation = Quaternion.identity;

        MeshRenderer renderer = cell.GetComponent<MeshRenderer>();
        if (renderer != null && config != null)
        {
            Material material = (x + z) % 2 == 0 ? config.cellMaterialA : config.cellMaterialB;
            if (material != null) renderer.sharedMaterial = material;
        }

        CellObject cellObject = cell.GetComponent<CellObject>();
        if (cellObject == null) cellObject = cell.AddComponent<CellObject>();
        cellObject.CellPosition = new Vector2Int(x, z);
    }
    #endregion

    #region Walls
    private static void BuildWalls(Transform parent, GridData data, CatPuzzleConfig config)
    {
        int width = data.gridWidth;
        int length = data.gridLength;
        bool[,,] wallMap = new bool[width, length, 4];

        // Every playable cell reports which of its four sides touch the outside.
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                if (!data.IsPlayable(x, z)) continue;
                wallMap[x, z, (int)Side.North] = !data.IsPlayable(x, z + 1);
                wallMap[x, z, (int)Side.East] = !data.IsPlayable(x + 1, z);
                wallMap[x, z, (int)Side.South] = !data.IsPlayable(x, z - 1);
                wallMap[x, z, (int)Side.West] = !data.IsPlayable(x - 1, z);
            }
        }

        // Gate cells (see CatLevelJson.ApplyGates) open a specific edge even though it sits on the
        // board boundary, so no wall - straight or corner - gets built there.
        if (data.wallOpenings != null)
        {
            foreach (GridData.WallOpening opening in data.wallOpenings)
            {
                if (!data.IsWithinGrid(opening.x, opening.z)) continue;
                if (opening.side < 0 || opening.side > 3) continue;
                wallMap[opening.x, opening.z, opening.side] = false;
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                if (wallMap[x, z, (int)Side.North]) CreateStraightWall(parent, wallMap, data, config, x, z, Side.North);
                if (wallMap[x, z, (int)Side.East]) CreateStraightWall(parent, wallMap, data, config, x, z, Side.East);
                if (wallMap[x, z, (int)Side.South]) CreateStraightWall(parent, wallMap, data, config, x, z, Side.South);
                if (wallMap[x, z, (int)Side.West]) CreateStraightWall(parent, wallMap, data, config, x, z, Side.West);
            }
        }

        // Outer corners: two walls of the same playable cell meet.
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                if (!data.IsPlayable(x, z)) continue;
                if (wallMap[x, z, (int)Side.North] && wallMap[x, z, (int)Side.East]) CreateCornerWall(parent, data, config, x, z, 0);
                if (wallMap[x, z, (int)Side.East] && wallMap[x, z, (int)Side.South]) CreateCornerWall(parent, data, config, x, z, 1);
                if (wallMap[x, z, (int)Side.South] && wallMap[x, z, (int)Side.West]) CreateCornerWall(parent, data, config, x, z, 2);
                if (wallMap[x, z, (int)Side.West] && wallMap[x, z, (int)Side.North]) CreateCornerWall(parent, data, config, x, z, 3);
            }
        }

        // Inner corners: a blocked cell wedged between two playable neighbours.
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                if (data.IsPlayable(x, z)) continue;
                if (data.IsPlayable(x + 1, z) && data.IsPlayable(x, z + 1)) CreateInteriorCornerWall(parent, data, config, x, z, 2);
                if (data.IsPlayable(x - 1, z) && data.IsPlayable(x, z + 1)) CreateInteriorCornerWall(parent, data, config, x, z, 1);
                if (data.IsPlayable(x - 1, z) && data.IsPlayable(x, z - 1)) CreateInteriorCornerWall(parent, data, config, x, z, 0);
                if (data.IsPlayable(x + 1, z) && data.IsPlayable(x, z - 1)) CreateInteriorCornerWall(parent, data, config, x, z, 3);
            }
        }
    }

    private static void CreateStraightWall(Transform parent, bool[,,] wallMap, GridData data, CatPuzzleConfig config, int x, int z, Side side)
    {
        bool cornerNorth = false, cornerSouth = false, cornerEast = false, cornerWest = false;
        switch (side)
        {
            case Side.North:
            case Side.South:
                cornerEast = wallMap[x, z, (int)Side.East];
                cornerWest = wallMap[x, z, (int)Side.West];
                break;
            case Side.East:
            case Side.West:
                cornerNorth = wallMap[x, z, (int)Side.North];
                cornerSouth = wallMap[x, z, (int)Side.South];
                break;
        }
        bool hasCornerConnection = cornerNorth || cornerSouth || cornerEast || cornerWest;

        GameObject prefab = hasCornerConnection && config != null && config.wallEndPrefab != null ? config.wallEndPrefab : config?.straightWallPrefab;
        GameObject wall = SpawnWall(parent, prefab, config, data.cellSize);
        wall.name = hasCornerConnection ? $"Wall_StraightEnd_{x}_{z}_{side}" : $"Wall_Straight_{x}_{z}_{side}";

        float spacing = data.cellSize;
        float offset = config != null ? config.wallOffset : 0.4f;
        float height = config != null ? config.wallHeight : 0.17f;
        Vector2Int wallPosition = new Vector2Int(x, z);

        switch (side)
        {
            case Side.West:
                wall.transform.localPosition = new Vector3(x * spacing - offset, height, z * spacing);
                if (!data.IsPlayable(x - 1, z) && data.IsWithinGrid(x - 1, z)) wallPosition = new Vector2Int(x - 1, z);
                break;
            case Side.East:
                wall.transform.localPosition = new Vector3(x * spacing + offset, height, z * spacing);
                if (!data.IsPlayable(x + 1, z) && data.IsWithinGrid(x + 1, z)) wallPosition = new Vector2Int(x + 1, z);
                break;
            case Side.South:
                wall.transform.localPosition = new Vector3(x * spacing, height, z * spacing - offset);
                if (!data.IsPlayable(x, z - 1) && data.IsWithinGrid(x, z - 1)) wallPosition = new Vector2Int(x, z - 1);
                break;
            default:
                wall.transform.localPosition = new Vector3(x * spacing, height, z * spacing + offset);
                if (!data.IsPlayable(x, z + 1) && data.IsWithinGrid(x, z + 1)) wallPosition = new Vector2Int(x, z + 1);
                break;
        }

        // A wall that runs into a corner uses the capped mesh, turned so the cap faces the corner.
        if (hasCornerConnection)
        {
            if (cornerNorth) wall.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            else if (cornerSouth) wall.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            else if (cornerEast) wall.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            else wall.transform.localRotation = Quaternion.Euler(0f, 270f, 0f);
        }
        else
        {
            wall.transform.localRotation = Quaternion.Euler(0f, side == Side.East || side == Side.West ? 0f : 90f, 0f);
        }

        if (prefab != null)
        {
            Vector3 scale = wall.transform.localScale;
            wall.transform.localScale = new Vector3(scale.x, scale.y, (config != null ? config.straightWallThickness : 0.35f) * 1.1f);
        }
        else
        {
            wall.transform.localScale = new Vector3(config != null ? config.straightWallThickness : 0.35f, height, spacing * 1.1f);
        }

        wall.AddComponent<WallData>().wallGridPosition = wallPosition;
    }

    private static void CreateCornerWall(Transform parent, GridData data, CatPuzzleConfig config, int x, int z, int cornerType)
    {
        GameObject wall = SpawnWall(parent, config?.cornerWallPrefab, config, data.cellSize);
        wall.name = $"Wall_Corner_{x}_{z}_{cornerType}";

        float spacing = data.cellSize;
        float wallOffset = config != null ? config.wallOffset : 0.4f;
        float y = config != null ? config.cornerWallHeight : 0.225f;
        Vector2Int wallPosition = new Vector2Int(x, z);

        switch (cornerType)
        {
            case 0: // North east
                wall.transform.localPosition = new Vector3(x * spacing + wallOffset, y, z * spacing + wallOffset);
                if (!data.IsPlayable(x + 1, z + 1) && data.IsWithinGrid(x + 1, z + 1)) wallPosition = new Vector2Int(x + 1, z + 1);
                break;
            case 1: // South east
                wall.transform.localPosition = new Vector3(x * spacing + wallOffset, y, z * spacing - wallOffset);
                if (!data.IsPlayable(x + 1, z - 1) && data.IsWithinGrid(x + 1, z - 1)) wallPosition = new Vector2Int(x + 1, z - 1);
                break;
            case 2: // South west
                wall.transform.localPosition = new Vector3(x * spacing - wallOffset, y, z * spacing - wallOffset);
                if (!data.IsPlayable(x - 1, z - 1) && data.IsWithinGrid(x - 1, z - 1)) wallPosition = new Vector2Int(x - 1, z - 1);
                break;
            default: // North west
                wall.transform.localPosition = new Vector3(x * spacing - wallOffset, y, z * spacing + wallOffset);
                if (!data.IsPlayable(x - 1, z + 1) && data.IsWithinGrid(x - 1, z + 1)) wallPosition = new Vector2Int(x - 1, z + 1);
                break;
        }

        if (config != null && config.cornerWallPrefab != null)
        {
            float yaw = cornerType == 2 ? 0f : cornerType == 0 ? 180f : 270f * cornerType;
            wall.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        wall.AddComponent<WallData>().wallGridPosition = wallPosition;
    }

    private static void CreateInteriorCornerWall(Transform parent, GridData data, CatPuzzleConfig config, int x, int z, int cornerType)
    {
        GameObject wall = SpawnWall(parent, config?.cornerWallPrefab, config, data.cellSize);
        wall.name = $"Wall_InteriorCorner_{x}_{z}_{cornerType}";

        float spacing = data.cellSize;
        float offset = config != null ? config.interiorCornerOffset : 0.13f;
        float y = config != null ? config.cornerWallHeight : 0.225f;

        switch (cornerType)
        {
            case 0: wall.transform.localPosition = new Vector3(x * spacing - offset, y, z * spacing - offset); break;
            case 1: wall.transform.localPosition = new Vector3(x * spacing - offset, y, z * spacing + offset); break;
            case 2: wall.transform.localPosition = new Vector3(x * spacing + offset, y, z * spacing + offset); break;
            default: wall.transform.localPosition = new Vector3(x * spacing + offset, y, z * spacing - offset); break;
        }

        if (config != null && config.cornerWallPrefab != null)
        {
            float yaw = cornerType == 1 ? 90f : cornerType == 3 ? -90f : 270f * cornerType;
            wall.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        wall.AddComponent<WallData>().wallGridPosition = new Vector2Int(x, z);
        NarrowStraightWallsAt(parent, x, z, cornerType, offset);
    }

    /// <summary>Thins and nudges the straight walls sharing a cell with an inner corner so the meshes do not overlap.</summary>
    private static void NarrowStraightWallsAt(Transform parent, int x, int z, int cornerType, float offset)
    {
        foreach (Transform child in parent)
        {
            if (!child.name.StartsWith("Wall_Straight")) continue;
            WallData wallData = child.GetComponent<WallData>();
            if (wallData == null || wallData.wallGridPosition.x != x || wallData.wallGridPosition.y != z) continue;

            Vector3 scale = child.localScale;
            scale.z = 0.25f;
            child.localScale = scale;

            float yaw = child.localRotation.eulerAngles.y;
            bool eastWest = Mathf.Approximately(yaw, 0f) || Mathf.Approximately(yaw, 180f);
            bool northSouth = Mathf.Approximately(yaw, 90f) || Mathf.Approximately(yaw, 270f);

            Vector3 position = child.localPosition;
            switch (cornerType)
            {
                case 0: if (eastWest) position.z += offset; if (northSouth) position.x += offset; break;
                case 1: if (eastWest) position.z -= offset; if (northSouth) position.x += offset; break;
                case 2: if (eastWest) position.z -= offset; if (northSouth) position.x -= offset; break;
                default: if (eastWest) position.z += offset; if (northSouth) position.x -= offset; break;
            }
            child.localPosition = position;
        }
    }

    private static GameObject SpawnWall(Transform parent, GameObject prefab, CatPuzzleConfig config, float spacing)
    {
        GameObject wall;
        if (prefab != null)
        {
            wall = InstantiatePrefab(prefab, parent);
        }
        else
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(parent, false);
            float thickness = config != null ? config.straightWallThickness : 0.35f;
            wall.transform.localScale = new Vector3(thickness, config != null ? config.wallHeight : 0.17f, thickness);
            RegisterCreated(wall, "Create Wall");
        }
        wall.tag = "Wall";
        return wall;
    }
    #endregion

    #region Helpers
    private static Transform CreateContainer(Transform parent, string name, float height)
    {
        GameObject container = new GameObject(name);
        RegisterCreated(container, $"Create {name}");
        container.transform.SetParent(parent, false);
        container.transform.localPosition = new Vector3(0f, height, 0f);
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;
        return container.transform;
    }

    /// <summary>Keeps the prefab link alive when building from an editor tool.</summary>
    internal static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(prefab))
        {
            GameObject editorInstance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
            UnityEditor.Undo.RegisterCreatedObjectUndo(editorInstance, "Create Grid Element");
            return editorInstance;
        }
#endif
        return Object.Instantiate(prefab, parent);
    }

    internal static void RegisterCreated(GameObject instance, string undoName)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(instance, undoName);
#endif
    }

    /// <summary>Destroys children in a way that works in both edit and play mode.</summary>
    internal static void DestroyChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
        }
    }
    #endregion
}
