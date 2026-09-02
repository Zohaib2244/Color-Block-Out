#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reverse of WebLevelExporter: reads the JSON files under level-editor/public/exported-levels
/// (the same folder the exporter writes to, and the same schema the web editor imports/exports)
/// and writes them back into the existing GridData/CatLevelData assets under
/// Data/Grids/HassamLevels and Data/Levels/HassamLevels.
///
/// Assets are matched by name and updated in place rather than recreated, so their GUIDs — and
/// therefore the references already held by HassamLevels.asset (a LevelData) — stay valid.
/// A JSON file whose levelName doesn't match any existing CatLevelData creates a new asset and
/// appends it to HassamLevels.asset; a grid id that doesn't match an existing GridData creates a
/// new grid asset the same way.
/// </summary>
public static class WebLevelImporter
{
    private const string InputRelativeToProjectRoot = "level-editor/public/exported-levels";
    private const string GridFolder = "Assets/_GameData/Systems/Data/Grids/HassamLevels";
    private const string LevelFolder = "Assets/_GameData/Systems/Data/Levels/HassamLevels";
    private const string CollectionPath = "Assets/_GameData/Systems/Data/Levels/HassamLevels/HassamLevels.asset";

    [Serializable]
    private class GridDto
    {
        public string id;
        public int width;
        public int length;
        public float cellSize;
        public bool[] playableCells;
    }

    [Serializable]
    private class CatDto
    {
        public string color;
        public int x;
        public int z;
    }

    [Serializable]
    private class HoleCellDto
    {
        public int x;
        public int z;
        public string holeType;
        public int rotationQuarterTurns;
    }

    [Serializable]
    private class HoleDto
    {
        public string id;
        public string color;
        public int capacity;
        public HoleCellDto[] cells;
    }

    [Serializable]
    private class LevelJsonDto
    {
        public int formatVersion;
        public string levelName;
        public GridDto grid;
        public CatDto[] cats;
        public HoleDto[] holes;
    }

    [MenuItem("Cat Puzzle/Import Levels From Web Editor")]
    public static void ImportAll()
    {
        CatColorPalette palette = AssetDatabase.FindAssets("t:CatColorPalette")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CatColorPalette>)
            .FirstOrDefault(p => p != null);
        if (palette == null)
        {
            Debug.LogError("No CatColorPalette asset found; cannot map colour names back to ids.");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string inputDir = Path.Combine(projectRoot, InputRelativeToProjectRoot);
        if (!Directory.Exists(inputDir))
        {
            Debug.LogError($"{inputDir} does not exist. Export or place level JSON there first.");
            return;
        }

        string[] jsonPaths = Directory.GetFiles(inputDir, "*.json")
            .Where(p => !Path.GetFileName(p).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        CatPuzzleAssetCreator.EnsureFolder(GridFolder);
        CatPuzzleAssetCreator.EnsureFolder(LevelFolder);

        Dictionary<string, GridData> gridsById = AssetDatabase.FindAssets("t:GridData", new[] { GridFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GridData>)
            .Where(g => g != null)
            .ToDictionary(g => g.name, g => g);

        Dictionary<string, CatLevelData> levelsByName = AssetDatabase.FindAssets("t:CatLevelData", new[] { LevelFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CatLevelData>)
            .Where(l => l != null)
            .ToDictionary(l => l.name, l => l);

        int updated = 0, created = 0, failed = 0;
        List<CatLevelData> newlyCreated = new List<CatLevelData>();

        foreach (string path in jsonPaths)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            try
            {
                LevelJsonDto dto = JsonUtility.FromJson<LevelJsonDto>(File.ReadAllText(path));
                if (dto == null || dto.grid == null)
                {
                    Debug.LogWarning($"{fileName}: could not parse level JSON, skipping.");
                    failed++;
                    continue;
                }

                GridData grid = ResolveGrid(dto.grid, gridsById);
                CatLevelData level = ResolveLevel(fileName, levelsByName, out bool wasCreated);
                ApplyLevel(level, dto, grid, palette);

                if (wasCreated) { created++; newlyCreated.Add(level); }
                else updated++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{fileName}: import failed — {ex.Message}");
                failed++;
            }
        }

        if (newlyCreated.Count > 0) AppendToCollection(newlyCreated);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Imported from {inputDir}: {updated} level(s) updated, {created} created, {failed} failed.");
    }

    private static GridData ResolveGrid(GridDto dto, Dictionary<string, GridData> gridsById)
    {
        if (!gridsById.TryGetValue(dto.id, out GridData grid))
        {
            grid = ScriptableObject.CreateInstance<GridData>();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{GridFolder}/{dto.id}.asset");
            AssetDatabase.CreateAsset(grid, path);
            gridsById[dto.id] = grid;
        }

        Undo.RecordObject(grid, "Import Grid");
        grid.Initialize(dto.width, dto.length);
        grid.cellSize = dto.cellSize;
        for (int z = 0; z < dto.length; z++)
            for (int x = 0; x < dto.width; x++)
                grid.SetWall(x, z, !dto.playableCells[z * dto.width + x]);
        EditorUtility.SetDirty(grid);
        return grid;
    }

    private static CatLevelData ResolveLevel(string fileName, Dictionary<string, CatLevelData> levelsByName, out bool wasCreated)
    {
        if (levelsByName.TryGetValue(fileName, out CatLevelData level))
        {
            wasCreated = false;
            return level;
        }

        level = ScriptableObject.CreateInstance<CatLevelData>();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{LevelFolder}/{fileName}.asset");
        AssetDatabase.CreateAsset(level, path);
        levelsByName[fileName] = level;
        wasCreated = true;
        return level;
    }

    private static void ApplyLevel(CatLevelData level, LevelJsonDto dto, GridData grid, CatColorPalette palette)
    {
        Undo.RecordObject(level, "Import Level");
        level.levelName = dto.levelName;
        level.grid = grid;

        level.cats = dto.cats.Select(c => new CatPlacement
        {
            colorId = ColorId(palette, c.color),
            cell = new Vector2Int(c.x, c.z),
        }).ToList();

        level.holes = dto.holes.Select(h =>
        {
            List<Vector2Int> cells = h.cells.Select(c => new Vector2Int(c.x, c.z)).ToList();
            Vector2Int origin = new Vector2Int(cells.Min(c => c.x), cells.Min(c => c.y));
            return new CatHolePlacement
            {
                colorId = ColorId(palette, h.color),
                origin = origin,
                offsets = CatHoleBuilder.Normalise(cells.Select(c => c - origin)),
            };
        }).ToList();

        EditorUtility.SetDirty(level);
    }

    private static int ColorId(CatColorPalette palette, string name)
    {
        foreach (int id in palette.Ids())
            if (string.Equals(palette.GetName(id), name, StringComparison.OrdinalIgnoreCase))
                return id;
        Debug.LogWarning($"Colour '{name}' not found in the palette; defaulting to {palette.DefaultId}.");
        return palette.DefaultId;
    }

    private static void AppendToCollection(List<CatLevelData> newlyCreated)
    {
        LevelData collection = AssetDatabase.LoadAssetAtPath<LevelData>(CollectionPath);
        if (collection == null)
        {
            Debug.LogWarning($"{CollectionPath} not found; {newlyCreated.Count} new level(s) were created but not added to any collection.");
            return;
        }

        Undo.RecordObject(collection, "Append Imported Levels");
        collection.levels.AddRange(newlyCreated);
        EditorUtility.SetDirty(collection);
    }
}
#endif
