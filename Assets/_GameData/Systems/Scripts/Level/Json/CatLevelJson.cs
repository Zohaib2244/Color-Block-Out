using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The bridge between the JSON the web level editor exports and the data the game plays.
///
/// Levels ship as JSON files, so this runs at runtime and not just in the editor:
/// <see cref="LevelData"/> holds the play order as a list of those files and asks for one to be
/// parsed as it is loaded. The same code writes files back out, so Unity's own authoring tools and
/// the web tool speak exactly one format.
///
/// Two things are translated rather than copied:
/// <list type="bullet">
/// <item>Colours. The file names a colour ("Red"); levels store the palette id it maps to, matched
/// against <see cref="CatColorEntry.displayName"/>. A name the palette does not know is an error,
/// because it cannot be filled in later.</item>
/// <item>Holes. The file lists the absolute cells of a shape; a <see cref="CatHolePlacement"/> is an
/// origin plus offsets from it, so the origin is taken as the shape's low corner.</item>
/// </list>
/// </summary>
public static class CatLevelJson
{
    #region Results
    /// <summary>What one file turned into, plus everything worth saying about it.</summary>
    public sealed class LevelResult
    {
        public string sourceName;
        public CatLevelData level;
        public readonly List<CatLevelValidator.Issue> issues = new List<CatLevelValidator.Issue>();

        public bool Succeeded => level != null;
        public bool HasErrors => CatLevelValidator.HasErrors(issues);
        public string Describe() => CatLevelValidator.Describe(issues);
    }

    /// <summary>A read only look at a file, so a list can show what it holds without building it.</summary>
    public sealed class Summary
    {
        public string levelName;
        public int width;
        public int length;
        public int catCount;
        public int holeCount;
        public int gateCount;

        /// <summary>Set when the file cannot be used at all.</summary>
        public string error;

        /// <summary>Colour names the palette has no entry for. Loading refuses the file until they resolve.</summary>
        public readonly List<string> unknownColors = new List<string>();

        public bool IsValid => string.IsNullOrEmpty(error);
    }
    #endregion

    #region Reading levels
    /// <summary>
    /// Parses a level file. Never throws: everything that went wrong comes back in
    /// <see cref="LevelResult.issues"/>, and <see cref="LevelResult.level"/> is null when the file
    /// could not be used at all.
    /// </summary>
    public static LevelResult ParseLevel(string json, string sourceName, CatPuzzleConfig config)
    {
        LevelResult result = new LevelResult { sourceName = sourceName };

        if (!TryReadLevelDocument(json, out LevelJsonFormat.LevelDocument document, out string error))
        {
            Error(result.issues, error);
            return result;
        }

        CatColorPalette palette = config != null ? config.palette : null;
        if (palette == null)
        {
            Error(result.issues, "No CatColorPalette. Assign a CatPuzzleConfig with a palette to the LevelSpawner, " +
                                 "or run Cat Puzzle/Create Default Assets.");
            return result;
        }

        // Colours are the one thing that cannot be filled in later, so a file naming an unknown one
        // is refused whole rather than loaded with the wrong cats in it.
        Dictionary<string, int> colors = ResolveColors(document, palette, result.issues);
        if (colors == null) return result;

        CatLevelData level = new CatLevelData
        {
            levelName = DisplayName(document, sourceName),
            grid = GridFromSection(document.grid, config),
            cats = BuildCats(document, colors),
            holes = BuildHoles(document, colors, result.issues)
        };

        WarnAboutGates(document, result.issues);
        result.level = level;
        result.issues.AddRange(CatLevelValidator.Validate(level, palette));
        return result;
    }

