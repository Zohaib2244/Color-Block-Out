#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>One-shot check of every generated level under Data/Levels/HassamLevels against CatLevelValidator.</summary>
public static class HassamLevelValidatorTool
{
    private const string FolderPath = "Assets/_GameData/Systems/Data/Levels/HassamLevels";

    [MenuItem("Cat Puzzle/Validate Hassam Levels")]
    public static void ValidateAll()
    {
        CatColorPalette palette = AssetDatabase.FindAssets("t:CatColorPalette")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CatColorPalette>)
            .FirstOrDefault(p => p != null);

        string[] guids = AssetDatabase.FindAssets("t:CatLevelData", new[] { FolderPath });
        int errorLevels = 0, warningLevels = 0;

        foreach (string guid in guids.OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CatLevelData level = AssetDatabase.LoadAssetAtPath<CatLevelData>(path);
            if (level == null) continue;

            var issues = CatLevelValidator.Validate(level, palette);
            bool hasError = CatLevelValidator.HasErrors(issues);
            if (hasError) errorLevels++;
            else if (issues.Count > 0) warningLevels++;

            if (issues.Count > 0)
                Debug.Log($"{Path.GetFileNameWithoutExtension(path)}: {issues.Count} issue(s)\n{CatLevelValidator.Describe(issues)}", level);
        }

        Debug.Log($"Validated {guids.Length} levels under {FolderPath} — {errorLevels} with errors, {warningLevels} with warnings only, {guids.Length - errorLevels - warningLevels} clean.");
    }
}
#endif
