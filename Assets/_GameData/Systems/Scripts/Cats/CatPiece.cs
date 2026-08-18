using UnityEngine;

/// <summary>Logical representation of a single 1x1 cat.</summary>
public sealed class CatPiece : MonoBehaviour
{
    [SerializeField] private BlockColorTypes color;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private bool collected;

    public BlockColorTypes Color => color;
    public Vector2Int GridPosition => gridPosition;
    public bool IsCollected => collected;

    public void Configure(BlockColorTypes newColor, Vector2Int position)
    {
        color = newColor;
        gridPosition = position;
        collected = false;
    }

    public void SetGridPosition(Vector2Int position) => gridPosition = position;

    public void SetColor(BlockColorTypes newColor)
    {
        color = newColor;
        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer != null) renderer.sharedMaterial = GameConstants.GetBlockColorMaterial(color);
    }

    public void Collect()
    {
        if (collected) return;
        collected = true;
        gameObject.SetActive(false);
    }
}
