using System.Collections.Generic;
using UnityEngine;

/// <summary>An ordered set of cat levels. Levels are assets, not prefabs, so several of them can share one grid.</summary>
[CreateAssetMenu(fileName = "New Level Collection", menuName = "Cat Puzzle/Level Collection")]
public class LevelData : ScriptableObject
{
    [Tooltip("Name of this level collection")]
    public string collectionName;

    [Tooltip("Description of this level collection")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Levels in play order")]
    public List<CatLevelData> levels = new List<CatLevelData>();

    public int Count => levels.Count;

    public CatLevelData Get(int index) => levels.Count == 0 ? null : levels[((index % levels.Count) + levels.Count) % levels.Count];
}
