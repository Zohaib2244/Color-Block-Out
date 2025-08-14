using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsBlockMover : MonoBehaviour
{
    public BlockState blockState = BlockState.Idle;

    [Header("Physics Movement")]
    [SerializeField] private bool enablePhysicsMovement = true;
    [SerializeField] private float moveForce = 4f;
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float mouseSensitivity = 1f;
    [SerializeField] private float physicsTiltMultiplier = 1.5f;
    [SerializeField] private LayerMask blockLayer = -1; // What layers to consider as blocks

    [Header("Tilt Settings")]
    [SerializeField] private float maxTiltAngle = 45f;
    [SerializeField] private float tiltSmoothness = 10f;
    [SerializeField] private float bumpForce = 2f;
    [SerializeField] private float bumpTiltAmount = 20f; // degrees
    [SerializeField] private float bumpTiltDuration = 0.25f; // seconds

    [Header("Visual Feedback")]
    [SerializeField] private Outline outline;

    private Rigidbody rb;
    private bool isPhysicsControlled = false;
    private Camera playerCamera;
    private Vector3 lastMousePosition;
    private Vector3 mouseVelocity;
    private bool isMouseControlling = false;
    private Quaternion originalRotation;
    private Tween bumpTween;

    // Static reference to currently controlled block
    private static PhysicsBlockMover currentlyControlled;

    void Start()
    {
        SetupPhysicsMovement();
        originalRotation = transform.rotation;
    }

    void Update()
    {
        if (enablePhysicsMovement)
        {
            HandlePhysicsInput();
        }

        if (isPhysicsControlled)
        {
            ApplyPhysicsTilt();
        }
        else
        {
            // Return to original rotation when not controlled
            transform.rotation = Quaternion.Slerp(transform.rotation, originalRotation, Time.deltaTime * tiltSmoothness);
        }
    }

    void FixedUpdate()
    {
        if (enablePhysicsMovement && isPhysicsControlled && rb != null)
        {
            ApplyMovementForces();
        }
    }

    #region Physics Movement Setup
    private void SetupPhysicsMovement()
    {
        // Get or add Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Configure Rigidbody for physics movement
        if (enablePhysicsMovement)
        {
            rb.useGravity = false; // We control hover manually
            rb.linearDamping = 2f; // Add some air resistance
            rb.angularDamping = 5f; // Prevent excessive spinning
            rb.interpolation = RigidbodyInterpolation.Interpolate; // Smooth movement
            rb.isKinematic = true;
        }
        else
        {
            rb.isKinematic = true; // Disable physics when not using physics movement
        }

        // Get camera reference
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Ensure collider exists
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }

        // Setup outline if not assigned
        if (outline == null)
        {
            outline = GetComponent<Outline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
            }
            outline.enabled = false;
        }
    }

    public void EnablePhysicsMovement(bool enable)
    {
        enablePhysicsMovement = enable;

        if (rb != null)
        {
            rb.isKinematic = !enable;
            rb.useGravity = false; // Always false, we control hover
        }

        if (enable)
        {
            isPhysicsControlled = false; // Start uncontrolled
        }
    }
    #endregion

    #region Physics Input Handling
    private void HandlePhysicsInput()
    {
        // Start controlling this block if clicked
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == this.gameObject)
                {
                    StartPhysicsControl();
                }
            }
        }

        // Handle mouse movement for currently controlled block
        if (isPhysicsControlled && isMouseControlling)
        {
            Vector3 currentMousePosition = Input.mousePosition;
            Vector3 mouseDelta = currentMousePosition - lastMousePosition;

            // Convert mouse delta to world movement
            mouseVelocity = ScreenToWorldMovement(mouseDelta);

            lastMousePosition = currentMousePosition;

            // Stop controlling if mouse button released
            if (Input.GetMouseButtonUp(0))
            {
                StopPhysicsControl();
            }
        }
    }
    private Vector3 ScreenToWorldMovement(Vector3 mouseDelta)
    {
        if (playerCamera == null) return Vector3.zero;
        if (mouseDelta.sqrMagnitude < 0.01f) return Vector3.zero;

        // Project mouse position to a plane at the block's current height
        Plane plane = new Plane(Vector3.up, transform.position);
        Ray rayCurrent = playerCamera.ScreenPointToRay(Input.mousePosition);
        Ray rayLast = playerCamera.ScreenPointToRay(Input.mousePosition - mouseDelta);

        if (plane.Raycast(rayLast, out float enterLast) && plane.Raycast(rayCurrent, out float enterCurrent))
        {
            Vector3 worldLast = rayLast.GetPoint(enterLast);
            Vector3 worldCurrent = rayCurrent.GetPoint(enterCurrent);
            Vector3 worldDelta = worldCurrent - worldLast;
            worldDelta.y = 0;
            return worldDelta * mouseSensitivity / Time.deltaTime;
        }
        return Vector3.zero;
    }
    private void StartPhysicsControl()
    {
        blockState = BlockState.Moving;
        // Stop any other blocks from being controlled
        if (currentlyControlled != null && currentlyControlled != this)
        {
            currentlyControlled.StopPhysicsControl();
        }
        rb.isKinematic = false; // Enable physics for this block
        currentlyControlled = this;
        isPhysicsControlled = true;
        isMouseControlling = true;
        lastMousePosition = Input.mousePosition;
        mouseVelocity = Vector3.zero;

        // Visual feedback
        if (outline != null)
        {
            outline.enabled = true;
        }

        Debug.Log($"Started controlling: {gameObject.name}");
    }

    private void StopPhysicsControl()
    {
        blockState = BlockState.Idle;
        rb.isKinematic = true; // Disable physics for this block
        if (currentlyControlled == this)
        {
            currentlyControlled = null;
        }

        isPhysicsControlled = false;
        StopMouseControl();
    }

    private void StopMouseControl()
    {
        isMouseControlling = false;
        mouseVelocity = Vector3.zero;

        // Remove visual feedback
        if (outline != null)
        {
            outline.enabled = false;
        }

        Debug.Log($"Stopped controlling: {gameObject.name}");
    }
    #endregion

    #region Physics Movement
    private void ApplyMovementForces()
    {
        if (mouseVelocity.magnitude > 0.01f)
        {
            // Apply movement force
            Vector3 force = mouseVelocity * moveForce;
            rb.AddForce(force, ForceMode.Force);

            // Limit maximum speed
            if (rb.linearVelocity.magnitude > maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }

            // Gradually reduce mouse velocity for smooth deceleration
            mouseVelocity = Vector3.Lerp(mouseVelocity, Vector3.zero, Time.fixedDeltaTime * 5f);
        }
    }

    private void ApplyPhysicsTilt()
    {
        if (rb != null && rb.linearVelocity.magnitude > 0.1f)
        {
            // Calculate tilt based on velocity
            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0; // Ignore vertical velocity

            // Calculate tilt angles
            float tiltZ = -velocity.x * physicsTiltMultiplier;
            float tiltX = velocity.z * physicsTiltMultiplier;

            // Clamp tilt angles
            tiltZ = Mathf.Clamp(tiltZ, -maxTiltAngle, maxTiltAngle);
            tiltX = Mathf.Clamp(tiltX, -maxTiltAngle, maxTiltAngle);

            // Always set Y rotation to zero (or your desired fixed value)
            float fixedYRotation = 0f;

            // Create target rotation with physics tilt, but no Y rotation
            Quaternion physicsTiltRotation = Quaternion.Euler(tiltX, fixedYRotation, tiltZ);

            // Apply tilt smoothly
            transform.rotation = Quaternion.Slerp(transform.rotation, physicsTiltRotation, Time.deltaTime * tiltSmoothness);
        }
    }
    #endregion

    #region Collision Handling
    private void OnCollisionEnter(Collision collision)
    {
        if (!enablePhysicsMovement) return;

        PhysicsBlockMover otherMover = collision.gameObject.GetComponent<PhysicsBlockMover>();
        if (otherMover != null)
        {
            float collisionForce = collision.relativeVelocity.magnitude;
            // If this block is idle and hit by a moving block, apply bump tilt
            if (collisionForce > bumpForce && blockState == BlockState.Idle && otherMover.blockState == BlockState.Moving)
            {
                // Calculate bump direction (opposite to collision)
                Vector3 bumpDir = -collision.relativeVelocity.normalized;
                bumpDir.y = 0;
                float tiltZ = bumpDir.x * bumpTiltAmount;
                float tiltX = -bumpDir.z * bumpTiltAmount;
                Quaternion bumpRotation = Quaternion.Euler(tiltX, 0f, tiltZ);

                // Kill any existing tween
                if (bumpTween != null && bumpTween.IsActive()) bumpTween.Kill();

                // Instantly tilt to bump, then return to original
                transform.rotation = bumpRotation;
                bumpTween?.Kill();
                bumpTween = transform.DORotateQuaternion(originalRotation, bumpTiltDuration).SetEase(Ease.OutBack);
            }
        }
    }
    #endregion
    void OnDestroy()
    {
        // Clean up static reference if this was the controlled block
        if (currentlyControlled == this)
        {
            currentlyControlled = null;
        }
    }
}