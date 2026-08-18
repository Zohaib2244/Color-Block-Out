using UnityEngine;

/// <summary>Routes the active touch to a CatHoleDragHandler without touching block input.</summary>
public sealed class CatHoleInputManager : MonoBehaviour
{
    private CatHoleDragHandler activeHandler;
    private int activeTouchId = -1;

    private void Update()
    {
        if (!GameConstants.inputEnabled) return;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (activeHandler != null && touch.fingerId == activeTouchId)
            {
                if (touch.phase == TouchPhase.Moved) activeHandler.OnTouchMove(touch);
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) EndDrag();
                continue;
            }

            if (activeHandler == null && touch.phase == TouchPhase.Began && Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.TryGetComponent(out CatHoleDragHandler handler))
                {
                    activeHandler = handler;
                    activeTouchId = touch.fingerId;
                    activeHandler.OnTouchBegin();
                }
            }
        }

        if (activeHandler != null && Input.touchCount == 0) EndDrag();
    }

    private void EndDrag()
    {
        activeHandler.OnTouchEnd();
        activeHandler = null;
        activeTouchId = -1;
    }
}
