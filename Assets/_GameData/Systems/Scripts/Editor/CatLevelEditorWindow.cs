#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Authoring tool for cat levels. Start from a grid file, build it in the scene, then paint cats and
/// draw holes straight onto the board. The scene is the working document: anything you drag around
/// afterwards is picked up again when you save.
///
/// A level is a JSON file - the same format the web level editor exports and the game loads - so
/// Save Level writes that file and Load Into Scene reads it back. The level carries its own copy of
/// the board, so it is a single self-contained file once saved.
/// </summary>
public sealed class CatLevelEditorWindow : EditorWindow
{
    private enum Tool { Cats, Holes, Erase }

    // Serialized so the window still knows which files it was working on after a domain reload.
    // The parsed level does not survive - it is read back from levelFile in RefreshSceneReferences.
    [SerializeField] private CatPuzzleConfig config;
    [SerializeField] private TextAsset gridFile;
    [SerializeField] private TextAsset levelFile;
    [SerializeField] private LevelData collection;

    private CatPuzzleController controller;
    private LevelSpawner spawner;
    private CatLevelInstance instance;

    /// <summary>The level being edited, in memory. The file only changes when Save Level is pressed.</summary>
    private CatLevelData level;

    /// <summary>The board the level stands on: the level's own copy once there is a level.</summary>
    private GridData grid;

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

        DrawFiles();
        if (level == null && grid == null)
        {
            EditorGUILayout.HelpBox("Pick a grid file to start a new level, or a level file to edit an existing one. " +
                                    "Both are the JSON the web level editor exports.", MessageType.Info);
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
        if (level == null) return;
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
        if (level == null) return;
        issues = CatLevelValidator.Validate(instance != null ? Snapshot() : level, Palette);
        Repaint();
    }

    /// <summary>The level as the scene currently has it, without touching what is being edited.</summary>
    private CatLevelData Snapshot()
    {
        CatLevelData snapshot = new CatLevelData
        {
            levelName = level.levelName,
            levelTime = level.levelTime,
            grid = grid != null ? grid.Clone() : null
        };
        CatLevelBuilder.Capture(instance, snapshot);
        return snapshot;
    }

    #region Sections
    private void DrawFiles()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Files", EditorStyles.boldLabel);

        config = (CatPuzzleConfig)EditorGUILayout.ObjectField("Puzzle Config", config, typeof(CatPuzzleConfig), false);
        if (config == null)
        {
            EditorGUILayout.HelpBox("No CatPuzzleConfig found. Use Cat Puzzle/Create Default Assets to make one.", MessageType.Warning);
            if (GUILayout.Button("Create Default Assets")) CatPuzzleAssetCreator.CreateDefaults();
            EditorGUILayout.EndVertical();
            return;
        }

        // A picked file is only adopted once it has been read, so one that turns out not to be a
        // level leaves the level already open - and the path Save Level writes to - untouched.
        EditorGUI.BeginChangeCheck();
        TextAsset pickedLevel = (TextAsset)EditorGUILayout.ObjectField("Level File", levelFile, typeof(TextAsset), false);
        if (EditorGUI.EndChangeCheck() && pickedLevel != levelFile) Defer(() => LoadLevelFile(pickedLevel));

        EditorGUI.BeginChangeCheck();
        TextAsset pickedGrid = (TextAsset)EditorGUILayout.ObjectField("Grid File", gridFile, typeof(TextAsset), false);
        if (EditorGUI.EndChangeCheck() && pickedGrid != gridFile) Defer(() => LoadGridFile(pickedGrid));

        if (grid != null) EditorGUILayout.LabelField("Board", $"{grid.gridWidth} x {grid.gridLength}, cell {grid.cellSize}");

        newLevelName = EditorGUILayout.TextField("New Level Name", newLevelName);
        EditorGUILayout.LabelField(" ", $"Saved as JSON to {CatLevelFiles.LevelFolder}", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(grid == null))
            if (GUILayout.Button("New Level")) Defer(CreateLevel);
        using (new EditorGUI.DisabledScope(level == null))
        {
            if (GUILayout.Button("Load Into Scene")) Defer(BuildScene);
            if (GUILayout.Button("Save Level")) Defer(SaveLevel);
        }
        EditorGUILayout.EndHorizontal();

