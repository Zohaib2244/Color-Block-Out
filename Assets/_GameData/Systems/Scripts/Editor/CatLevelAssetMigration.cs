#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot conversion of the level and grid content that was authored while <c>CatLevelData</c> and
/// <c>GridData</c> were ScriptableObjects. Both are plain data now, so those .asset files can no
/// longer be loaded at all - Unity has no serializable type behind them any more. This reads their
/// YAML off disk instead and writes each one out as the JSON the game now loads, next to the asset
/// it came from, then repoints every <see cref="LevelData"/> collection at the new files.
///
/// It never deletes anything on its own: the old assets are left in place and reported, so the
/// conversion can be checked before they go.
/// </summary>
public static class CatLevelAssetMigration
{
    [MenuItem("Cat Puzzle/Migrate Level Assets To JSON")]
    public static void Migrate()
    {
        CatPuzzleConfig config = CatPuzzleAssetCreator.FindConfig();
        if (config == null)
        {
            EditorUtility.DisplayDialog("Migrate Level Assets",
                "No CatPuzzleConfig found. Run Cat Puzzle/Create Default Assets first, so colours can be named.", "OK");
            return;
        }

        string gridScript = ScriptGuid("GridData");
        string levelScript = ScriptGuid("CatLevelData");
        string collectionScript = ScriptGuid("LevelData");
        if (gridScript == null || levelScript == null || collectionScript == null)
        {
            EditorUtility.DisplayDialog("Migrate Level Assets", "Could not find the GridData / CatLevelData / LevelData scripts.", "OK");
            return;
        }

        List<YamlAsset> assets = ReadAllAssets();
        List<YamlAsset> gridAssets = assets.Where(asset => asset.ScriptGuid == gridScript).ToList();
        List<YamlAsset> levelAssets = assets.Where(asset => asset.ScriptGuid == levelScript).ToList();
        List<YamlAsset> collectionAssets = assets.Where(asset => asset.ScriptGuid == collectionScript).ToList();

        if (gridAssets.Count == 0 && levelAssets.Count == 0)
        {
            EditorUtility.DisplayDialog("Migrate Level Assets", "Nothing left to migrate: no GridData or CatLevelData assets found.", "OK");
            return;
        }

        // Deliberately not batched with StartAssetEditing: each file has to come back as a
        // TextAsset the moment it is written, so a collection can be repointed at it below.
        Dictionary<string, GridData> gridsByGuid = WriteGrids(gridAssets, config);
        Dictionary<string, string> levelPathsByGuid = WriteLevels(levelAssets, gridsByGuid, config, out int failed);
        AssetDatabase.Refresh();

        int repointed = RepointCollections(collectionAssets, levelPathsByGuid);
        AssetDatabase.SaveAssets();

        string summary = $"Migrated {gridsByGuid.Count} grid(s) and {levelPathsByGuid.Count} level(s) to JSON" +
                         (failed > 0 ? $", {failed} could not be converted (see the console)" : string.Empty) +
                         $".\nRepointed {repointed} collection(s) at the new files.";
        Debug.Log(summary);
        OfferToDelete(summary, gridAssets, levelAssets, levelPathsByGuid, gridsByGuid);
    }

    #region Writing
    private static Dictionary<string, GridData> WriteGrids(List<YamlAsset> gridAssets, CatPuzzleConfig config)
    {
        Dictionary<string, GridData> byGuid = new Dictionary<string, GridData>();

        foreach (YamlAsset asset in gridAssets)
        {
            GridData grid = ParseGrid(asset, config);
            if (grid == null)
            {
                Debug.LogError($"Could not read the board out of {asset.path}.");
                continue;
            }

            byGuid[asset.guid] = grid;
            string path = JsonPathFor(asset);
            if (CatLevelFiles.WriteGrid(grid, path, out string error) == null)
                Debug.LogError($"Could not write {path}: {error}");
        }
        return byGuid;
    }

