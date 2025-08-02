using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

[Serializable]
public class BlockColor
{
    public BlockColorTypes colorType;
    public Material colorMaterial;
}
[Serializable]
public class gateColor
{
    public BlockColorTypes colorType;
    public Material colorMaterial;
}
[Serializable]
public class Gate
{
    public BlockColorTypes colorType; // Changed from Color to BlockColorTypes
    public List<Vector2Int> positions = new List<Vector2Int>();
    public Material originalMeshMaterial; // Optional: Store original mesh material if needed
    public Gate(BlockColorTypes colorType, List<Vector2Int> positions, Material originalMeshMaterial)
    {
        this.colorType = colorType;
        this.positions = positions;
        this.originalMeshMaterial = originalMeshMaterial;
    }
}

[Serializable]
public class UISCreens
{
    public ScreenType screenType;
    public Transform screenTransform;
    public bool showOverlay = false;

}
[Serializable]
public class HolePrefabData
{
    public HoleType holeType;
    public GameObject prefab;
    public List<Direction> defaultOpenings = new List<Direction>();
}