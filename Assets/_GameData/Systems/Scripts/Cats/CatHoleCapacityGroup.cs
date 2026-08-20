using System;

/// <summary>
/// Shared, mutable capacity counter for every CatHole cell that was authored
/// as part of the same connected hole shape. Cells move and resolve
/// independently (see CatHoleDragHandler -- each cell has its own collider
/// and drag handler), but the SHAPE as a whole can only swallow a fixed
/// number of cats before it's spent, so the cap has to live in shared state
/// rather than on any single CatHole instance.
/// </summary>
public sealed class CatHoleCapacityGroup
{
    public int Capacity { get; }
    public int Collected { get; private set; }
    public bool IsFull => Collected >= Capacity;

    public CatHoleCapacityGroup(int capacity)
    {
        Capacity = Math.Max(1, capacity);
    }

    public void CollectOne()
    {
        if (Collected < Capacity) Collected++;
    }
}
