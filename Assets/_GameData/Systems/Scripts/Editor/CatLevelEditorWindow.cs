#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Authoring tool for cat levels. Pick a GridData asset, build it in the scene, then
/// paint cats and draw holes straight onto the board. The scene is the working document:
/// anything you drag around afterwards is picked up again when you save.
/// </summary>
public sealed class CatLevelEditorWindow : EditorWindow
{
    private enum Tool { Cats, Holes, Erase }

    private CatPuzzleConfig config;
    private GridData gridAsset;
    private CatLevelData levelAsset;
    private CatPuzzleController controller;
    private CatLevelInstance instance;

    private Tool tool = Tool.Holes;
    private BlockColorTypes selectedColor = BlockColorTypes.Red;
    private readonly HashSet<Vector2Int> selection = new HashSet<Vector2Int>();
    private Vector2 scroll;
    private float cellSize = 26f;
    private bool showCoordinates;
    private readonly List<System.Action> deferred = new List<System.Action>();

    private static readonly Color WallColor = new Color(0.16f, 0.16f, 0.18f);
    private static readonly Color EmptyColor = new Color(0.34f, 0.36f, 0.38f);
    private static readonly Color EmptyAltColor = new Color(0.30f, 0.32f, 0.34f);
    private static readonly Color SelectionColor = new Color(1f, 1f, 1f, 0.35f);

    [MenuItem("Cat Puzzle/Cat Level Editor")]
    public static void ShowWindow()
    {
        CatLevelEditorWindow window = GetWindow<CatLevelEditorWindow>("Cat Level Editor");
        window.minSize = new Vector2(420f, 560f);
    }

    private void OnEnable()
    {
        config = CatPuzzleConfig.Resolve(config);
        RefreshSceneReferences();
    }

    private void OnFocus() => RefreshSceneReferences();

    /// <summary>
    /// Anything that adds or removes scene objects runs on the next layout pass. Doing it
    /// mid-event would change the control count between Layout and Repaint and upset GUILayout.
    /// </summary>
    private void Defer(System.Action action)
    {
        deferred.Add(action);
        Repaint();
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.Layout && deferred.Count > 0)
        {
            List<System.Action> pending = new List<System.Action>(deferred);
            deferred.Clear();
            foreach (System.Action action in pending) action();
        }

        DrawAssets();
        if (levelAsset == null && gridAsset == null)
        {
            EditorGUILayout.HelpBox("Pick a GridData asset to start a new level, or a CatLevelData asset to edit an existing one.", MessageType.Info);
            return;
        }

