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
public class UISCreens
{
    public ScreenType screenType;
    public Transform screenTransform;
    public bool showOverlay = false;

}