    /// <summary>Reads a file without building anything from it, for listing a folder.</summary>
    public static Summary Inspect(string json, string sourceName, CatColorPalette palette)
    {
        Summary summary = new Summary();
        if (!TryReadLevelDocument(json, out LevelJsonFormat.LevelDocument document, out string error))
        {
            summary.error = error;
            return summary;
        }

        summary.levelName = DisplayName(document, sourceName);
        summary.width = document.grid.width;
        summary.length = document.grid.length;
        summary.catCount = document.cats != null ? document.cats.Length : 0;
        summary.holeCount = document.holes != null ? document.holes.Length : 0;
        summary.gateCount = document.gates != null ? document.gates.Length : 0;

        Dictionary<string, int> byName = PaletteByName(palette);
        foreach (string name in ColorNames(document))
            if (!byName.ContainsKey(name) && !summary.unknownColors.Contains(name)) summary.unknownColors.Add(name);

        return summary;
    }

    /// <summary>Loads a document and checks it well enough that everything past this point can trust it.</summary>
    public static bool TryReadLevelDocument(string json, out LevelJsonFormat.LevelDocument document, out string error)
    {
        document = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The file is empty.";
            return false;
        }

        try
        {
            document = JsonUtility.FromJson<LevelJsonFormat.LevelDocument>(json);
        }
        catch (Exception exception)
        {
            error = $"Not valid JSON: {exception.Message}";
            return false;
        }

        error = FindStructuralError(document);
        if (error == null) return true;