        DrawSceneActions();
        DrawLevelSettings();
        DrawTools();
        DrawBoard();
        DrawContentList();
    }

    #region Sections
    private void DrawAssets()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);

        config = (CatPuzzleConfig)EditorGUILayout.ObjectField("Puzzle Config", config, typeof(CatPuzzleConfig), false);
        if (config == null)
        {
            EditorGUILayout.HelpBox("No CatPuzzleConfig found. Use Cat Puzzle/Create Default Assets to make one.", MessageType.Warning);
            if (GUILayout.Button("Create Default Assets")) CatPuzzleAssetCreator.CreateDefaults();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUI.BeginChangeCheck();
        levelAsset = (CatLevelData)EditorGUILayout.ObjectField("Level Asset", levelAsset, typeof(CatLevelData), false);
        if (EditorGUI.EndChangeCheck() && levelAsset != null && levelAsset.grid != null) gridAsset = levelAsset.grid;

        gridAsset = (GridData)EditorGUILayout.ObjectField("Grid Asset", gridAsset, typeof(GridData), false);
        if (gridAsset != null) EditorGUILayout.LabelField("Board", $"{gridAsset.gridWidth} x {gridAsset.gridLength}, cell {gridAsset.cellSize}");

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(gridAsset == null))
            if (GUILayout.Button("New Level")) Defer(CreateLevelAsset);
        using (new EditorGUI.DisabledScope(levelAsset == null))
        {
            if (GUILayout.Button("Load Into Scene")) Defer(BuildScene);
            if (GUILayout.Button("Save Level")) Defer(SaveLevel);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawSceneActions()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Scene", EditorStyles.boldLabel);

        controller = (CatPuzzleController)EditorGUILayout.ObjectField("Controller", controller, typeof(CatPuzzleController), true);
        if (controller == null)
        {
            EditorGUILayout.HelpBox("The scene needs one CatPuzzleController. Levels bind themselves to it when they spawn.", MessageType.Warning);
            if (GUILayout.Button("Create Puzzle Controller", GUILayout.Height(24))) Defer(CreateController);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.ObjectField("Loaded Level", instance, typeof(CatLevelInstance), true);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Rebuild From Asset")) Defer(BuildScene);
        if (GUILayout.Button("Snap To Grid")) Defer(SnapContentToGrid);
        if (GUILayout.Button("Clear Scene")) Defer(ClearScene);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawLevelSettings()
    {
        if (levelAsset == null) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        levelAsset.levelName = EditorGUILayout.TextField("Level Name", levelAsset.levelName);
        levelAsset.levelTime = EditorGUILayout.IntField("Level Time", levelAsset.levelTime);
        levelAsset.cameraPosition = EditorGUILayout.Vector3Field("Camera Position", levelAsset.cameraPosition);
        levelAsset.cameraFOV = EditorGUILayout.FloatField("Camera FOV", levelAsset.cameraFOV);
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(levelAsset);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Capture Camera") && Camera.main != null)
        {
            Undo.RecordObject(levelAsset, "Capture Camera");
            levelAsset.cameraPosition = Camera.main.transform.position;
            levelAsset.cameraFOV = Camera.main.fieldOfView;
            EditorUtility.SetDirty(levelAsset);
        }
        if (GUILayout.Button("Move Camera To Level") && Camera.main != null)
        {
            Undo.RecordObject(Camera.main.transform, "Move Camera");
            Camera.main.transform.position = levelAsset.cameraPosition;
            Camera.main.fieldOfView = levelAsset.cameraFOV;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawTools()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        tool = (Tool)GUILayout.Toolbar((int)tool, new[] { "Place Cats", "Draw Holes", "Erase" }, GUILayout.Height(24));
        DrawColorSwatches();

        if (tool == Tool.Holes)
        {
            EditorGUILayout.LabelField($"Selected cells: {selection.Count}");
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(selection.Count == 0))
            {
                if (GUILayout.Button("Create Hole(s) From Selection", GUILayout.Height(24))) Defer(CreateHolesFromSelection);
                if (GUILayout.Button("Clear Selection", GUILayout.Height(24))) { selection.Clear(); Repaint(); }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Paint the cells a hole should cover, then create it. Connected cells become one hole and each cell picks its own piece (Isolated, End Cap, Straight, Corner, One Side, Middle). Separate islands become separate holes.", MessageType.None);
        }
        else if (tool == Tool.Cats)
        {
            EditorGUILayout.HelpBox("Click a cell to place a cat of the selected colour. Clicking a cat that is already there recolours it.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Click a cell to remove the cat or hole on it.", MessageType.None);
        }

        cellSize = EditorGUILayout.Slider("Board Zoom", cellSize, 14f, 48f);
        showCoordinates = EditorGUILayout.Toggle("Show Coordinates", showCoordinates);
        EditorGUILayout.EndVertical();
    }

    private void DrawColorSwatches()
    {
        BlockColorTypes[] colors = (BlockColorTypes[])System.Enum.GetValues(typeof(BlockColorTypes));
        int perRow = 5;
        for (int i = 0; i < colors.Length; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + perRow, colors.Length); j++)
            {
                BlockColorTypes color = colors[j];
                bool isSelected = selectedColor == color;
                GUI.backgroundColor = GameConstants.GetSwatchColor(color);
                GUIStyle style = new GUIStyle(GUI.skin.button) { fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal };
                style.normal.textColor = isSelected ? Color.white : Color.black;
                if (GUILayout.Button(isSelected ? $"[{color}]" : color.ToString(), style, GUILayout.Height(24))) selectedColor = color;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawContentList()
    {
        if (instance == null) return;
        instance.RefreshContents();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Contents — {instance.Cats.Count(cat => cat != null)} cats, {instance.Holes.Count(hole => hole != null)} holes", EditorStyles.boldLabel);

        foreach (CatHole hole in instance.Holes.ToArray())
        {
            if (hole == null) continue;
            EditorGUILayout.BeginHorizontal();
            Rect swatch = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f));
            EditorGUI.DrawRect(swatch, GameConstants.GetSwatchColor(hole.Color));
            EditorGUILayout.LabelField($"{hole.Color} hole · {hole.CellCount} cells · at {hole.OriginCell}");
            if (GUILayout.Button("Select", GUILayout.Width(60f))) Selection.activeGameObject = hole.gameObject;
            if (GUILayout.Button("X", GUILayout.Width(24f)))
            {
                CatHole target = hole;
                Defer(() =>
                {
                    Undo.DestroyObjectImmediate(target.gameObject);
                    if (instance != null) instance.RefreshContents();
                    MarkSceneDirty();
                });
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }
    #endregion

    #region Board
    private void DrawBoard()
    {
        if (gridAsset == null) return;
        int width = gridAsset.gridWidth;
        int height = gridAsset.gridLength;
        float boardWidth = width * cellSize + 24f;
        float boardHeight = height * cellSize + 24f;

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(boardHeight + 12f, 460f)));
        Rect area = GUILayoutUtility.GetRect(boardWidth, boardHeight);
        Rect board = new Rect(area.x + 20f, area.y + 4f, width * cellSize, height * cellSize);

        HandleBoardInput(board, width, height);
        Dictionary<Vector2Int, CatPiece> catsByCell = MapCats();
        Dictionary<Vector2Int, CatHole> holesByCell = MapHoles();

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);
                Rect rect = CellRect(board, cell, height);
                bool playable = gridAsset.IsPlayable(x, z);
                EditorGUI.DrawRect(rect, playable ? ((x + z) % 2 == 0 ? EmptyColor : EmptyAltColor) : WallColor);

                if (holesByCell.TryGetValue(cell, out CatHole hole)) DrawHoleCell(rect, board, cell, height, hole, holesByCell);
                if (catsByCell.TryGetValue(cell, out CatPiece cat)) DrawCatCell(rect, cat);
                if (selection.Contains(cell)) EditorGUI.DrawRect(rect, SelectionColor);

                if (showCoordinates && playable)
                    GUI.Label(rect, $"{x},{z}", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerRight, normal = { textColor = new Color(1f, 1f, 1f, 0.4f) } });

                Handles.color = new Color(0f, 0f, 0f, 0.35f);
                Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.xMax, rect.y));
                Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.x, rect.yMax));
            }
        }

        Handles.color = new Color(0f, 0f, 0f, 0.35f);
        Handles.DrawLine(new Vector3(board.xMax, board.y), new Vector3(board.xMax, board.yMax));
        Handles.DrawLine(new Vector3(board.x, board.yMax), new Vector3(board.xMax, board.yMax));
        EditorGUILayout.EndScrollView();
    }

    private Rect CellRect(Rect board, Vector2Int cell, int height) =>
        new Rect(board.x + cell.x * cellSize, board.y + (height - 1 - cell.y) * cellSize, cellSize, cellSize);

    private void DrawHoleCell(Rect rect, Rect board, Vector2Int cell, int height, CatHole hole, Dictionary<Vector2Int, CatHole> holesByCell)
    {
        Color color = GameConstants.GetSwatchColor(hole.Color);
        EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), new Color(color.r, color.g, color.b, 0.75f));

        // Outline only the sides that leave this hole, so the drawn shape reads at a glance.
        Handles.color = Color.black;
        DrawEdgeIfOutside(rect, board, cell, height, Vector2Int.up, hole, holesByCell);
        DrawEdgeIfOutside(rect, board, cell, height, Vector2Int.down, hole, holesByCell);
        DrawEdgeIfOutside(rect, board, cell, height, Vector2Int.left, hole, holesByCell);
        DrawEdgeIfOutside(rect, board, cell, height, Vector2Int.right, hole, holesByCell);
    }

    private void DrawEdgeIfOutside(Rect rect, Rect board, Vector2Int cell, int height, Vector2Int step, CatHole hole, Dictionary<Vector2Int, CatHole> holesByCell)
    {
        if (holesByCell.TryGetValue(cell + step, out CatHole neighbour) && neighbour == hole) return;
        if (step == Vector2Int.up) Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.xMax, rect.y));
        else if (step == Vector2Int.down) Handles.DrawLine(new Vector3(rect.x, rect.yMax), new Vector3(rect.xMax, rect.yMax));
        else if (step == Vector2Int.left) Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.x, rect.yMax));
        else Handles.DrawLine(new Vector3(rect.xMax, rect.y), new Vector3(rect.xMax, rect.yMax));
    }

    private void DrawCatCell(Rect rect, CatPiece cat)
    {
        float inset = rect.width * 0.22f;
        Rect body = new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f);
        Handles.color = GameConstants.GetSwatchColor(cat.Color);
        Handles.DrawSolidDisc(body.center, Vector3.forward, body.width * 0.5f);
        Handles.color = Color.black;
        Handles.DrawWireDisc(body.center, Vector3.forward, body.width * 0.5f);
    }

    private void HandleBoardInput(Rect board, int width, int height)
    {
        Event evt = Event.current;
        bool paint = evt.type == EventType.MouseDown || (evt.type == EventType.MouseDrag && tool != Tool.Cats);
        if (!paint || evt.button != 0 || !board.Contains(evt.mousePosition)) return;

        int x = Mathf.FloorToInt((evt.mousePosition.x - board.x) / cellSize);
        int z = height - 1 - Mathf.FloorToInt((evt.mousePosition.y - board.y) / cellSize);
        if (x < 0 || x >= width || z < 0 || z >= height) return;

        Vector2Int cell = new Vector2Int(x, z);
        switch (tool)
        {
            case Tool.Cats: Defer(() => PlaceCat(cell)); break;
            case Tool.Holes:
                if (!gridAsset.IsPlayable(cell)) break;
                if (evt.type == EventType.MouseDrag) selection.Add(cell);
                else if (!selection.Add(cell)) selection.Remove(cell);
                break;
            case Tool.Erase: Defer(() => EraseAt(cell)); break;
        }
        evt.Use();
        Repaint();
    }
    #endregion

    #region Scene operations
    private void RefreshSceneReferences()
    {
        if (controller == null) controller = FindFirstObjectByType<CatPuzzleController>();
        instance = controller != null ? controller.LevelRoot.GetComponentInChildren<CatLevelInstance>(true) : null;
        if (instance != null && instance.Source != null)
        {
            if (levelAsset == null) levelAsset = instance.Source;
            if (gridAsset == null) gridAsset = instance.Source.grid;
        }
    }

    private void CreateController()
    {
        GameObject controllerObject = new GameObject("Cat Puzzle Controller");
        Undo.RegisterCreatedObjectUndo(controllerObject, "Create Cat Puzzle Controller");
        controller = controllerObject.AddComponent<CatPuzzleController>();
        controllerObject.AddComponent<CatHoleInputManager>();

        GameObject root = new GameObject("Level Root");
        Undo.RegisterCreatedObjectUndo(root, "Create Level Root");
        root.transform.SetParent(controllerObject.transform, false);

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("levelRoot").objectReferenceValue = root.transform;
        serialized.ApplyModifiedProperties();
        MarkSceneDirty();
    }

    private void CreateLevelAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create Cat Level", "New Cat Level", "asset",
            "Choose where to save this level.", "Assets/_GameData/Systems/Scriptable Objects/Levels");
        if (string.IsNullOrEmpty(path)) return;

        levelAsset = CreateInstance<CatLevelData>();
        levelAsset.levelName = System.IO.Path.GetFileNameWithoutExtension(path);
        levelAsset.grid = gridAsset;
        AssetDatabase.CreateAsset(levelAsset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = levelAsset;
        BuildScene();
    }

    private void BuildScene()
    {
        if (levelAsset == null || controller == null) { EditorUtility.DisplayDialog("Cat Level Editor", "A level asset and a scene controller are both needed.", "OK"); return; }
        if (levelAsset.grid == null) levelAsset.grid = gridAsset;
        if (levelAsset.grid == null) { EditorUtility.DisplayDialog("Cat Level Editor", "Assign a GridData asset to this level first.", "OK"); return; }

        ClearScene();
        instance = CatLevelBuilder.Build(levelAsset, controller.LevelRoot, config);
        if (instance != null) controller.BindLevel(instance);
        gridAsset = levelAsset.grid;
        selection.Clear();
        MarkSceneDirty();
    }

    private void ClearScene()
    {
        if (controller == null) return;
        Transform root = controller.LevelRoot;
        for (int i = root.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
        controller.UnbindLevel(instance);
        instance = null;
        MarkSceneDirty();
    }

    private void SaveLevel()
    {
        if (levelAsset == null) { CreateLevelAsset(); return; }
        if (instance == null) { EditorUtility.DisplayDialog("Cat Level Editor", "Nothing is loaded in the scene to save.", "OK"); return; }

        Undo.RecordObject(levelAsset, "Save Cat Level");
        SnapContentToGrid();
        CatLevelBuilder.Capture(instance, levelAsset);
        levelAsset.grid = gridAsset;
        EditorUtility.SetDirty(levelAsset);
        AssetDatabase.SaveAssets();
        Debug.Log($"Saved '{levelAsset.DisplayName}': {levelAsset.cats.Count} cats, {levelAsset.holes.Count} holes.", levelAsset);
    }

    /// <summary>Re-snaps everything the designer dragged around back onto whole cells.</summary>
    private void SnapContentToGrid()
    {
        if (instance == null || instance.Grid == null) return;
        instance.RefreshContents();

        foreach (CatPiece cat in instance.Cats)
        {
            if (cat == null) continue;
            Undo.RecordObject(cat.transform, "Snap Cat");
            CatLevelBuilder.PlaceCat(cat, instance.Grid.LocalPositionToCell(cat.transform.localPosition), instance.Grid, config);
        }
        foreach (CatHole hole in instance.Holes)
        {
            if (hole == null) continue;
            Undo.RecordObject(hole.transform, "Snap Hole");
            CatHoleBuilder.MoveTo(hole, instance.Grid.LocalPositionToCell(hole.transform.localPosition), instance.Grid, config);
        }
        MarkSceneDirty();
    }

    private void PlaceCat(Vector2Int cell)
    {
        if (!EnsureSceneLevel() || !gridAsset.IsPlayable(cell)) return;

        CatPiece existing = MapCats().TryGetValue(cell, out CatPiece found) ? found : null;
        if (existing != null)
        {
            Undo.RecordObject(existing, "Recolour Cat");
            existing.Configure(selectedColor, cell);
            existing.name = $"Cat_{selectedColor}_{cell.x}_{cell.y}";
        }
        else
        {
            CatLevelBuilder.SpawnCat(new CatPlacement { color = selectedColor, cell = cell }, instance, config);
        }
        MarkSceneDirty();
    }

    private void CreateHolesFromSelection()
    {
        if (!EnsureSceneLevel() || selection.Count == 0) return;
        foreach (CatHolePlacement placement in CatHoleBuilder.SplitIntoPlacements(selection, selectedColor))
            CatLevelBuilder.SpawnHole(placement, instance, config);
        selection.Clear();
        MarkSceneDirty();
    }

    private void EraseAt(Vector2Int cell)
    {
        if (instance == null) return;
        if (MapCats().TryGetValue(cell, out CatPiece cat) && cat != null) Undo.DestroyObjectImmediate(cat.gameObject);
        else if (MapHoles().TryGetValue(cell, out CatHole hole) && hole != null) Undo.DestroyObjectImmediate(hole.gameObject);
        selection.Remove(cell);
        instance.RefreshContents();
        MarkSceneDirty();
    }

    private bool EnsureSceneLevel()
    {
        if (instance != null) return true;
        BuildScene();
        return instance != null;
    }

    private Dictionary<Vector2Int, CatPiece> MapCats()
    {
        Dictionary<Vector2Int, CatPiece> map = new Dictionary<Vector2Int, CatPiece>();
        if (instance == null) return map;
        foreach (CatPiece cat in instance.Cats)
        {
            if (cat == null) continue;
            map[CellOf(cat.transform, cat.GridPosition)] = cat;
        }
        return map;
    }

    private Dictionary<Vector2Int, CatHole> MapHoles()
    {
        Dictionary<Vector2Int, CatHole> map = new Dictionary<Vector2Int, CatHole>();
        if (instance == null) return map;
        foreach (CatHole hole in instance.Holes)
        {
            if (hole == null) continue;
            Vector2Int origin = CellOf(hole.transform, hole.OriginCell);
            foreach (Vector2Int offset in hole.Offsets) map[origin + offset] = hole;
        }
        return map;
    }

    /// <summary>Reads a live transform back to a cell so the board reflects manual moves immediately.</summary>
    private Vector2Int CellOf(Transform target, Vector2Int fallback) =>
        instance != null && instance.Grid != null ? instance.Grid.LocalPositionToCell(target.localPosition) : fallback;

    private void MarkSceneDirty()
    {
        if (controller != null) EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        Repaint();
    }
    #endregion
}
#endif
