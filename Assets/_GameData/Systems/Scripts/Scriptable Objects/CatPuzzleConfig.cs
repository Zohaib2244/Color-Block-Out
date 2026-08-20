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
    [Tooltip("Cat prefab. Its CatPiece component carries the collection animation settings.")]
    public GameObject catPrefab;

    [Tooltip("Hole piece prefabs and the hole root prefab, which carries the exit and highlight settings.")]
    public CatHoleConfiguration holeConfiguration;

    public CatColorPalette Palette => palette;
}
