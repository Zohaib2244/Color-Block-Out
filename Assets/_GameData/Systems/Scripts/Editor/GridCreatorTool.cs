#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Authors the reusable board shapes. A grid is saved as a <see cref="GridData"/> asset
/// and can back any number of levels; the meshes are rebuilt from that asset by
/// <see cref="GridBuilder"/> whenever a level is loaded.
/// </summary>
public sealed class GridCreatorTool : EditorWindow
{
    private const string PreviewName = "Grid Preview";

    private CatPuzzleConfig config;
    private GridData gridAsset;
    private string gridName = "New Grid";
    private int gridWidth = 10;
    private int gridLength = 10;
    private float cellSize = 0.57f;
    private float catParentHeight = 0.178f;
    private float holeParentHeight = 0.08f;

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
        if (config == null) config = CatPuzzleAssetCreator.FindConfig();
        EnsureCells();
    }

    private void OnGUI()
    {
        DrawAsset();
        DrawSize();
        DrawPaintTools();
        DrawLayout();
        DrawActions();
    }

    #region Sections
    private void DrawAsset()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Grid Asset", EditorStyles.boldLabel);

        config = (CatPuzzleConfig)EditorGUILayout.ObjectField("Puzzle Config", config, typeof(CatPuzzleConfig), false);

        EditorGUI.BeginChangeCheck();
        gridAsset = (GridData)EditorGUILayout.ObjectField("Edit Existing Grid", gridAsset, typeof(GridData), false);
        if (EditorGUI.EndChangeCheck() && gridAsset != null) LoadFromAsset(gridAsset);

        gridName = EditorGUILayout.TextField("Grid Name", gridName);
        EditorGUILayout.LabelField(" ", $"Saved to {CatPuzzleAssetCreator.GridFolder}", EditorStyles.miniLabel);
        EditorGUILayout.HelpBox("One grid can back many levels. Draw the playable shape here, then build levels on it in the Cat Level Editor.", MessageType.None);
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
        EditorGUILayout.HelpBox("Local Y of the Cats and Holes parents created inside the grid. Saved with the grid, so every level built on it lines up.", MessageType.None);
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
        if (GUILayout.Button(gridAsset != null ? "Save To Asset" : "Create Grid Asset", GUILayout.Height(30))) SaveAsset(false);
        using (new EditorGUI.DisabledScope(gridAsset == null))
            if (GUILayout.Button("Save As New", GUILayout.Height(30))) SaveAsset(true);
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

    #region Asset and preview
    private void LoadFromAsset(GridData data)
    {
        data.EnsureArrays();
        gridWidth = data.gridWidth;
        gridLength = data.gridLength;
        cellSize = data.cellSize;
        catParentHeight = data.catParentHeight;
        holeParentHeight = data.holeParentHeight;
        gridName = data.name;
        blockedCells = new bool[gridWidth, gridLength];
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                blockedCells[x, z] = data.IsWall(x, z);
        Repaint();
    }

    private void SaveAsset(bool forceNew)
    {
        bool[,] interior = FindInteriorCells();
        GridData target = forceNew ? null : gridAsset;

        if (target == null)
        {
            // Grids are filed automatically; the name field is the only decision.
            CatPuzzleAssetCreator.EnsureFolder(CatPuzzleAssetCreator.GridFolder);
            string safeName = string.IsNullOrWhiteSpace(gridName) ? "New Grid" : gridName.Trim();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{CatPuzzleAssetCreator.GridFolder}/{safeName}.asset");
            target = CreateInstance<GridData>();
            AssetDatabase.CreateAsset(target, path);
            gridName = System.IO.Path.GetFileNameWithoutExtension(path);
        }

        Undo.RecordObject(target, "Save Grid");
        target.Initialize(gridWidth, gridLength);
        target.cellSize = cellSize;
        target.catParentHeight = catParentHeight;
        target.holeParentHeight = holeParentHeight;
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                target.SetWall(x, z, blockedCells[x, z] || !interior[x, z]);

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
        gridAsset = target;
        Selection.activeObject = target;
        Debug.Log($"Saved grid '{target.name}' ({gridWidth}x{gridLength}).", target);
        ReportAffectedLevels(target);
    }

    /// <summary>
    /// A grid backs many levels, so reshaping one can strand content that was authored on the old
    /// shape. Re-check every level built on this grid and say which ones broke.
    /// </summary>
    private void ReportAffectedLevels(GridData grid)
    {
        CatColorPalette palette = config != null ? config.palette : null;
        List<string> broken = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:CatLevelData"))
        {
            CatLevelData level = AssetDatabase.LoadAssetAtPath<CatLevelData>(AssetDatabase.GUIDToAssetPath(guid));
            if (level == null || level.grid != grid) continue;

            List<CatLevelValidator.Issue> issues = CatLevelValidator.Validate(level, palette);
            if (CatLevelValidator.HasErrors(issues)) broken.Add($"• {level.DisplayName}\n{CatLevelValidator.Describe(issues)}");
        }

        if (broken.Count == 0) return;
        string report = string.Join("\n", broken);
        Debug.LogError($"Saving grid '{grid.name}' broke {broken.Count} level(s) built on it:\n{report}", grid);
        EditorUtility.DisplayDialog("Grid Creator",
            $"{broken.Count} level(s) built on this grid no longer fit it. Details are in the console.\n\nOpen each in the Cat Level Editor and fix the flagged cells.", "OK");
    }

    private void BuildPreview()
    {
        RemovePreview();
        GridData data = CreateInstance<GridData>();
        data.Initialize(gridWidth, gridLength);
        data.cellSize = cellSize;
        data.catParentHeight = catParentHeight;
        data.holeParentHeight = holeParentHeight;
        bool[,] interior = FindInteriorCells();
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridLength; z++)
                data.SetWall(x, z, blockedCells[x, z] || !interior[x, z]);

        GameObject root = new GameObject(PreviewName);
        Undo.RegisterCreatedObjectUndo(root, "Grid Preview");
        GridBuilder.Build(data, root.transform, config);
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
