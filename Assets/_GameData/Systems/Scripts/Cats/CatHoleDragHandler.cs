using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Touch-facing drag behaviour for a hole. The logical position still moves a whole cell at a
/// time, but the transform is never teleported there: it eases toward the cell it belongs on
/// every frame, so the shape trails the finger instead of snapping between cells.
/// </summary>
[RequireComponent(typeof(CatHole))]
public sealed class CatHoleDragHandler : MonoBehaviour
{
    [Header("Follow")]
    [Tooltip("Roughly how long the shape takes to catch up to its cell. Higher is looser.")]
    [SerializeField] private float followSmoothTime = 0.07f;

    [Tooltip("Upper bound on follow speed, in cells per second, so a long slide cannot overshoot wildly.")]
    [SerializeField] private float maxFollowSpeed = 60f;

    private readonly List<Vector2Int> path = new List<Vector2Int>();
    private CatHole hole;
    private CatHoleHighlight highlight;
    private Vector2Int originalCell;
    private Vector2Int previewCell;
    private Vector3 grabOffset;
    private Vector3 targetLocalPosition;
    private Vector3 followVelocity;
    private bool dragging;
    private bool following;

    private CatPuzzleController Controller => CatPuzzleController.Instance;

    private void Awake()
    {
        hole = GetComponent<CatHole>();
        highlight = GetComponent<CatHoleHighlight>();
    }

    private void Update()
    {
        if (!following) return;

        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, targetLocalPosition,
            ref followVelocity, followSmoothTime, maxFollowSpeed);

        // Settle exactly on the cell once the easing has effectively arrived.
        if ((transform.localPosition - targetLocalPosition).sqrMagnitude > 0.0000001f) return;
        transform.localPosition = targetLocalPosition;
        followVelocity = Vector3.zero;
        if (!dragging) following = false;
    }

    public void OnTouchBegin(Vector2 screenPosition)
    {
        if (Controller == null || hole == null || !hole.IsActive) return;
        originalCell = hole.OriginCell;
        previewCell = originalCell;
        grabOffset = transform.position - GetWorldPosition(screenPosition);
        dragging = true;
        FollowCell(previewCell);
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
        FollowCell(previewCell);

        // Every cell passed through counts, so a fast drag cannot skip over a cat.
        foreach (Vector2Int step in path) Controller.CollectCatsUnder(hole, step);
    }

    public void OnTouchEnd()
    {
        if (!dragging) return;
        dragging = false;

        // The hole may have been completed mid-drag. Leave its exit animation alone.
        if (hole == null || !hole.IsActive) { following = false; return; }

        if (highlight != null) highlight.SetHighlighted(false);

        // Commit the logical move without snapping the transform, so the easing finishes the trip.
        if (previewCell != originalCell && Controller != null && Controller.TryMoveHole(hole, previewCell, false))
        {
            FollowCell(previewCell);
            return;
        }
        FollowCell(originalCell);
    }

    private void FollowCell(Vector2Int cell)
    {
        if (Controller == null) return;
        targetLocalPosition = Controller.CellToLocal(cell);
        following = true;
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
