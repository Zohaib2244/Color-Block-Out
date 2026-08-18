using UnityEngine;

/// <summary>One-cell destination for cats of a specific color.</summary>
public sealed class CatHole : MonoBehaviour
{
    [SerializeField] private BlockColorTypes color;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private bool active = true;

    public BlockColorTypes Color => color;
    public Vector2Int GridPosition => gridPosition;
    public bool IsActive => active;

    public void Configure(BlockColorTypes newColor, Vector2Int position)
    {
        color = newColor;
        gridPosition = position;
        active = true;
    }

    public void SetGridPosition(Vector2Int position) => gridPosition = position;

    public void Complete()
    {
        if (!active) return;
        active = false;
        gameObject.SetActive(false);
    }
}
