#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Small inspector helper for the scene level manager. Level content itself is authored
/// in the Cat Level Editor window; this only exposes the camera framing shortcuts.
/// </summary>
[CustomEditor(typeof(LevelManager))]
public class LevelManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CatPuzzleController controller = CatPuzzleController.Instance != null ? CatPuzzleController.Instance : FindFirstObjectByType<CatPuzzleController>();
        CatLevelData level = controller != null ? controller.CurrentLevel : null;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Active Level", EditorStyles.boldLabel);
        if (level == null)
        {
            EditorGUILayout.HelpBox("No level is loaded. Open Cat Puzzle/Cat Level Editor to build one.", MessageType.Info);
            if (GUILayout.Button("Open Cat Level Editor", GUILayout.Height(24))) CatLevelEditorWindow.ShowWindow();
            return;
        }

        EditorGUILayout.ObjectField("Level Asset", level, typeof(CatLevelData), false);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Capture Camera", GUILayout.Height(24)) && Camera.main != null)
        {
            Undo.RecordObject(level, "Capture Camera");
            level.cameraPosition = Camera.main.transform.position;
            level.cameraFOV = Camera.main.fieldOfView;
            EditorUtility.SetDirty(level);
        }
        if (GUILayout.Button("Move Camera To Level", GUILayout.Height(24)) && Camera.main != null)
        {
            Undo.RecordObject(Camera.main.transform, "Move Camera");
            Camera.main.transform.position = level.cameraPosition;
            Camera.main.fieldOfView = level.cameraFOV;
        }
        EditorGUILayout.EndHorizontal();
    }
}
#endif
