using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A number shown over a cat pile or a hole. Purely a view - something else decides when to spawn
/// one, where it sits and what it shows: <see cref="CatLevelBuilder.RefreshPileBadges"/> for piles,
/// <see cref="CatHoleCountBadge"/> for holes.
/// </summary>
public sealed class CatCountBadge : MonoBehaviour
{
    [Tooltip("Found automatically among the children if left empty.")]
    [SerializeField] private TMP_Text label;

    [Tooltip("Anything else that should pick up the pile/hole colour, e.g. a background Image.")]
    [SerializeField] private Graphic[] tintedGraphics;

    private void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
    }

    public void SetCount(int count)
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = count.ToString();
    }

    public void SetColor(Color color)
    {
        if (label != null) label.color = color;
        if (tintedGraphics == null) return;
        for (int i = 0; i < tintedGraphics.Length; i++)
            if (tintedGraphics[i] != null) tintedGraphics[i].color = color;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }
}
