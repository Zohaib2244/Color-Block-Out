using UnityEditor;
using UnityEngine;

public class BlockPropertiesEditor : EditorWindow
{
    private float bumpForce;
    private float bumpTiltAmount;
    private float bumpTiltDuration;
    private float bumpDetectDirectionThreshold;
    [MenuItem("Block Puzzle/Block Properties Editor")]
    public static void ShowWindow()
    {
        GetWindow<BlockPropertiesEditor>("Block Properties Editor");
    }

    private void OnEnable()
    {
        // Load current values from BlockProperties
        bumpForce = BlockProperties.bumpForce;
        bumpTiltAmount = BlockProperties.bumpTiltAmount;
        bumpTiltDuration = BlockProperties.bumpTiltDuration;
    }

    private void OnGUI()
    {
        GUILayout.Label("Edit Block Properties", EditorStyles.boldLabel);

        // Editable fields for static properties
        bumpForce = EditorGUILayout.FloatField("Bump Force", bumpForce);
        bumpTiltAmount = EditorGUILayout.FloatField("Bump Tilt Amount", bumpTiltAmount);
        bumpTiltDuration = EditorGUILayout.FloatField("Bump Tilt Duration", bumpTiltDuration);
        bumpDetectDirectionThreshold = EditorGUILayout.FloatField("Bump Detect Direction Threshold", bumpDetectDirectionThreshold);
        // Apply changes button
        if (GUILayout.Button("Apply Changes"))
        {
            ApplyChanges();
        }
    }

    private void ApplyChanges()
    {
        // Update static values in BlockProperties
        BlockProperties.bumpForce = bumpForce;
        BlockProperties.bumpTiltAmount = bumpTiltAmount;
        BlockProperties.bumpTiltDuration = bumpTiltDuration;
        BlockProperties.bumpDetectDirectionThreshold = bumpDetectDirectionThreshold;
        // Log confirmation
        Debug.Log("Block Properties updated!");
    }
}