using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using DG.Tweening;

public class Block : MonoBehaviour
{
    [SerializeField] private BlockShape shape;
    public BlockShape Shape => shape;

    [SerializeField] private BlockColorTypes blockColor;
    public BlockColorTypes BlockColor => blockColor;
    [SerializeField] private float yPos = 0f;
    public float YPos => yPos;
    [SerializeField] private Outline outline;
    public UnityEvent OnBlockPlaced;
    public Vector2Int[] gridPosition;
    public Vector2Int[] GridPosition => gridPosition;
    public BlockState BlockState { get; private set; }
    // Reference to the grid manager
    [SerializeField] public GridManager gridManager;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Material originalMaterial;
    public Rigidbody Rigidbody = null;
    private Tween bumpTween;
    public bool canBump = true;
    void Start()
    {
        OnBlockPlaced.AddListener(() => gridManager.CheckGatePulls());
        Initialize();
        gridManager.onGridInitialized.AddListener(StartAnimation);
    }
    #region Block Setup For Level
    public void Initialize()
    {
        originalMaterial = GameConstants.GetBlockColorMaterial(blockColor);
        originalRotation = transform.rotation;
        // Apply the block's color/material to the mesh renderer
        if (originalMaterial)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer)
            {
                renderer.material = originalMaterial;
            }
        }
        outline.enabled = false;
        Rigidbody.isKinematic = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        return;
        if (BlockState != BlockState.Idle || !canBump) return; // Prevent bumping if cooldown is active
    
        Block otherBlock = collision.gameObject.GetComponent<Block>();
        if (otherBlock)
        {
            float collisionForce = collision.relativeVelocity.magnitude;
    
            // If this block is idle and hit by a moving block, apply bump tilt
            if (collisionForce > BlockProperties.bumpForce && BlockState == BlockState.Idle && otherBlock.BlockState == BlockState.Moving)
            {
                DebugLogger.Log("Block bumped");
    
                // Get the collision point relative to this block's pivot
                ContactPoint contact = collision.GetContact(0); // Get the first contact point
                Vector3 localCollisionPoint = transform.InverseTransformPoint(contact.point); // Convert to local space
        
                // Determine the collision direction based on the local collision point
                if (Mathf.Abs(localCollisionPoint.x) > Mathf.Abs(localCollisionPoint.z) + BlockProperties.bumpDetectDirectionThreshold)
                {
                    if (localCollisionPoint.x > 0)
                    {
                        Debug.Log("Bumping Right");
                        BumpBlock(new Vector3(0, 0, -10.47f)); // Rotate around Z-axis
                    }
                    else
                    {
                        Debug.Log("Bumping Left");
                        BumpBlock(new Vector3(0, 0, 10.47f)); // Rotate around Z-axis
                    }
                }
                else if (Mathf.Abs(localCollisionPoint.z) > Mathf.Abs(localCollisionPoint.x) + BlockProperties.bumpDetectDirectionThreshold)
                {
                    if (localCollisionPoint.z > 0)
                    {
                        Debug.Log("Bumping Up");
                        BumpBlock(new Vector3(-6.47f, 0, 0)); // Rotate around X-axis
                    }
                    else
                    {
                        Debug.Log("Bumping Down");
                        BumpBlock(new Vector3(6.47f, 0, 0)); // Rotate around X-axis
                    }
                }
            }
        }
    }
    #endregion
    #region Block Movement Methods
    // Method to handle the bump rotation
    private void BumpBlock(Vector3 rotationAxis)
    {
        if (!canBump) return;
        if (bumpTween != null && bumpTween.IsActive()) bumpTween.Kill(); // Kill any existing tween

        Quaternion targetRotation = Quaternion.Euler(transform.rotation.eulerAngles + rotationAxis);

        bumpTween = transform.DORotate(targetRotation.eulerAngles, 0.25f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                // Rotate back to the original rotation
                transform.DORotate(new Vector3(0, transform.rotation.eulerAngles.y, 0), 0.25f).SetEase(Ease.InQuad).OnComplete(() =>
                {
                    canBump = true;

                });
            });
    }
    #endregion
    public void BlockSelectedToMove()
    {
        originalPosition = transform.position;
        transform.position = new Vector3(transform.position.x, 0.075f, transform.position.z);

        gridManager.onBlockClicked?.Invoke();

        //* Activate Rigidbody
        Rigidbody.isKinematic = false;

        //* Update Block State
        BlockState = BlockState.Moving;

        //* Play Feel
        AudioManager.Instance.PlayBlockMove();
        outline.enabled = true;
    }
    public void BlockDeselected()
    {
        //* Update Block State
        BlockState = BlockState.Idle;

        //* Deactivate Rigidbody
        Rigidbody.isKinematic = true;

        //* Update the Blocks Physical Transform
        (Vector3 snappedPosition, Vector2Int[] newGridPositions) = ReturnBlocksPlacementPosition();
        transform.position = snappedPosition;
        transform.localPosition = new Vector3(transform.localPosition.x, yPos, transform.localPosition.z); // Ensure Y position is set correctly
        transform.rotation = originalRotation;

        //* Update The Position In Grid Manager
        gridManager.PlaceBlock(this, newGridPositions, gridPosition);

        //* Update the Blocks Grid Position
        gridPosition = newGridPositions;
        //* Play Feel
        outline.enabled = false;
        AudioManager.Instance.PlayBlockPlace();

        OnBlockPlaced?.Invoke(); // Trigger event for block placement
    }
    public void SetColor(BlockColorTypes newColor)
    {
        blockColor = newColor;
        Material mat = GameConstants.GetBlockColorMaterial(blockColor);
        var renderer = gameObject.GetComponent<Renderer>();
        if (renderer)
        {
            renderer.materials = new Material[] { mat };
        }
    }
    void OnValidate()
    {
        Material mat = GameConstants.GetBlockColorMaterial(blockColor);
        var renderer = gameObject.GetComponent<Renderer>();
        if (renderer)
        {
            renderer.materials = new Material[] { mat };
        }
    }
    void StartAnimation()
    {
        transform.localPosition = new Vector3(transform.localPosition.x, yPos, transform.localPosition.z);
        return;
        Rigidbody.constraints = RigidbodyConstraints.None;
        (Vector3 snappedPosition, Vector2Int[] newGridPositions) = ReturnBlocksPlacementPosition();
        transform.DOLocalJump(
            new Vector3(transform.localPosition.x, yPos, transform.localPosition.z),
            jumpPower: 0.5f,
            numJumps: 1,
            duration: BlockProperties.GetBlockStartRandomAnimationDuration())
            .SetEase(Ease.OutQuad).OnComplete(() => {
                // Rigidbody.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationY;
                // transform.position = snappedPosition;
            });
    }
    #region Utility Methods
    public (Vector3 snappedPosition, Vector2Int[] newGridPositions) ReturnBlocksPlacementPosition()
    {
        if (gridManager == null) return (Vector3.zero, null);

        // Get grid properties
        float cellSize = gridManager.GetCellSize();
        Vector3 gridOrigin = gridManager.GridStartPosition;

        // Find nearest grid position
        Vector2Int snappedGridPos = new Vector2Int(
            Mathf.RoundToInt((transform.position.x - gridOrigin.x) / cellSize),
            Mathf.RoundToInt((transform.position.z - gridOrigin.z) / cellSize)
        );

        // Get the rotation around Y axis (in degrees)
        float yRotation = transform.rotation.eulerAngles.y;

        // Round to nearest 90 degrees for snapping
        int rotationIndex = Mathf.RoundToInt(yRotation / 90f) % 4;

        // Get the properly rotated positions based on current rotation
        List<Vector2Int> rotatedOccupiedPositions = rotationIndex switch
        {
            1 => shape.GetRotatedPositions90(),
            2 => shape.GetRotatedPositions180(),
            3 => shape.GetRotatedPositions270(),
            _ => shape.GetOccupiedPositions(),
        };

        // Calculate all positions this rotated block would occupy
        Vector2Int[] newGridPositions = new Vector2Int[rotatedOccupiedPositions.Count];
        for (int i = 0; i < rotatedOccupiedPositions.Count; i++)
        {
            newGridPositions[i] = snappedGridPos + rotatedOccupiedPositions[i];
        }

        // Calculate the snapped world position
        float snappedX = gridOrigin.x + (snappedGridPos.x * cellSize);
        float snappedZ = gridOrigin.z + (snappedGridPos.y * cellSize);
        Vector3 snappedPosition = new Vector3(snappedX, yPos, snappedZ);

        return (snappedPosition, newGridPositions);
    }
    #endregion
}