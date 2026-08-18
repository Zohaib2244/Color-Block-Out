using UnityEngine;

/// <summary>Logical representation of a single 1x1 cat.</summary>
public sealed class CatPiece : MonoBehaviour
{
    [SerializeField] private BlockColorTypes color;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private bool collected;

    [Tooltip("Renderers tinted by the cat's colour. Left empty, the first renderer found is used.")]
    [SerializeField] private Renderer[] coloredRenderers;

    public BlockColorTypes Color => color;
    public Vector2Int GridPosition => gridPosition;
    public bool IsCollected => collected;

    public void Configure(BlockColorTypes newColor, Vector2Int position)
    {
        gridPosition = position;
        collected = false;
        SetColor(newColor);
    }

    public void SetGridPosition(Vector2Int position) => gridPosition = position;

    public void SetColor(BlockColorTypes newColor)
    {
        color = newColor;
        Material material = GameConstants.GetBlockColorMaterial(color);
        if (material == null) return;

        if (coloredRenderers != null && coloredRenderers.Length > 0)
        {
            foreach (Renderer renderer in coloredRenderers)
                if (renderer != null) renderer.sharedMaterial = material;
            return;
        }

        Renderer fallback = GetComponent<Renderer>();
        if (fallback == null) fallback = GetComponentInChildren<Renderer>();
        if (fallback != null) fallback.sharedMaterial = material;
    }

    public void Collect()
    {
        if (collected) return;
        collected = true;
        gameObject.SetActive(false);
    }
}
