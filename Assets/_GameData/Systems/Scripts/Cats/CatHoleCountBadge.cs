using UnityEngine;

/// <summary>
/// Companion to a CatHole: shows how many more cats it can still swallow, tinted to its colour.
/// Parented under the hole's own root at build time (see CatHoleBuilder.BuildCountBadge), so a drag
/// carries it along for free - only the count needs pushing, which happens on build and after every
/// collect (see CatPuzzleController.CollectCatsUnder).
/// </summary>
[RequireComponent(typeof(CatHole))]
public sealed class CatHoleCountBadge : MonoBehaviour
{
    [SerializeField] private CatCountBadge badge;
    private CatHole hole;

    public void Initialize(CatCountBadge badgeInstance, CatColorPalette palette)
    {
        badge = badgeInstance;
        hole = GetComponent<CatHole>();
        Refresh(palette);
    }

    /// <summary>
    /// A hole's own capacity is one cat per cell (see CatHole.CellCount), so what's left is that
    /// minus what it has already collected. Hides itself once nothing is left to show.
    /// </summary>
    public void Refresh(CatColorPalette palette)
    {
        if (badge == null) return;
        if (hole == null) hole = GetComponent<CatHole>();
        if (hole == null) return;

        int remaining = hole.CellCount - hole.CollectedCount;
        if (remaining <= 0)
        {
            badge.SetVisible(false);
            return;
        }

        badge.SetVisible(true);
        badge.SetCount(remaining);
        if (palette != null) badge.SetColor(palette.GetColor(hole.ColorId));
    }

    public void Hide()
    {
        if (badge != null) badge.SetVisible(false);
    }
}
