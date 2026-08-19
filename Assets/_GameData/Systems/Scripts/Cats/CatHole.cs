using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// A movable hole made of one or more connected cells. The transform sits on
/// <see cref="OriginCell"/> and every piece mesh is a child placed at one of
/// <see cref="Offsets"/>. Cats of the matching colour that end up under any of
/// the covered cells are collected.
/// </summary>
public sealed class CatHole : MonoBehaviour
{
    [FormerlySerializedAs("color")]
    [SerializeField] private int colorId;
    [SerializeField] private Vector2Int originCell;
    [SerializeField] private List<Vector2Int> offsets = new List<Vector2Int> { Vector2Int.zero };
    [SerializeField] private bool active = true;

    [Header("Exit")]
    [Tooltip("Where the meshes slide to before the hole hides, relative to their resting position.")]
    [SerializeField] private Vector3 exitOffset = new Vector3(0f, -0.6f, 0f);
    [SerializeField] private float exitDuration = 0.45f;
    [SerializeField] private Ease exitEase = Ease.InOutBounce;

    private readonly List<Renderer> rendererBuffer = new List<Renderer>();

    public int ColorId => colorId;
    public Vector2Int OriginCell => originCell;
    public IReadOnlyList<Vector2Int> Offsets => offsets;
    public bool IsActive => active;
    public int CellCount => offsets.Count;

    public void Configure(int newColorId, Vector2Int origin, IEnumerable<Vector2Int> shapeOffsets)
    {
        colorId = newColorId;
        originCell = origin;
        offsets = new List<Vector2Int>(shapeOffsets);
        if (offsets.Count == 0) offsets.Add(Vector2Int.zero);
        active = true;
    }

    /// <summary>
    /// Re-tints every piece. Needed after a scene reload as well as on build, because the tint
    /// lives in a material property block and those are not saved with the scene.
    /// </summary>
    public void ApplyColor(CatColorPalette palette)
    {
        if (palette == null) return;
        rendererBuffer.Clear();
        GetComponentsInChildren(true, rendererBuffer);
        palette.PaintHole(rendererBuffer, colorId);
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
    public void Complete()
    {
        if (!active) return;
        active = false;

        Transform visual = transform.Find(CatHoleHighlight.VisualName);
        if (visual == null || !Application.isPlaying)
        {
            gameObject.SetActive(false);
            return;
        }

        visual.DOKill();
        Sequence exit = DOTween.Sequence();
        exit.Append(visual.DOLocalMove(exitOffset, exitDuration).SetEase(exitEase));
        // Undo any highlight swell on the way out.
        exit.Join(visual.DOScale(Vector3.one, exitDuration));
        exit.OnComplete(() =>
        {
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            gameObject.SetActive(false);
        });
    }
}
