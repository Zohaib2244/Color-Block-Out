using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Every colour a cat or hole can be. Numeric values are load-bearing: levels store a colour by
/// this id (see <see cref="CatColorEntry.Id"/>), so existing values must never be renumbered or
/// reused. Add new colours at the end.
/// </summary>
public enum CatColorType
{
    Red = 0,
    Orange = 1,
    Yellow = 2,
    Blue = 3,
    Cyan = 4,
    Green = 5,
    Purple = 6,
    Pink = 7,
    Teal = 8
}

[Serializable]
public sealed class CatColorEntry
{
    [FormerlySerializedAs("id")]
    public CatColorType type;
    public string displayName = "New Colour";

    [Tooltip("Tint applied to this colour's duplicated hole material, and shown as the swatch in editor tooling.")]
    public Color color = Color.white;

    [Tooltip("Fully authored material assigned directly to cats of this colour.")]
    public Material catMaterial;

    /// <summary>Stable id levels are saved with. Backed by the enum value, not list position.</summary>
    public int Id => (int)type;
}

/// <summary>
/// Cats are painted with their own colour's authored <see cref="CatColorEntry.catMaterial"/>
/// directly. Holes have no per-colour material of their own: <see cref="holeMaterial"/> is
/// duplicated and tinted the first time a colour is needed and cached from then on, so every hole
/// of the same colour shares one material instance rather than each hole getting its own.
/// </summary>
[CreateAssetMenu(fileName = "CatColorPalette", menuName = "Cat Puzzle/Color Palette")]
public sealed class CatColorPalette : ScriptableObject
{
    [Header("Hole Template")]
    [Tooltip("Duplicated and tinted per colour for holes. Cats use their own CatColorEntry.catMaterial instead.")]
    public Material holeMaterial;

    [Tooltip("Colour property tinted on the duplicated hole material. URP Lit uses _BaseColor, built-in uses _Color.")]
    public string colorProperty = "_BaseColor";

    [Header("Colours")]
    public List<CatColorEntry> entries = new List<CatColorEntry>();

    /// <summary>
    /// One tinted material per colour, shared by every hole of that colour. Not saved with the
    /// scene (see <see cref="GetOrCreateHoleMaterial"/>), so it is rebuilt lazily as needed.
    /// </summary>
    private readonly Dictionary<CatColorType, Material> holeMaterialCache = new Dictionary<CatColorType, Material>();

    public int Count => entries.Count;
    public int DefaultId => entries.Count > 0 && entries[0] != null ? entries[0].Id : 0;

    /// <summary>Looked up by id rather than position, so reordering the list is safe.</summary>
    public CatColorEntry Find(int id)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].Id == id) return entries[i];
        return null;
    }

    public bool Contains(int id) => Find(id) != null;

    public Color GetColor(int id)
    {
        CatColorEntry entry = Find(id);
        return entry != null ? entry.color : Color.magenta;
    }

    public string GetName(int id)
    {
        CatColorEntry entry = Find(id);
        return entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : $"Colour {id}";
    }

    /// <summary>Ids in list order, for editor swatch rows.</summary>
    public List<int> Ids()
    {
        List<int> ids = new List<int>(entries.Count);
        foreach (CatColorEntry entry in entries) if (entry != null) ids.Add(entry.Id);
        return ids;
    }

    /// <summary>The next free id, so a new entry never collides with saved content.</summary>
    public int NextFreeId()
    {
        int highest = -1;
        foreach (CatColorEntry entry in entries) if (entry != null && entry.Id > highest) highest = entry.Id;
        return highest + 1;
    }

    /// <summary>Assigns each renderer the colour's own authored material directly - no runtime tinting.</summary>
    public void PaintCat(IList<Renderer> renderers, int id)
    {
        if (renderers == null || renderers.Count == 0) return;
        CatColorEntry entry = Find(id);
        Material material = entry != null ? entry.catMaterial : null;
        if (material == null) return;

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null) renderer.sharedMaterial = material;
        }
    }

    /// <summary>
    /// Assigns each renderer the shared tinted-copy material for this colour, creating it on first
    /// use. Rebuilt or reloaded holes ask again rather than relying on anything having stuck, the
    /// same way the old property-block tint worked.
    /// </summary>
    public void PaintHole(IList<Renderer> renderers, int id)
    {
        if (renderers == null || renderers.Count == 0) return;
        Material material = GetOrCreateHoleMaterial(id);
        if (material == null) return;

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null) renderer.sharedMaterial = material;
        }
    }

    private Material GetOrCreateHoleMaterial(int id)
    {
        CatColorType type = (CatColorType)id;
        if (holeMaterialCache.TryGetValue(type, out Material cached) && cached != null) return cached;
        if (holeMaterial == null) return null;

        Material instance = new Material(holeMaterial) { name = $"{holeMaterial.name}_{GetName(id)}" };
        // Runtime-only, like the tint it replaces: not saved with the scene, so a rebuilt or
        // reloaded hole re-creates (or re-fetches) it rather than depending on it having persisted.
        instance.hideFlags = HideFlags.DontSave;
        instance.SetColor(colorProperty, GetColor(id));
        holeMaterialCache[type] = instance;
        return instance;
    }
}
