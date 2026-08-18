using DG.Tweening;
using UnityEngine;

/// <summary>
/// Picked-up look for a hole. Everything happens on the Visual child, never on the hole root,
/// so the root stays exactly on its cell while the meshes lift and swell.
/// </summary>
[RequireComponent(typeof(CatHole))]
public sealed class CatHoleHighlight : MonoBehaviour
{
    public const string VisualName = "Visual";

    [SerializeField] private Transform visual;
    [SerializeField] private float scale = 1.08f;
    [SerializeField] private float lift = 0.04f;
    [SerializeField] private float duration = 0.15f;

    private bool highlighted;

    public bool IsHighlighted => highlighted;

    public void SetVisual(Transform target) => visual = target;

    private void Awake()
    {
        if (visual == null) visual = transform.Find(VisualName);
    }

    /// <summary>Reads the tuning values a level was built with. Safe to call before Awake.</summary>
    public void Configure(float highlightScale, float highlightLift, float highlightDuration)
    {
        scale = highlightScale;
        lift = highlightLift;
        duration = highlightDuration;
    }

    public void SetHighlighted(bool on)
    {
        if (visual == null) visual = transform.Find(VisualName);
        if (visual == null || highlighted == on) return;
        highlighted = on;

        visual.DOKill();
        visual.DOScale(on ? Vector3.one * scale : Vector3.one, duration).SetEase(on ? Ease.OutBack : Ease.OutQuad);
        visual.DOLocalMoveY(on ? lift : 0f, duration).SetEase(Ease.OutQuad);
    }

    private void OnDisable()
    {
        if (visual == null) return;
        visual.DOKill();
        visual.localScale = Vector3.one;
        visual.localPosition = Vector3.zero;
        highlighted = false;
    }
}
