#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CatHoleConfigurationEditor
{
    [MenuItem("Cat Puzzle/Create Default Hole Configuration")]
    public static void CreateDefault()
    {
        CatHoleConfiguration asset = ScriptableObject.CreateInstance<CatHoleConfiguration>();
        asset.holePrefabs = new[]
        {
            Entry(CatHoleType.Isolated, "Isolated", new Direction[0]),
            Entry(CatHoleType.EndCap, "End Cap", new[] { Direction.Right }),
            Entry(CatHoleType.Straight, "Straight", new[] { Direction.Right, Direction.Left }),
            Entry(CatHoleType.Corner, "Corner", new[] { Direction.Right, Direction.Down }),
            Entry(CatHoleType.OneSide, "One Side", new[] { Direction.Right, Direction.Down, Direction.Left }),
            Entry(CatHoleType.Middle, "Middle", new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left })
        };
        string folder = "Assets/_GameData/Systems/Scriptable Objects/MISC";
        string path = EditorUtility.SaveFilePanelInProject("Save Hole Configuration", "CatHoleConfiguration", "asset", "Choose a location.", folder);
        if (string.IsNullOrEmpty(path)) { Object.DestroyImmediate(asset); return; }
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
    }

    private static CatHolePrefabData Entry(CatHoleType type, string prefabName, Direction[] openings)
    {
        string[] guids = AssetDatabase.FindAssets($"{prefabName} t:Prefab", new[] { "Assets/_GameData/Systems/Prefabs/Holes" });
        GameObject prefab = guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        return new CatHolePrefabData { holeType = type, prefab = prefab, defaultOpenings = openings };
    }
}
#endif
