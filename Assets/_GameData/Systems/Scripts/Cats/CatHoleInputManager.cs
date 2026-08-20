using UnityEngine;

/// <summary>Routes the active touch (or mouse, for editor testing) to a CatHoleDragHandler.</summary>
public sealed class CatHoleInputManager : MonoBehaviour
{
    private CatHoleDragHandler activeHandler;
    private int activeTouchId = -1;

    private void Update()
    {
        if (!GameConstants.inputEnabled) { EndDrag(); return; }
        if (Input.touchSupported && Input.touchCount > 0) UpdateTouch();
        else UpdateMouse();
    }

    private void UpdateTouch()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (activeHandler != null && touch.fingerId == activeTouchId)
            {
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) activeHandler.OnTouchMove(touch.position);
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) EndDrag();
                continue;
            }

            if (activeHandler == null && touch.phase == TouchPhase.Began && BeginDrag(touch.position)) activeTouchId = touch.fingerId;
        }
    }

    private void UpdateMouse()
    {
        if (Input.GetMouseButtonDown(0)) BeginDrag(Input.mousePosition);
        else if (activeHandler != null && Input.GetMouseButton(0)) activeHandler.OnTouchMove(Input.mousePosition);
        else if (activeHandler != null && Input.GetMouseButtonUp(0)) EndDrag();
    }

    private bool BeginDrag(Vector2 screenPosition)
    {
        Camera camera = Camera.main;
        if (camera == null) return false;

        // Cats sit on top of the holes, so take the nearest hit that is actually a hole.
        RaycastHit[] hits = Physics.RaycastAll(camera.ScreenPointToRay(screenPosition));
        CatHoleDragHandler handler = null;
        float nearest = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            CatHoleDragHandler candidate = hit.collider.GetComponentInParent<CatHoleDragHandler>();
            if (candidate == null || hit.distance >= nearest) continue;
            handler = candidate;
            nearest = hit.distance;
        }
        if (handler == null) return false;

        activeHandler = handler;
        activeHandler.OnTouchBegin(screenPosition);
        return true;
    }

    private void EndDrag()
    {
        if (activeHandler == null) return;
        activeHandler.OnTouchEnd();
        activeHandler = null;
        activeTouchId = -1;
    }
}
