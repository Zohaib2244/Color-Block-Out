using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An ordered set of cat levels: the play order, and nothing else.
///
/// A level is a JSON file exported by the web level editor, so this holds the files themselves
/// rather than any Unity asset built from them. <see cref="Get"/> parses one on demand through
/// <see cref="CatLevelJson"/> and keeps the result, so replaying or retrying a level does not
/// re-read it. The parsed level is handed out as a copy - the game moves cats and holes around,
/// and a retry has to start from the file again, not from where the last attempt left off.
/// </summary>
[CreateAssetMenu(fileName = "New Level Collection", menuName = "Cat Puzzle/Level Collection")]
public class LevelData : ScriptableObject
{
    [Tooltip("Name of this level collection")]
    public string collectionName;

    [Tooltip("Description of this level collection")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Level JSON files exported by the web level editor, in play order")]
    public List<TextAsset> levelFiles = new List<TextAsset>();

    /// <summary>Parsed levels by list position. Cleared whenever the asset is (re)loaded.</summary>
    private readonly Dictionary<int, CatLevelData> parsed = new Dictionary<int, CatLevelData>();

    public int Count => levelFiles != null ? levelFiles.Count : 0;

    /// <summary>Wraps out of range indices, the way the old asset list did.</summary>
    public int Wrap(int index) => Count == 0 ? -1 : ((index % Count) + Count) % Count;

    public TextAsset GetFile(int index)
    {
        int wrapped = Wrap(index);
        return wrapped < 0 ? null : levelFiles[wrapped];
    }

    /// <summary>
    /// The level at <paramref name="index"/>, parsed from its file. Returns null - and says why in
    /// the console - when the file is missing or cannot be used.
    /// </summary>
    public CatLevelData Get(int index, CatPuzzleConfig config)
    {
        int wrapped = Wrap(index);
        if (wrapped < 0)
        {
            Debug.LogError($"Level collection '{name}' has no level files.", this);
            return null;
        }

        if (parsed.TryGetValue(wrapped, out CatLevelData cached) && cached != null) return cached.Clone();

        TextAsset file = levelFiles[wrapped];
        if (file == null)
        {
            Debug.LogError($"Level collection '{name}' has no file in slot {wrapped + 1}.", this);
            return null;
        }

        CatLevelJson.LevelResult result = CatLevelJson.ParseLevel(file.text, file.name, config);
        if (!result.Succeeded)
        {
            Debug.LogError($"Level '{file.name}' could not be loaded:\n{result.Describe()}", file);
            return null;
        }

        // A level that validates badly still loads. Refusing it would leave the player on a dead
        // screen with no way forward, which is worse than an unwinnable board plus a loud log.
        if (result.HasErrors) Debug.LogError($"Level '{file.name}' has problems that will break it:\n{result.Describe()}", file);
        else if (result.issues.Count > 0) Debug.LogWarning($"Level '{file.name}' loaded with warnings:\n{result.Describe()}", file);

        parsed[wrapped] = result.level;
        return result.level.Clone();
    }

    /// <summary>Drops the parsed copies, so an edited file is picked up on the next load.</summary>
    public void ClearCache() => parsed.Clear();

    private void OnEnable() => parsed.Clear();

    private void OnValidate() => parsed.Clear();
}
