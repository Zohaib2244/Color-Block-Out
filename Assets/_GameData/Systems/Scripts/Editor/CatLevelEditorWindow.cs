#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Asset-driven editor for cat levels. A GridData asset can be reused by many levels.</summary>
public sealed class CatLevelEditorWindow : EditorWindow
{
    private CatPuzzleController sceneController;
    private GridData gridAsset;
    private CatLevelData levelAsset;
    private CatHoleConfiguration holeConfiguration;
    private GameObject catPrefab;
    private BlockColorTypes selectedColor = BlockColorTypes.Red;
    private bool placingCats = true;
    private bool drawingHole = false;
    private CatHoleType selectedHoleType = CatHoleType.Isolated;
    private readonly HashSet<Vector2Int> selectedCells = new HashSet<Vector2Int>();
    private Vector2 scroll;
    private float cellSize = 28f;

    [MenuItem("Cat Puzzle/Cat Level Editor")]
    public static void ShowWindow() => GetWindow<CatLevelEditorWindow>("Cat Level Editor");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Cat Level Editor", EditorStyles.boldLabel);
        sceneController = (CatPuzzleController)EditorGUILayout.ObjectField("Scene Controller", sceneController, typeof(CatPuzzleController), true);
        gridAsset = (GridData)EditorGUILayout.ObjectField("Grid Asset", gridAsset, typeof(GridData), false);
        levelAsset = (CatLevelData)EditorGUILayout.ObjectField("Level Asset", levelAsset, typeof(CatLevelData), false);
        catPrefab = (GameObject)EditorGUILayout.ObjectField("Cat Prefab", catPrefab, typeof(GameObject), false);
        holeConfiguration = (CatHoleConfiguration)EditorGUILayout.ObjectField("Hole Prefabs", holeConfiguration, typeof(CatHoleConfiguration), false);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Load Grid")) EnsureGridInScene();
        if (GUILayout.Button("New Level")) CreateLevelAsset();
        if (GUILayout.Button("Load Level")) LoadLevelIntoScene();
        if (GUILayout.Button("Save Level")) SaveLevelAsset();
        EditorGUILayout.EndHorizontal();

        if (levelAsset == null && gridAsset == null)
        {
            EditorGUILayout.HelpBox("Choose a GridData asset, then create or load a CatLevelData asset.", MessageType.Info);
            return;
        }

