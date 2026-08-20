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
    public Vector2Int gridPosition;
}

[Serializable]
public sealed class CatHolePlacement
{
    public BlockColorTypes color;
    public Vector2Int gridPosition;
    public CatHoleType holeType;
    [Range(0, 3)] public int rotationQuarterTurns;

    /// <summary>
    /// Identifies which connected hole shape this cell belongs to -- every
    /// cell drawn in the same gesture (see CatLevelEditorWindow) shares one
    /// id and one <see cref="capacity"/>, since a hole's cat cap applies to
    /// the whole shape, not to each cell individually. Blank means "its own
    /// one-cell shape" (also how older level assets without this field read).
    /// </summary>
    public string holeGroupId;

    /// <summary>How many cats this hole's whole connected shape can swallow before it's spent. Minimum 1.</summary>
    [Min(1)] public int capacity = 1;
}

[CreateAssetMenu(fileName = "New Cat Level", menuName = "Cat Puzzle/Level")]
public sealed class CatLevelData : ScriptableObject
{
    public string levelName;
    public GridData grid;
    public GameObject catPrefab;
    public CatHoleConfiguration holeConfiguration;
    public List<CatPlacement> cats = new List<CatPlacement>();
    public List<CatHolePlacement> holes = new List<CatHolePlacement>();
}
