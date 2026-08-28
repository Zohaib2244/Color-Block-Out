using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    [FormerlySerializedAs("color")]
    public int colorId;
    public Vector2Int cell;
}

[Serializable]
public sealed class CatHolePlacement
{
    [FormerlySerializedAs("color")]
    public int colorId;

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
/// One playable level: a board plus the cats and holes standing on it.
///
/// This is plain data, not an asset. Levels are authored in the web level editor and shipped as
/// JSON files; <see cref="CatLevelJson"/> parses one into this, and <see cref="LevelData"/> holds
/// the play order as a list of those files. The board travels inside the level rather than being
/// referenced, so a level is a single self-contained file.
/// </summary>
[Serializable]
public sealed class CatLevelData
{
    public string levelName;

    /// <summary>The board this level is played on. Owned by the level - see <see cref="GridData.Clone"/>.</summary>
    public GridData grid;

    [Header("Presentation")]
    public int levelTime = 60;

    [Header("Content")]
    public List<CatPlacement> cats = new List<CatPlacement>();
    public List<CatHolePlacement> holes = new List<CatHolePlacement>();

    public string DisplayName => string.IsNullOrEmpty(levelName) ? "Untitled Level" : levelName;

    /// <summary>A deep copy, so editing a loaded level never writes back into the one it came from.</summary>
    public CatLevelData Clone()
    {
        CatLevelData copy = new CatLevelData
        {
            levelName = levelName,
            grid = grid != null ? grid.Clone() : null,
            levelTime = levelTime,
            cats = new List<CatPlacement>(),
            holes = new List<CatHolePlacement>()
        };

        if (cats != null)
            foreach (CatPlacement cat in cats)
                if (cat != null) copy.cats.Add(new CatPlacement { colorId = cat.colorId, cell = cat.cell });

        if (holes != null)
            foreach (CatHolePlacement hole in holes)
                if (hole != null) copy.holes.Add(new CatHolePlacement
                {
                    colorId = hole.colorId,
                    origin = hole.origin,
                    offsets = new List<Vector2Int>(hole.offsets)
                });

        return copy;
    }
}
