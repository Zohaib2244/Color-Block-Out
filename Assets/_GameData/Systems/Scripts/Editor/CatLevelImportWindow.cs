#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Brings level and grid JSON exported by the web level editor into the project. Files arrive in a
/// browser's downloads folder, so they are dropped here, checked, and filed where the game and the
/// authoring tools look for them.
///
/// The text is copied through untouched rather than re-written from what Unity parsed, so anything
/// the file carries that Unity has no use for yet - gates, for one - survives the trip and is still
/// there when the web tool opens it again.
/// </summary>
public sealed class CatLevelImportWindow : EditorWindow
{
    private CatPuzzleConfig config;
    private LevelData collection;
    private bool addToCollection = true;
    private bool overwriteExisting = true;
    private Vector2 scroll;

    private readonly List<Entry> queue = new List<Entry>();

    private sealed class Entry
    {
        public string path;
        public string fileName;
        public bool isGrid;
        public string title;
        public string detail;
        public string error;
        public readonly List<string> unknownColors = new List<string>();
        public bool include = true;

        public bool CanImport => string.IsNullOrEmpty(error) && unknownColors.Count == 0;
    }

    [MenuItem("Cat Puzzle/Import Level JSON")]
    public static void ShowWindow()
    {
        CatLevelImportWindow window = GetWindow<CatLevelImportWindow>("Import Level JSON");
        window.minSize = new Vector2(460f, 420f);
    }

    private void OnEnable()
    {
        if (config == null) config = CatPuzzleAssetCreator.FindConfig();
    }

    private void OnGUI()
    {
        DrawSettings();
        DrawDropArea();
        DrawQueue();
        DrawActions();
    }

