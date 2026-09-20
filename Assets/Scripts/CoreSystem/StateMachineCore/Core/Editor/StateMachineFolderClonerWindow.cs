using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// One-time tool: clones one or more folders into new folders as independent files (same
// folder structure, so normal Project window rename/delete/browsing still works exactly
// like today), renaming Find Text -> Replace Text in every file/folder name, and re-pointing
// every internal reference so the clones link to each other instead of the originals.
//
// Multiple folder pairs can be queued in one run and share a single reference map, so a
// reference that crosses between two source folders (e.g. a Stats modifier's _target
// pointing at a Condition SO that lives in a separate StateMachine folder) still resolves
// to the correct clone instead of being left pointing at the original.
//
// Everything under a selected Source Folder gets cloned, including Anchors — there's no way
// to tell "shared across every character" (e.g. PlayerTransformAnchor) apart from "this one
// character's own anchor" (e.g. PKaGameObjectAnchor, which its own Stats depend on) by type
// alone. Folder selection is what decides that instead: simply don't include a folder whose
// contents should stay shared, and it's left untouched automatically.
public class StateMachineFolderClonerWindow : EditorWindow
{
    private class FolderEntry
    {
        public DefaultAsset sourceFolder;
        public string destinationFolder = "";
    }

    private readonly List<FolderEntry> _folderEntries = new List<FolderEntry> { new FolderEntry() };
    private string _findText = "PKa";
    private string _replaceText = "Krasue";
    private Vector2 _previewScroll;
    private List<(string oldPath, string newPath)> _plan;

    [MenuItem("Tools/State Machine/Clone Folder (Rename + Fix References)")]
    private static void Open()
    {
        GetWindow<StateMachineFolderClonerWindow>("Clone State Machine Folder");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Copies every asset under each Source Folder into its matching Destination Folder as " +
            "independent files, replacing Find Text with Replace Text in every file/folder name. " +
            "All folder pairs share one reference map, so references crossing between them (e.g. a " +
            "Stats modifier pointing at a Condition SO in a different folder) still resolve to the " +
            "correct clone. Anything you don't want duplicated (e.g. a shared PlayerTransformAnchor) " +
            "should simply be left out of every Source Folder.",
            MessageType.Info);

        EditorGUILayout.Space();
        _findText = EditorGUILayout.TextField("Find Text", _findText);
        _replaceText = EditorGUILayout.TextField("Replace Text", _replaceText);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Folder Pairs", EditorStyles.boldLabel);

        for (int i = 0; i < _folderEntries.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _folderEntries[i].sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                _folderEntries[i].sourceFolder, typeof(DefaultAsset), false);
            _folderEntries[i].destinationFolder = EditorGUILayout.TextField(
                _folderEntries[i].destinationFolder);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                _folderEntries.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add Folder Pair"))
            _folderEntries.Add(new FolderEntry());

        EditorGUILayout.Space();
        bool canRun = !string.IsNullOrEmpty(_findText) && HasAtLeastOneValidEntry();

        GUI.enabled = canRun;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview (Dry Run)", GUILayout.Height(28)))
            _plan = BuildPlan();
        if (GUILayout.Button("Clone For Real", GUILayout.Height(28)))
        {
            _plan = BuildPlan();
            Execute(_plan);
            _plan = null;
        }
        EditorGUILayout.EndHorizontal();
        GUI.enabled = true;

        if (_plan != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Preview: {_plan.Count} asset(s) will be created");

            _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.Height(220));
            foreach (var (oldPath, newPath) in _plan)
                EditorGUILayout.LabelField(Path.GetFileName(oldPath) + "  ->  " + newPath);
            EditorGUILayout.EndScrollView();
        }
    }

    private bool HasAtLeastOneValidEntry()
    {
        foreach (FolderEntry entry in _folderEntries)
        {
            if (entry.sourceFolder != null &&
                AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(entry.sourceFolder)) &&
                !string.IsNullOrEmpty(entry.destinationFolder) &&
                entry.destinationFolder.StartsWith("Assets/"))
            {
                return true;
            }
        }
        return false;
    }

    private List<(string oldPath, string newPath)> BuildPlan()
    {
        var plan = new List<(string, string)>();

        foreach (FolderEntry entry in _folderEntries)
        {
            if (entry.sourceFolder == null ||
                !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(entry.sourceFolder)) ||
                string.IsNullOrEmpty(entry.destinationFolder) ||
                !entry.destinationFolder.StartsWith("Assets/"))
            {
                continue;
            }

            string sourceRoot = AssetDatabase.GetAssetPath(entry.sourceFolder);
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { sourceRoot });

            foreach (string guid in guids)
            {
                string oldPath = AssetDatabase.GUIDToAssetPath(guid);
                Object obj = AssetDatabase.LoadMainAssetAtPath(oldPath);
                if (obj == null)
                    continue;

                string relative = oldPath.Substring(sourceRoot.Length).TrimStart('/');
                string renamedRelative = relative.Replace(_findText, _replaceText);
                string newPath = entry.destinationFolder.TrimEnd('/') + "/" + renamedRelative;

                plan.Add((oldPath, newPath));
            }
        }

        return plan;
    }

    private void Execute(List<(string oldPath, string newPath)> plan)
    {
        // Shared across every folder pair in this run, so a reference from an asset cloned
        // out of one folder to an asset cloned out of another still gets fixed up below.
        var oldToNew = new Dictionary<Object, Object>();

        foreach (var (oldPath, newPath) in plan)
        {
            string destFolder = Path.GetDirectoryName(newPath)?.Replace('\\', '/');
            EnsureFolderExists(destFolder);

            if (!AssetDatabase.CopyAsset(oldPath, newPath))
            {
                Debug.LogError($"[StateMachineFolderCloner] Failed to copy '{oldPath}' -> '{newPath}'");
                continue;
            }

            Object oldObj = AssetDatabase.LoadMainAssetAtPath(oldPath);
            Object newObj = AssetDatabase.LoadMainAssetAtPath(newPath);
            oldToNew[oldObj] = newObj;
        }

        // References are fixed up only after every copy (across every folder pair) exists,
        // so cross-links between any two cloned assets resolve correctly either way.
        foreach (Object newObj in oldToNew.Values)
            RemapReferences(newObj, oldToNew);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[StateMachineFolderCloner] Cloned {oldToNew.Count} asset(s) across {_folderEntries.Count} folder pair(s).");
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);

        EnsureFolderExists(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static void RemapReferences(Object target, Dictionary<Object, Object> oldToNew)
    {
        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = true;

            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue != null &&
                oldToNew.TryGetValue(property.objectReferenceValue, out Object replacement))
            {
                property.objectReferenceValue = replacement;
            }
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
