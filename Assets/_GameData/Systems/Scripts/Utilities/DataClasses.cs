using System;
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
