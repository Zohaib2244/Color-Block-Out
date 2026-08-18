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

    private CatHole hole;
    private Vector2Int originalCell;
    private Vector2Int previewCell;
    private Vector3 grabOffset;
    private bool dragging;

    private CatPuzzleController Controller => CatPuzzleController.Instance;

    private void Awake() => hole = GetComponent<CatHole>();

    public void OnTouchBegin(Vector2 screenPosition)
    {
        if (Controller == null || hole == null || !hole.IsActive) return;
        originalCell = hole.OriginCell;
        previewCell = originalCell;
        grabOffset = transform.position - GetWorldPosition(screenPosition);
        dragging = true;
    }

    public void OnTouchMove(Vector2 screenPosition)
    {
        if (!dragging || Controller == null) return;
        Vector2Int candidate = Controller.WorldToGrid(GetWorldPosition(screenPosition) + grabOffset);
        if (candidate == previewCell) return;
        if (!Controller.CanPlaceHole(hole, candidate)) return;

        previewCell = candidate;
        transform.DOKill();
        transform.DOMove(Controller.GridToWorld(candidate, transform.position.y), moveDuration).SetEase(Ease.OutQuad);
    }

    public void OnTouchEnd()
    {
        if (!dragging) return;
        dragging = false;
        transform.DOKill();

        if (previewCell != originalCell && Controller != null && Controller.TryMoveHole(hole, previewCell)) return;
        transform.DOMove(Controller != null ? Controller.GridToWorld(originalCell, transform.position.y) : transform.position, moveDuration).SetEase(Ease.OutQuad);
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
