using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Logical representation of a single 1x1 cat.</summary>
public sealed class CatPiece : MonoBehaviour
{
    [FormerlySerializedAs("color")]
    [SerializeField] private int colorId;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private bool collected;

    [Tooltip("Renderers tinted by the cat's colour. Left empty, every renderer on the cat is used.")]
    [SerializeField] private Renderer[] coloredRenderers;

    [Header("Collection")]
    [Tooltip("Arc height of the hop into the hole.")]
    [SerializeField] private float jumpPower = 0.25f;
    [SerializeField] private float jumpDuration = 0.18f;

    [Tooltip("How far below the hole surface the cat sinks as it shrinks away.")]
    [SerializeField] private float sinkDepth = 0.15f;
    [SerializeField] private float exitDuration = 0.22f;

    private readonly List<Renderer> rendererBuffer = new List<Renderer>();

    public int ColorId => colorId;
    public Vector2Int GridPosition => gridPosition;
    public bool IsCollected => collected;

    public void Configure(int newColorId, Vector2Int position, CatColorPalette palette)
    {
        gridPosition = position;
        collected = false;
        SetColor(newColorId, palette);
    }

    public void SetGridPosition(Vector2Int position) => gridPosition = position;

    public void SetColor(int newColorId, CatColorPalette palette)
    {
        colorId = newColorId;
        ApplyColor(palette);
    }

    /// <summary>
    /// Re-tints the cat. Needed after a scene reload as well as on build, because the tint lives in
    /// a material property block and those are not saved with the scene.
    /// </summary>
    public void ApplyColor(CatColorPalette palette)
    {
        if (palette == null) return;
        palette.PaintCat(Renderers(), colorId);
    }

    private List<Renderer> Renderers()
    {
        rendererBuffer.Clear();
        if (coloredRenderers != null && coloredRenderers.Length > 0) rendererBuffer.AddRange(coloredRenderers);
        else GetComponentsInChildren(true, rendererBuffer);
        return rendererBuffer;
    }

    /// <summary>
    /// Hops the cat to <paramref name="localTarget"/> inside the hole, then drops it through the
    /// hole surface while shrinking it away. <paramref name="surfaceY"/> is where that surface sits
    /// in the cat's own space; how far past it the cat goes is this component's business. The cat
    /// counts as collected the moment this starts, so it stops blocking before the animation ends.
    /// </summary>
    public void CollectInto(Vector3 localTarget, float surfaceY)
    {
        if (collected) return;
        collected = true;

        if (!Application.isPlaying)
        {
            gameObject.SetActive(false);
            return;
        }

        Vector3 restingScale = transform.localScale;
        transform.DOKill();

        Sequence exit = DOTween.Sequence();
        exit.Append(transform.DOLocalJump(localTarget, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad));
        exit.Append(transform.DOLocalMoveY(surfaceY - sinkDepth, exitDuration).SetEase(Ease.InQuad));
        exit.Join(transform.DOScale(Vector3.zero, exitDuration).SetEase(Ease.InBack));
        exit.OnComplete(() =>
        {
            // Restore the resting scale so the object is reusable if the level is rebuilt.
            transform.localScale = restingScale;
            gameObject.SetActive(false);
        });
    }
}
