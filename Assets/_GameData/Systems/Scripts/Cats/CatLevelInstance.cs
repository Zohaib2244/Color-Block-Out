using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The spawned form of a <see cref="CatLevelData"/>: a grid plus its cats and
/// holes. It hands itself to the single scene <see cref="CatPuzzleController"/> on
/// awake, so the controller never has to live inside a level.
/// </summary>
public sealed class CatLevelInstance : MonoBehaviour
{
    /// <summary>
    /// The level this was built from, kept as its own JSON so it survives a scene save and a script
    /// recompile. A plain serialized <see cref="CatLevelData"/> field could not: Unity rebuilds a
    /// serializable class field as a default instance rather than leaving it null, so a level that
    /// was never assigned one would come back looking like an empty level instead of like nothing.
    /// This is the internal shape, not the exported format, so it needs no palette to read back.
    /// </summary>
    [SerializeField, HideInInspector] private string sourceJson;

    [System.NonSerialized] private CatLevelData source;

    [SerializeField] private GridManager grid;
    [SerializeField] private Transform catRoot;
    [SerializeField] private Transform holeRoot;
    [SerializeField] private List<CatPiece> cats = new List<CatPiece>();
    [SerializeField] private List<CatHole> holes = new List<CatHole>();

    /// <summary>
    /// One badge per pile cell, keyed by grid position. Not serialized: rebuilt as piles change (see
    /// <see cref="CatLevelBuilder.RefreshPileBadges"/>), so it would only go stale across a reload.
    /// </summary>
    private readonly Dictionary<Vector2Int, CatCountBadge> pileBadges = new Dictionary<Vector2Int, CatCountBadge>();

    public CatLevelData Source
    {
        get
        {
            if (source == null && !string.IsNullOrEmpty(sourceJson)) source = JsonUtility.FromJson<CatLevelData>(sourceJson);
            return source;
        }
    }

    public GridManager Grid => grid;
    public Transform CatRoot => catRoot;
    public Transform HoleRoot => holeRoot;
    public IReadOnlyList<CatPiece> Cats => cats;
    public IReadOnlyList<CatHole> Holes => holes;

    public void Initialize(CatLevelData level, GridManager gridManager, Transform catsParent, Transform holesParent)
    {
        source = level;
        sourceJson = level != null ? JsonUtility.ToJson(level) : null;
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

    public IReadOnlyDictionary<Vector2Int, CatCountBadge> PileBadges => pileBadges;
    public bool TryGetPileBadge(Vector2Int cell, out CatCountBadge badge) => pileBadges.TryGetValue(cell, out badge);
    public void SetPileBadge(Vector2Int cell, CatCountBadge badge) => pileBadges[cell] = badge;

    public void RemovePileBadge(Vector2Int cell)
    {
        if (pileBadges.TryGetValue(cell, out CatCountBadge badge) && badge != null)
        {
            if (Application.isPlaying) Destroy(badge.gameObject);
            else DestroyImmediate(badge.gameObject);
        }
        pileBadges.Remove(cell);
    }

    private void Awake()
    {
        if (grid == null) grid = GetComponentInChildren<GridManager>(true);
        if (cats.Count == 0 && holes.Count == 0) RefreshContents();
    }
}
