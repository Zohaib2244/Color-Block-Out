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
    /// <summary>Where authored content is filed. Nothing prompts for a location.</summary>
    public const string GridFolder = "Assets/_GameData/Systems/Data/Grids";
    public const string LevelFolder = "Assets/_GameData/Systems/Data/Levels";

    private const string ConfigFolder = "Assets/_GameData/Systems/Scriptable Objects/MISC";
    private const string HoleConfigFolder = "Assets/_GameData/Systems/Scriptable Objects/MISC";
    private const string HolePrefabFolder = "Assets/_GameData/Systems/Prefabs/Holes";
    private const string GridPrefabFolder = "Assets/_GameData/Systems/Prefabs/UI/GridElements";
    private const string CatPrefabFolder = "Assets/_GameData/Systems/Prefabs/Cats";

    [MenuItem("Cat Puzzle/Create Default Assets")]
    public static void CreateDefaults()
    {
        CatHoleConfiguration holeConfiguration = CreateHoleConfiguration();
        CatColorPalette palette = CreatePalette();
        CatPuzzleConfig config = CreatePuzzleConfig(holeConfiguration, palette);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    /// <summary>Finds the project's config asset. Editor convenience only — nothing loads it at runtime.</summary>
    public static CatPuzzleConfig FindConfig() => FindFirst<CatPuzzleConfig>();

    private static T FindFirst<T>() where T : ScriptableObject
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) return asset;
        }
        return null;
    }

    /// <summary>
    /// Seeds the palette with the colours the project used before, in their original order, so
    /// ids 0..8 keep meaning what already-saved levels expect them to mean.
    /// </summary>
    private static CatColorPalette CreatePalette()
    {
        CatColorPalette palette = FindFirst<CatColorPalette>();
        if (palette == null)
        {
            EnsureFolder(ConfigFolder);
            palette = ScriptableObject.CreateInstance<CatColorPalette>();
            AssetDatabase.CreateAsset(palette, $"{ConfigFolder}/CatColorPalette.asset");
        }

        if (palette.entries.Count == 0)
        {
            (string name, Color color)[] defaults =
            {
                ("Red", new Color32(0xE5, 0x39, 0x35, 0xFF)),
                ("Orange", new Color32(0xFB, 0x8C, 0x00, 0xFF)),
                ("Yellow", new Color32(0xFD, 0xD8, 0x35, 0xFF)),
                ("Blue", new Color32(0x1E, 0x88, 0xE5, 0xFF)),
                ("Cyan", new Color32(0x00, 0xAC, 0xC1, 0xFF)),
                ("Green", new Color32(0x43, 0xA0, 0x47, 0xFF)),
                ("Purple", new Color32(0x8E, 0x24, 0xAA, 0xFF)),
                ("Pink", new Color32(0xEC, 0x40, 0x7A, 0xFF)),
                ("Teal", new Color32(0x00, 0x89, 0x7B, 0xFF))
            };
            for (int i = 0; i < defaults.Length; i++)
                palette.entries.Add(new CatColorEntry { type = (CatColorType)i, displayName = defaults[i].name, color = defaults[i].color });
        }

        EditorUtility.SetDirty(palette);
        return palette;
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

    private static CatPuzzleConfig CreatePuzzleConfig(CatHoleConfiguration holeConfiguration, CatColorPalette palette)
    {
        CatPuzzleConfig config = FindFirst<CatPuzzleConfig>();
        if (config == null)
        {
            EnsureFolder(ConfigFolder);
            config = ScriptableObject.CreateInstance<CatPuzzleConfig>();
            AssetDatabase.CreateAsset(config, $"{ConfigFolder}/CatPuzzleConfig.asset");
        }

        EnsureFolder(GridFolder);
        EnsureFolder(LevelFolder);

        config.holeConfiguration = holeConfiguration;
        if (config.palette == null) config.palette = palette;
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

    public static void EnsureFolder(string folder)
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
