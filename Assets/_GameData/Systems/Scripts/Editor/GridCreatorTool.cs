#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Authors the reusable board shapes. A board is saved as a grid JSON file - the same format the
/// web level editor exports - and the Cat Level Editor starts a level from one of these.
///
/// A level keeps its own copy of the board it was built on, exactly as the web tool's export does,
/// so re-saving a board here does not reshape levels that were already made from it. Saving reports
/// which of those levels no longer fit, so they can be rebuilt deliberately.
/// </summary>
public sealed class GridCreatorTool : EditorWindow
{
    private const string PreviewName = "Grid Preview";

    // Serialized so a domain reload does not quietly lose which file is open - saving after that
    // would file a second copy of the board instead of updating this one.
    [SerializeField] private CatPuzzleConfig config;
    [SerializeField] private TextAsset gridFile;
    [SerializeField] private string gridName = "New Grid";
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridLength = 10;
    [SerializeField] private float cellSize = 0.57f;
    [SerializeField] private float catParentHeight = 0.178f;
    [SerializeField] private float holeParentHeight = 0.08f;

    /// <summary>The painted layout, flattened. Unity cannot serialize a bool[,], so it travels as one.</summary>
    [SerializeField] private bool[] savedCells;

    private bool[,] blockedCells;
    private Vector2 scroll;
    private float paintCellSize = 26f;
    private bool paintingWalls = true;
    private Transform preview;

    private static readonly Color PlayableColor = new Color(0.30f, 0.72f, 0.36f);
    private static readonly Color BlockedColor = new Color(0.28f, 0.28f, 0.30f);
    private static readonly Color OutsideColor = new Color(0.16f, 0.16f, 0.18f);

    [MenuItem("Cat Puzzle/Grid Creator")]
    public static void ShowWindow()
    {
        GridCreatorTool window = GetWindow<GridCreatorTool>("Grid Creator");
        window.minSize = new Vector2(420f, 520f);
    }

    private void OnEnable()
    {
        bool fresh = savedCells == null || savedCells.Length != gridWidth * gridLength;
        if (config == null) config = CatPuzzleAssetCreator.FindConfig();
        // Only seed the heights from the config on a genuinely new window; a reload keeps what was typed.
        if (config != null && fresh)
        {
            catParentHeight = config.catParentHeight;
            holeParentHeight = config.holeParentHeight;
        }

        EnsureCells();
        if (fresh) return;

        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                blockedCells[x, z] = savedCells[z * gridWidth + x];
    }

    /// <summary>Flattens the painted layout so it survives the domain reload a recompile brings.</summary>
    private void OnDisable()
    {
        if (blockedCells == null) return;
        savedCells = new bool[gridWidth * gridLength];
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                savedCells[z * gridWidth + x] = blockedCells[x, z];
    }

    private void OnGUI()
    {
        DrawFile();
        DrawSize();
        DrawPaintTools();
        DrawLayout();
        DrawActions();
    }

    #region Sections
    private void DrawFile()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Grid File", EditorStyles.boldLabel);

        config = (CatPuzzleConfig)EditorGUILayout.ObjectField("Puzzle Config", config, typeof(CatPuzzleConfig), false);

        EditorGUI.BeginChangeCheck();
        gridFile = (TextAsset)EditorGUILayout.ObjectField("Edit Existing Grid", gridFile, typeof(TextAsset), false);
        if (EditorGUI.EndChangeCheck() && gridFile != null) LoadFromFile(gridFile);

        gridName = EditorGUILayout.TextField("Grid Name", gridName);
        EditorGUILayout.LabelField(" ", $"Saved as JSON to {CatLevelFiles.GridFolder}", EditorStyles.miniLabel);
        EditorGUILayout.HelpBox("Draw the playable shape here and save it. The Cat Level Editor starts a level from a grid file, " +
                                "and the level then carries its own copy of the board.", MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void DrawSize()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Board Size", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int newWidth = Mathf.Max(1, EditorGUILayout.IntField("Width", gridWidth));
        int newLength = Mathf.Max(1, EditorGUILayout.IntField("Length", gridLength));
        if (EditorGUI.EndChangeCheck() && (newWidth != gridWidth || newLength != gridLength)) Resize(newWidth, newLength);

        cellSize = Mathf.Max(0.01f, EditorGUILayout.FloatField("Cell Size", cellSize));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Content Heights", EditorStyles.boldLabel);
        catParentHeight = EditorGUILayout.FloatField("Cats Parent Y", catParentHeight);
        holeParentHeight = EditorGUILayout.FloatField("Holes Parent Y", holeParentHeight);
        EditorGUILayout.HelpBox("Local Y of the Cats and Holes parents created inside the grid. The web tool does not export these, " +
                                "so a board drawn there takes the defaults on the puzzle config; a board saved here writes its own.",
                                MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void DrawPaintTools()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
        paintingWalls = GUILayout.Toolbar(paintingWalls ? 0 : 1, new[] { "Paint Blocked", "Paint Playable" }, GUILayout.Height(22)) == 0;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All Playable")) Fill(false);
        if (GUILayout.Button("All Blocked")) Fill(true);
        if (GUILayout.Button("Border Only")) FillBorder();
        if (GUILayout.Button("Invert")) Invert();
        EditorGUILayout.EndHorizontal();

        paintCellSize = EditorGUILayout.Slider("Zoom", paintCellSize, 14f, 44f);
        EditorGUILayout.EndVertical();
    }

    private void DrawActions()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(gridFile != null ? "Save To File" : "Create Grid File", GUILayout.Height(30))) SaveFile(false);
        using (new EditorGUI.DisabledScope(gridFile == null))
            if (GUILayout.Button("Save As New", GUILayout.Height(30))) SaveFile(true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview In Scene", GUILayout.Height(24))) BuildPreview();
        if (GUILayout.Button("Remove Preview", GUILayout.Height(24))) RemovePreview();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("The preview is a throwaway object for checking the shape. Levels build their own grid when loaded.", MessageType.None);
        EditorGUILayout.EndVertical();
    }
    #endregion

