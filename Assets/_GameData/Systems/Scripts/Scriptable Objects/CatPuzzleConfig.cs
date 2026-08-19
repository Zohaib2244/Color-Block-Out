using DG.Tweening;
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

    [Header("Cat Collection")]
    [Tooltip("Arc height of the hop a cat makes into the hole.")]
    public float catJumpPower = 0.25f;
    public float catJumpDuration = 0.18f;

    [Tooltip("How far below the hole surface a cat sinks as it shrinks away.")]
    public float catSinkDepth = 0.15f;
    public float catExitDuration = 0.22f;

    [Header("Hole Exit")]
    [Tooltip("Where a finished hole slides to before it hides, relative to its resting position.")]
    public Vector3 holeExitOffset = new Vector3(0f, -0.6f, 0f);
    public float holeExitDuration = 0.45f;
    public Ease holeExitEase = Ease.InOutBounce;

    [Header("Hole Highlight")]
    [Tooltip("Scale multiplier applied to a hole's meshes while it is being dragged.")]
    public float highlightScale = 1.08f;

    [Tooltip("How far a hole's meshes lift while it is being dragged.")]
    public float highlightLift = 0.04f;

    public float highlightDuration = 0.15f;

    [Header("Defaults For New Grids")]
    [Tooltip("Seeds GridData.catParentHeight when a grid is created. Per-grid value wins after that.")]
    public float defaultCatParentHeight = 0.03f;

    [Tooltip("Seeds GridData.holeParentHeight when a grid is created. Per-grid value wins after that.")]
    public float defaultHoleParentHeight = 0.03f;

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
