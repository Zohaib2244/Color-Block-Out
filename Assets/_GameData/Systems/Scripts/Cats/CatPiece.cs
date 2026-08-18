using DG.Tweening;
using UnityEngine;

/// <summary>Logical representation of a single 1x1 cat.</summary>
public sealed class CatPiece : MonoBehaviour
{
    [SerializeField] private BlockColorTypes color;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private bool collected;

    [Tooltip("Renderers tinted by the cat's colour. Left empty, the first renderer found is used.")]
    [SerializeField] private Renderer[] coloredRenderers;

    public BlockColorTypes Color => color;
    public Vector2Int GridPosition => gridPosition;
    public bool IsCollected => collected;

    public void Configure(BlockColorTypes newColor, Vector2Int position)
    {
        gridPosition = position;
        collected = false;
        SetColor(newColor);
    }

    public void SetGridPosition(Vector2Int position) => gridPosition = position;

    public void SetColor(BlockColorTypes newColor)
    {
        color = newColor;
        Material material = GameConstants.GetBlockColorMaterial(color);
        if (material == null) return;

        if (coloredRenderers != null && coloredRenderers.Length > 0)
        {
            foreach (Renderer renderer in coloredRenderers)
                if (renderer != null) renderer.sharedMaterial = material;
            return;
        }

        Renderer fallback = GetComponent<Renderer>();
        if (fallback == null) fallback = GetComponentInChildren<Renderer>();
        if (fallback != null) fallback.sharedMaterial = material;
    }

    /// <summary>
    /// Hops the cat to <paramref name="localTarget"/> inside the hole, then drops it to
    /// <paramref name="sinkY"/> while shrinking it away. The cat counts as collected the moment
    /// this starts, so it stops blocking before the animation has finished.
    /// </summary>
    public void CollectInto(Vector3 localTarget, float sinkY, CatPuzzleConfig config)
    {
        if (collected) return;
        collected = true;

        if (config == null || !Application.isPlaying)
        {
            gameObject.SetActive(false);
            return;
        }

        Vector3 restingScale = transform.localScale;
        transform.DOKill();

        Sequence exit = DOTween.Sequence();
        exit.Append(transform.DOLocalJump(localTarget, config.catJumpPower, 1, config.catJumpDuration).SetEase(Ease.OutQuad));
        exit.Append(transform.DOLocalMoveY(sinkY, config.catExitDuration).SetEase(Ease.InQuad));
        exit.Join(transform.DOScale(Vector3.zero, config.catExitDuration).SetEase(Ease.InBack));
        exit.OnComplete(() =>
        {
            // Restore the resting scale so the object is reusable if the level is rebuilt.
            transform.localScale = restingScale;
            gameObject.SetActive(false);
        });
    }
}
