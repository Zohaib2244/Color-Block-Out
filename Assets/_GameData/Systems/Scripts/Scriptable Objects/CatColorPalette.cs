using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CatColorEntry
{
    [Tooltip("Stable identifier written into level assets. Changing or reusing an id repoints saved content.")]
    public int id;
    public string displayName = "New Colour";
    public Color color = Color.white;
}

/// <summary>
/// Every colour a cat or hole can be. One template material each is tinted per colour through a
/// property block, so adding a colour is a list entry rather than a new material asset.
/// Levels store an entry's <see cref="CatColorEntry.id"/> and lookups go by id, never by position,
/// so the list can be reordered freely without touching saved content.
/// </summary>
[CreateAssetMenu(fileName = "CatColorPalette", menuName = "Cat Puzzle/Color Palette")]
public sealed class CatColorPalette : ScriptableObject
{
    [Header("Template Materials")]
    [Tooltip("Shared by every cat. Tinted per colour, so one material covers the whole palette.")]
    public Material catMaterial;

    [Tooltip("Shared by every hole piece.")]
    public Material holeMaterial;

    [Tooltip("Colour property tinted on both templates. URP Lit uses _BaseColor, built-in uses _Color.")]
    public string colorProperty = "_BaseColor";

    [Header("Colours")]
    public List<CatColorEntry> entries = new List<CatColorEntry>();

    private static MaterialPropertyBlock block;

    public int Count => entries.Count;
    public int DefaultId => entries.Count > 0 && entries[0] != null ? entries[0].id : 0;

    /// <summary>Looked up by id rather than position, so reordering the list is safe.</summary>
    public CatColorEntry Find(int id)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].id == id) return entries[i];
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
        foreach (CatColorEntry entry in entries) if (entry != null) ids.Add(entry.id);
        return ids;
    }

    /// <summary>The next free id, so a new entry never collides with saved content.</summary>
    public int NextFreeId()
    {
        int highest = -1;
        foreach (CatColorEntry entry in entries) if (entry != null && entry.id > highest) highest = entry.id;
        return highest + 1;
    }

    public void PaintCat(IList<Renderer> renderers, int id) => Paint(renderers, catMaterial, id);
    public void PaintHole(IList<Renderer> renderers, int id) => Paint(renderers, holeMaterial, id);

    /// <summary>
    /// Assigns the template and tints it. Property blocks are runtime state and are not saved with
    /// the scene, so anything rebuilt or reloaded re-applies its colour rather than relying on this
    /// having stuck.
    /// </summary>
    private void Paint(IList<Renderer> renderers, Material template, int id)
    {
        if (renderers == null || renderers.Count == 0) return;
        Color tint = GetColor(id);
        if (block == null) block = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            if (template != null) renderer.sharedMaterial = template;
            renderer.GetPropertyBlock(block);
            block.SetColor(colorProperty, tint);
            renderer.SetPropertyBlock(block);
        }
    }
}
