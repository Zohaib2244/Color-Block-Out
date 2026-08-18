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
    public GameObject obj;
    public List<Vector2Int> positions = new List<Vector2Int>();
    public Gate(BlockColorTypes colorType, List<Vector2Int> positions, GameObject obj)
    {
        this.colorType = colorType;
        this.positions = positions;
        this.obj = obj;
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