using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Touch-facing drag behaviour for a hole. The whole shape moves as one piece and
/// only settles on cells where every covered cell is free.
/// </summary>
[RequireComponent(typeof(CatHole))]
public sealed class CatHoleDragHandler : MonoBehaviour
{
    [SerializeField] private float moveDuration = 0.12f;

    private readonly List<Vector2Int> path = new List<Vector2Int>();
    private CatHole hole;
    private CatHoleHighlight highlight;
    private Vector2Int originalCell;
    private Vector2Int previewCell;
    private Vector3 grabOffset;
    private bool dragging;

    private CatPuzzleController Controller => CatPuzzleController.Instance;

    private void Awake()
    {
        hole = GetComponent<CatHole>();
        highlight = GetComponent<CatHoleHighlight>();
    }

    public void OnTouchBegin(Vector2 screenPosition)
    {
        if (Controller == null || hole == null || !hole.IsActive) return;
        originalCell = hole.OriginCell;
        previewCell = originalCell;
        grabOffset = transform.position - GetWorldPosition(screenPosition);
        dragging = true;
        if (highlight != null) highlight.SetHighlighted(true);
    }

    public void OnTouchMove(Vector2 screenPosition)
    {
        if (!dragging || Controller == null) return;
        Vector2Int target = Controller.WorldToGrid(GetWorldPosition(screenPosition) + grabOffset);
        if (target == previewCell) return;

        // Walk toward the finger rather than teleporting, so obstacles actually stop the shape.
        Vector2Int reachable = Controller.SlideHole(hole, previewCell, target, path);
        if (reachable == previewCell) return;

        previewCell = reachable;
        MoveTo(previewCell);

        // Every cell passed through counts, so a fast drag cannot skip over a cat.
        foreach (Vector2Int step in path) Controller.CollectCatsUnder(hole, step);
    }

    public void OnTouchEnd()
    {
        if (!dragging) return;
        dragging = false;
        transform.DOKill();

        // The hole may have been completed mid-drag. Leave its exit animation alone.
        if (hole == null || !hole.IsActive) return;

        if (highlight != null) highlight.SetHighlighted(false);
        if (previewCell != originalCell && Controller != null && Controller.TryMoveHole(hole, previewCell)) return;
        MoveTo(originalCell);
    }

    private void MoveTo(Vector2Int cell)
    {
        transform.DOKill();
        if (Controller == null) return;
        transform.DOLocalMove(Controller.CellToLocal(cell), moveDuration).SetEase(Ease.OutQuad);
    }

    private Vector3 GetWorldPosition(Vector2 screenPosition)
    {
        Camera camera = Camera.main;
        if (camera == null) return transform.position;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        Ray ray = camera.ScreenPointToRay(screenPosition);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : transform.position;
    }
}
