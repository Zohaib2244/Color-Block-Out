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
    private LevelSpawner spawner;
    private CatLevelInstance instance;

    private Tool tool = Tool.Holes;
    private string newLevelName = "Level_1";
    private int selectedColorId;
    private readonly HashSet<Vector2Int> selection = new HashSet<Vector2Int>();
    private Vector2 scroll;
    private float cellSize = 26f;
    private bool showCoordinates;
    private bool showIssues = true;
    private readonly List<System.Action> deferred = new List<System.Action>();
    private List<CatLevelValidator.Issue> issues = new List<CatLevelValidator.Issue>();

    private CatColorPalette Palette => config != null ? config.palette : null;

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
        if (config == null) config = CatPuzzleAssetCreator.FindConfig();
        RefreshSceneReferences();
    }

    private void OnFocus()
    {
        RefreshSceneReferences();
        // Tints are property blocks and do not survive a scene reload, so restore them on return.
        CatLevelBuilder.ApplyColors(instance, config);
    }

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
        DrawIssues();
        DrawContentList();
    }

    private void DrawIssues()
    {
        if (levelAsset == null) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        showIssues = EditorGUILayout.Foldout(showIssues, $"Validation — {issues.Count} issue(s)", true);
        if (GUILayout.Button("Validate", GUILayout.Width(80f))) Defer(Validate);
        EditorGUILayout.EndHorizontal();

        if (showIssues)
        {
            if (issues.Count == 0) EditorGUILayout.HelpBox("No problems found. Validate again after editing.", MessageType.Info);
            foreach (CatLevelValidator.Issue issue in issues)
                EditorGUILayout.HelpBox(issue.message, issue.severity == CatLevelValidator.Severity.Error ? MessageType.Error : MessageType.Warning);
        }
        EditorGUILayout.EndVertical();
    }

    /// <summary>Captures the scene into a scratch copy first, so what is checked is what would save.</summary>
    private void Validate()
    {
        if (levelAsset == null) return;
        if (instance != null)
        {
            CatLevelData snapshot = CreateInstance<CatLevelData>();
            snapshot.grid = levelAsset.grid != null ? levelAsset.grid : gridAsset;
            CatLevelBuilder.Capture(instance, snapshot);
            issues = CatLevelValidator.Validate(snapshot, Palette);
            DestroyImmediate(snapshot);
        }
        else issues = CatLevelValidator.Validate(levelAsset, Palette);
        Repaint();
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

        newLevelName = EditorGUILayout.TextField("New Level Name", newLevelName);
        EditorGUILayout.LabelField(" ", $"Saved to {CatPuzzleAssetCreator.LevelFolder}", EditorStyles.miniLabel);

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

        spawner = (LevelSpawner)EditorGUILayout.ObjectField("Spawner", spawner, typeof(LevelSpawner), true);
        controller = (CatPuzzleController)EditorGUILayout.ObjectField("Rules", controller, typeof(CatPuzzleController), true);
        if (spawner == null)
        {
            EditorGUILayout.HelpBox("The scene needs a LevelSpawner to build levels under, plus a CatPuzzleController for the rules and a LevelManager to own the level.", MessageType.Warning);
            if (GUILayout.Button("Create Puzzle Rig", GUILayout.Height(24))) Defer(CreateRig);
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
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(levelAsset);
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
        CatColorPalette palette = Palette;
        if (palette == null || palette.Count == 0)
        {
            EditorGUILayout.HelpBox("The puzzle config has no colour palette, or the palette is empty. Add colours to it to paint with them.", MessageType.Warning);
            if (palette != null && GUILayout.Button("Select Palette")) Selection.activeObject = palette;
            return;
        }

        List<int> ids = palette.Ids();
        if (!ids.Contains(selectedColorId)) selectedColorId = palette.DefaultId;

        int perRow = 5;
        for (int i = 0; i < ids.Count; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + perRow, ids.Count); j++)
            {
                int id = ids[j];
                bool isSelected = selectedColorId == id;
                GUI.backgroundColor = palette.GetColor(id);
                GUIStyle style = new GUIStyle(GUI.skin.button) { fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal };
                style.normal.textColor = isSelected ? Color.white : Color.black;
                string label = palette.GetName(id);
                if (GUILayout.Button(isSelected ? $"[{label}]" : label, style, GUILayout.Height(24))) selectedColorId = id;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
    }

    private Color SwatchOf(int colorId)
    {
        CatColorPalette palette = Palette;
        return palette != null ? palette.GetColor(colorId) : Color.magenta;
    }

    private string ColorName(int colorId)
    {
        CatColorPalette palette = Palette;
        return palette != null ? palette.GetName(colorId) : colorId.ToString();
    }

    private void DrawContentList()
    {
        if (instance == null) return;
        // Only on Layout: this walks the hierarchy, and doing it on every repaint is wasteful.
        if (Event.current.type == EventType.Layout) instance.RefreshContents();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Contents — {instance.Cats.Count(cat => cat != null)} cats, {instance.Holes.Count(hole => hole != null)} holes", EditorStyles.boldLabel);

        foreach (CatHole hole in instance.Holes.ToArray())
        {
            if (hole == null) continue;
            EditorGUILayout.BeginHorizontal();
            Rect swatch = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f));
            EditorGUI.DrawRect(swatch, SwatchOf(hole.ColorId));
            EditorGUILayout.LabelField($"{ColorName(hole.ColorId)} hole · {hole.CellCount} cells · at {hole.OriginCell}");
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
        Color color = SwatchOf(hole.ColorId);
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
        Handles.color = SwatchOf(cat.ColorId);
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
        if (spawner == null) spawner = FindFirstObjectByType<LevelSpawner>();
        instance = spawner != null ? spawner.LevelRoot.GetComponentInChildren<CatLevelInstance>(true) : null;
        if (instance != null && instance.Source != null)
        {
            if (levelAsset == null) levelAsset = instance.Source;
            if (gridAsset == null) gridAsset = instance.Source.grid;
        }
    }

    /// <summary>Builds the whole scene rig: rules, level ownership and spawning, wired together.</summary>
    private void CreateRig()
    {
        GameObject rig = new GameObject("Cat Puzzle");
        Undo.RegisterCreatedObjectUndo(rig, "Create Cat Puzzle Rig");
        controller = rig.AddComponent<CatPuzzleController>();
        rig.AddComponent<CatHoleInputManager>();
        LevelManager levelManager = rig.AddComponent<LevelManager>();
        spawner = rig.AddComponent<LevelSpawner>();

        GameObject root = new GameObject("Level Root");
        Undo.RegisterCreatedObjectUndo(root, "Create Level Root");
        root.transform.SetParent(rig.transform, false);

        SerializedObject serialized = new SerializedObject(spawner);
        serialized.FindProperty("levelRoot").objectReferenceValue = root.transform;
        // Nothing is loaded implicitly any more, so wire the config while we are here.
        serialized.FindProperty("config").objectReferenceValue = config;
        serialized.FindProperty("levelManager").objectReferenceValue = levelManager;
        serialized.FindProperty("puzzle").objectReferenceValue = controller;
        serialized.ApplyModifiedProperties();

        SerializedObject managerSerialized = new SerializedObject(levelManager);
        managerSerialized.FindProperty("puzzle").objectReferenceValue = controller;
        managerSerialized.ApplyModifiedProperties();
        MarkSceneDirty();
    }

    private void CreateLevelAsset()
    {
        // Levels are filed automatically; the name field is the only decision.
        CatPuzzleAssetCreator.EnsureFolder(CatPuzzleAssetCreator.LevelFolder);
        string safeName = string.IsNullOrWhiteSpace(newLevelName) ? "New Cat Level" : newLevelName.Trim();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{CatPuzzleAssetCreator.LevelFolder}/{safeName}.asset");

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
        if (levelAsset == null || spawner == null) { EditorUtility.DisplayDialog("Cat Level Editor", "A level asset and a scene LevelSpawner are both needed.", "OK"); return; }
        if (levelAsset.grid == null) levelAsset.grid = gridAsset;
        if (levelAsset.grid == null) { EditorUtility.DisplayDialog("Cat Level Editor", "Assign a GridData asset to this level first.", "OK"); return; }

        ClearScene();
        instance = CatLevelBuilder.Build(levelAsset, spawner.LevelRoot, config);
        if (instance != null && controller != null) controller.SetLevel(instance);
        gridAsset = levelAsset.grid;
        selection.Clear();
        MarkSceneDirty();
    }

    private void ClearScene()
    {
        if (spawner == null) return;
        Transform root = spawner.LevelRoot;
        for (int i = root.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
        if (controller != null) controller.ClearLevel();
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

        issues = CatLevelValidator.Validate(levelAsset, Palette);
        string summary = $"Saved '{levelAsset.DisplayName}': {levelAsset.cats.Count} cats, {levelAsset.holes.Count} holes.";
        if (CatLevelValidator.HasErrors(issues))
        {
            showIssues = true;
            Debug.LogError($"{summary}\nThe level has problems that will break it:\n{CatLevelValidator.Describe(issues)}", levelAsset);
        }
        else Debug.Log(summary, levelAsset);
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
            CatLevelBuilder.PlaceCat(cat, instance.Grid.LocalPositionToCell(cat.transform.localPosition), instance.Grid);
        }
        foreach (CatHole hole in instance.Holes)
        {
            if (hole == null) continue;
            Undo.RecordObject(hole.transform, "Snap Hole");
            CatHoleBuilder.MoveTo(hole, instance.Grid.LocalPositionToCell(hole.transform.localPosition), instance.Grid);
        }
        MarkSceneDirty();
    }

    private void PlaceCat(Vector2Int cell)
    {
        if (!EnsureSceneLevel() || !gridAsset.IsPlayable(cell)) return;

        // The instance keeps serialized lists of its content, so undo has to cover it too.
        Undo.RecordObject(instance, "Edit Cat Level");

        CatPiece existing = MapCats().TryGetValue(cell, out CatPiece found) ? found : null;
        if (existing != null)
        {
            Undo.RecordObject(existing, "Recolour Cat");
            existing.Configure(selectedColorId, cell, Palette);
            existing.name = $"Cat_{ColorName(selectedColorId)}_{cell.x}_{cell.y}";
        }
        else
        {
            CatLevelBuilder.SpawnCat(new CatPlacement { colorId = selectedColorId, cell = cell }, instance, config);
        }
        MarkSceneDirty();
    }

    private void CreateHolesFromSelection()
    {
        if (!EnsureSceneLevel() || selection.Count == 0) return;
        Undo.RecordObject(instance, "Create Holes");
        foreach (CatHolePlacement placement in CatHoleBuilder.SplitIntoPlacements(selection, selectedColorId))
            CatLevelBuilder.SpawnHole(placement, instance, config);
        selection.Clear();
        MarkSceneDirty();
    }

    private void EraseAt(Vector2Int cell)
    {
        if (instance == null) return;
        Undo.RecordObject(instance, "Erase Cell");
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