        document = null;
        return false;
    }

    private static string FindStructuralError(LevelJsonFormat.LevelDocument document)
    {
        if (document == null) return "The file is empty, or is not a level export.";

        if (document.formatVersion < LevelJsonFormat.MinLevelVersion || document.formatVersion > LevelJsonFormat.LevelVersion)
            return $"formatVersion {document.formatVersion} is not supported (expected " +
                   $"{LevelJsonFormat.MinLevelVersion}-{LevelJsonFormat.LevelVersion}). Older files upgrade when the web " +
                   "level editor imports them, so open it there and export again.";

        if (document.grid == null) return "The file has no grid.";
        return FindGridError(document.grid.width, document.grid.length, document.grid.playableCells);
    }

    private static string FindGridError(int width, int length, bool[] playableCells)
    {
        if (width <= 0 || length <= 0) return $"Board size {width}x{length} is not usable.";

        int required = width * length;
        int actual = playableCells != null ? playableCells.Length : 0;
        if (actual != required) return $"playableCells holds {actual} entries but a {width}x{length} board needs {required}.";

        return null;
    }
    #endregion

    #region Reading grids
    /// <summary>
    /// Parses a standalone grid file. Levels carry their own board, so this is for the authoring
    /// tools: a shape drawn once and reused across levels.
    /// </summary>
    public static bool TryParseGrid(string json, string sourceName, CatPuzzleConfig config, out GridData grid, out string error)
    {
        grid = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The file is empty.";
            return false;
        }

        LevelJsonFormat.GridDocument document;
        try
        {
            document = JsonUtility.FromJson<LevelJsonFormat.GridDocument>(json);
        }
        catch (Exception exception)
        {
            error = $"Not valid JSON: {exception.Message}";
            return false;
        }

        if (document == null)
        {
            error = "The file is empty, or is not a grid export.";
            return false;
        }

        if (document.formatVersion != LevelJsonFormat.GridVersion)
        {
            error = $"formatVersion {document.formatVersion} is not supported (expected {LevelJsonFormat.GridVersion}).";
            return false;
        }

        error = FindGridError(document.width, document.length, document.playableCells);
        if (error != null) return false;

        grid = GridFromSection(new LevelJsonFormat.GridSection
        {
            gridName = string.IsNullOrEmpty(document.gridName) ? sourceName : document.gridName,
            width = document.width,
            length = document.length,
            cellSize = document.cellSize,
            playableCells = document.playableCells,
            catParentHeight = document.catParentHeight,
            holeParentHeight = document.holeParentHeight
        }, config);
        return true;
    }

    /// <summary>
    /// The file holds the post flood fill playable mask, the same mask the Grid Creator saves, so it
    /// inverts straight into <c>wallCells</c> with nothing to redo.
    /// </summary>
    public static GridData GridFromSection(LevelJsonFormat.GridSection section, CatPuzzleConfig config)
    {
        if (section == null) return null;

        GridData grid = new GridData(section.width, section.length)
        {
            gridName = string.IsNullOrEmpty(section.gridName) ? section.id : section.gridName,
            cellSize = section.cellSize > 0f ? section.cellSize : 0.57f,
            // The two heights are Unity-side presentation and the web tool does not export them, so
            // a file without them takes the project defaults.
            catParentHeight = section.catParentHeight > 0f ? section.catParentHeight : DefaultCatHeight(config),
            holeParentHeight = section.holeParentHeight > 0f ? section.holeParentHeight : DefaultHoleHeight(config)
        };

        // Both sides index a flat array as z * width + x, so the mask copies across as it stands.
        if (section.playableCells != null)
            for (int i = 0; i < section.playableCells.Length && i < grid.gridData.wallCells.Length; i++)
                grid.gridData.wallCells[i] = !section.playableCells[i];

        return grid;
    }

    private static float DefaultCatHeight(CatPuzzleConfig config) => config != null ? config.catParentHeight : 0.178f;

    private static float DefaultHoleHeight(CatPuzzleConfig config) => config != null ? config.holeParentHeight : 0.08f;
    #endregion

    #region Writing
    /// <summary>Writes a level out in the format the web tool reads back.</summary>
    public static string ToJson(CatLevelData level, CatPuzzleConfig config) => ToJson(level, config, out _);

    public static string ToJson(CatLevelData level, CatPuzzleConfig config, out List<CatLevelValidator.Issue> issues)
    {
        issues = new List<CatLevelValidator.Issue>();
        if (level == null)
        {
            Error(issues, "No level to write.");
            return null;
        }
        if (level.grid == null)
        {
            Error(issues, $"Level '{level.DisplayName}' has no grid, so it cannot be written.");
            return null;
        }

        CatColorPalette palette = config != null ? config.palette : null;
        CatHoleConfiguration holeConfiguration = config != null ? config.holeConfiguration : null;
        int catCount = level.cats != null ? level.cats.Count : 0;
        int holeCount = level.holes != null ? level.holes.Count : 0;

        LevelJsonFormat.LevelDocument document = new LevelJsonFormat.LevelDocument
        {
            formatVersion = LevelJsonFormat.LevelVersion,
            levelName = level.DisplayName,
            grid = SectionFromGrid(level.grid, level.DisplayName),
            cats = new LevelJsonFormat.CatEntry[catCount],
            holes = new LevelJsonFormat.HoleEntry[holeCount],
            // Gates are authored in the web tool and have no Unity counterpart, so an export from
            // here never carries any. Re-exporting a level that had some drops them.
            gates = new LevelJsonFormat.GateEntry[0]
        };

        // Order is load bearing: cats sharing a cell are a stack, earliest lowest.
        for (int i = 0; i < catCount; i++)
        {
            CatPlacement cat = level.cats[i];
            document.cats[i] = cat == null
                ? new LevelJsonFormat.CatEntry()
                : new LevelJsonFormat.CatEntry { color = ColorName(palette, cat.colorId, issues), x = cat.cell.x, z = cat.cell.y };
        }

        for (int i = 0; i < holeCount; i++)
            document.holes[i] = HoleEntryFrom(level.holes[i], i, palette, holeConfiguration, issues);

        return JsonUtility.ToJson(document, true);
    }

    /// <summary>Writes a board on its own, so a shape can be reused across levels.</summary>
    public static string GridToJson(GridData grid)
    {
        if (grid == null) return null;
        grid.EnsureArrays();

        LevelJsonFormat.GridDocument document = new LevelJsonFormat.GridDocument
        {
            formatVersion = LevelJsonFormat.GridVersion,
            gridName = string.IsNullOrEmpty(grid.gridName) ? "Grid" : grid.gridName,
            width = grid.gridWidth,
            length = grid.gridLength,
            cellSize = grid.cellSize,
            playableCells = PlayableMask(grid),
            catParentHeight = grid.catParentHeight,
            holeParentHeight = grid.holeParentHeight
        };
        return JsonUtility.ToJson(document, true);
    }

    public static LevelJsonFormat.GridSection SectionFromGrid(GridData grid, string levelName)
    {
        if (grid == null) return null;
        grid.EnsureArrays();

        return new LevelJsonFormat.GridSection
        {
            id = string.IsNullOrEmpty(grid.gridName) ? levelName : grid.gridName,
            gridName = grid.gridName,
            width = grid.gridWidth,
            length = grid.gridLength,
            cellSize = grid.cellSize,
            playableCells = PlayableMask(grid),
            catParentHeight = grid.catParentHeight,
            holeParentHeight = grid.holeParentHeight
        };
    }

    private static bool[] PlayableMask(GridData grid)
    {
        bool[] mask = new bool[grid.gridWidth * grid.gridLength];
        for (int z = 0; z < grid.gridLength; z++)
            for (int x = 0; x < grid.gridWidth; x++)
                mask[grid.GetIndex(x, z)] = grid.IsPlayable(x, z);
        return mask;
    }

    /// <summary>
    /// One <see cref="CatHolePlacement"/> becomes one JSON hole. Unity has no capacity of its own,
    /// a hole holds one cat per cell, so that is what gets written.
    /// </summary>
    private static LevelJsonFormat.HoleEntry HoleEntryFrom(CatHolePlacement hole, int index, CatColorPalette palette,
        CatHoleConfiguration holeConfiguration, List<CatLevelValidator.Issue> issues)
    {
        if (hole == null)
            return new LevelJsonFormat.HoleEntry { id = $"hole-{index + 1}", cells = new LevelJsonFormat.HoleCellEntry[0] };

        List<Vector2Int> offsets = hole.offsets != null && hole.offsets.Count > 0
            ? hole.offsets
            : new List<Vector2Int> { Vector2Int.zero };
        HashSet<Vector2Int> shape = new HashSet<Vector2Int>(offsets);

        LevelJsonFormat.HoleCellEntry[] cells = new LevelJsonFormat.HoleCellEntry[offsets.Count];
        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2Int offset = offsets[i];
            List<Direction> connections = CatHoleBuilder.GetConnections(offset, shape);
            CatHoleType type = CatHoleBuilder.GetHoleType(connections);
            CatHolePrefabData data = holeConfiguration != null ? holeConfiguration.GetData(type) : null;

            cells[i] = new LevelJsonFormat.HoleCellEntry
            {
                x = hole.origin.x + offset.x,
                z = hole.origin.y + offset.y,
                holeType = type.ToString(),
                // Solved against this project's prefab openings, the same way the meshes are built.
                rotationQuarterTurns = CatHoleBuilder.GetQuarterTurns(data, connections)
            };
        }

        return new LevelJsonFormat.HoleEntry
        {
            id = $"hole-{index + 1}",
            color = ColorName(palette, hole.colorId, issues),
            capacity = offsets.Count,
            cells = cells
        };
    }

    private static string ColorName(CatColorPalette palette, int colorId, List<CatLevelValidator.Issue> issues)
    {
        CatColorEntry entry = palette != null ? palette.Find(colorId) : null;
        if (entry != null && !string.IsNullOrEmpty(entry.displayName)) return entry.displayName;

        Warning(issues, $"Colour id {colorId} is not in the palette, so it was written out as '{colorId}'. " +
                        "The web level editor will not recognise it.");
        return colorId.ToString();
    }
    #endregion

    #region Building content
    /// <summary>
    /// The web tool writes colour names ("Red"); levels store palette ids. Names are matched against
    /// <see cref="CatColorEntry.displayName"/>, so a colour renamed on one side has to be renamed on
    /// the other. Returns null when any name is unknown, having reported which.
    /// </summary>
    private static Dictionary<string, int> ResolveColors(LevelJsonFormat.LevelDocument document, CatColorPalette palette,
        List<CatLevelValidator.Issue> issues)
    {
        Dictionary<string, int> byName = PaletteByName(palette);
        Dictionary<string, int> used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        List<string> unknown = new List<string>();

        foreach (string name in ColorNames(document))
        {
            if (used.ContainsKey(name)) continue;
            if (byName.TryGetValue(name, out int id)) used[name] = id;
            else if (!unknown.Contains(name)) unknown.Add(name);
        }

        if (unknown.Count == 0) return used;

        string names = string.Join(", ", unknown.Select(name => $"'{name}'"));
        Error(issues, $"Palette '{palette.name}' has no colour named {names}. Add {(unknown.Count == 1 ? "it" : "them")} to the " +
                      "palette, or rename so both sides agree, then export again.");
        return null;
    }

    private static List<CatPlacement> BuildCats(LevelJsonFormat.LevelDocument document, Dictionary<string, int> colors)
    {
        List<CatPlacement> cats = new List<CatPlacement>();
        if (document.cats == null) return cats;

        // Order is preserved on purpose: cats sharing a cell are a stack, and the file lists them
        // bottom first (see CatLevelBuilder.RestackCats).
        foreach (LevelJsonFormat.CatEntry entry in document.cats)
        {
            if (entry == null) continue;
            cats.Add(new CatPlacement { colorId = colors[Key(entry.color)], cell = new Vector2Int(entry.x, entry.z) });
        }
        return cats;
    }

    /// <summary>
    /// One JSON hole becomes one <see cref="CatHolePlacement"/>: origin at the shape's low corner and
    /// offsets from there, which is the same anchoring <c>CatHoleBuilder.SplitIntoPlacements</c> uses.
    /// </summary>
    private static List<CatHolePlacement> BuildHoles(LevelJsonFormat.LevelDocument document, Dictionary<string, int> colors,
        List<CatLevelValidator.Issue> issues)
    {
        List<CatHolePlacement> holes = new List<CatHolePlacement>();
        if (document.holes == null) return holes;

        for (int i = 0; i < document.holes.Length; i++)
        {
            LevelJsonFormat.HoleEntry entry = document.holes[i];
            if (entry == null) continue;

            List<Vector2Int> cells = new List<Vector2Int>();
            if (entry.cells != null)
                foreach (LevelJsonFormat.HoleCellEntry cell in entry.cells)
                    if (cell != null) cells.Add(new Vector2Int(cell.x, cell.z));
            cells = cells.Distinct().ToList();

            if (cells.Count == 0)
            {
                Warning(issues, $"Hole {i + 1} covers no cells and was skipped.");
                continue;
            }

            Vector2Int origin = new Vector2Int(cells.Min(cell => cell.x), cells.Min(cell => cell.y));
            holes.Add(new CatHolePlacement
            {
                colorId = colors[Key(entry.color)],
                origin = origin,
                offsets = CatHoleBuilder.Normalise(cells)
            });

            CheckShape(entry, cells, i + 1, issues);
        }
        return holes;
    }

    /// <summary>
    /// The per cell holeType in the file is derived from the shape, so a disagreement means the file
    /// was hand edited or written by a different version of the tool. Rotation is deliberately not
    /// checked: <see cref="CatHoleBuilder"/> solves it against this project's
    /// <see cref="CatHolePrefabData.defaultOpenings"/>, which the web tool only guesses at.
    /// </summary>
    private static void CheckShape(LevelJsonFormat.HoleEntry entry, List<Vector2Int> cells, int number,
        List<CatLevelValidator.Issue> issues)
    {
        int islands = CountIslands(cells);
        if (islands > 1)
            Warning(issues, $"Hole {number} is drawn as {islands} separate islands. It builds as one hole covering all of them, " +
                            "so its cats all count against the same capacity.");

        // Unity has no separate capacity field: a hole's cap is implicitly its cell count, one cat
        // per cell (see CatHole.CellCount). The web tool's capacity is free standing, so a value that
        // does not match what Unity will actually enforce is worth flagging.
        if (entry.capacity > 0 && entry.capacity != cells.Count)
            Warning(issues, $"Hole {number} was authored with capacity {entry.capacity}, but Unity gives every hole one slot per cell " +
                            $"({cells.Count} here) and does not track capacity separately, so it will hold {cells.Count}.");

        if (entry.cells == null) return;
        HashSet<Vector2Int> shape = new HashSet<Vector2Int>(cells);

        foreach (LevelJsonFormat.HoleCellEntry cell in entry.cells)
        {
            if (cell == null || string.IsNullOrEmpty(cell.holeType)) continue;
            Vector2Int position = new Vector2Int(cell.x, cell.z);
            CatHoleType expected = CatHoleBuilder.GetHoleType(CatHoleBuilder.GetConnections(position, shape));

            if (!Enum.TryParse(cell.holeType, true, out CatHoleType authored))
                Warning(issues, $"Hole {number} cell {position} names an unknown piece '{cell.holeType}'. It builds as {expected}.");
            else if (authored != expected)
                Warning(issues, $"Hole {number} cell {position} was exported as {authored} but its neighbours make it {expected}. " +
                                $"It builds as {expected}.");
        }
    }

    private static int CountIslands(List<Vector2Int> cells)
    {
        HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(cells);
        Vector2Int[] steps = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        int islands = 0;

        while (remaining.Count > 0)
        {
            islands++;
            Queue<Vector2Int> pending = new Queue<Vector2Int>();
            Vector2Int seed = remaining.First();
            remaining.Remove(seed);
            pending.Enqueue(seed);

            while (pending.Count > 0)
            {
                Vector2Int current = pending.Dequeue();
                foreach (Vector2Int step in steps)
                    if (remaining.Remove(current + step)) pending.Enqueue(current + step);
            }
        }
        return islands;
    }

    /// <summary>
    /// Gates are authored in the web tool but have no Unity counterpart yet, so they are dropped on
    /// load. Silently losing authored content would be worse than saying so plainly.
    /// </summary>
    private static void WarnAboutGates(LevelJsonFormat.LevelDocument document, List<CatLevelValidator.Issue> issues)
    {
        if (document.gates == null || document.gates.Length == 0) return;

        int queued = 0;
        foreach (LevelJsonFormat.GateEntry gate in document.gates)
            if (gate != null && gate.cats != null) queued += gate.cats.Length;

        Warning(issues, $"The file has {document.gates.Length} gate(s) holding {queued} cat(s). Unity has no gate yet, " +
                        "so they were not loaded and those cats are missing from this level.");
    }
    #endregion

    #region Helpers
    private static Dictionary<string, int> PaletteByName(CatColorPalette palette)
    {
        Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (palette == null) return byName;

        foreach (CatColorEntry entry in palette.entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.displayName)) continue;
            string key = Key(entry.displayName);
            if (!byName.ContainsKey(key)) byName[key] = entry.Id;
        }
        return byName;
    }

    private static IEnumerable<string> ColorNames(LevelJsonFormat.LevelDocument document)
    {
        if (document.cats != null)
            foreach (LevelJsonFormat.CatEntry cat in document.cats)
                if (cat != null) yield return Key(cat.color);

        if (document.holes != null)
            foreach (LevelJsonFormat.HoleEntry hole in document.holes)
                if (hole != null) yield return Key(hole.color);
    }

    private static string Key(string name) => (name ?? string.Empty).Trim();

    private static string DisplayName(LevelJsonFormat.LevelDocument document, string sourceName)
    {
        string name = Key(document.levelName);
        if (!string.IsNullOrEmpty(name)) return name;
        return string.IsNullOrEmpty(sourceName) ? "Untitled Level" : sourceName;
    }

    private static void Error(List<CatLevelValidator.Issue> issues, string message) =>
        issues.Add(new CatLevelValidator.Issue(CatLevelValidator.Severity.Error, message));

    private static void Warning(List<CatLevelValidator.Issue> issues, string message) =>
        issues.Add(new CatLevelValidator.Issue(CatLevelValidator.Severity.Warning, message));
    #endregion
}