    #region Layout painting
    private void DrawLayout()
    {
        EnsureCells();
        bool[,] interior = FindInteriorCells();

        float boardWidth = gridWidth * paintCellSize + 24f;
        float boardHeight = gridLength * paintCellSize + 24f;
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(boardHeight + 12f, 420f)));
        Rect area = GUILayoutUtility.GetRect(boardWidth, boardHeight);
        Rect board = new Rect(area.x + 12f, area.y + 4f, gridWidth * paintCellSize, gridLength * paintCellSize);

        HandleInput(board);

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridLength; z++)
            {
                Rect rect = new Rect(board.x + x * paintCellSize, board.y + (gridLength - 1 - z) * paintCellSize, paintCellSize, paintCellSize);
                Color color = blockedCells[x, z] ? BlockedColor : interior[x, z] ? PlayableColor : OutsideColor;
                EditorGUI.DrawRect(rect, color);

                Handles.color = new Color(0f, 0f, 0f, 0.35f);
                Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.xMax, rect.y));
                Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.x, rect.yMax));
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.LabelField("Green is playable, dark grey is blocked, near-black is outside the board.", EditorStyles.miniLabel);
    }

    private void HandleInput(Rect board)
    {
        Event evt = Event.current;
        if ((evt.type != EventType.MouseDown && evt.type != EventType.MouseDrag) || evt.button != 0 || !board.Contains(evt.mousePosition)) return;

        int x = Mathf.FloorToInt((evt.mousePosition.x - board.x) / paintCellSize);
        int z = gridLength - 1 - Mathf.FloorToInt((evt.mousePosition.y - board.y) / paintCellSize);
        if (x < 0 || x >= gridWidth || z < 0 || z >= gridLength) return;

        blockedCells[x, z] = paintingWalls;
        evt.Use();
        Repaint();
    }

    /// <summary>Flood fills from the border so cells cut off from the outside stay playable.</summary>
    private bool[,] FindInteriorCells()
    {
        bool[,] interior = new bool[gridWidth, gridLength];
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                interior[x, z] = !blockedCells[x, z];

        Queue<Vector2Int> pending = new Queue<Vector2Int>();
        void Seed(int x, int z)
        {
            if (x < 0 || x >= gridWidth || z < 0 || z >= gridLength) return;
            if (!interior[x, z]) return;
            interior[x, z] = false;
            pending.Enqueue(new Vector2Int(x, z));
        }

        for (int x = 0; x < gridWidth; x++) { Seed(x, 0); Seed(x, gridLength - 1); }
        for (int z = 0; z < gridLength; z++) { Seed(0, z); Seed(gridWidth - 1, z); }

        while (pending.Count > 0)
        {
            Vector2Int current = pending.Dequeue();
            Seed(current.x + 1, current.y);
            Seed(current.x - 1, current.y);
            Seed(current.x, current.y + 1);
            Seed(current.x, current.y - 1);
        }
        return interior;
    }
    #endregion

    #region File and preview
    private void LoadFromFile(TextAsset file)
    {
        if (!CatLevelJson.TryParseGrid(file.text, file.name, config, out GridData data, out string error))
        {
            EditorUtility.DisplayDialog("Grid Creator", $"'{file.name}' is not a grid file that can be opened.\n\n{error}", "OK");
            gridFile = null;
            return;
        }

        AdoptGrid(data, string.IsNullOrEmpty(data.gridName) ? file.name : data.gridName);
    }

    private void AdoptGrid(GridData data, string name)
    {
        data.EnsureArrays();
        gridWidth = data.gridWidth;
        gridLength = data.gridLength;
        cellSize = data.cellSize;
        catParentHeight = data.catParentHeight;
        holeParentHeight = data.holeParentHeight;
        gridName = name;
        blockedCells = new bool[gridWidth, gridLength];
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                blockedCells[x, z] = data.IsWall(x, z);
        Repaint();
    }

    /// <summary>Builds the board as it is drawn, with the flood fill already applied.</summary>
    private GridData BuildGridData()
    {
        bool[,] interior = FindInteriorCells();
        GridData data = new GridData(gridWidth, gridLength)
        {
            gridName = string.IsNullOrWhiteSpace(gridName) ? "New Grid" : gridName.Trim(),
            cellSize = cellSize,
            catParentHeight = catParentHeight,
            holeParentHeight = holeParentHeight
        };

        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                data.SetWall(x, z, blockedCells[x, z] || !interior[x, z]);
        return data;
    }

    private void SaveFile(bool forceNew)
    {
        GridData data = BuildGridData();
        string path = forceNew || gridFile == null ? null : AssetDatabase.GetAssetPath(gridFile);

        TextAsset written = CatLevelFiles.WriteGrid(data, path, out string error);
        if (written == null)
        {
            EditorUtility.DisplayDialog("Grid Creator", $"The board could not be saved.\n\n{error}", "OK");
            return;
        }

        gridFile = written;
        gridName = data.gridName;
        Selection.activeObject = written;
        Debug.Log($"Saved grid '{data.gridName}' ({gridWidth}x{gridLength}) to {AssetDatabase.GetAssetPath(written)}.", written);
        ReportStaleLevels(data);
    }

    /// <summary>
    /// A level carries its own copy of the board, so re-saving one here leaves existing levels
    /// alone. That is the safe behaviour, but it does mean a level can quietly fall out of step, so
    /// every level file that names this board and no longer matches it is reported.
    /// </summary>
    private void ReportStaleLevels(GridData grid)
    {
        List<string> stale = new List<string>();

        foreach (TextAsset file in CatLevelFiles.FindIn(CatLevelFiles.LevelFolder))
        {
            if (!CatLevelJson.TryReadLevelDocument(file.text, out LevelJsonFormat.LevelDocument document, out _)) continue;

            string boardName = document.grid != null
                ? (string.IsNullOrEmpty(document.grid.gridName) ? document.grid.id : document.grid.gridName)
                : null;
            if (string.IsNullOrEmpty(boardName) || boardName != grid.gridName) continue;

            GridData embedded = CatLevelJson.GridFromSection(document.grid, config);
            if (!grid.SameShapeAs(embedded)) stale.Add($"• {file.name}");
        }

        if (stale.Count == 0) return;
        string report = string.Join("\n", stale);
        Debug.LogWarning($"{stale.Count} level file(s) were built on an earlier version of grid '{grid.gridName}' and still carry it:\n{report}\n" +
                         "Open each in the Cat Level Editor and rebuild it on the saved grid to bring it up to date.");
    }

    private void BuildPreview()
    {
        RemovePreview();
        GameObject root = new GameObject(PreviewName);
        Undo.RegisterCreatedObjectUndo(root, "Grid Preview");
        GridBuilder.Build(BuildGridData(), root.transform, config);
        preview = root.transform;
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    private void RemovePreview()
    {
        if (preview == null)
        {
            GameObject existing = GameObject.Find(PreviewName);
            if (existing != null) preview = existing.transform;
        }
        if (preview == null) return;
        Undo.DestroyObjectImmediate(preview.gameObject);
        preview = null;
    }
    #endregion

    #region Cell helpers
    private void EnsureCells()
    {
        if (blockedCells == null || blockedCells.GetLength(0) != gridWidth || blockedCells.GetLength(1) != gridLength)
            Resize(gridWidth, gridLength);
    }

    private void Resize(int width, int length)
    {
        bool[,] resized = new bool[width, length];
        if (blockedCells != null)
        {
            int copyWidth = Mathf.Min(width, blockedCells.GetLength(0));
            int copyLength = Mathf.Min(length, blockedCells.GetLength(1));
            for (int x = 0; x < copyWidth; x++)
                for (int z = 0; z < copyLength; z++)
                    resized[x, z] = blockedCells[x, z];
        }
        gridWidth = width;
        gridLength = length;
        blockedCells = resized;
    }

    private void Fill(bool blocked)
    {
        EnsureCells();
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                blockedCells[x, z] = blocked;
        Repaint();
    }

    private void FillBorder()
    {
        Fill(false);
        for (int x = 0; x < gridWidth; x++) { blockedCells[x, 0] = true; blockedCells[x, gridLength - 1] = true; }
        for (int z = 0; z < gridLength; z++) { blockedCells[0, z] = true; blockedCells[gridWidth - 1, z] = true; }
        Repaint();
    }

    private void Invert()
    {
        EnsureCells();
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                blockedCells[x, z] = !blockedCells[x, z];
        Repaint();
    }
    #endregion
}
#endif
