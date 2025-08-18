using UnityEngine;

public class BlockInputManager : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveForce = 4f;
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float moveSensitivity = 1f;
    [SerializeField] private float velocityFallback = 5f;
    [Header("Tilt Settings")]
    [SerializeField] private float physicsTiltMultiplier = 1.5f;
    [SerializeField] private float maxTiltAngle = 45f;
    [SerializeField] private float tiltSmoothness = 10f;
    [SerializeField] private float bumpForce = 2f;
    [SerializeField] private float bumpTiltAmount = 20f; // degrees
    [SerializeField] private float bumpTiltDuration = 0.25f; // seconds

    private bool isDragging = false; // Track drag state
    private int activeTouchId = -1; // Track which touch is active for our drag
    private Vector3 movementForceVector = Vector3.zero;
    private Vector3 lastTouchPosition = Vector3.zero;
    private Block controlledBlock;
    private float cacheYValue = 0f;
    void Update()
    {
        // Handle touch input
        if (Input.touchCount > 0 && GameConstants.inputEnabled)
        {
            // Process all touches to find our active one
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                HandleInput(touch);
            }
        }
    }
    private void FixedUpdate()
    {
        ApplyMovementForces();
        // ApplyPhysicsTilt();
    }
    private void HandleInput(Touch touch)
    {
        if (touch.phase == TouchPhase.Began && activeTouchId == -1)
        {
            Ray ray = Camera.main.ScreenPointToRay(touch.position);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Try to get Block from the hit object or its parent
                Block block = hit.collider.GetComponent<Block>();
                if (block == null)
                    block = hit.collider.GetComponentInParent<Block>();
        
                if (block != null)
                {
                    StartPhysicsControl(block);
                    lastTouchPosition = touch.position;
                    activeTouchId = touch.fingerId; // Track the active touch ID
                }
            }
        }

        if (isDragging && controlledBlock && touch.fingerId == activeTouchId)
        {
            Vector3 currentTouchPosition = touch.position;
            Vector3 moveDelta = currentTouchPosition - lastTouchPosition;

            movementForceVector = ScreenToWorldMovement(moveDelta);
            lastTouchPosition = currentTouchPosition;

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                StopPhysicsControl();
                activeTouchId = -1; // Reset active touch ID
            }
        }
        
    }
    private Vector3 ScreenToWorldMovement(Vector3 moveDelta)
    {
        if (Camera.main == null) return Vector3.zero;
        if (moveDelta.sqrMagnitude < 0.01f) return Vector3.zero;

        Plane plane = new Plane(Vector3.up, transform.position);
        Ray rayCurrent = Camera.main.ScreenPointToRay(lastTouchPosition);
        Ray rayLast = Camera.main.ScreenPointToRay(lastTouchPosition - moveDelta);

        if (plane.Raycast(rayLast, out float enterLast) && plane.Raycast(rayCurrent, out float enterCurrent))
        {
            Vector3 worldLast = rayLast.GetPoint(enterLast);
            Vector3 worldCurrent = rayCurrent.GetPoint(enterCurrent);
            Vector3 worldDelta = worldCurrent - worldLast;
            worldDelta.y = 0;
            return worldDelta * moveSensitivity / Time.deltaTime;
        }
        return Vector3.zero;
    }
    void StartPhysicsControl(Block block)
    {
        isDragging = true;
        controlledBlock = block;
        controlledBlock.BlockSelectedToMove();
        cacheYValue = controlledBlock.transform.rotation.eulerAngles.y; // Store the current Y rotation
    }
    void StopPhysicsControl()
    {
        isDragging = false;
        movementForceVector = Vector3.zero;
        controlledBlock.BlockDeselected();
        controlledBlock = null;
        cacheYValue = 0f; // Reset Y value
    }
    #region Physics Application

    private void ApplyMovementForces()
    {
        if (controlledBlock && controlledBlock.BlockState == BlockState.Moving && controlledBlock.Rigidbody && movementForceVector.magnitude > 0.01f)
        {
            // Apply movement force
            Vector3 force = movementForceVector * moveForce;
            controlledBlock.Rigidbody.AddForce(force, ForceMode.Force);

            // Limit maximum speed
            if (controlledBlock.Rigidbody.linearVelocity.magnitude > maxSpeed)
            {
                controlledBlock.Rigidbody.linearVelocity = controlledBlock.Rigidbody.linearVelocity.normalized * maxSpeed;
            }

            // Gradually reduce mouse velocity for smooth deceleration
            movementForceVector = Vector3.Lerp(movementForceVector, Vector3.zero, Time.fixedDeltaTime * velocityFallback);
        }
    }

    private void ApplyPhysicsTilt()
    {
        if (controlledBlock && controlledBlock.BlockState == BlockState.Moving && controlledBlock.Rigidbody)
        {
            // Get the velocity of the block
            Vector3 velocity = controlledBlock.Rigidbody.linearVelocity;
            velocity.y = 0; // Ignore vertical velocity
    
            if (velocity.magnitude > 0.1f) // If the block is moving
            {
                // Calculate tilt based on velocity
                float tiltZ = -velocity.x * physicsTiltMultiplier;
                float tiltX = velocity.z * physicsTiltMultiplier;
    
                // Clamp tilt angles
                tiltZ = Mathf.Clamp(tiltZ, -maxTiltAngle, maxTiltAngle);
                tiltX = Mathf.Clamp(tiltX, -maxTiltAngle, maxTiltAngle);
    
                // Create target rotation with physics tilt, but no Y rotation
                Quaternion physicsTiltRotation = Quaternion.Euler(tiltX, cacheYValue, tiltZ);

                // Apply tilt smoothly
                controlledBlock.transform.rotation = Quaternion.Slerp(controlledBlock.transform.rotation, physicsTiltRotation, Time.deltaTime * tiltSmoothness);
            }
            else // If the block is not moving
            {
                // Smoothly reset the tilt to the neutral position
                Quaternion neutralRotation = Quaternion.Euler(0f, controlledBlock.transform.rotation.eulerAngles.y, 0f);
                controlledBlock.transform.rotation = Quaternion.Slerp(controlledBlock.transform.rotation, neutralRotation, Time.deltaTime * tiltSmoothness);
            }
        }
    }
    #endregion
}