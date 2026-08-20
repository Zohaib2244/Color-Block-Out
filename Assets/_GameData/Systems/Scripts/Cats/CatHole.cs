using UnityEngine;

/// <summary>One-cell destination for cats of a specific color.</summary>
public sealed class CatHole : MonoBehaviour
{
    [SerializeField] private BlockColorTypes color;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private bool active = true;

    private CatHoleCapacityGroup capacityGroup;

    public BlockColorTypes Color => color;
    public Vector2Int GridPosition => gridPosition;
    public bool IsActive => active;

    /// <summary>True once this hole's whole connected shape has swallowed as many cats as it can hold.</summary>
    public bool IsFull => capacityGroup != null && capacityGroup.IsFull;

    /// <summary>Shared capacity pool with the other CatHole cells from this hole's original connected shape.</summary>
    public CatHoleCapacityGroup CapacityGroup => capacityGroup;

    public void Configure(BlockColorTypes newColor, Vector2Int position, CatHoleCapacityGroup group = null)
    {
        color = newColor;
        gridPosition = position;
        capacityGroup = group ?? new CatHoleCapacityGroup(1);
        active = true;
    }

    public void SetGridPosition(Vector2Int position) => gridPosition = position;

    /// <summary>Registers one cat as collected against this hole's shared capacity pool.</summary>
    public void CollectCat() => capacityGroup?.CollectOne();

    public void Complete()
    {
        if (!active) return;
        active = false;
        gameObject.SetActive(false);
    }
}
