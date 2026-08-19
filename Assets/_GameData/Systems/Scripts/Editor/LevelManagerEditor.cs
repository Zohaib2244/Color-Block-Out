#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Small inspector helper for the scene level manager. Level content itself is authored
/// in the Cat Level Editor window; this only reports what is currently loaded.
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
        if (GUILayout.Button("Open Cat Level Editor", GUILayout.Height(24))) CatLevelEditorWindow.ShowWindow();
    }
}
#endif