    private static Dictionary<string, string> WriteLevels(List<YamlAsset> levelAssets, Dictionary<string, GridData> gridsByGuid,
        CatPuzzleConfig config, out int failed)
    {
        Dictionary<string, string> pathsByGuid = new Dictionary<string, string>();
        failed = 0;

        foreach (YamlAsset asset in levelAssets)
        {
            CatLevelData level = ParseLevel(asset, gridsByGuid, out string error);
            if (level == null)
            {
                Debug.LogError($"Could not convert {asset.path}: {error}");
                failed++;
                continue;
            }

            string path = JsonPathFor(asset);
            if (CatLevelFiles.WriteLevel(level, config, path, out string writeError) == null)
            {
                Debug.LogError($"Could not write {path}: {writeError}");
                failed++;
                continue;
            }

            pathsByGuid[asset.guid] = path;
        }
        return pathsByGuid;
    }

    /// <summary>
    /// A collection's old list is object references to level assets, which no longer load, so the
    /// order is read straight out of its YAML and turned back into the JSON files it now wants.
    /// </summary>
    private static int RepointCollections(List<YamlAsset> collectionAssets, Dictionary<string, string> levelPathsByGuid)
    {
        int repointed = 0;

        foreach (YamlAsset asset in collectionAssets)
        {
            LevelData collection = AssetDatabase.LoadAssetAtPath<LevelData>(asset.path);
            if (collection == null)
            {
                Debug.LogError($"Could not load the collection at {asset.path}.");
                continue;
            }

            List<string> order = ReferencedGuids(asset, "levels");
            if (order.Count == 0) continue;

            Undo.RecordObject(collection, "Migrate Play Order");
            collection.levelFiles = new List<TextAsset>();
            int missing = 0;

            foreach (string levelGuid in order)
            {
                if (!levelPathsByGuid.TryGetValue(levelGuid, out string path))
                {
                    missing++;
                    continue;
                }

                TextAsset file = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (file != null) collection.levelFiles.Add(file);
                else missing++;
            }

            collection.ClearCache();
            EditorUtility.SetDirty(collection);
            repointed++;

            string note = $"'{collection.name}': {collection.levelFiles.Count} of {order.Count} levels repointed at their JSON files.";
            if (missing > 0) Debug.LogWarning($"{note} {missing} could not be matched and were dropped from the play order.", collection);
            else Debug.Log(note, collection);
        }
        return repointed;
    }

    private static void OfferToDelete(string summary, List<YamlAsset> gridAssets, List<YamlAsset> levelAssets,
        Dictionary<string, string> levelPathsByGuid, Dictionary<string, GridData> gridsByGuid)
    {
        List<string> converted = new List<string>();
        converted.AddRange(levelAssets.Where(asset => levelPathsByGuid.ContainsKey(asset.guid)).Select(asset => asset.path));
        converted.AddRange(gridAssets.Where(asset => gridsByGuid.ContainsKey(asset.guid)).Select(asset => asset.path));
        if (converted.Count == 0) return;

        bool delete = EditorUtility.DisplayDialog("Migrate Level Assets",
            $"{summary}\n\nThe {converted.Count} old .asset file(s) they came from cannot be loaded any more. Delete them now, " +
            "or keep them until the JSON has been checked?",
            "Delete Old Assets", "Keep Them");

        if (!delete)
        {
            Debug.Log($"Kept {converted.Count} old level/grid asset(s). Run the migration again to be offered this once more, " +
                      "or delete them by hand once the JSON looks right.");
            return;
        }

        List<string> failedToDelete = new List<string>();
        AssetDatabase.DeleteAssets(converted.ToArray(), failedToDelete);
        AssetDatabase.Refresh();
        Debug.Log($"Deleted {converted.Count - failedToDelete.Count} old level/grid asset(s)." +
                  (failedToDelete.Count > 0 ? $" {failedToDelete.Count} could not be deleted." : string.Empty));
    }
    #endregion

    #region Parsing
    /// <summary>One .asset file, read as text because nothing can load it as an object any more.</summary>
    private sealed class YamlAsset
    {
        public string path;
        public string guid;
        public string name;
        public string[] lines;

        public string ScriptGuid
        {
            get
            {
                foreach (string line in lines)
                {
                    if (!line.TrimStart().StartsWith("m_Script:", StringComparison.Ordinal)) continue;
                    Match match = Reference.Match(line);
                    return match.Success ? match.Groups["guid"].Value : null;
                }
                return null;
            }
        }
    }

