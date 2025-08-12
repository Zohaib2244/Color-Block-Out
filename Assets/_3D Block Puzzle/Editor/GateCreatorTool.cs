#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class GateCreatorTool : EditorWindow
{
    #region Variables
    private GridManager targetGrid;
    private HoleConfiguration holeConfiguration;
    private BlockColorTypes selectedColorType = BlockColorTypes.Red;
    private List<Vector2Int> selectedPositions = new List<Vector2Int>();
    private Vector2 scrollPosition;

    // Grid visualization settings
    private float cellSize = 20f;
    private float gridPadding = 10f;
    private bool showCellLabels = true;

    // Selection mode
    private bool isSelectingGate = false;
    private bool isRemovingGate = false;
    #endregion

    [MenuItem("Block Puzzle/Gate Creator")]
    public static void ShowWindow()
    {
        GateCreatorTool window = GetWindow<GateCreatorTool>("Gate Creator");
        window.minSize = new Vector2(400, 500);
    }
    #region OnGui
    private void OnGUI()
    {
        // Header
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Gate Creator Tool", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();

        // Grid Selection
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);

        // Target grid selection
        targetGrid = (GridManager)EditorGUILayout.ObjectField("Target Grid", targetGrid, typeof(GridManager), true);
        // Hole Configuration selection
        holeConfiguration = (HoleConfiguration)EditorGUILayout.ObjectField("Hole Configuration", holeConfiguration, typeof(HoleConfiguration), false);

        if (targetGrid == null)
        {
            EditorGUILayout.HelpBox("Please select a GridManager object", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }
        if (holeConfiguration == null)
        {
            // Optionally, you can provide a default path as a string for reference or logging
            string configPath = "Assets/_3D Block Puzzle/Gameplay/Scriptable Objects/HoleConfiguration.asset";
            holeConfiguration = AssetDatabase.LoadAssetAtPath<HoleConfiguration>(configPath);
        }

        // Grid visualization settings
        cellSize = EditorGUILayout.Slider("Cell Size", cellSize, 10f, 40f);
        showCellLabels = EditorGUILayout.Toggle("Show Cell Labels", showCellLabels);

        EditorGUILayout.EndVertical();

        // Gate Settings
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Gate Settings", EditorStyles.boldLabel);

        // Color selection buttons
        EditorGUILayout.LabelField("Gate Color (Block Type):", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        BlockColorTypes[] colorTypes = (BlockColorTypes[])System.Enum.GetValues(typeof(BlockColorTypes));
        foreach (BlockColorTypes colorType in colorTypes)
        {
            Color buttonColor = GameConstants.GetGateColorMaterial(colorType).color;
            string colorName = colorType.ToString();

            GUI.backgroundColor = (selectedColorType == colorType) ?
                Color.white : new Color(buttonColor.r * 0.7f, buttonColor.g * 0.7f, buttonColor.b * 0.7f);

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            if (selectedColorType == colorType)
            {
                buttonStyle.fontStyle = FontStyle.Bold;
                buttonStyle.normal.textColor = Color.black;
            }

            if (GUILayout.Button(colorName, buttonStyle, GUILayout.Height(30)))
            {
                selectedColorType = colorType;
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // Draw color preview
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Gate Color Preview");
        var colorRect = EditorGUILayout.GetControlRect(GUILayout.Height(20f));
        EditorGUI.DrawRect(colorRect, GameConstants.GetGateColorMaterial(selectedColorType).color);
        EditorGUILayout.EndHorizontal();

        // Selection info
        EditorGUILayout.LabelField($"Selected Positions: {selectedPositions.Count}");

        // Buttons for gate mode selection
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = isSelectingGate ? Color.green : Color.white;
        if (GUILayout.Button("Select Gate Cells", GUILayout.Height(30)))
        {
            isSelectingGate = true;
            isRemovingGate = false;
        }

        GUI.backgroundColor = isRemovingGate ? Color.red : Color.white;
        if (GUILayout.Button("Remove Gates", GUILayout.Height(30)))
        {
            isRemovingGate = true;
            isSelectingGate = false;
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // Clear selection button
        if (selectedPositions.Count > 0)
        {
            if (GUILayout.Button("Clear Selection"))
            {
                selectedPositions.Clear();
                Repaint();
            }
        }

        // Add gate button
        GUI.enabled = selectedPositions.Count > 0;
        if (GUILayout.Button("Create Gate", GUILayout.Height(35)))
        {
            CreateHoleGate();
            selectedPositions.Clear();
        }
        GUI.enabled = true;

        EditorGUILayout.EndVertical();

        // Grid visualization
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Grid Visualization", EditorStyles.boldLabel);

        // Calculate grid dimensions
        float gridWidth = targetGrid.GetGridWidth() * cellSize + gridPadding * 2;
        float gridHeight = targetGrid.GetGridLength() * cellSize + gridPadding * 2;

        // Begin scrollview if needed
        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition,
            GUILayout.Height(Mathf.Min(gridHeight + 20, position.height - 300)));

        // Calculate the rect for our grid display
        Rect gridRect = GUILayoutUtility.GetRect(gridWidth, gridHeight);

        // Draw the grid background
        EditorGUI.DrawRect(gridRect, new Color(0.2f, 0.2f, 0.2f));

        // Mark the active area
        Rect activeArea = new Rect(
            gridRect.x + gridPadding,
            gridRect.y + gridPadding,
            targetGrid.GetGridWidth() * cellSize,
            targetGrid.GetGridLength() * cellSize);

        EditorGUI.DrawRect(activeArea, new Color(0.3f, 0.3f, 0.3f));

        // Draw grid lines
        Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        for (int x = 0; x <= targetGrid.GetGridWidth(); x++)
        {
            Vector3 start = new Vector3(activeArea.x + x * cellSize, activeArea.y, 0);
            Vector3 end = new Vector3(activeArea.x + x * cellSize, activeArea.y + activeArea.height, 0);
            Handles.DrawLine(start, end);
        }
        for (int z = 0; z <= targetGrid.GetGridLength(); z++)
        {
            Vector3 start = new Vector3(activeArea.x, activeArea.y + z * cellSize, 0);
            Vector3 end = new Vector3(activeArea.x + activeArea.width, activeArea.y + z * cellSize, 0);
            Handles.DrawLine(start, end);
        }

        // Draw all walls
        if (targetGrid != null)
        {
            var wallPositions = targetGrid.GetWallPositions();
            foreach (var wallPos in wallPositions)
            {
                DrawWallCell(activeArea, wallPos);
            }
        }

        // Draw all gates
        foreach (var gate in targetGrid.Gates)
        {
            foreach (var pos in gate.positions)
            {
                DrawGateCell(activeArea, pos, gate.colorType);
            }
        }

        // Draw selected positions
        foreach (var pos in selectedPositions)
        {
            DrawSelectedCell(activeArea, pos);
        }

        // Handle cell selection
        if (Event.current.type == EventType.MouseDown &&
            Event.current.button == 0 &&
            activeArea.Contains(Event.current.mousePosition))
        {
            // Calculate which cell was clicked - adjusted for flipped Y axis
            int x = Mathf.FloorToInt((Event.current.mousePosition.x - activeArea.x) / cellSize);
            int z = targetGrid.GetGridLength() - 1 - Mathf.FloorToInt((Event.current.mousePosition.y - activeArea.y) / cellSize);

            if (x >= 0 && x < targetGrid.GetGridWidth() && z >= 0 && z < targetGrid.GetGridLength())
            {
                Vector2Int clickPos = new Vector2Int(x, z);

                if (isSelectingGate)
                {
                    // Only allow selecting empty cells (not walls or occupied)
                    if (!targetGrid.IsCellWall(x, z))
                    {
                        // Toggle selection
                        if (selectedPositions.Contains(clickPos))
                        {
                            selectedPositions.Remove(clickPos);
                        }
                        else
                        {
                            selectedPositions.Add(clickPos);
                        }
                    }
                }
                else if (isRemovingGate)
                {
                    // Try to get gate at this position
                    Gate gateToRemove = targetGrid.GetGate(clickPos);
                    if (gateToRemove != null)
                    {
                        Undo.RecordObject(targetGrid, "Remove Hole");
                        targetGrid.RemoveHole(gateToRemove);
                        EditorUtility.SetDirty(targetGrid);
                    }
                }

                Event.current.Use();
                Repaint();
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // Instructions
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Instructions:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Select 'Select Gate Cells' to choose empty cells for gate placement\n" +
            "2. Click on empty cells in the grid to select them\n" +
            "3. Choose a gate color that matches the block type\n" +
            "4. Click 'Create Gate' to mark selected cells as gate cells\n" +
            "5. Use 'Remove Gates' to delete existing gates\n" +
            "6. Blocks will fall through gates when they completely overlap and match color",
            MessageType.Info);
        EditorGUILayout.EndVertical();
    }
    #endregion

    #region Draw Cells on GUI
    private void DrawWallCell(Rect gridArea, Vector2Int pos)
    {
        Rect cellRect = new Rect(
            gridArea.x + pos.x * cellSize,
            gridArea.y + (targetGrid.GetGridLength() - 1 - pos.y) * cellSize,
            cellSize,
            cellSize);

        // Draw wall cell
        EditorGUI.DrawRect(cellRect, new Color(0.4f, 0.4f, 0.4f));

        // Draw wall label if enabled
        if (showCellLabels)
        {
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = Color.white;
            GUI.Label(cellRect, "W", labelStyle);
        }
    }

    private void DrawGateCell(Rect gridArea, Vector2Int pos, BlockColorTypes colorType)
    {
        Rect cellRect = new Rect(
            gridArea.x + pos.x * cellSize,
            gridArea.y + (targetGrid.GetGridLength() - 1 - pos.y) * cellSize,
            cellSize,
            cellSize);

        // Draw gate cell with colored background
        Material gateMaterial = GameConstants.GetGateColorMaterial(colorType);
        EditorGUI.DrawRect(cellRect, gateMaterial.color);

        // Draw gate label if enabled
        if (showCellLabels)
        {
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = Color.black;
            labelStyle.fontStyle = FontStyle.Bold;
            GUI.Label(cellRect, "G", labelStyle);
        }

        // Draw a small border to distinguish gates
        Handles.color = Color.black;
        Vector3[] corners = new Vector3[]
        {
            new Vector3(cellRect.x, cellRect.y),
            new Vector3(cellRect.x + cellRect.width, cellRect.y),
            new Vector3(cellRect.x + cellRect.width, cellRect.y + cellRect.height),
            new Vector3(cellRect.x, cellRect.y + cellRect.height)
        };

        for (int i = 0; i < 4; i++)
        {
            Handles.DrawLine(corners[i], corners[(i + 1) % 4]);
        }
    }

    private void DrawSelectedCell(Rect gridArea, Vector2Int pos)
    {
        Rect cellRect = new Rect(
            gridArea.x + pos.x * cellSize,
            gridArea.y + (targetGrid.GetGridLength() - 1 - pos.y) * cellSize,
            cellSize,
            cellSize);

        // Draw selection with preview color
        Material gateMaterial = GameConstants.GetGateColorMaterial(selectedColorType);
        Color previewColor = new Color(gateMaterial.color.r, gateMaterial.color.g, gateMaterial.color.b, 0.7f);
        EditorGUI.DrawRect(cellRect, previewColor);

        // Draw selection border
        Handles.color = Color.white;
        Vector3[] corners = new Vector3[]
        {
            new Vector3(cellRect.x, cellRect.y),
            new Vector3(cellRect.x + cellRect.width, cellRect.y),
            new Vector3(cellRect.x + cellRect.width, cellRect.y + cellRect.height),
            new Vector3(cellRect.x, cellRect.y + cellRect.height)
        };

        for (int i = 0; i < 4; i++)
        {
            Handles.DrawLine(corners[i], corners[(i + 1) % 4]);
        }

        // Draw preview label
        if (showCellLabels)
        {
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = Color.black;
            labelStyle.fontStyle = FontStyle.Bold;
            GUI.Label(cellRect, "G", labelStyle);
        }
    }
    #endregion
    #region Hole Creation Logic

    private void CreateHoleGate()
    {
        if (selectedPositions.Count == 0 || holeConfiguration == null)
            return;

        Undo.RecordObject(targetGrid, "Create Hole Gate");

        // Create a parent GameObject to hold all hole meshes for this gate
        GameObject gateParent = new GameObject("HoleGate");
        Undo.RegisterCreatedObjectUndo(gateParent, "Create HoleGate Parent");

        // Use a HashSet for efficient neighbor checking
        var selectedSet = new HashSet<Vector2Int>(selectedPositions);

        foreach (var pos in selectedPositions)
        {
            // 1. Analyze the cell's connections
            var connections = GetConnections(pos, selectedSet);
            var holeType = GetHoleType(connections);
            var prefabData = holeConfiguration.GetDataForType(holeType);

            if (prefabData == null || prefabData.prefab == null)
            {
                Debug.LogWarning($"No prefab configured for HoleType: {holeType}");
                continue;
            }

            // 2. Calculate rotation
            float rotationAngle = CalculateRotation(prefabData, connections);

            // 3. Instantiate and configure the prefab
            Vector3 worldPos = targetGrid.GridToWorldPosition(pos);
            GameObject holeInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabData.prefab, gateParent.transform);
            holeInstance.transform.position = worldPos;
            holeInstance.transform.rotation = Quaternion.Euler(0, rotationAngle, 0);

            // Apply color to the new instance
            var renderer = holeInstance.GetComponentInChildren<MeshRenderer>();
            if (renderer)
            {
                // Create a new material instance to avoid changing the asset
                renderer.material = GameConstants.GetGateColorMaterial(selectedColorType);
            }

            Undo.RegisterCreatedObjectUndo(holeInstance, "Create Hole Piece");
        }

        // Parent the gateParent under the HoleParent in GridManager, if assigned
        if (targetGrid.HoleParent != null)
        {
            gateParent.transform.SetParent(targetGrid.HoleParent, true);
        }
        else
        {
            gateParent.transform.SetParent(targetGrid.transform, true);
        }
        gateParent.transform.localPosition = Vector3.zero;
        // 4. Register the logical gate in GridManager
        targetGrid.AddHole(selectedColorType, new List<Vector2Int>(selectedPositions), gateParent);
        EditorUtility.SetDirty(targetGrid);
    }

    private List<Direction> GetConnections(Vector2Int pos, HashSet<Vector2Int> selectedSet)
    {
        var connections = new List<Direction>();
        if (selectedSet.Contains(pos + Vector2Int.up)) connections.Add(Direction.Up);
        if (selectedSet.Contains(pos + Vector2Int.right)) connections.Add(Direction.Right);
        if (selectedSet.Contains(pos + Vector2Int.down)) connections.Add(Direction.Down);
        if (selectedSet.Contains(pos + Vector2Int.left)) connections.Add(Direction.Left);
        return connections;
    }

    private HoleType GetHoleType(List<Direction> connections)
    {
        switch (connections.Count)
        {
            case 0: return HoleType.Isolated;
            case 1: return HoleType.EndCap;
            case 2:
                // Check if connections are opposite (Straight) or adjacent (Corner)
                bool isOpposite = (connections.Contains(Direction.Up) && connections.Contains(Direction.Down)) ||
                                  (connections.Contains(Direction.Right) && connections.Contains(Direction.Left));
                return isOpposite ? HoleType.Straight : HoleType.Corner;
            case 3: return HoleType.One_Side;
            case 4: return HoleType.Middle;
            default: return HoleType.Isolated;
        }
    }

    private float CalculateRotation(HolePrefabData prefabData, List<Direction> actualConnections)
    {
        if (actualConnections.Count == 0 || actualConnections.Count >= 4) return 0;

        // Convert connection directions to a set for easy lookup
        var actualSet = new HashSet<Direction>(actualConnections);
        var defaultSet = new HashSet<Direction>(prefabData.defaultOpenings);

        // Try rotating 0, 90, 180, 270 degrees to find a match
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f;
            int rotationSteps = Mathf.RoundToInt(angle / 90f);
            var rotatedDefaultSet = new HashSet<Direction>(defaultSet.Select(d => RotateDirection(d, rotationSteps)));
            if (rotatedDefaultSet.SetEquals(actualSet))
            {
                return angle;
            }
        }

        return 0; // Fallback
    }

    private Direction RotateDirection(Direction dir, int rotationSteps)
    {
        // Always rotate clockwise in 90-degree steps
        int dirInt = (int)dir;
        int newDirInt = (dirInt + rotationSteps) % 4;
        return (Direction)newDirInt;
    }
    #endregion
}
#endif