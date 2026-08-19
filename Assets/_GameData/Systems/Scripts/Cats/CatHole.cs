using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// A movable hole made of one or more connected cells. The transform sits on
/// <see cref="OriginCell"/> and every piece mesh is a child placed at one of
/// <see cref="Offsets"/>. Cats of the matching colour that end up under any of
/// the covered cells are collected.
/// </summary>
public sealed class CatHole : MonoBehaviour
{
    [SerializeField] private BlockColorTypes color;
    [SerializeField] private Vector2Int originCell;
    [SerializeField] private List<Vector2Int> offsets = new List<Vector2Int> { Vector2Int.zero };
    [SerializeField] private bool active = true;

    public BlockColorTypes Color => color;
    public Vector2Int OriginCell => originCell;
    public IReadOnlyList<Vector2Int> Offsets => offsets;
    public bool IsActive => active;
    public int CellCount => offsets.Count;

    public void Configure(BlockColorTypes newColor, Vector2Int origin, IEnumerable<Vector2Int> shapeOffsets)
    {
        color = newColor;
        originCell = origin;
        offsets = new List<Vector2Int>(shapeOffsets);
        if (offsets.Count == 0) offsets.Add(Vector2Int.zero);
        active = true;
    }

    public void SetOriginCell(Vector2Int cell) => originCell = cell;

    /// <summary>Cells this hole covers if its origin were moved to <paramref name="origin"/>.</summary>
    public IEnumerable<Vector2Int> CellsAt(Vector2Int origin)
    {
        for (int i = 0; i < offsets.Count; i++) yield return origin + offsets[i];
    }

    /// <summary>Cells this hole currently covers.</summary>
    public IEnumerable<Vector2Int> OccupiedCells() => CellsAt(originCell);

    public bool Covers(Vector2Int cell)
    {
        for (int i = 0; i < offsets.Count; i++) if (originCell + offsets[i] == cell) return true;
        return false;
    }

    /// <summary>
    /// Retires the hole once its colour is cleared. The meshes slide away to the configured
    /// offset before the object hides; the hole stops counting as active immediately, so it
    /// blocks nothing while the animation plays.
    /// </summary>
    public void Complete(CatPuzzleConfig config = null)
    {
        if (!active) return;
        active = false;

        Transform visual = transform.Find(CatHoleHighlight.VisualName);
        if (visual == null || config == null || !Application.isPlaying)
        {
            gameObject.SetActive(false);
            return;
        }

        visual.DOKill();
        Sequence exit = DOTween.Sequence();
        exit.Append(visual.DOLocalMove(config.holeExitOffset, config.holeExitDuration).SetEase(config.holeExitEase));
        // Undo any highlight swell on the way out.
        exit.Join(visual.DOScale(Vector3.one, config.holeExitDuration));
        exit.OnComplete(() =>
        {
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            gameObject.SetActive(false);
        });
    }
}
