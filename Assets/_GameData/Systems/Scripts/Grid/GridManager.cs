using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Grid services shared by the cat puzzle and its editor.</summary>
public sealed class GridManager : MonoBehaviour
{
    [SerializeField] private GridData savedGridData;
    [SerializeField] private float gridSpacing = 2.25f;
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridLength = 10;
    [SerializeField] private Vector3 gridStartPosition;
    public Transform WallParent;
    public Transform CellParent;
    public Transform HoleParent;
    public GridData SavedGridData => savedGridData;
    public Vector3 GridStartPosition { get => gridStartPosition; set => gridStartPosition = value; }
    public UnityEvent onGridInitialized = new UnityEvent();
    private bool[,] wallCells;
    private Dictionary<Vector2Int, GameObject> wallRegistry = new Dictionary<Vector2Int, GameObject>();

    private void Start()
    {
        if (savedGridData != null) LoadGridData(); else InitializeGridFromChildren();
        BuildWallRegistry();
        onGridInitialized.Invoke();
    }

    public int GetGridWidth() => gridWidth;
    public int GetGridLength() => gridLength;
    public float GetCellSize() => gridSpacing;
    public bool IsWithinGrid(int x, int z) => x >= 0 && x < gridWidth && z >= 0 && z < gridLength;
    public bool IsCellWall(int x, int z) => IsWithinGrid(x, z) && wallCells != null && wallCells[x, z];

    public void UpdateGridProperties(int width, int length, float size)
    {
        gridWidth = Mathf.Max(1, width); gridLength = Mathf.Max(1, length); gridSpacing = Mathf.Max(0.01f, size); EnsureArrays();
    }

    private void EnsureArrays()
    {
        if (wallCells == null || wallCells.GetLength(0) != gridWidth || wallCells.GetLength(1) != gridLength) wallCells = new bool[gridWidth, gridLength];
    }

    public void InitializeGridFromChildren()
    {
        EnsureArrays();
        if (WallParent == null) return;
        foreach (WallData wall in WallParent.GetComponentsInChildren<WallData>(true))
            if (IsWithinGrid(wall.wallGridPosition.x, wall.wallGridPosition.y)) wallCells[wall.wallGridPosition.x, wall.wallGridPosition.y] = true;
    }

    public void MarkExteriorCellsAsOccupied(bool[,] interiorCells)
    {
        EnsureArrays();
        for (int x = 0; x < gridWidth; x++) for (int z = 0; z < gridLength; z++) wallCells[x, z] = !interiorCells[x, z];
    }

    private void BuildWallRegistry()
    {
        wallRegistry.Clear(); if (WallParent == null) return;
        foreach (WallData wall in WallParent.GetComponentsInChildren<WallData>(true)) wallRegistry[wall.wallGridPosition] = wall.gameObject;
    }

    public Vector3 GridToWorldPosition(Vector2Int gridPos) => new Vector3(gridStartPosition.x + gridPos.x * gridSpacing, gridStartPosition.y, gridStartPosition.z + gridPos.y * gridSpacing);
    public Vector2Int WorldToGridPosition(Vector3 worldPos) => new Vector2Int(Mathf.RoundToInt((worldPos.x - gridStartPosition.x) / gridSpacing), Mathf.RoundToInt((worldPos.z - gridStartPosition.z) / gridSpacing));
    public Vector2Int[] WorldToGridPositions(Vector3[] positions)
    {
        Vector2Int[] result = new Vector2Int[positions.Length]; for (int i = 0; i < positions.Length; i++) result[i] = WorldToGridPosition(positions[i]); return result;
    }
    public Vector3 GetGridCentrePosition() => new Vector3(gridStartPosition.x + (gridWidth - 1) * gridSpacing / 2f, gridStartPosition.y, gridStartPosition.z + (gridLength - 1) * gridSpacing / 2f);

    public void LoadGridData()
    {
        if (savedGridData == null) return;
        gridWidth = savedGridData.gridWidth; gridLength = savedGridData.gridLength; gridSpacing = savedGridData.cellSize; gridStartPosition = savedGridData.gridStartPosition; EnsureArrays();
        if (savedGridData.gridData == null || savedGridData.gridData.wallCells == null) return;
        for (int x = 0; x < gridWidth; x++) for (int z = 0; z < gridLength; z++) wallCells[x, z] = savedGridData.gridData.wallCells[savedGridData.GetIndex(x, z)];
    }

    public void ApplyGridData(GridData data)
    {
        savedGridData = data;
        LoadGridData();
        BuildWallRegistry();
    }

    public void SaveGridDataToAsset(string assetName)
    {
#if UNITY_EDITOR
        string folder = "Assets/_GameData/Levels/GridData";
        if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_GameData/Levels")) UnityEditor.AssetDatabase.CreateFolder("Assets/_GameData", "Levels");
            UnityEditor.AssetDatabase.CreateFolder("Assets/_GameData/Levels", "GridData");
        }
        GridData data = ScriptableObject.CreateInstance<GridData>(); data.Initialize(gridWidth, gridLength); data.cellSize = gridSpacing; data.gridStartPosition = gridStartPosition;
        for (int x = 0; x < gridWidth; x++) for (int z = 0; z < gridLength; z++) data.gridData.wallCells[data.GetIndex(x, z)] = wallCells[x, z];
        UnityEditor.AssetDatabase.CreateAsset(data, UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset"));
        savedGridData = data;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }
}
