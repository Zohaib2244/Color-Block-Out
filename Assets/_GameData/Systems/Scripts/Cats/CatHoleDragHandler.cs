using UnityEngine;
using DG.Tweening;

/// <summary>
/// Touch-facing drag behaviour for a single CatHole. Add a collider and this
/// component to each hole prefab; CatHoleInputManager routes touches to it.
/// </summary>
[RequireComponent(typeof(CatHole))]
public sealed class CatHoleDragHandler : MonoBehaviour
{
    [SerializeField] private CatPuzzleController controller;
    [SerializeField] private float moveDuration = 0.12f;

    private CatHole hole;
    private Vector2Int originalCell;
    private Vector2Int previewCell;
    private bool dragging;

    private void Awake()
    {
        hole = GetComponent<CatHole>();
        if (controller == null) controller = GetComponentInParent<CatPuzzleController>();
    }

    public void OnTouchBegin()
    {
        if (controller == null || hole == null || !hole.IsActive) return;
        originalCell = hole.GridPosition;
        previewCell = originalCell;
        dragging = true;
    }

    public void OnTouchMove(Touch touch)
    {
        if (!dragging || controller == null || Camera.main == null) return;
        Vector3 world = GetWorldPosition(touch.position);
        Vector2Int candidate = controller.WorldToGrid(world);
        if (candidate == previewCell || !controller.IsInside(candidate)) return;
        if (controller.TryPreviewHoleDestination(hole, candidate))
        {
            previewCell = candidate;
            transform.DOMove(controller.GridToWorld(candidate, transform.position.y), moveDuration).SetEase(Ease.OutQuad);
        }
    }

    public void OnTouchEnd()
    {
        if (!dragging) return;
        dragging = false;
        if (previewCell == originalCell) return;
        if (!controller.TryMoveHole(hole, previewCell))
        {
            transform.DOMove(controller.GridToWorld(originalCell, transform.position.y), moveDuration).SetEase(Ease.OutQuad);
        }
    }

    private Vector3 GetWorldPosition(Vector2 screenPosition)
    {
        Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : transform.position;
    }
}
