using UnityEngine;

/// <summary>
/// Spawns the shared count-badge prefab from <see cref="CatPuzzleConfig.countBadgePrefab"/>.
/// This only instantiates it - the caller (a pile refresh or a hole build) positions it and pushes
/// its first count and colour.
/// </summary>
public static class CatCountBadgeBuilder
{
    public static CatCountBadge Spawn(Transform parent, CatPuzzleConfig config)
    {
        if (config == null || config.countBadgePrefab == null || parent == null) return null;

        GameObject instance = GridBuilder.InstantiatePrefab(config.countBadgePrefab, parent);
        instance.name = "CountBadge";
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        CatCountBadge badge = instance.GetComponent<CatCountBadge>();
        if (badge == null) badge = instance.AddComponent<CatCountBadge>();
        return badge;
    }
}
