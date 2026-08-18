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
