#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reads the JSON the web level editor (level-editor/) exports and files it as the assets the game
/// actually loads: a <see cref="GridData"/> for the board shape and a <see cref="CatLevelData"/> for
/// its contents. The JSON is the authored document, so importing the same file twice rewrites the
/// level it produced rather than leaving a second copy behind.
/// </summary>
public static class CatLevelJsonImporter
{
    /// <summary>Mirrors LEVEL_JSON_FORMAT_VERSION in level-editor/src/io/levelJson.ts.</summary>
    public const int SupportedFormatVersion = 4;

    /// <summary>
    /// v3 files still import unchanged: v4 only *added* gates and cat stacking, so everything a v3
    /// file can express means the same thing here.
    /// </summary>
    public const int MinSupportedFormatVersion = 3;

    #region Document
    // Field names mirror level-editor/src/io/levelJson.ts exactly, because JsonUtility matches by
    // name. Anything the file does not carry stays at its default and is checked before it is used.

    [Serializable]
    public sealed class Document
    {
        public int formatVersion;
        public string levelName;
        public GridSection grid;
        public CatEntry[] cats;
        public HoleEntry[] holes;
        public GateEntry[] gates;
    }

    /// <summary>
    /// A v4 gate: a queue of cats on one boundary edge of a cell. Parsed so the count can be
    /// reported, but there is no Unity-side gate yet, so importing drops them (with a warning).
    /// </summary>
    [Serializable]
    public sealed class GateEntry
    {
        public string id;
        public int x;
        public int z;
        public string side;
        public string[] cats;
    }

    [Serializable]
    public sealed class GridSection
    {
        public string id;
        public int width;
        public int length;
        public float cellSize;

        /// <summary>Flat, index = z * width + x. True where a cat or hole may stand.</summary>
        public bool[] playableCells;
    }

    [Serializable]
    public sealed class CatEntry
    {
        public string color;
        public int x;
        public int z;
    }

    /// <summary>One hole: a connected shape sharing a single colour and a single capacity.</summary>
    [Serializable]
    public sealed class HoleEntry
    {
        public string id;
        public string color;
        public int capacity;
        public HoleCellEntry[] cells;
    }

    [Serializable]
    public sealed class HoleCellEntry
    {
        public int x;
        public int z;

        /// <summary>What the web tool drew. Unity rebuilds it from the shape, so it is only cross checked.</summary>
        public string holeType;

        public int rotationQuarterTurns;
    }
    #endregion

    #region Settings and results
    public sealed class ImportSettings
    {
        public CatPuzzleConfig config;

        [Tooltip("Reuse a GridData that already has this exact shape instead of filing another copy of it.")]
        public bool reuseMatchingGrid = true;

        /// <summary>Write over the level asset an earlier import of the same file produced.</summary>
        public bool overwriteExistingLevel = true;

        /// <summary>Only used for levels being created; a re-import keeps whatever time was tuned in Unity.</summary>
        public int levelTime = 60;

        /// <summary>Only used for grids being created. Not in the JSON, and not touched on a reused grid.</summary>
        public float catParentHeight = 0.178f;
        public float holeParentHeight = 0.08f;

        /// <summary>Optional play order to append newly imported levels to.</summary>
        public LevelData collection;

        public CatColorPalette Palette => config != null ? config.palette : null;
    }

    /// <summary>What one file turned into, plus everything worth saying about it.</summary>
    public sealed class ImportResult
    {
        public string sourcePath;
        public string levelName;
        public CatLevelData level;
        public GridData grid;
        public bool gridWasCreated;
        public bool levelWasReplaced;
        public bool addedToCollection;
        public readonly List<CatLevelValidator.Issue> issues = new List<CatLevelValidator.Issue>();

        public bool Succeeded => level != null;
        public bool HasErrors => CatLevelValidator.HasErrors(issues);
        public string FileName => string.IsNullOrEmpty(sourcePath) ? "(no file)" : Path.GetFileName(sourcePath);
    }

    /// <summary>A read only look at a file, so a queue can show what an import would do before it runs.</summary>
    public sealed class Summary
    {
        public string levelName;
        public int width;
        public int length;
        public int catCount;
        public int holeCount;

        /// <summary>Set when the file cannot be imported at all.</summary>
        public string error;

        /// <summary>Colour names the palette has no entry for. Import refuses the file until they resolve.</summary>
        public readonly List<string> unknownColors = new List<string>();

        public bool IsValid => string.IsNullOrEmpty(error);
    }
    #endregion

    #region Reading
    /// <summary>Loads a file and checks it well enough that everything past this point can trust it.</summary>
    public static bool TryRead(string path, out Document document, out string error)
    {
        document = null;
        error = null;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            error = $"File not found: {path}";
            return false;
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception)
        {
            error = $"Could not read the file: {exception.Message}";
            return false;
        }

