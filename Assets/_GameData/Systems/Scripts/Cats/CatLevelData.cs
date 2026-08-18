using System;
using System.Collections.Generic;
using UnityEngine;

public enum CatHoleType
{
    Isolated,
    EndCap,
    Straight,
    Corner,
    OneSide,
    Middle
}

[Serializable]
public sealed class CatPlacement
{
    public BlockColorTypes color;
    public Vector2Int cell;
}

[Serializable]
public sealed class CatHolePlacement
{
    public BlockColorTypes color;

    /// <summary>Grid cell the hole's anchor sits on.</summary>
    public Vector2Int origin;

    /// <summary>Cells the hole covers, relative to <see cref="origin"/>. Always contains (0,0).</summary>
    public List<Vector2Int> offsets = new List<Vector2Int> { Vector2Int.zero };

    public IEnumerable<Vector2Int> Cells()
    {
        for (int i = 0; i < offsets.Count; i++) yield return origin + offsets[i];
    }
}

/// <summary>
/// One playable level. The board shape comes from a shared <see cref="GridData"/>
/// asset, so many levels can be authored on top of the same grid.
/// </summary>
[CreateAssetMenu(fileName = "New Cat Level", menuName = "Cat Puzzle/Level")]
public sealed class CatLevelData : ScriptableObject
{
    public string levelName;
    public GridData grid;

    [Header("Presentation")]
    public int levelTime = 60;
    public Vector3 cameraPosition = new Vector3(0f, 10f, -6f);
    public float cameraFOV = 60f;

    [Header("Content")]
    public List<CatPlacement> cats = new List<CatPlacement>();
    public List<CatHolePlacement> holes = new List<CatHolePlacement>();

    public string DisplayName => string.IsNullOrEmpty(levelName) ? name : levelName;
}
