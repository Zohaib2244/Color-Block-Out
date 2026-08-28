#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Filing for the JSON that levels and boards now live in. Every authoring tool writes through
/// here, so a level saved from the Cat Level Editor and one exported from the web tool land in the
/// same place, in the same format, and are picked up as <see cref="TextAsset"/>s the same way.
/// </summary>
public static class CatLevelFiles
{
    public const string LevelExtension = ".json";

    /// <summary>Where a level file goes when the tool files it automatically.</summary>
    public static string LevelFolder => CatPuzzleAssetCreator.LevelFolder;

    /// <summary>Where a board file goes when the tool files it automatically.</summary>
    public static string GridFolder => CatPuzzleAssetCreator.GridFolder;

    #region Writing
    /// <summary>
    /// Writes a level to <paramref name="assetPath"/> and returns the file as an asset. Passing null
    /// or an empty path files a new one under <see cref="LevelFolder"/>, named after the level.
    /// </summary>
    public static TextAsset WriteLevel(CatLevelData level, CatPuzzleConfig config, string assetPath, out string error)
    {
        error = null;
        if (level == null)
        {
            error = "No level to write.";
            return null;
        }

        string json = CatLevelJson.ToJson(level, config, out List<CatLevelValidator.Issue> issues);
        if (json == null)
        {
            error = CatLevelValidator.Describe(issues);
            return null;
        }
        if (CatLevelValidator.HasErrors(issues))
        {
            error = CatLevelValidator.Describe(issues);
            return null;
        }

        if (string.IsNullOrEmpty(assetPath)) assetPath = UniquePath(LevelFolder, level.DisplayName);
        return Write(assetPath, json);
    }

    /// <summary>Writes a board on its own, so a shape can be drawn once and reused across levels.</summary>
    public static TextAsset WriteGrid(GridData grid, string assetPath, out string error)
    {
        error = null;
        if (grid == null)
        {
            error = "No board to write.";
            return null;
        }

        string json = CatLevelJson.GridToJson(grid);
        if (json == null)
        {
            error = "The board could not be written.";
            return null;
        }

        if (string.IsNullOrEmpty(assetPath)) assetPath = UniquePath(GridFolder, grid.gridName);
        return Write(assetPath, json);
    }

    /// <summary>Writes the text and brings the file into the project, replacing whatever was there.</summary>
    public static TextAsset Write(string assetPath, string json)
    {
        CatPuzzleAssetCreator.EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
        File.WriteAllText(ToSystemPath(assetPath), json);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
    }
    #endregion

    #region Paths
    /// <summary>A free path in <paramref name="folder"/> for something called <paramref name="name"/>.</summary>
    public static string UniquePath(string folder, string name)
    {
        CatPuzzleAssetCreator.EnsureFolder(folder);
        return AssetDatabase.GenerateUniqueAssetPath($"{folder}/{SafeFileName(name)}{LevelExtension}");
    }

    /// <summary>The path a file called <paramref name="name"/> would take, whether or not it exists.</summary>
    public static string PathIn(string folder, string name) => $"{folder}/{SafeFileName(name)}{LevelExtension}";

    /// <summary>Level names are typed freely on the web, so anything a file name cannot hold is replaced.</summary>
    public static string SafeFileName(string name)
    {
        string safe = name ?? string.Empty;
        foreach (char invalid in Path.GetInvalidFileNameChars()) safe = safe.Replace(invalid, '_');
        safe = safe.Trim();
        return string.IsNullOrEmpty(safe) ? "Untitled" : safe;
    }

    public static string ToSystemPath(string assetPath) =>
        Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
    #endregion

    #region Finding
    /// <summary>Every JSON file under a project folder, in name order.</summary>
    public static List<TextAsset> FindIn(string folder)
    {
        List<TextAsset> files = new List<TextAsset>();
        if (!AssetDatabase.IsValidFolder(folder)) return files;

        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(LevelExtension, System.StringComparison.OrdinalIgnoreCase)) continue;
            TextAsset file = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (file != null) files.Add(file);
        }

        files.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
        return files;
    }

    /// <summary>
    /// Expands dropped paths into the JSON files under them, so a whole folder of exports can be
    /// dropped at once. Paths are from the OS, not the project, since exports arrive from a browser.
    /// </summary>
    public static List<string> CollectJsonFiles(IEnumerable<string> paths)
    {
        List<string> files = new List<string>();
        if (paths == null) return files;

        foreach (string path in paths)
        {
            if (string.IsNullOrEmpty(path)) continue;

            if (Directory.Exists(path))
            {
                foreach (string found in Directory.GetFiles(path, "*" + LevelExtension, SearchOption.AllDirectories))
                    if (!files.Contains(found)) files.Add(found);
            }
            else if (File.Exists(path) && string.Equals(Path.GetExtension(path), LevelExtension, System.StringComparison.OrdinalIgnoreCase))
            {
                if (!files.Contains(path)) files.Add(path);
            }
        }

        files.Sort(System.StringComparer.OrdinalIgnoreCase);
        return files;
    }
    #endregion
}
#endif
