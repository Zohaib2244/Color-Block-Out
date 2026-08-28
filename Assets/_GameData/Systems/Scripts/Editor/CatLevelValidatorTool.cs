#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks every level file in the project - or just the ones in a collection's play order - against
/// <see cref="CatLevelValidator"/>, so a board that cannot be finished is found before it ships
/// rather than when someone plays it.
/// </summary>
public static class CatLevelValidatorTool
{
    [MenuItem("Cat Puzzle/Validate Level Files")]
    public static void ValidateAll()
    {
        CatPuzzleConfig config = CatPuzzleAssetCreator.FindConfig();
        if (config == null)
        {
            Debug.LogError("No CatPuzzleConfig found. Run Cat Puzzle/Create Default Assets first.");
            return;
        }

        List<TextAsset> files = CatLevelFiles.FindIn(CatLevelFiles.LevelFolder);
        if (files.Count == 0)
        {
            Debug.LogWarning($"No level files found under {CatLevelFiles.LevelFolder}.");
            return;
        }

        Report(files, config, CatLevelFiles.LevelFolder);
    }

    [MenuItem("Assets/Cat Puzzle/Validate Play Order", true)]
    private static bool ValidateSelectedCollectionEnabled() => Selection.activeObject is LevelData;

    [MenuItem("Assets/Cat Puzzle/Validate Play Order")]
    private static void ValidateSelectedCollection()
    {
        LevelData collection = Selection.activeObject as LevelData;
        if (collection == null) return;

        CatPuzzleConfig config = CatPuzzleAssetCreator.FindConfig();
        if (config == null)
        {
            Debug.LogError("No CatPuzzleConfig found. Run Cat Puzzle/Create Default Assets first.");
            return;
        }

        List<TextAsset> files = new List<TextAsset>();
        for (int i = 0; i < collection.Count; i++)
        {
            TextAsset file = collection.GetFile(i);
            if (file == null) Debug.LogError($"'{collection.name}' has no file in slot {i + 1}.", collection);
            else files.Add(file);
        }

        Report(files, config, $"'{collection.name}'");
    }

    private static void Report(List<TextAsset> files, CatPuzzleConfig config, string where)
    {
        int broken = 0, warned = 0;

        foreach (TextAsset file in files)
        {
            CatLevelJson.LevelResult result = CatLevelJson.ParseLevel(file.text, file.name, config);

            if (result.HasErrors) broken++;
            else if (result.issues.Count > 0) warned++;

            if (result.issues.Count > 0)
            {
                string message = $"{file.name}: {result.issues.Count} issue(s)\n{result.Describe()}";
                if (result.HasErrors) Debug.LogError(message, file);
                else Debug.LogWarning(message, file);
            }
        }

        Debug.Log($"Validated {files.Count} level file(s) in {where} — {broken} with errors, {warned} with warnings only, " +
                  $"{files.Count - broken - warned} clean.");
    }
}
#endif
