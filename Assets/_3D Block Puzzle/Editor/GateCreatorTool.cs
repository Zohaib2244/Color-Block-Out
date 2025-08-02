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

        if (targetGrid == null)
        {
            EditorGUILayout.HelpBox("Please select a GridManager object", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
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
            CreateGate();
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
                        Undo.RecordObject(targetGrid, "Remove Gate");
                        targetGrid.RemoveGate(gateToRemove);
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
    private void CreateGate()
    {
        if (selectedPositions.Count == 0)
            return;

        Undo.RecordObject(targetGrid, "Add Gate");

        // Create the gate in the GridManager (no mesh needed)
        targetGrid.AddGate(selectedColorType, new List<Vector2Int>(selectedPositions));

        EditorUtility.SetDirty(targetGrid);
    }
    
}
#endif