    #region Sections
    private void DrawSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        config = (CatPuzzleConfig)EditorGUILayout.ObjectField("Puzzle Config", config, typeof(CatPuzzleConfig), false);
        if (config == null || config.palette == null)
        {
            EditorGUILayout.HelpBox("A CatPuzzleConfig with a colour palette is needed: the files name their colours, and " +
                                    "the palette is what those names mean. Run Cat Puzzle/Create Default Assets.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        collection = (LevelData)EditorGUILayout.ObjectField("Add To Play Order", collection, typeof(LevelData), false);
        using (new EditorGUI.DisabledScope(collection == null))
            addToCollection = EditorGUILayout.Toggle("Append Imported Levels", addToCollection);
        overwriteExisting = EditorGUILayout.Toggle("Overwrite Same Name", overwriteExisting);

        EditorGUILayout.LabelField("Levels go to", CatLevelFiles.LevelFolder, EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Grids go to", CatLevelFiles.GridFolder, EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawDropArea()
    {
        Rect area = GUILayoutUtility.GetRect(0f, 60f, GUILayout.ExpandWidth(true));
        GUI.Box(area, "Drop exported .json files or a folder of them here", EditorStyles.helpBox);

        Event evt = Event.current;
        if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && area.Contains(evt.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                Enqueue(CatLevelFiles.CollectJsonFiles(DragAndDrop.paths));
            }
            evt.Use();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Browse For Files…", GUILayout.Height(22)))
        {
            string picked = EditorUtility.OpenFilePanel("Import Level JSON", "", "json");
            if (!string.IsNullOrEmpty(picked)) Enqueue(new List<string> { picked });
        }
        if (GUILayout.Button("Browse For Folder…", GUILayout.Height(22)))
        {
            string picked = EditorUtility.OpenFolderPanel("Import Level JSON", "", "");
            if (!string.IsNullOrEmpty(picked)) Enqueue(CatLevelFiles.CollectJsonFiles(new List<string> { picked }));
        }
        using (new EditorGUI.DisabledScope(queue.Count == 0))
            if (GUILayout.Button("Clear", GUILayout.Height(22), GUILayout.Width(70f))) queue.Clear();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawQueue()
    {
        if (queue.Count == 0)
        {
            EditorGUILayout.HelpBox("Nothing queued yet.", MessageType.None);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Entry entry in queue)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!entry.CanImport))
                entry.include = EditorGUILayout.Toggle(entry.include && entry.CanImport, GUILayout.Width(18f));
            EditorGUILayout.LabelField($"{entry.title}  ({(entry.isGrid ? "grid" : "level")})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(" ", entry.fileName, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(entry.detail)) EditorGUILayout.LabelField(" ", entry.detail, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(entry.error)) EditorGUILayout.HelpBox(entry.error, MessageType.Error);
            if (entry.unknownColors.Count > 0)
                EditorGUILayout.HelpBox($"The palette has no colour named {string.Join(", ", entry.unknownColors)}. " +
                                        "Add it to the palette, or rename so both sides agree, then queue the file again.", MessageType.Error);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawActions()
    {
        int importable = 0;
        foreach (Entry entry in queue) if (entry.include && entry.CanImport) importable++;

        using (new EditorGUI.DisabledScope(importable == 0 || config == null))
            if (GUILayout.Button($"Import {importable} File(s)", GUILayout.Height(30))) Import();
    }
    #endregion

    #region Queue
    private void Enqueue(List<string> paths)
    {
        foreach (string path in paths)
        {
            if (queue.Exists(entry => entry.path == path)) continue;
            queue.Add(Inspect(path));
        }
        Repaint();
    }

    /// <summary>Reads a file without writing anything, so the queue can show what importing it would do.</summary>
    private Entry Inspect(string path)
    {
        Entry entry = new Entry { path = path, fileName = Path.GetFileName(path), title = Path.GetFileNameWithoutExtension(path) };

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (System.Exception exception)
        {
            entry.error = $"Could not read the file: {exception.Message}";
            return entry;
        }

        CatLevelJson.Summary summary = CatLevelJson.Inspect(text, entry.title, config != null ? config.palette : null);
        if (summary.IsValid)
        {
            entry.title = summary.levelName;
            entry.detail = $"{summary.width}x{summary.length} board · {summary.catCount} cats · {summary.holeCount} holes" +
                           (summary.gateCount > 0 ? $" · {summary.gateCount} gates (loaded as piles, Unity has no gate yet)" : string.Empty);
            entry.unknownColors.AddRange(summary.unknownColors);
            return entry;
        }

        // Not a level; a grid file is the other thing the web tool exports.
        if (CatLevelJson.TryParseGrid(text, entry.title, out GridData grid, out _))
        {
            entry.isGrid = true;
            entry.title = string.IsNullOrEmpty(grid.gridName) ? entry.title : grid.gridName;
            entry.detail = $"{grid.gridWidth}x{grid.gridLength} board · cell {grid.cellSize}";
            return entry;
        }

        entry.error = summary.error;
        return entry;
    }
    #endregion

    #region Importing
    private void Import()
    {
        List<TextAsset> imported = new List<TextAsset>();
        // Only what actually landed leaves the queue, so a file that failed can be retried.
        HashSet<string> done = new HashSet<string>();
        int failed = 0;

        foreach (Entry entry in queue)
        {
            if (!entry.include || !entry.CanImport) continue;

            string folder = entry.isGrid ? CatLevelFiles.GridFolder : CatLevelFiles.LevelFolder;
            string wanted = CatLevelFiles.PathIn(folder, entry.title);
            string path = overwriteExisting || AssetDatabase.LoadAssetAtPath<TextAsset>(wanted) == null
                ? wanted
                : CatLevelFiles.UniquePath(folder, entry.title);

            string text;
            try
            {
                text = File.ReadAllText(entry.path);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"Could not read {entry.path}: {exception.Message}");
                failed++;
                continue;
            }

            TextAsset file = CatLevelFiles.Write(path, text);
            if (file == null)
            {
                Debug.LogError($"Could not write {path}.");
                failed++;
                continue;
            }

            done.Add(entry.path);
            if (!entry.isGrid)
            {
                imported.Add(file);
                Report(file);
            }
            else Debug.Log($"Imported grid '{entry.title}' to {path}.", file);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AppendToCollection(imported);

        Debug.Log($"Imported {done.Count} file(s), {imported.Count} of them levels" +
                  (failed > 0 ? $"; {failed} failed and were left queued" : string.Empty) + ".");
        queue.RemoveAll(entry => done.Contains(entry.path));
        Repaint();
    }

    /// <summary>Says what the file will actually build, now that it is somewhere Unity can load it.</summary>
    private void Report(TextAsset file)
    {
        CatLevelJson.LevelResult result = CatLevelJson.ParseLevel(file.text, file.name, config);
        if (result.issues.Count == 0)
        {
            Debug.Log($"Imported level '{file.name}'.", file);
            return;
        }

        string message = $"Imported level '{file.name}' with {result.issues.Count} issue(s):\n{result.Describe()}";
        if (result.HasErrors) Debug.LogError(message, file);
        else Debug.LogWarning(message, file);
    }

    private void AppendToCollection(List<TextAsset> imported)
    {
        if (collection == null || !addToCollection || imported.Count == 0) return;

        Undo.RecordObject(collection, "Add Imported Levels");
        int added = 0;
        foreach (TextAsset file in imported)
        {
            if (collection.levelFiles.Contains(file)) continue;
            collection.levelFiles.Add(file);
            added++;
        }

        collection.ClearCache();
        EditorUtility.SetDirty(collection);
        AssetDatabase.SaveAssets();
        Debug.Log($"Added {added} level(s) to '{collection.name}', now {collection.Count} long.", collection);
    }
    #endregion
}
#endif
