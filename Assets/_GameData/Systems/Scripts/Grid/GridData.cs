using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A reusable board layout: which cells of a width x length board are playable and how big a cell
/// is. How high the cell, wall, cat and hole parents sit inside a grid is project wide and lives on
/// <see cref="CatPuzzleConfig"/>, so it is deliberately not part of a board.
///
/// This is plain data, not an asset. Boards are authored in the web level editor and shipped as
/// JSON; <see cref="CatLevelJson"/> turns that JSON into one of these. A level carries its own
/// copy (<see cref="CatLevelData.grid"/>), so nothing has to be resolved at load time.
/// </summary>
[Serializable]
public class GridData
{
    /// <summary>Name of the board this came from. Presentation only - nothing looks a grid up by name.</summary>
    public string gridName;

    public int gridWidth = 10;
    public int gridLength = 10;
    public float cellSize = 0.57f;

    [Serializable]
    public class SerializableGridData
    {
        public bool[] wallCells;
    }

    /// <summary>
    /// Blocked cells, flat, index = z * width + x. The JSON carries the inverse of this
    /// (playableCells), which is flipped on the way in and back out again on the way out.
    /// </summary>
    public SerializableGridData gridData;

    /// <summary>One boundary edge <see cref="GridBuilder"/> should leave open rather than wall off.</summary>
    [Serializable]
    public struct WallOpening
    {
        public int x;
        public int z;

        /// <summary>Matches <c>GridBuilder.Side</c> / <see cref="Direction"/>: 0=North, 1=East, 2=South, 3=West.</summary>
        public int side;
    }

    /// <summary>
    /// Edges left un-walled even though they sit on the board boundary. Unity has no gate yet, so
    /// this is how <see cref="CatLevelJson"/> opens a gap where one was authored - see
    /// <c>CatLevelJson.ApplyGates</c>. Never written by the Grid Creator; only a level's own copy of
    /// its board carries any.
    /// </summary>
    public List<WallOpening> wallOpenings = new List<WallOpening>();

    public GridData() { }

    public GridData(int width, int length) => Initialize(width, length);

    /// <summary>A deep copy, so a level editing its grid cannot reach into another level's.</summary>
    public GridData Clone()
    {
        EnsureArrays();
        GridData copy = new GridData
        {
            gridName = gridName,
            gridWidth = gridWidth,
            gridLength = gridLength,
            cellSize = cellSize,
            gridData = new SerializableGridData { wallCells = (bool[])gridData.wallCells.Clone() },
            wallOpenings = new List<WallOpening>(wallOpenings ?? new List<WallOpening>())
        };
        return copy;
    }

    public void Initialize(int width, int length)
    {
        gridWidth = Mathf.Max(1, width);
        gridLength = Mathf.Max(1, length);
        gridData = new SerializableGridData { wallCells = new bool[gridWidth * gridLength] };
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
    }

    /// <summary>True when both describe the same board, so a grid file can be recognised as one already in hand.</summary>
    public bool SameShapeAs(GridData other)
    {
        if (other == null) return false;
        if (gridWidth != other.gridWidth || gridLength != other.gridLength) return false;
        if (!Mathf.Approximately(cellSize, other.cellSize)) return false;

        EnsureArrays();
        other.EnsureArrays();
        for (int z = 0; z < gridLength; z++)
            for (int x = 0; x < gridWidth; x++)
                if (IsWall(x, z) != other.IsWall(x, z)) return false;
        return true;
    }
}
