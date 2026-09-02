#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports every CatLevelData under Data/Levels/HassamLevels into the JSON schema
/// (formatVersion 3) consumed by the standalone web level editor at /level-editor,
/// so those levels can be reviewed/edited there without touching Unity.
///
/// Hole `holeType`/`rotationQuarterTurns` are not stored on CatHolePlacement — they are
/// derived at build time by CatHoleBuilder. This exporter calls those exact methods
/// (GetConnections/GetHoleType/GetQuarterTurns) so the exported values match what the
/// game itself would build, rather than re-deriving the algorithm independently.
/// </summary>
public static class WebLevelExporter
{
    private const string FolderPath = "Assets/_GameData/Systems/Data/Levels/HassamLevels";
    private const string OutputRelativeToProjectRoot = "level-editor/public/exported-levels";

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
        public int formatVersion = 3;
        public string levelName;
        public GridDto grid;
        public CatDto[] cats;
        public HoleDto[] holes;
    }

    [Serializable]
    private class ManifestDto
    {
        public string[] files;
    }

    [MenuItem("Cat Puzzle/Export Levels To Web Editor")]
    public static void ExportAll()
    {
        CatColorPalette palette = AssetDatabase.FindAssets("t:CatColorPalette")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CatColorPalette>)
            .FirstOrDefault(p => p != null);
        if (palette == null)
        {
            Debug.LogError("No CatColorPalette asset found; cannot map colour ids to names.");
            return;
        }

        CatPuzzleConfig config = AssetDatabase.FindAssets("t:CatPuzzleConfig")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CatPuzzleConfig>)
            .FirstOrDefault(c => c != null);
        if (config == null || config.holeConfiguration == null)
        {
            Debug.LogError("CatPuzzleConfig (with a HoleConfiguration assigned) is required to derive hole rotations.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:CatLevelData", new[] { FolderPath });
        string[] paths = guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string outputDir = Path.Combine(projectRoot, OutputRelativeToProjectRoot);
        Directory.CreateDirectory(outputDir);

        List<string> writtenFiles = new List<string>();
        int failed = 0;

        foreach (string path in paths)
        {
            CatLevelData level = AssetDatabase.LoadAssetAtPath<CatLevelData>(path);
            if (level == null) continue;

            if (level.grid == null)
            {
                Debug.LogWarning($"{path}: no GridData assigned, skipping.");
                failed++;
                continue;
            }

            LevelJsonDto dto = BuildDto(level, palette, config.holeConfiguration);
            string fileName = SanitizeFileName(Path.GetFileNameWithoutExtension(path)) + ".json";
            string json = JsonUtility.ToJson(dto, true);
            File.WriteAllText(Path.Combine(outputDir, fileName), json);
            writtenFiles.Add(fileName);
        }

        string manifestJson = JsonUtility.ToJson(new ManifestDto { files = writtenFiles.ToArray() }, true);
        File.WriteAllText(Path.Combine(outputDir, "manifest.json"), manifestJson);

        Debug.Log($"Exported {writtenFiles.Count} level(s) to {outputDir} ({failed} skipped).");
        EditorUtility.RevealInFinder(outputDir);
    }

    private static LevelJsonDto BuildDto(CatLevelData level, CatColorPalette palette, CatHoleConfiguration holeConfiguration)
    {
        GridData grid = level.grid;
        grid.EnsureArrays();

        bool[] playable = new bool[grid.gridWidth * grid.gridLength];
        for (int z = 0; z < grid.gridLength; z++)
            for (int x = 0; x < grid.gridWidth; x++)
                playable[grid.GetIndex(x, z)] = grid.IsPlayable(x, z);

        CatDto[] cats = level.cats
            .Where(c => c != null)
            .Select(c => new CatDto { color = ColorName(palette, c.colorId), x = c.cell.x, z = c.cell.y })
            .ToArray();

        HoleDto[] holes = level.holes
            .Where(h => h != null)
            .Select(h => BuildHoleDto(h, palette, holeConfiguration))
            .ToArray();

        return new LevelJsonDto
        {
            levelName = level.DisplayName,
            grid = new GridDto
            {
                id = grid.name,
                width = grid.gridWidth,
                length = grid.gridLength,
                cellSize = grid.cellSize,
                playableCells = playable,
            },
            cats = cats,
            holes = holes,
        };
    }

    private static HoleDto BuildHoleDto(CatHolePlacement placement, CatColorPalette palette, CatHoleConfiguration holeConfiguration)
    {
        // Deliberately NOT re-anchored via CatHoleBuilder.Normalise: that shifts offsets to the
        // shape's own bounding-box corner, which is a different anchor than placement.origin
        // whenever the two have drifted apart (e.g. a hole nudged in the scene). Using the raw
        // offsets keeps them addressed against the same origin used below, so exported absolute
        // cells match what's actually on the board.
        List<Vector2Int> offsets = placement.offsets ?? new List<Vector2Int> { Vector2Int.zero };
        HashSet<Vector2Int> shape = new HashSet<Vector2Int>(offsets);

        HoleCellDto[] cells = offsets.Select(offset =>
        {
            List<Direction> connections = CatHoleBuilder.GetConnections(offset, shape);
            CatHoleType type = CatHoleBuilder.GetHoleType(connections);
            CatHolePrefabData data = holeConfiguration.GetData(type);
            int quarterTurns = CatHoleBuilder.GetQuarterTurns(data, connections);
            Vector2Int cell = placement.origin + offset;
            return new HoleCellDto { x = cell.x, z = cell.y, holeType = type.ToString(), rotationQuarterTurns = quarterTurns };
        }).ToArray();

        return new HoleDto
        {
            id = Guid.NewGuid().ToString("N"),
            color = ColorName(palette, placement.colorId),
            capacity = offsets.Count, // matches CatHole.CellCount: one cat per covered cell
            cells = cells,
        };
    }

    private static string ColorName(CatColorPalette palette, int colorId) => palette.GetName(colorId);

    private static string SanitizeFileName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return name;
    }
}
#endif
