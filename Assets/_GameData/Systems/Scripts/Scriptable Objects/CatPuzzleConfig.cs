using UnityEngine;

/// <summary>
/// Project wide prefabs and metrics used to build grids, cats and holes.
/// One asset lives in a Resources folder so runtime spawning and the level
/// editor read the same values without wiring them into every level asset.
/// </summary>
[CreateAssetMenu(fileName = "CatPuzzleConfig", menuName = "Cat Puzzle/Puzzle Config")]
public sealed class CatPuzzleConfig : ScriptableObject
{
    public const string ResourcePath = "CatPuzzleConfig";

    [Header("Grid Prefabs")]
    public GameObject cellPrefab;
    public GameObject straightWallPrefab;
    public GameObject wallEndPrefab;
    public GameObject cornerWallPrefab;
    public Material cellMaterialA;
    public Material cellMaterialB;

    [Header("Grid Metrics")]
    public float wallHeight = 0.17f;
    public float straightWallThickness = 0.35f;
    public float wallOffset = 0.4f;
    public float cornerWallHeight = 0.225f;
    public float cornerWallOffset = 0.17f;
    public float interiorCornerOffset = 0.13f;

    [Header("Pieces")]
    public GameObject catPrefab;
    public CatHoleConfiguration holeConfiguration;
    public float catHeight = 0.03f;
    public float holeHeight = 0.03f;

    private static CatPuzzleConfig cached;

    /// <summary>The single configuration asset, loaded from Resources on first use.</summary>
    public static CatPuzzleConfig Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<CatPuzzleConfig>(ResourcePath);
            if (cached == null) Debug.LogError($"No CatPuzzleConfig found at Resources/{ResourcePath}. Use Cat Puzzle/Create Default Assets.");
            return cached;
        }
    }

    /// <summary>Falls back to the shared asset when a caller passes null.</summary>
    public static CatPuzzleConfig Resolve(CatPuzzleConfig candidate) => candidate != null ? candidate : Instance;
}
