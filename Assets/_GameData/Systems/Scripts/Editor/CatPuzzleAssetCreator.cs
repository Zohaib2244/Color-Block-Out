#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates (or repairs) the two shared assets the cat puzzle needs: the hole prefab
/// lookup and the project wide <see cref="CatPuzzleConfig"/> in Resources.
/// </summary>
public static class CatPuzzleAssetCreator
{
    private const string ConfigFolder = "Assets/Resources";
    private const string HoleConfigFolder = "Assets/_GameData/Systems/Scriptable Objects/MISC";
    private const string HolePrefabFolder = "Assets/_GameData/Systems/Prefabs/Holes";
    private const string GridPrefabFolder = "Assets/_GameData/Systems/Prefabs/UI/GridElements";
    private const string CatPrefabFolder = "Assets/_GameData/Systems/Prefabs/Cats";

    [MenuItem("Cat Puzzle/Create Default Assets")]
    public static void CreateDefaults()
    {
        CatHoleConfiguration holeConfiguration = CreateHoleConfiguration();
        CatPuzzleConfig config = CreatePuzzleConfig(holeConfiguration);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    private static CatHoleConfiguration CreateHoleConfiguration()
    {
        // Reuse whichever configuration the project already has rather than adding a second one.
        CatHoleConfiguration asset = null;
        foreach (string guid in AssetDatabase.FindAssets("t:CatHoleConfiguration"))
        {
            asset = AssetDatabase.LoadAssetAtPath<CatHoleConfiguration>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) break;
        }

        if (asset == null)
        {
            EnsureFolder(HoleConfigFolder);
            asset = ScriptableObject.CreateInstance<CatHoleConfiguration>();
            AssetDatabase.CreateAsset(asset, $"{HoleConfigFolder}/CatHoleConfiguration.asset");
        }

        asset.holePrefabs = new[]
        {
            Entry(CatHoleType.Isolated, "Isolated", new Direction[0]),
            Entry(CatHoleType.EndCap, "End Cap", new[] { Direction.Right }),
            Entry(CatHoleType.Straight, "Straight", new[] { Direction.Right, Direction.Left }),
            Entry(CatHoleType.Corner, "Corner", new[] { Direction.Right, Direction.Down }),
            Entry(CatHoleType.OneSide, "One Side", new[] { Direction.Right, Direction.Down, Direction.Left }),
            Entry(CatHoleType.Middle, "Middle", new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left })
        };
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static CatPuzzleConfig CreatePuzzleConfig(CatHoleConfiguration holeConfiguration)
    {
        string path = $"{ConfigFolder}/{CatPuzzleConfig.ResourcePath}.asset";
        CatPuzzleConfig config = AssetDatabase.LoadAssetAtPath<CatPuzzleConfig>(path);
        if (config == null)
        {
            EnsureFolder(ConfigFolder);
            config = ScriptableObject.CreateInstance<CatPuzzleConfig>();
            AssetDatabase.CreateAsset(config, path);
        }

        config.holeConfiguration = holeConfiguration;
        if (config.catPrefab == null) config.catPrefab = FindPrefab("Cat_01", CatPrefabFolder);
        if (config.cellPrefab == null) config.cellPrefab = FindPrefab("GridCell", GridPrefabFolder);
        if (config.straightWallPrefab == null) config.straightWallPrefab = FindPrefab("StraightWall", GridPrefabFolder);
        if (config.wallEndPrefab == null) config.wallEndPrefab = FindPrefab("WallEnd", GridPrefabFolder);
        if (config.cornerWallPrefab == null) config.cornerWallPrefab = FindPrefab("CornerWall", GridPrefabFolder);
        if (config.cellMaterialA == null) config.cellMaterialA = FindMaterial("GridCellMaterial_1");
        if (config.cellMaterialB == null) config.cellMaterialB = FindMaterial("GridCellMaterial_2");

        EditorUtility.SetDirty(config);
        return config;
    }

    private static CatHolePrefabData Entry(CatHoleType type, string prefabName, Direction[] openings) => new CatHolePrefabData
    {
        holeType = type,
        prefab = FindPrefab(prefabName, HolePrefabFolder),
        defaultOpenings = openings
    };

    private static GameObject FindPrefab(string prefabName, string folder)
    {
        string direct = $"{folder}/{prefabName}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(direct);
        if (prefab != null) return prefab;

        string[] guids = AssetDatabase.IsValidFolder(folder)
            ? AssetDatabase.FindAssets($"\"{prefabName}\" t:Prefab", new[] { folder })
            : AssetDatabase.FindAssets($"\"{prefabName}\" t:Prefab");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == prefabName) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        Debug.LogWarning($"Cat Puzzle: could not find prefab '{prefabName}'.");
        return null;
    }

    private static Material FindMaterial(string materialName)
    {
        foreach (string guid in AssetDatabase.FindAssets($"\"{materialName}\" t:Material"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == materialName) return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        return null;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
