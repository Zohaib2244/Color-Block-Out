using System;

/// <summary>
/// The on-disk shape of what the web level editor (level-editor/) exports, field for field.
/// <see cref="UnityEngine.JsonUtility"/> matches by name, so these names must stay in step with
/// <c>level-editor/src/io/levelJson.ts</c> and <c>level-editor/src/io/gridJson.ts</c>.
///
/// Nothing here is used directly by the game: <see cref="CatLevelJson"/> turns a document into the
/// <see cref="CatLevelData"/> and <see cref="GridData"/> the rest of the project works with, and
/// back again for export. Anything a file does not carry stays at its default and is checked
/// before it is used.
/// </summary>
public static class LevelJsonFormat
{
    /// <summary>Mirrors LEVEL_JSON_FORMAT_VERSION in level-editor/src/io/levelJson.ts.</summary>
    public const int LevelVersion = 4;

    /// <summary>
    /// v3 files still load unchanged: v4 only *added* gates and cat stacking, so everything a v3
    /// file can express means the same thing here.
    /// </summary>
    public const int MinLevelVersion = 3;

    /// <summary>Mirrors GRID_JSON_FORMAT_VERSION in level-editor/src/io/gridJson.ts.</summary>
    public const int GridVersion = 1;

    [Serializable]
    public sealed class LevelDocument
    {
        public int formatVersion;
        public string levelName;
        public GridSection grid;
        public CatEntry[] cats;
        public HoleEntry[] holes;
        public GateEntry[] gates;
    }

    /// <summary>
    /// A board inside a level file. Deliberately the same fields as <see cref="GridDocument"/>
    /// (minus the format version, plus the web tool's grid id) so the two never drift.
    ///
    /// A board is shape only. How high the content parents sit inside a grid is project wide and
    /// lives on <see cref="CatPuzzleConfig"/>, so no file carries it; heights written by older
    /// versions of the Unity tools are ignored.
    /// </summary>
    [Serializable]
    public sealed class GridSection
    {
        public string id;
        public string gridName;
        public int width;
        public int length;
        public float cellSize;

        /// <summary>Flat, index = z * width + x. True where a cat or hole may stand.</summary>
        public bool[] playableCells;
    }

    [Serializable]
    public sealed class CatEntry
    {
        public string color;
        public int x;
        public int z;
    }

    /// <summary>One hole: a connected shape sharing a single colour and a single capacity.</summary>
    [Serializable]
    public sealed class HoleEntry
    {
        public string id;
        public string color;
        public int capacity;
        public HoleCellEntry[] cells;
    }

    [Serializable]
    public sealed class HoleCellEntry
    {
        public int x;
        public int z;

        /// <summary>What the web tool drew. Unity rebuilds it from the shape, so it is only cross checked.</summary>
        public string holeType;

        public int rotationQuarterTurns;
    }

    /// <summary>
    /// A v4 gate: a queue of cats on one boundary edge of a cell. Parsed so the count can be
    /// reported, but there is no Unity-side gate yet, so loading drops them (with a warning).
    /// </summary>
    [Serializable]
    public sealed class GateEntry
    {
        public string id;
        public int x;
        public int z;
        public string side;
        public string[] cats;
    }

    /// <summary>A board on its own, so a shape can be shared between levels without being trapped in one.</summary>
    [Serializable]
    public sealed class GridDocument
    {
        public int formatVersion;
        public string gridName;
        public int width;
        public int length;
        public float cellSize;

        /// <summary>Flat, index = z * width + x. True where a cat or hole may stand.</summary>
        public bool[] playableCells;
    }
}