        DrawCollection();
        EditorGUILayout.EndVertical();
    }

    /// <summary>A level only reaches the game once its file is in a collection's play order.</summary>
    private void DrawCollection()
    {
        collection = (LevelData)EditorGUILayout.ObjectField("Play Order", collection, typeof(LevelData), false);
        if (collection == null) return;

        bool alreadyIn = levelFile != null && collection.levelFiles.Contains(levelFile);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(" ", alreadyIn
            ? $"In '{collection.name}' at #{collection.levelFiles.IndexOf(levelFile) + 1} of {collection.Count}"
            : $"Not in '{collection.name}' ({collection.Count} levels)", EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(levelFile == null || alreadyIn))
            if (GUILayout.Button("Add To Play Order", GUILayout.Width(140f))) Defer(AddToCollection);
        EditorGUILayout.EndHorizontal();
    }

    private void AddToCollection()
    {
        if (collection == null || levelFile == null || collection.levelFiles.Contains(levelFile)) return;
        Undo.RecordObject(collection, "Add Level To Play Order");
        collection.levelFiles.Add(levelFile);
        collection.ClearCache();
        EditorUtility.SetDirty(collection);
        AssetDatabase.SaveAssets();
        Debug.Log($"Added '{levelFile.name}' to '{collection.name}' as level {collection.Count}.", collection);
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
        if (GUILayout.Button("Rebuild From File")) Defer(BuildScene);
        if (GUILayout.Button("Snap To Grid")) Defer(SnapContentToGrid);
        if (GUILayout.Button("Clear Scene")) Defer(ClearScene);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawLevelSettings()
    {
        if (level == null) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);

        level.levelName = EditorGUILayout.TextField("Level Name", level.levelName);
        level.levelTime = EditorGUILayout.IntField("Level Time", level.levelTime);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Board", grid != null && !string.IsNullOrEmpty(grid.gridName) ? grid.gridName : "(unnamed)");
        using (new EditorGUI.DisabledScope(grid == null))
            if (GUILayout.Button("Save Board As Grid File", GUILayout.Width(170f))) Defer(SaveBoardAsGridFile);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Level settings are written when Save Level is pressed. The level carries its own copy of the board, " +
                                "so reshaping the grid file later leaves this level as it is.", MessageType.None);
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
            EditorGUILayout.HelpBox("Click a cell to place a cat of the selected colour. Clicking a cat of a different colour recolours it; clicking one that's already the selected colour stacks another on top. Shift+click always stacks, even over a different colour.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Click a cell to remove the cat or hole on it. On a stack of cats this removes only the top one.", MessageType.None);
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
        if (grid == null) return;
        int width = grid.gridWidth;
        int height = grid.gridLength;
        float boardWidth = width * cellSize + 24f;
        float boardHeight = height * cellSize + 24f;

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(boardHeight + 12f, 460f)));
        Rect area = GUILayoutUtility.GetRect(boardWidth, boardHeight);
        Rect board = new Rect(area.x + 20f, area.y + 4f, width * cellSize, height * cellSize);

        HandleBoardInput(board, width, height);
        Dictionary<Vector2Int, List<CatPiece>> catStacksByCell = MapCatStacks();
        Dictionary<Vector2Int, CatHole> holesByCell = MapHoles();

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);
                Rect rect = CellRect(board, cell, height);
                bool playable = grid.IsPlayable(x, z);
                EditorGUI.DrawRect(rect, playable ? ((x + z) % 2 == 0 ? EmptyColor : EmptyAltColor) : WallColor);

                if (holesByCell.TryGetValue(cell, out CatHole hole)) DrawHoleCell(rect, board, cell, height, hole, holesByCell);
                if (catStacksByCell.TryGetValue(cell, out List<CatPiece> stack)) DrawCatCell(rect, stack);
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

    /// <summary>Draws every cat on a cell as concentric rings, outermost = bottom of the stack, so a stack reads at a glance.</summary>
    private void DrawCatCell(Rect rect, List<CatPiece> stack)
    {
        float inset = rect.width * 0.22f;
        Rect body = new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f);
        float maxRadius = body.width * 0.5f;
        float shrinkPerLayer = stack.Count > 1 ? maxRadius * 0.22f : 0f;

        for (int i = stack.Count - 1; i >= 0; i--)
        {
            float radius = maxRadius - (stack.Count - 1 - i) * shrinkPerLayer;
            Handles.color = SwatchOf(stack[i].ColorId);
            Handles.DrawSolidDisc(body.center, Vector3.forward, radius);
            Handles.color = Color.black;
            Handles.DrawWireDisc(body.center, Vector3.forward, radius);
        }

        if (stack.Count > 1)
        {
            GUIStyle badge = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.UpperRight, normal = { textColor = Color.white } };
            GUI.Label(rect, $"x{stack.Count}", badge);
        }
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
            case Tool.Cats:
                bool stackOnTop = evt.shift;
                Defer(() => PlaceCat(cell, stackOnTop));
                break;
            case Tool.Holes:
                if (!grid.IsPlayable(cell)) break;
                if (evt.type == EventType.MouseDrag) selection.Add(cell);
                else if (!selection.Add(cell)) selection.Remove(cell);
                break;
            case Tool.Erase: Defer(() => EraseAt(cell)); break;
        }
        evt.Use();
        Repaint();
    }
    #endregion

    #region Files
    private void LoadLevelFile(TextAsset file)
    {
        if (file == null)
        {
            levelFile = null;
            level = null;
            issues.Clear();
            Repaint();
            return;
        }

        CatLevelJson.LevelResult result = CatLevelJson.ParseLevel(file.text, file.name, config);
        if (!result.Succeeded)
        {
            EditorUtility.DisplayDialog("Cat Level Editor", $"'{file.name}' could not be opened as a level.\n\n{result.Describe()}", "OK");
            return;
        }

        levelFile = file;
        level = result.level;
        grid = level.grid;
        newLevelName = level.DisplayName;
        issues = result.issues;
        showIssues = issues.Count > 0;
        selection.Clear();
        BuildScene();
    }

    private void LoadGridFile(TextAsset file)
    {
        if (file == null) return;

        if (!CatLevelJson.TryParseGrid(file.text, file.name, out GridData data, out string error))
        {
            EditorUtility.DisplayDialog("Cat Level Editor", $"'{file.name}' could not be opened as a grid.\n\n{error}", "OK");
            return;
        }

        // Picking a board is how a new level starts; it does not reshape the one already open.
        if (level != null &&
            !EditorUtility.DisplayDialog("Cat Level Editor",
                $"Rebuild '{level.DisplayName}' on board '{data.gridName}'?\n\n" +
                "Cats and holes stay where they are, so anything now standing on a blocked cell will be flagged.",
                "Rebuild", "Keep Current Board"))
        {
            gridFile = file;
            return;
        }

        gridFile = file;
        grid = data;
        if (level != null)
        {
            level.grid = data.Clone();
            BuildScene();
        }
        selection.Clear();
        Repaint();
    }

    private void CreateLevel()
    {
        if (grid == null) return;

        string safeName = string.IsNullOrWhiteSpace(newLevelName) ? "New Cat Level" : newLevelName.Trim();
        level = new CatLevelData { levelName = safeName, grid = grid.Clone() };

        TextAsset written = CatLevelFiles.WriteLevel(level, config, CatLevelFiles.UniquePath(CatLevelFiles.LevelFolder, safeName), out string error);
        if (written == null)
        {
            EditorUtility.DisplayDialog("Cat Level Editor", $"The level file could not be created.\n\n{error}", "OK");
            level = null;
            return;
        }

        levelFile = written;
        Selection.activeObject = written;
        selection.Clear();
        BuildScene();
    }

    private void SaveLevel()
    {
        if (level == null) { CreateLevel(); return; }
        if (instance == null) { EditorUtility.DisplayDialog("Cat Level Editor", "Nothing is loaded in the scene to save.", "OK"); return; }

        SnapContentToGrid();
        CatLevelBuilder.Capture(instance, level);
        if (grid != null) level.grid = grid.Clone();

        string path = levelFile != null ? AssetDatabase.GetAssetPath(levelFile) : null;
        TextAsset written = CatLevelFiles.WriteLevel(level, config, path, out string error);
        if (written == null)
        {
            EditorUtility.DisplayDialog("Cat Level Editor", $"The level could not be saved.\n\n{error}", "OK");
            return;
        }

        levelFile = written;
        if (collection != null) collection.ClearCache();

        issues = CatLevelValidator.Validate(level, Palette);
        string summary = $"Saved '{level.DisplayName}' to {AssetDatabase.GetAssetPath(written)}: {level.cats.Count} cats, {level.holes.Count} holes.";
        if (CatLevelValidator.HasErrors(issues))
        {
            showIssues = true;
            Debug.LogError($"{summary}\nThe level has problems that will break it:\n{CatLevelValidator.Describe(issues)}", written);
        }
        else Debug.Log(summary, written);
    }

    /// <summary>Writes this level's board out on its own, so another level can be started from it.</summary>
    private void SaveBoardAsGridFile()
    {
        if (grid == null) return;
        if (string.IsNullOrEmpty(grid.gridName)) grid.gridName = level != null ? $"{level.DisplayName} Grid" : "New Grid";

        TextAsset written = CatLevelFiles.WriteGrid(grid, CatLevelFiles.UniquePath(CatLevelFiles.GridFolder, grid.gridName), out string error);
        if (written == null)
        {
            EditorUtility.DisplayDialog("Cat Level Editor", $"The board could not be written.\n\n{error}", "OK");
            return;
        }

        gridFile = written;
        Selection.activeObject = written;
        Debug.Log($"Wrote board '{grid.gridName}' to {AssetDatabase.GetAssetPath(written)}.", written);
    }
    #endregion

    #region Scene operations
    private void RefreshSceneReferences()
    {
        if (controller == null) controller = FindFirstObjectByType<CatPuzzleController>();
        if (spawner == null) spawner = FindFirstObjectByType<LevelSpawner>();
        instance = spawner != null ? spawner.LevelRoot.GetComponentInChildren<CatLevelInstance>(true) : null;

        // A domain reload drops the parsed level but keeps the file, so read it back rather than
        // starting over - otherwise Save Level would file a second copy instead of updating this one.
        if (level == null && levelFile != null)
        {
            CatLevelJson.LevelResult result = CatLevelJson.ParseLevel(levelFile.text, levelFile.name, config);
            if (result.Succeeded)
            {
                level = result.level;
                grid = level.grid;
                newLevelName = level.DisplayName;
                issues = result.issues;
            }
        }

        // Failing that, a level left in the scene carries its own copy of what it was built from,
        // so editing can pick up where it left off even with no file to hand. Saving it files a new
        // one, which is the honest outcome: nothing here knows where it came from.
        if (level == null && instance != null && instance.Source != null)
        {
            level = instance.Source;
            grid = level.grid;
            newLevelName = level.DisplayName;
        }

        if (level == null && gridFile != null && grid == null &&
            CatLevelJson.TryParseGrid(gridFile.text, gridFile.name, out GridData board, out _))
            grid = board;
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

    private void BuildScene()
    {
        if (level == null || spawner == null) { EditorUtility.DisplayDialog("Cat Level Editor", "A level and a scene LevelSpawner are both needed.", "OK"); return; }
        if (level.grid == null) level.grid = grid != null ? grid.Clone() : null;
        if (level.grid == null) { EditorUtility.DisplayDialog("Cat Level Editor", "Pick a grid file for this level first.", "OK"); return; }

        ClearScene();
        instance = CatLevelBuilder.Build(level, spawner.LevelRoot, config);
        if (instance != null && controller != null) controller.SetLevel(instance);
        grid = level.grid;
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

    /// <summary>Re-snaps everything the designer dragged around back onto whole cells.</summary>
    private void SnapContentToGrid()
    {
        if (instance == null || instance.Grid == null) return;
        instance.RefreshContents();

        foreach (CatPiece cat in instance.Cats)
        {
            if (cat == null) continue;
            Undo.RecordObject(cat.transform, "Snap Cat");
            cat.SetGridPosition(instance.Grid.LocalPositionToCell(cat.transform.localPosition));
        }
        CatLevelBuilder.RestackCats(instance.Cats, instance.Grid, config.catStackHeight);
        foreach (CatHole hole in instance.Holes)
        {
            if (hole == null) continue;
            Undo.RecordObject(hole.transform, "Snap Hole");
            CatHoleBuilder.MoveTo(hole, instance.Grid.LocalPositionToCell(hole.transform.localPosition), instance.Grid);
        }
        MarkSceneDirty();
    }

    /// <summary>
    /// Places a cat of the selected colour. An empty cell just gets one. A cell that already has a
    /// cat of a *different* colour gets its top cat recoloured. A cell whose top cat already
    /// matches the selected colour adds another cat on top instead, since recolouring it to what it
    /// already is would do nothing — this is also how same-colour stacks get built without needing
    /// shift. <paramref name="stackOnTop"/> (a shift+click) always adds on top regardless of colour.
    /// </summary>
    private void PlaceCat(Vector2Int cell, bool stackOnTop)
    {
        if (!EnsureSceneLevel() || !grid.IsPlayable(cell)) return;

        // The instance keeps serialized lists of its content, so undo has to cover it too.
        Undo.RecordObject(instance, "Edit Cat Level");

        List<CatPiece> existing = MapCatStacks().TryGetValue(cell, out List<CatPiece> found) ? found : null;
        CatPiece top = existing != null && existing.Count > 0 ? existing[existing.Count - 1] : null;
        bool addNew = top == null || stackOnTop || top.ColorId == selectedColorId;

        if (!addNew)
        {
            Undo.RecordObject(top, "Recolour Cat");
            top.Configure(selectedColorId, cell, Palette);
            top.name = $"Cat_{ColorName(selectedColorId)}_{cell.x}_{cell.y}";
        }
        else
        {
            CatLevelBuilder.SpawnCat(new CatPlacement { colorId = selectedColorId, cell = cell }, instance, config);
            CatLevelBuilder.RestackCats(instance.Cats, instance.Grid, config.catStackHeight);
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

    /// <summary>Removes one cat/hole from a cell. A stack of cats loses only its top one per click.</summary>
    private void EraseAt(Vector2Int cell)
    {
        if (instance == null) return;
        Undo.RecordObject(instance, "Erase Cell");
        if (MapCatStacks().TryGetValue(cell, out List<CatPiece> stack) && stack.Count > 0)
        {
            Undo.DestroyObjectImmediate(stack[stack.Count - 1].gameObject);
            instance.RefreshContents();
            CatLevelBuilder.RestackCats(instance.Cats, instance.Grid, config.catStackHeight);
        }
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

    /// <summary>Cats per cell, ordered bottom of the stack first (lowest local Y) so the last entry is always the top one.</summary>
    private Dictionary<Vector2Int, List<CatPiece>> MapCatStacks()
    {
        Dictionary<Vector2Int, List<CatPiece>> map = new Dictionary<Vector2Int, List<CatPiece>>();
        if (instance == null) return map;
        foreach (CatPiece cat in instance.Cats)
        {
            if (cat == null) continue;
            Vector2Int cell = CellOf(cat.transform, cat.GridPosition);
            if (!map.TryGetValue(cell, out List<CatPiece> stack)) map[cell] = stack = new List<CatPiece>();
            stack.Add(cat);
        }
        foreach (List<CatPiece> stack in map.Values) stack.Sort((a, b) => a.transform.localPosition.y.CompareTo(b.transform.localPosition.y));
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
