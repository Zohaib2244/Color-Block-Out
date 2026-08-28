using UnityEngine;

/// <summary>
/// Grid services shared by the cat puzzle and its editor. The component sits on the
/// object placed at grid cell (0,0), so world positions follow the transform and the
/// same board can be reused by levels anywhere in the scene.
/// </summary>
public sealed class GridManager : MonoBehaviour
{
    /// <summary>
    /// The board this grid was built from, kept as its own JSON so it survives a scene save and a
    /// script recompile. See <see cref="CatLevelInstance.Source"/> for why a serialized
    /// <see cref="GridData"/> field would not: Unity would rebuild it as a default 10x10 board
    /// rather than leaving it null, and a grid with no board would look like an empty one.
    /// </summary>
    [SerializeField, HideInInspector] private string savedGridJson;

    [System.NonSerialized] private GridData savedGridData;

    [SerializeField] private float gridSpacing = 0.57f;
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridLength = 10;

    public Transform WallParent;
    public Transform CellParent;

    /// <summary>Content parents, created with the grid so cats and holes share its cell space.</summary>
    public Transform CatParent;
    public Transform HoleParent;

    public GridData SavedGridData
    {
        get
        {
            if (savedGridData == null && !string.IsNullOrEmpty(savedGridJson)) savedGridData = JsonUtility.FromJson<GridData>(savedGridJson);
            return savedGridData;
        }
    }

    public Vector3 GridStartPosition => transform.position;

    private bool[,] wallCells;

    private void Awake()
    {
        // GridBuilder calls ApplyGridData as it builds, so this only matters for a grid that was
        // saved into the scene and is coming back with the level already assembled.
        if (wallCells == null)
        {
            if (SavedGridData != null) LoadGridData();
            else InitializeGridFromChildren();
        }
    }

    public int GetGridWidth() => gridWidth;
    public int GetGridLength() => gridLength;
    public float GetCellSize() => gridSpacing;

    public bool IsWithinGrid(int x, int z) => x >= 0 && x < gridWidth && z >= 0 && z < gridLength;
    public bool IsWithinGrid(Vector2Int cell) => IsWithinGrid(cell.x, cell.y);

    public bool IsCellWall(int x, int z)
    {
        if (!IsWithinGrid(x, z)) return true;
        EnsureArrays();
        return wallCells[x, z];
    }

    public bool IsCellWall(Vector2Int cell) => IsCellWall(cell.x, cell.y);

    /// <summary>True when a cat or hole is allowed to occupy the cell.</summary>
    public bool IsCellPlayable(Vector2Int cell) => IsWithinGrid(cell) && !IsCellWall(cell);

    public void UpdateGridProperties(int width, int length, float size)
    {
        gridWidth = Mathf.Max(1, width);
        gridLength = Mathf.Max(1, length);
        gridSpacing = Mathf.Max(0.01f, size);
        wallCells = null;
        EnsureArrays();
    }

    private void EnsureArrays()
    {
        if (wallCells == null || wallCells.GetLength(0) != gridWidth || wallCells.GetLength(1) != gridLength)
            wallCells = new bool[gridWidth, gridLength];
    }

    public void InitializeGridFromChildren()
    {
        EnsureArrays();
        if (WallParent == null) return;
        foreach (WallData wall in WallParent.GetComponentsInChildren<WallData>(true))
            if (IsWithinGrid(wall.wallGridPosition.x, wall.wallGridPosition.y))
                wallCells[wall.wallGridPosition.x, wall.wallGridPosition.y] = true;
    }

    /// <summary>Marks everything outside the drawn board as blocked.</summary>
    public void MarkExteriorCellsAsOccupied(bool[,] interiorCells)
    {
        EnsureArrays();
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                wallCells[x, z] = !interiorCells[x, z];
    }

    /// <summary>
    /// Cell position in the grid's own space. Content parented under <see cref="CatParent"/> or
    /// <see cref="HoleParent"/> uses this directly as its local position, so the parent's own Y is
    /// the only thing deciding how high it sits.
    /// </summary>
    public Vector3 CellToLocalPosition(Vector2Int cell) => new Vector3(cell.x * gridSpacing, 0f, cell.y * gridSpacing);

    /// <summary>Inverse of <see cref="CellToLocalPosition"/>, for reading content back off its transform.</summary>
    public Vector2Int LocalPositionToCell(Vector3 localPosition) =>
        new Vector2Int(Mathf.RoundToInt(localPosition.x / gridSpacing), Mathf.RoundToInt(localPosition.z / gridSpacing));

    public Vector3 GridToWorldPosition(Vector2Int gridPos) => transform.TransformPoint(CellToLocalPosition(gridPos));

    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        return new Vector2Int(Mathf.RoundToInt(local.x / gridSpacing), Mathf.RoundToInt(local.z / gridSpacing));
    }

    public Vector3 GetGridCentrePosition() => GridToWorldPosition(Vector2Int.zero)
        + transform.right * ((gridWidth - 1) * gridSpacing * 0.5f)
        + transform.forward * ((gridLength - 1) * gridSpacing * 0.5f);

    public void LoadGridData()
    {
        GridData data = SavedGridData;
        if (data == null) return;
        data.EnsureArrays();
        gridWidth = data.gridWidth;
        gridLength = data.gridLength;
        gridSpacing = data.cellSize;
        wallCells = null;
        EnsureArrays();
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                wallCells[x, z] = data.IsWall(x, z);
    }

    /// <summary>Adopts a board. The grid keeps its own copy, so the level it came from is never written through.</summary>
    public void ApplyGridData(GridData data)
    {
        savedGridData = data != null ? data.Clone() : null;
        savedGridJson = savedGridData != null ? JsonUtility.ToJson(savedGridData) : null;
        LoadGridData();
    }
}