        placingCats = GUILayout.Toolbar(placingCats ? 0 : 1, new[] { "Cats", "Holes" }) == 0;
        selectedColor = (BlockColorTypes)EditorGUILayout.EnumPopup("Color", selectedColor);
        if (!placingCats) selectedHoleType = (CatHoleType)EditorGUILayout.EnumPopup("Hole shape", selectedHoleType);
        drawingHole = !placingCats && GUILayout.Toggle(drawingHole, "Draw connected hole shape");
        cellSize = EditorGUILayout.Slider("Cell Size", cellSize, 16f, 48f);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Selection")) selectedCells.Clear();
        GUI.enabled = selectedCells.Count > 0;
        if (GUILayout.Button(placingCats ? "Place Cats" : (drawingHole ? "Create Hole Shape" : "Place Hole"))) PlaceSelection();
        if (GUILayout.Button("Remove Selected")) RemoveSelection();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        DrawGrid();
        EditorGUILayout.HelpBox("Select cells in X/Z coordinates. Hole shapes are built from connected selected cells and choose Corner/End Cap/Isolated/Middle/One Side/Straight automatically.", MessageType.None);
    }

    private GridManager EnsureGridInScene()
    {
        if (sceneController == null || gridAsset == null) return null;
        GridManager grid = sceneController.GridManager;
        if (grid == null)
        {
            GameObject gridObject = new GameObject("Level Grid");
            Undo.RegisterCreatedObjectUndo(gridObject, "Create Level Grid");
            gridObject.transform.SetParent(sceneController.transform, false);
            grid = Undo.AddComponent<GridManager>(gridObject);
            sceneController.SetGridManager(grid);
        }
        grid.ApplyGridData(gridAsset);
        EditorUtility.SetDirty(grid);
        return grid;
    }

    private void CreateLevelAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create Cat Level", "New Cat Level", "asset", "Choose a location for this level asset.");
        if (string.IsNullOrEmpty(path)) return;
        levelAsset = CreateInstance<CatLevelData>();
        levelAsset.levelName = System.IO.Path.GetFileNameWithoutExtension(path);
        levelAsset.grid = gridAsset;
        levelAsset.catPrefab = catPrefab;
        levelAsset.holeConfiguration = holeConfiguration;
        AssetDatabase.CreateAsset(levelAsset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = levelAsset;
    }

    private void LoadLevelIntoScene()
    {
        if (levelAsset == null || sceneController == null) return;
        gridAsset = levelAsset.grid; catPrefab = levelAsset.catPrefab; holeConfiguration = levelAsset.holeConfiguration;
        EnsureGridInScene();
        sceneController.LoadLevel(levelAsset);
        EditorSceneManager.MarkSceneDirty(sceneController.gameObject.scene);
    }

    private void SaveLevelAsset()
    {
        if (levelAsset == null) { CreateLevelAsset(); if (levelAsset == null) return; }
        levelAsset.grid = gridAsset;
        levelAsset.catPrefab = catPrefab;
        levelAsset.holeConfiguration = holeConfiguration;
        EditorUtility.SetDirty(levelAsset);
        AssetDatabase.SaveAssets();
    }

    private void PlaceSelection()
    {
        if (levelAsset == null) { EditorUtility.DisplayDialog("No level", "Create or load a CatLevelData asset first.", "OK"); return; }
        if (placingCats)
        {
            foreach (Vector2Int cell in selectedCells)
                if (!levelAsset.cats.Any(cat => cat.gridPosition == cell)) levelAsset.cats.Add(new CatPlacement { color = selectedColor, gridPosition = cell });
        }
        else if (drawingHole)
        {
            HashSet<Vector2Int> shape = new HashSet<Vector2Int>(selectedCells);
            foreach (Vector2Int cell in shape)
            {
                CatHoleType type = GetHoleType(cell, shape);
                levelAsset.holes.Add(new CatHolePlacement { color = selectedColor, gridPosition = cell, holeType = type, rotationQuarterTurns = GetRotation(cell, shape, type) });
            }
        }
        else
        {
            Vector2Int cell = selectedCells.First();
            levelAsset.holes.Add(new CatHolePlacement { color = selectedColor, gridPosition = cell, holeType = selectedHoleType });
        }
        SaveLevelAsset(); selectedCells.Clear();
    }

    private void RemoveSelection()
    {
        if (levelAsset == null) return;
        levelAsset.cats.RemoveAll(cat => selectedCells.Contains(cat.gridPosition));
        levelAsset.holes.RemoveAll(hole => selectedCells.Contains(hole.gridPosition));
        SaveLevelAsset(); selectedCells.Clear();
    }

    private CatHoleType GetHoleType(Vector2Int cell, HashSet<Vector2Int> shape)
    {
        int count = 0;
        if (shape.Contains(cell + Vector2Int.up)) count++;
        if (shape.Contains(cell + Vector2Int.right)) count++;
        if (shape.Contains(cell + Vector2Int.down)) count++;
        if (shape.Contains(cell + Vector2Int.left)) count++;
        if (count == 0) return CatHoleType.Isolated;
        if (count == 1) return CatHoleType.EndCap;
        if (count == 3) return CatHoleType.OneSide;
        if (count == 4) return CatHoleType.Middle;
        bool straight = (shape.Contains(cell + Vector2Int.up) && shape.Contains(cell + Vector2Int.down)) || (shape.Contains(cell + Vector2Int.left) && shape.Contains(cell + Vector2Int.right));
        return straight ? CatHoleType.Straight : CatHoleType.Corner;
    }

    private int GetRotation(Vector2Int cell, HashSet<Vector2Int> shape, CatHoleType type)
    {
        CatHolePrefabData data = holeConfiguration != null ? holeConfiguration.GetData(type) : null;
        if (data == null || data.defaultOpenings == null || data.defaultOpenings.Length == 0) return 0;
        HashSet<Direction> actual = new HashSet<Direction>();
        if (shape.Contains(cell + Vector2Int.up)) actual.Add(Direction.Up);
        if (shape.Contains(cell + Vector2Int.right)) actual.Add(Direction.Right);
        if (shape.Contains(cell + Vector2Int.down)) actual.Add(Direction.Down);
        if (shape.Contains(cell + Vector2Int.left)) actual.Add(Direction.Left);
        for (int quarterTurns = 0; quarterTurns < 4; quarterTurns++)
        {
            HashSet<Direction> rotated = new HashSet<Direction>(data.defaultOpenings.Select(direction => (Direction)(((int)direction + quarterTurns) % 4)));
            if (rotated.SetEquals(actual)) return quarterTurns;
        }
        return 0;
    }

    private void DrawGrid()
    {
        int width = gridAsset != null ? gridAsset.gridWidth : 10;
        int height = gridAsset != null ? gridAsset.gridLength : 10;
        float gridWidth = width * cellSize + 20f, gridHeight = height * cellSize + 20f;
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(gridHeight + 10f, 450f)));
        Rect area = GUILayoutUtility.GetRect(gridWidth, gridHeight);
        Event evt = Event.current;
        if (evt.type == EventType.MouseDown && evt.button == 0 && area.Contains(evt.mousePosition))
        {
            int x = Mathf.FloorToInt((evt.mousePosition.x - area.x - 10f) / cellSize);
            int z = height - 1 - Mathf.FloorToInt((evt.mousePosition.y - area.y - 10f) / cellSize);
            if (x >= 0 && x < width && z >= 0 && z < height)
            {
                Vector2Int cell = new Vector2Int(x, z);
                if (!selectedCells.Add(cell)) selectedCells.Remove(cell);
                evt.Use(); Repaint();
            }
        }
        for (int x = 0; x < width; x++) for (int z = 0; z < height; z++)
        {
            Rect rect = new Rect(area.x + 10f + x * cellSize, area.y + 10f + (height - 1 - z) * cellSize, cellSize, cellSize);
            EditorGUI.DrawRect(rect, selectedCells.Contains(new Vector2Int(x, z)) ? new Color(0.25f, 0.75f, 1f) : new Color(0.18f, 0.18f, 0.18f));
            Handles.color = Color.gray; Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.xMax, rect.y)); Handles.DrawLine(new Vector3(rect.x, rect.y), new Vector3(rect.x, rect.yMax));
        }
        EditorGUILayout.EndScrollView();
    }
}
#endif
