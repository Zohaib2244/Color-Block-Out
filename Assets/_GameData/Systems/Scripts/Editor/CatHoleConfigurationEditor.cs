#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Inspector for the hole piece lookup, with a one-click repair from the prefab folder.</summary>
[CustomEditor(typeof(CatHoleConfiguration))]
public sealed class CatHoleConfigurationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Each cell of a hole picks the piece that matches how it connects to its neighbours. " +
            "'Default Openings' describes which sides the unrotated prefab is open on; the builder rotates from there.",
            MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Repair From Prefab Folder", GUILayout.Height(26)))
        {
            CatPuzzleAssetCreator.CreateDefaults();
            EditorUtility.SetDirty(target);
        }
    }
}
#endif  
