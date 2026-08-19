using DG.Tweening;
using UnityEngine;

/// <summary>
/// Project wide prefabs and metrics used to build grids, cats and holes. Assign one of these
/// to the scene's <see cref="CatPuzzleController"/> and to the authoring windows; nothing loads
/// it implicitly, so what ships is always what someone wired up.
/// </summary>
[CreateAssetMenu(fileName = "CatPuzzleConfig", menuName = "Cat Puzzle/Puzzle Config")]
public sealed class CatPuzzleConfig : ScriptableObject
{
    [Header("Colours")]
    public CatColorPalette palette;

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

    public CatColorPalette Palette => palette;
}