    private static readonly Regex Reference = new Regex(@"guid:\s*(?<guid>[0-9a-fA-F]{32})");
    private static readonly Regex Vector2 = new Regex(@"x:\s*(?<x>-?\d+).*?y:\s*(?<y>-?\d+)");

    private static List<YamlAsset> ReadAllAssets()
    {
        List<YamlAsset> assets = new List<YamlAsset>();

        foreach (string systemPath in Directory.GetFiles(Application.dataPath, "*.asset", SearchOption.AllDirectories))
        {
            string path = "Assets" + systemPath.Substring(Application.dataPath.Length).Replace('\\', '/');
            string[] lines;
            try
            {
                lines = File.ReadAllLines(systemPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read {path}: {exception.Message}");
                continue;
            }

            // Only the small, single-object MonoBehaviour assets this migration is about.
            if (lines.Length < 4 || Array.FindIndex(lines, line => line.StartsWith("MonoBehaviour:", StringComparison.Ordinal)) < 0) continue;

            assets.Add(new YamlAsset
            {
                path = path,
                guid = AssetDatabase.AssetPathToGUID(path),
                name = Scalar(lines, "m_Name") ?? Path.GetFileNameWithoutExtension(path),
                lines = lines
            });
        }
        return assets;
    }

    private static GridData ParseGrid(YamlAsset asset, CatPuzzleConfig config)
    {
        int width = ParseInt(Scalar(asset.lines, "gridWidth"), 0);
        int length = ParseInt(Scalar(asset.lines, "gridLength"), 0);
        if (width <= 0 || length <= 0) return null;

        GridData grid = new GridData(width, length)
        {
            gridName = asset.name,
            cellSize = ParseFloat(Scalar(asset.lines, "cellSize"), 0.57f),
            catParentHeight = ParseFloat(Scalar(asset.lines, "catParentHeight"), config.catParentHeight),
            holeParentHeight = ParseFloat(Scalar(asset.lines, "holeParentHeight"), config.holeParentHeight)
        };

        // Unity writes a bool[] as one hex string, two characters per entry.
        string packed = Scalar(asset.lines, "wallCells");
        if (!string.IsNullOrEmpty(packed))
            for (int i = 0; i < grid.gridData.wallCells.Length && (i * 2) + 1 < packed.Length; i++)
                grid.gridData.wallCells[i] = packed.Substring(i * 2, 2) != "00";

        return grid;
    }

    private static CatLevelData ParseLevel(YamlAsset asset, Dictionary<string, GridData> gridsByGuid, out string error)
    {
        error = null;

        string gridGuid = null;
        foreach (string line in asset.lines)
        {
            if (!line.StartsWith("  grid:", StringComparison.Ordinal)) continue;
            Match match = Reference.Match(line);
            if (match.Success) gridGuid = match.Groups["guid"].Value;
            break;
        }

        if (gridGuid == null)
        {
            error = "it has no grid reference.";
            return null;
        }
        if (!gridsByGuid.TryGetValue(gridGuid, out GridData grid))
        {
            error = $"its grid asset ({gridGuid}) is missing, so the board could not be recovered.";
            return null;
        }

        CatLevelData level = new CatLevelData
        {
            levelName = Scalar(asset.lines, "levelName") ?? asset.name,
            levelTime = ParseInt(Scalar(asset.lines, "levelTime"), 60),
            grid = grid.Clone()
        };

        ReadContent(asset.lines, level);
        return level;
    }

    /// <summary>
    /// Walks the cats and holes sections. Indentation carries the structure: a list entry starts at
    /// two spaces, its fields at four, and a hole's offsets are the four-space entries after them.
    /// </summary>
    private static void ReadContent(string[] lines, CatLevelData level)
    {
        const int None = 0, Cats = 1, Holes = 2;
        int section = None;
        CatPlacement cat = null;
        CatHolePlacement hole = null;
        bool inOffsets = false;

        foreach (string raw in lines)
        {
            string line = raw.TrimEnd();
            if (line.Length == 0) continue;

            // A new two-space field ends whatever section was open.
            if (line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("   ", StringComparison.Ordinal) &&
                !line.StartsWith("  - ", StringComparison.Ordinal))
            {
                string field = line.Substring(2);
                section = field.StartsWith("cats:", StringComparison.Ordinal) ? Cats
                        : field.StartsWith("holes:", StringComparison.Ordinal) ? Holes
                        : None;
                cat = null;
                hole = null;
                inOffsets = false;
                continue;
            }

            if (section == None) continue;
            string trimmed = line.Trim();

            if (line.StartsWith("  - ", StringComparison.Ordinal))
            {
                // "- colorId: 0", or the pre-rename "- color: 0".
                int colorId = ParseInt(AfterColon(trimmed.Substring(2)), 0);
                inOffsets = false;

                if (section == Cats)
                {
                    cat = new CatPlacement { colorId = colorId };
                    level.cats.Add(cat);
                }
                else
                {
                    hole = new CatHolePlacement { colorId = colorId, offsets = new List<Vector2Int>() };
                    level.holes.Add(hole);
                }
                continue;
            }

            if (section == Cats && cat != null && trimmed.StartsWith("cell:", StringComparison.Ordinal))
            {
                cat.cell = ParseVector(trimmed);
                continue;
            }

            if (section != Holes || hole == null) continue;

            if (trimmed.StartsWith("origin:", StringComparison.Ordinal))
            {
                hole.origin = ParseVector(trimmed);
                inOffsets = false;
            }
            else if (trimmed.StartsWith("offsets:", StringComparison.Ordinal))
            {
                inOffsets = true;
            }
            else if (inOffsets && trimmed.StartsWith("- {", StringComparison.Ordinal))
            {
                hole.offsets.Add(ParseVector(trimmed));
            }
        }

        // A hole with no offsets still covers its own cell.
        foreach (CatHolePlacement placement in level.holes)
            if (placement.offsets.Count == 0) placement.offsets.Add(Vector2Int.zero);
    }

    /// <summary>The ordered asset GUIDs of an object-reference list field, e.g. a collection's levels.</summary>
    private static List<string> ReferencedGuids(YamlAsset asset, string fieldName)
    {
        List<string> guids = new List<string>();
        bool inField = false;

        foreach (string raw in asset.lines)
        {
            string line = raw.TrimEnd();
            if (line.Length == 0) continue;

            if (line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("  - ", StringComparison.Ordinal) &&
                !line.StartsWith("   ", StringComparison.Ordinal))
            {
                inField = line.Substring(2).StartsWith(fieldName + ":", StringComparison.Ordinal);
                continue;
            }

            if (!inField || !line.StartsWith("  - ", StringComparison.Ordinal)) continue;
            Match match = Reference.Match(line);
            if (match.Success) guids.Add(match.Groups["guid"].Value);
        }
        return guids;
    }
    #endregion

    #region Helpers
    /// <summary>The value of a <c>name: value</c> line, wherever it is indented.</summary>
    private static string Scalar(string[] lines, string fieldName)
    {
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith(fieldName + ":", StringComparison.Ordinal)) continue;
            return trimmed.Substring(fieldName.Length + 1).Trim();
        }
        return null;
    }

    private static string AfterColon(string text)
    {
        int colon = text.IndexOf(':');
        return colon < 0 ? text : text.Substring(colon + 1).Trim();
    }

    private static Vector2Int ParseVector(string text)
    {
        Match match = Vector2.Match(text);
        return match.Success
            ? new Vector2Int(ParseInt(match.Groups["x"].Value, 0), ParseInt(match.Groups["y"].Value, 0))
            : Vector2Int.zero;
    }

    private static int ParseInt(string text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

    private static float ParseFloat(string text, float fallback) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;

    /// <summary>The JSON goes next to the asset it came from, so the folder layout is kept.</summary>
    private static string JsonPathFor(YamlAsset asset)
    {
        string folder = Path.GetDirectoryName(asset.path).Replace('\\', '/');
        return CatLevelFiles.PathIn(folder, Path.GetFileNameWithoutExtension(asset.path));
    }

    private static string ScriptGuid(string scriptName)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{scriptName} t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == scriptName) return guid;
        }
        return null;
    }
    #endregion
}
#endif
