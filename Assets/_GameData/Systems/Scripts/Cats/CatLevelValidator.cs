using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Checks a level asset for the states that otherwise ship silently: content standing on walls,
/// holes that overlap each other, colours that can never be cleared, and ids the palette no
/// longer knows about.
/// </summary>
public static class CatLevelValidator
{
    public enum Severity { Warning, Error }

    public readonly struct Issue
    {
        public readonly Severity severity;
        public readonly string message;

        public Issue(Severity severity, string message)
        {
            this.severity = severity;
            this.message = message;
        }

        public override string ToString() => $"{(severity == Severity.Error ? "Error" : "Warning")}: {message}";
    }

    public static bool HasErrors(List<Issue> issues)
    {
        foreach (Issue issue in issues) if (issue.severity == Severity.Error) return true;
        return false;
    }

    public static string Describe(List<Issue> issues)
    {
        if (issues.Count == 0) return "No problems found.";
        StringBuilder builder = new StringBuilder();
        foreach (Issue issue in issues) builder.AppendLine(issue.ToString());
        return builder.ToString();
    }

    public static List<Issue> Validate(CatLevelData level, CatColorPalette palette)
    {
        List<Issue> issues = new List<Issue>();
        if (level == null)
        {
            issues.Add(new Issue(Severity.Error, "No level asset."));
            return issues;
        }

        GridData grid = level.grid;
        if (grid == null)
        {
            issues.Add(new Issue(Severity.Error, "No GridData assigned, so the level cannot be built."));
            return issues;
        }
        grid.EnsureArrays();

        ValidateCats(level, grid, palette, issues);
        ValidateHoles(level, grid, palette, issues);
        ValidateSolvable(level, palette, issues);
        return issues;
    }

    private static void ValidateCats(CatLevelData level, GridData grid, CatColorPalette palette, List<Issue> issues)
    {
        if (level.cats.Count == 0) issues.Add(new Issue(Severity.Warning, "The level has no cats, so it is complete the moment it loads."));

        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
        foreach (CatPlacement cat in level.cats)
        {
            if (cat == null) continue;
            if (!grid.IsWithinGrid(cat.cell.x, cat.cell.y))
                issues.Add(new Issue(Severity.Error, $"Cat at {cat.cell} is outside the {grid.gridWidth}x{grid.gridLength} board."));
            else if (!grid.IsPlayable(cat.cell))
                issues.Add(new Issue(Severity.Error, $"Cat at {cat.cell} stands on a blocked cell."));

            if (!seen.Add(cat.cell))
                issues.Add(new Issue(Severity.Error, $"Two cats share cell {cat.cell}."));

            if (palette != null && !palette.Contains(cat.colorId))
                issues.Add(new Issue(Severity.Error, $"Cat at {cat.cell} uses colour id {cat.colorId}, which is not in the palette."));
        }
    }

    private static void ValidateHoles(CatLevelData level, GridData grid, CatColorPalette palette, List<Issue> issues)
    {
        if (level.holes.Count == 0) issues.Add(new Issue(Severity.Warning, "The level has no holes, so nothing can be collected."));

        Dictionary<Vector2Int, int> owner = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < level.holes.Count; i++)
        {
            CatHolePlacement hole = level.holes[i];
            if (hole == null) continue;

            if (palette != null && !palette.Contains(hole.colorId))
                issues.Add(new Issue(Severity.Error, $"Hole at {hole.origin} uses colour id {hole.colorId}, which is not in the palette."));

            List<Vector2Int> offsets = hole.offsets != null && hole.offsets.Count > 0 ? hole.offsets : new List<Vector2Int> { Vector2Int.zero };
            foreach (Vector2Int offset in offsets)
            {
                Vector2Int cell = hole.origin + offset;
                if (!grid.IsWithinGrid(cell.x, cell.y))
                    issues.Add(new Issue(Severity.Error, $"Hole at {hole.origin} covers {cell}, which is outside the board."));
                else if (!grid.IsPlayable(cell))
                    issues.Add(new Issue(Severity.Error, $"Hole at {hole.origin} covers blocked cell {cell}."));

                if (owner.TryGetValue(cell, out int other))
                    issues.Add(new Issue(Severity.Error, $"Holes {other + 1} and {i + 1} both cover {cell}; neither will be able to move."));
                else owner[cell] = i;
            }
        }
    }

    /// <summary>A colour with cats but no hole can never be cleared, so the level is unwinnable.</summary>
    private static void ValidateSolvable(CatLevelData level, CatColorPalette palette, List<Issue> issues)
    {
        HashSet<int> catColors = new HashSet<int>();
        HashSet<int> holeColors = new HashSet<int>();
        foreach (CatPlacement cat in level.cats) if (cat != null) catColors.Add(cat.colorId);
        foreach (CatHolePlacement hole in level.holes) if (hole != null) holeColors.Add(hole.colorId);

        foreach (int color in catColors)
            if (!holeColors.Contains(color))
                issues.Add(new Issue(Severity.Error, $"Cats of colour '{Name(palette, color)}' have no hole of that colour, so the level cannot be finished."));

        foreach (int color in holeColors)
            if (!catColors.Contains(color))
                issues.Add(new Issue(Severity.Warning, $"A hole of colour '{Name(palette, color)}' has no cats to collect."));
    }

    private static string Name(CatColorPalette palette, int colorId) => palette != null ? palette.GetName(colorId) : colorId.ToString();
}
