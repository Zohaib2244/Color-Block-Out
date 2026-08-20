using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The spawned form of a <see cref="CatLevelData"/> asset: a grid plus its cats and
/// holes. It hands itself to the single scene <see cref="CatPuzzleController"/> on
/// awake, so the controller never has to live inside a level.
/// </summary>
public sealed class CatLevelInstance : MonoBehaviour
{
    [SerializeField] private CatLevelData source;
    [SerializeField] private GridManager grid;
    [SerializeField] private Transform catRoot;
    [SerializeField] private Transform holeRoot;
    [SerializeField] private List<CatPiece> cats = new List<CatPiece>();
    [SerializeField] private List<CatHole> holes = new List<CatHole>();

    public CatLevelData Source => source;
    public GridManager Grid => grid;
    public Transform CatRoot => catRoot;
    public Transform HoleRoot => holeRoot;
    public IReadOnlyList<CatPiece> Cats => cats;
    public IReadOnlyList<CatHole> Holes => holes;

    public void Initialize(CatLevelData level, GridManager gridManager, Transform catsParent, Transform holesParent)
    {
        source = level;
        grid = gridManager;
        catRoot = catsParent;
        holeRoot = holesParent;
        RefreshContents();
    }

    /// <summary>Re-reads the cats and holes that currently live under this level.</summary>
    public void RefreshContents()
    {
        cats.Clear();
        holes.Clear();
        if (catRoot != null) cats.AddRange(catRoot.GetComponentsInChildren<CatPiece>(true));
        else cats.AddRange(GetComponentsInChildren<CatPiece>(true));
        if (holeRoot != null) holes.AddRange(holeRoot.GetComponentsInChildren<CatHole>(true));
        else holes.AddRange(GetComponentsInChildren<CatHole>(true));
    }

    public void Register(CatPiece cat)
    {
        if (cat != null && !cats.Contains(cat)) cats.Add(cat);
    }

    public void Register(CatHole hole)
    {
        if (hole != null && !holes.Contains(hole)) holes.Add(hole);
    }

    public void Forget(CatPiece cat) => cats.Remove(cat);
    public void Forget(CatHole hole) => holes.Remove(hole);

    private void Awake()
    {
        if (grid == null) grid = GetComponentInChildren<GridManager>(true);
        if (cats.Count == 0 && holes.Count == 0) RefreshContents();
    }
}