        try
        {
            document = JsonUtility.FromJson<Document>(text);
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

    private static string FindStructuralError(Document document)
    {
        if (document == null) return "The file is empty, or is not a level export.";

        if (document.formatVersion < MinSupportedFormatVersion || document.formatVersion > SupportedFormatVersion)
            return $"formatVersion {document.formatVersion} is not supported (expected {MinSupportedFormatVersion}-{SupportedFormatVersion}). " +
                   "Older files upgrade when the web level editor imports them, so open it there and export again.";

        if (document.grid == null) return "The file has no grid.";
        if (document.grid.width <= 0 || document.grid.length <= 0)
            return $"Board size {document.grid.width}x{document.grid.length} is not usable.";

        int required = document.grid.width * document.grid.length;
        int actual = document.grid.playableCells != null ? document.grid.playableCells.Length : 0;
        if (actual != required)
            return $"playableCells holds {actual} entries but a {document.grid.width}x{document.grid.length} board needs {required}.";

        return null;
    }

    /// <summary>Reads a file without writing anything, for listing a queue.</summary>
    public static Summary Inspect(string path, CatColorPalette palette)
    {
        Summary summary = new Summary();
        if (!TryRead(path, out Document document, out string error))
        {
            summary.error = error;
            return summary;
        }

        summary.levelName = DisplayName(document, path);
        summary.width = document.grid.width;
        summary.length = document.grid.length;
        summary.catCount = document.cats != null ? document.cats.Length : 0;
        summary.holeCount = document.holes != null ? document.holes.Length : 0;

        Dictionary<string, int> byName = PaletteByName(palette);
        foreach (string name in ColorNames(document))
            if (!byName.ContainsKey(name) && !summary.unknownColors.Contains(name)) summary.unknownColors.Add(name);

        return summary;
    }

    /// <summary>Expands dropped paths into the JSON files under them, so a folder can be dropped whole.</summary>
    public static List<string> CollectJsonFiles(IEnumerable<string> paths)
    {
        List<string> files = new List<string>();
        if (paths == null) return files;

        foreach (string path in paths)
        {
            if (string.IsNullOrEmpty(path)) continue;

            if (Directory.Exists(path))
            {
                foreach (string found in Directory.GetFiles(path, "*.json", SearchOption.AllDirectories))
                    if (!files.Contains(found)) files.Add(found);
            }
            else if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
            {
                if (!files.Contains(path)) files.Add(path);
            }
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }
    #endregion

    #region Importing
    public static List<ImportResult> ImportAll(IEnumerable<string> paths, ImportSettings settings)
    {
        List<ImportResult> results = new List<ImportResult>();
        if (paths == null) return results;

        foreach (string path in paths) results.Add(Import(path, settings));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return results;
    }

    public static ImportResult Import(string path, ImportSettings settings)
    {
        if (settings == null) settings = new ImportSettings();
        ImportResult result = new ImportResult { sourcePath = path };

        if (!TryRead(path, out Document document, out string error))
        {
            Error(result.issues, error);
            return result;
        }

        result.levelName = DisplayName(document, path);
        string assetName = AssetName(result.levelName);

        CatColorPalette palette = settings.Palette;
        if (palette == null)
        {
            Error(result.issues, "No CatColorPalette. Assign a CatPuzzleConfig with a palette, or run Cat Puzzle/Create Default Assets.");
            return result;
        }

        // Colours are the one thing that cannot be filled in later, so a file with an unknown one is
        // refused whole rather than imported with the wrong cats in it.
        Dictionary<string, int> colors = ResolveColors(document, palette, result.issues);
        if (colors == null) return result;

        result.grid = ResolveGrid(document, settings, assetName, out bool gridWasCreated);
        result.gridWasCreated = gridWasCreated;

        CatLevelData level = ResolveLevel(assetName, result.levelName, settings.overwriteExistingLevel, out bool replaced);
        Undo.RecordObject(level, "Import Cat Level");
        level.levelName = result.levelName;
        level.grid = result.grid;
        // A re-import is a content update; the timer was tuned in Unity and is not in the JSON.
        if (!replaced) level.levelTime = settings.levelTime;
        level.cats = BuildCats(document, colors);
        level.holes = BuildHoles(document, colors, result.issues);
        WarnAboutGates(document, result.issues);
        EditorUtility.SetDirty(level);

        result.level = level;
        result.levelWasReplaced = replaced;
        result.addedToCollection = AddToCollection(settings.collection, level);
        result.issues.AddRange(CatLevelValidator.Validate(level, palette));
        return result;
    }

    /// <summary>
    /// The web tool writes colour names ("Red"); levels store palette ids. Names are matched against
    /// <see cref="CatColorEntry.displayName"/>, so a colour renamed on one side has to be renamed on
    /// the other. Returns null when any name is unknown, having reported which.
    /// </summary>
    private static Dictionary<string, int> ResolveColors(Document document, CatColorPalette palette, List<CatLevelValidator.Issue> issues)
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
        Error(issues, $"Palette '{palette.name}' has no colour named {names}. Add {(unknown.Count == 1 ? "it" : "them")} to the palette, " +
                      "or rename so both sides agree, then import again.");
        return null;
    }

    /// <summary>
    /// Finds or files the board shape. The JSON already holds the post flood fill mask, the same one
    /// <c>GridCreatorTool</c> saves, so it inverts straight into <c>wallCells</c> with nothing to redo.
    /// </summary>
    private static GridData ResolveGrid(Document document, ImportSettings settings, string assetName, out bool created)
    {
        created = false;

        bool[] walls = new bool[document.grid.playableCells.Length];
        for (int i = 0; i < walls.Length; i++) walls[i] = !document.grid.playableCells[i];

        if (settings.reuseMatchingGrid)
        {
            GridData match = FindMatchingGrid(document.grid, walls);
            if (match != null) return match;
        }

        CatPuzzleAssetCreator.EnsureFolder(CatPuzzleAssetCreator.GridFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{CatPuzzleAssetCreator.GridFolder}/{assetName} Grid.asset");

        GridData grid = ScriptableObject.CreateInstance<GridData>();
        grid.Initialize(document.grid.width, document.grid.length);
        if (document.grid.cellSize > 0f) grid.cellSize = document.grid.cellSize;
        grid.catParentHeight = settings.catParentHeight;
        grid.holeParentHeight = settings.holeParentHeight;

        // Both sides index a flat array as z * width + x, so the mask copies across as it stands.
        for (int i = 0; i < walls.Length; i++) grid.gridData.wallCells[i] = walls[i];

        AssetDatabase.CreateAsset(grid, path);
        EditorUtility.SetDirty(grid);
        created = true;
        return grid;
    }

    /// <summary>
    /// A board shape is meant to back many levels, so an import looks for the grid it would have
    /// created before filing another copy of it.
    /// </summary>
    private static GridData FindMatchingGrid(GridSection section, bool[] walls)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:GridData"))
        {
            GridData candidate = AssetDatabase.LoadAssetAtPath<GridData>(AssetDatabase.GUIDToAssetPath(guid));
            if (candidate == null) continue;
            if (candidate.gridWidth != section.width || candidate.gridLength != section.length) continue;
            // A different cell size is a different board, even where the shape lines up.
            if (section.cellSize > 0f && !Mathf.Approximately(candidate.cellSize, section.cellSize)) continue;

            candidate.EnsureArrays();
            bool same = true;
            for (int z = 0; z < section.length && same; z++)
                for (int x = 0; x < section.width && same; x++)
                    same = candidate.IsWall(x, z) == walls[z * section.width + x];

            if (same) return candidate;
        }
        return null;
    }

    /// <summary>Reuses the asset an earlier import left behind, wherever it has been filed since.</summary>
    private static CatLevelData ResolveLevel(string assetName, string displayName, bool overwrite, out bool replaced)
    {
        replaced = false;

        if (overwrite)
        {
            CatLevelData existing = FindLevel(assetName, displayName);
            if (existing != null)
            {
                replaced = true;
                return existing;
            }
        }

        CatPuzzleAssetCreator.EnsureFolder(CatPuzzleAssetCreator.LevelFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{CatPuzzleAssetCreator.LevelFolder}/{assetName}.asset");
        CatLevelData level = ScriptableObject.CreateInstance<CatLevelData>();
        AssetDatabase.CreateAsset(level, path);
        return level;
    }

    private static CatLevelData FindLevel(string assetName, string displayName)
    {
        CatLevelData direct = AssetDatabase.LoadAssetAtPath<CatLevelData>($"{CatPuzzleAssetCreator.LevelFolder}/{assetName}.asset");
        if (direct != null) return direct;

        // The asset may have been renamed or moved after the last import; the level name still ties it back.
        foreach (string guid in AssetDatabase.FindAssets("t:CatLevelData"))
        {
            CatLevelData candidate = AssetDatabase.LoadAssetAtPath<CatLevelData>(AssetDatabase.GUIDToAssetPath(guid));
            if (candidate == null) continue;
            if (candidate.name == assetName) return candidate;
            if (!string.IsNullOrEmpty(displayName) && candidate.levelName == displayName) return candidate;
        }
        return null;
    }

    /// <summary>
    /// Gates are authored in the web tool but have no Unity counterpart yet, so they are dropped
    /// on import. Silently losing authored content would be worse than saying so plainly.
    /// </summary>
    private static void WarnAboutGates(Document document, List<CatLevelValidator.Issue> issues)
    {
        if (document.gates == null || document.gates.Length == 0) return;

        int queued = 0;
        foreach (GateEntry gate in document.gates)
            if (gate != null && gate.cats != null) queued += gate.cats.Length;

        Warning(issues, $"The file has {document.gates.Length} gate(s) holding {queued} cat(s). Unity has no gate yet, " +
                        "so they were not imported and those cats are missing from this level.");
    }

    private static List<CatPlacement> BuildCats(Document document, Dictionary<string, int> colors)
    {
        List<CatPlacement> cats = new List<CatPlacement>();
        if (document.cats == null) return cats;

        foreach (CatEntry entry in document.cats)
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
    private static List<CatHolePlacement> BuildHoles(Document document, Dictionary<string, int> colors, List<CatLevelValidator.Issue> issues)
    {
        List<CatHolePlacement> holes = new List<CatHolePlacement>();
        if (document.holes == null) return holes;

        for (int i = 0; i < document.holes.Length; i++)
        {
            HoleEntry entry = document.holes[i];
            if (entry == null) continue;

            List<Vector2Int> cells = new List<Vector2Int>();
            if (entry.cells != null)
                foreach (HoleCellEntry cell in entry.cells)
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
    private static void CheckShape(HoleEntry entry, List<Vector2Int> cells, int number, List<CatLevelValidator.Issue> issues)
    {
        if (CountIslands(cells) > 1)
            Warning(issues, $"Hole {number} is drawn as {CountIslands(cells)} separate islands. It builds as one hole covering all of them, " +
                            "so its cats all count against the same capacity.");

        // Unity has no separate capacity field: a CatHolePlacement's cap is implicitly its cell
        // count, one cat per cell (see CatHole.CellCount). The web tool's capacity is free-standing,
        // so a value that does not match what Unity will actually enforce is worth flagging.
        if (entry.capacity > 0 && entry.capacity != cells.Count)
            Warning(issues, $"Hole {number} was authored with capacity {entry.capacity}, but Unity gives every hole one slot per cell " +
                            $"({cells.Count} here) and does not track capacity separately, so it will hold {cells.Count}.");

        if (entry.cells == null) return;
        HashSet<Vector2Int> shape = new HashSet<Vector2Int>(cells);

        foreach (HoleCellEntry cell in entry.cells)
        {
            if (cell == null || string.IsNullOrEmpty(cell.holeType)) continue;
            Vector2Int position = new Vector2Int(cell.x, cell.z);
            CatHoleType expected = CatHoleBuilder.GetHoleType(CatHoleBuilder.GetConnections(position, shape));

            if (!Enum.TryParse(cell.holeType, true, out CatHoleType authored))
                Warning(issues, $"Hole {number} cell {position} names an unknown piece '{cell.holeType}'. It builds as {expected}.");
            else if (authored != expected)
                Warning(issues, $"Hole {number} cell {position} was exported as {authored} but its neighbours make it {expected}. It builds as {expected}.");
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

    private static bool AddToCollection(LevelData collection, CatLevelData level)
    {
        if (collection == null || level == null || collection.levels.Contains(level)) return false;
        Undo.RecordObject(collection, "Add Imported Level");
        collection.levels.Add(level);
        EditorUtility.SetDirty(collection);
        return true;
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

    private static IEnumerable<string> ColorNames(Document document)
    {
        if (document.cats != null)
            foreach (CatEntry cat in document.cats)
                if (cat != null) yield return Key(cat.color);

        if (document.holes != null)
            foreach (HoleEntry hole in document.holes)
                if (hole != null) yield return Key(hole.color);
    }

    private static string Key(string name) => (name ?? string.Empty).Trim();

    private static string DisplayName(Document document, string path)
    {
        string name = Key(document.levelName);
        return string.IsNullOrEmpty(name) ? Path.GetFileNameWithoutExtension(path) : name;
    }

    /// <summary>The level name as a file name, since the web tool does not restrict what can be typed.</summary>
    private static string AssetName(string levelName)
    {
        string name = levelName;
        foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        name = name.Trim();
        return string.IsNullOrEmpty(name) ? "Imported Cat Level" : name;
    }

    private static void Error(List<CatLevelValidator.Issue> issues, string message) =>
        issues.Add(new CatLevelValidator.Issue(CatLevelValidator.Severity.Error, message));

    private static void Warning(List<CatLevelValidator.Issue> issues, string message) =>
        issues.Add(new CatLevelValidator.Issue(CatLevelValidator.Severity.Warning, message));
    #endregion
}
#endif
