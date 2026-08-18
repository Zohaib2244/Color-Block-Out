using UnityEngine;

/// <summary>
/// A reusable board layout. One GridData asset describes the playable shape of a
/// board and can back any number of <see cref="CatLevelData"/> levels.
/// </summary>
[CreateAssetMenu(fileName = "New Grid Data", menuName = "Cat Puzzle/Grid Data")]
public class GridData : ScriptableObject
{
    public int gridWidth = 10;
    public int gridLength = 10;
    public float cellSize = 0.57f;

    [HideInInspector] public Vector3 gridStartPosition = Vector3.zero;

    [System.Serializable]
    public class SerializableGridData
    {
        public bool[] occupiedCells;
        public bool[] wallCells;
    }

    public SerializableGridData gridData;

    public void Initialize(int width, int length)
    {
        gridWidth = Mathf.Max(1, width);
        gridLength = Mathf.Max(1, length);
        gridData = new SerializableGridData
        {
            occupiedCells = new bool[gridWidth * gridLength],
            wallCells = new bool[gridWidth * gridLength]
        };
        MarkDirty();
    }

    public int GetIndex(int x, int z) => z * gridWidth + x;

    public bool IsWithinGrid(int x, int z) => x >= 0 && x < gridWidth && z >= 0 && z < gridLength;

    /// <summary>True for cells that are walls or outside the drawn board.</summary>
    public bool IsWall(int x, int z)
    {
        if (!IsWithinGrid(x, z)) return true;
        if (gridData == null || gridData.wallCells == null) return false;
        int index = GetIndex(x, z);
        return index < gridData.wallCells.Length && gridData.wallCells[index];
    }

    /// <summary>True for cells a cat or hole may stand on.</summary>
    public bool IsPlayable(int x, int z) => IsWithinGrid(x, z) && !IsWall(x, z);

    public bool IsPlayable(Vector2Int cell) => IsPlayable(cell.x, cell.y);

    public void SetWall(int x, int z, bool isWall)
    {
        EnsureArrays();
        if (!IsWithinGrid(x, z)) return;
        gridData.wallCells[GetIndex(x, z)] = isWall;
    }

    /// <summary>Rebuilds the backing arrays when they are missing or the size changed.</summary>
    public void EnsureArrays()
    {
        int required = gridWidth * gridLength;
        if (gridData == null) gridData = new SerializableGridData();
        if (gridData.wallCells == null || gridData.wallCells.Length != required) gridData.wallCells = new bool[required];
        if (gridData.occupiedCells == null || gridData.occupiedCells.Length != required) gridData.occupiedCells = new bool[required];
    }

    public void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void OnEnable() => EnsureArrays();
}
