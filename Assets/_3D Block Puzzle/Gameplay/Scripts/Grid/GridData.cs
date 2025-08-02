using UnityEngine;
using System.IO;

[CreateAssetMenu(fileName = "New Grid Data", menuName = "3D Block Puzzle/Grid Data")]
public class GridData : ScriptableObject
{
    // Grid dimensions
    public int gridWidth = 10;
    public int gridLength = 10;
    public float cellSize = 1.0f;
    public Vector3 gridStartPosition = Vector3.zero;
    [SerializeField] private bool dirtyFlag = false; // Used to track modifications

    // Serializable arrays to store grid state
    [System.Serializable]
    public class SerializableGridData
    {
        public bool[] occupiedCells;
        public bool[] wallCells;
    }
    
    public SerializableGridData gridData;
    
    // Initialize arrays
    public void Initialize(int width, int length)
    {
        gridWidth = width;
        gridLength = length;
        gridData = new SerializableGridData
        {
            occupiedCells = new bool[width * length],
            wallCells = new bool[width * length]
        };
        
        MarkDirty("Initialize called");
    }
    
    // Helper methods to convert between 2D and 1D indices
    public int GetIndex(int x, int z)
    {
        return z * gridWidth + x;
    }
    
    

    // Mark data as dirty and ensure it gets saved
    public void MarkDirty(string modificationReason)
    {
        dirtyFlag = true;
        
        #if UNITY_EDITOR
        // Request immediate save in editor
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
    
    // Backup system to prevent data loss during play mode
    #if UNITY_EDITOR
    private void SaveDataToBackup()
    {
        try
        {
            // Only backup if we have meaningful data
            if (gridData == null || (gridData.occupiedCells == null && gridData.wallCells == null))
                return;
                
            string backupDir = "Assets/_3D Block Puzzle/Gameplay/Data/Backups";
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }
            
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            string backupPath = $"{backupDir}/{assetName}_backup.json";
            
            // Create a serializable version of our data
            string json = JsonUtility.ToJson(this, true);
            File.WriteAllText(backupPath, json);
            
            Debug.Log($"GridData backup saved to {backupPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to backup GridData: {e.Message}");
        }
    }
    #endif
    
    // Validate data when loading to catch potential issues
    private void OnEnable()
    {
        ValidateData();
    }
    
    public void ValidateData()
    {
        // Check if data structures match grid dimensions
        if (gridData != null)
        {
            if (gridData.occupiedCells != null && gridData.occupiedCells.Length != gridWidth * gridLength)
            {
                Debug.LogWarning($"GridData: Occupied cells array size ({gridData.occupiedCells.Length}) " +
                                $"doesn't match grid dimensions ({gridWidth}x{gridLength})");
            }

            if (gridData.wallCells != null && gridData.wallCells.Length != gridWidth * gridLength)
            {
                Debug.LogWarning($"GridData: Wall cells array size ({gridData.wallCells.Length}) " +
                                $"doesn't match grid dimensions ({gridWidth}x{gridLength})");
            }
        }        
    }
}