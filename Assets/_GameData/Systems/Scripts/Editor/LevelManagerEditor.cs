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

        // A level is plain data parsed from a JSON file now, so there is no asset to point at.
        EditorGUILayout.LabelField("Level", level.DisplayName);
        EditorGUILayout.LabelField("Contents", $"{level.cats.Count} cats, {level.holes.Count} holes, {level.levelTime}s");
        if (GUILayout.Button("Open Cat Level Editor", GUILayout.Height(24))) CatLevelEditorWindow.ShowWindow();
    }
}
#endif
