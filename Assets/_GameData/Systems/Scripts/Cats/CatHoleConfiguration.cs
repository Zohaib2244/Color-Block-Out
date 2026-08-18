using System;
using UnityEngine;

[Serializable]
public sealed class CatHolePrefabData
{
    public CatHoleType holeType;
    public GameObject prefab;
    public Direction[] defaultOpenings;
}

[CreateAssetMenu(fileName = "CatHoleConfiguration", menuName = "Cat Puzzle/Hole Configuration")]
public sealed class CatHoleConfiguration : ScriptableObject
{
    public CatHolePrefabData[] holePrefabs = new CatHolePrefabData[6];

    public GameObject GetPrefab(CatHoleType type)
    {
        foreach (CatHolePrefabData entry in holePrefabs)
            if (entry != null && entry.holeType == type) return entry.prefab;
        return null;
    }

    public CatHolePrefabData GetData(CatHoleType type)
    {
        foreach (CatHolePrefabData entry in holePrefabs)
            if (entry != null && entry.holeType == type) return entry;
        return null;
    }
}
