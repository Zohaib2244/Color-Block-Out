using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "HoleConfiguration", menuName = "3D Block Puzzle/Hole Configuration")]
public class HoleConfiguration : ScriptableObject
{
    public List<HolePrefabData> holePrefabs;

    // Helper method to find the full data package for a specific hole type
    public HolePrefabData GetDataForType(HoleType type)
    {
        return holePrefabs.FirstOrDefault(p => p.holeType == type);
    }
}