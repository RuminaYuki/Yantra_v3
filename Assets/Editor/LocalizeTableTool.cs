using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.Editor.Graph;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Object = UnityEngine.Object;

/// <summary>
/// ONE-TIME tool. Delete this file afterwards.
/// Select a Transition Table file (e.g. PKa_TransitionTable) and run it: every state machine asset the
/// table reaches (states, actions, conditions, sub-state machine tables, exposed parameters, all the way
/// down) that lives in another file is copied into the table's file, and the file uses the copies.
/// Assets in the General folder stay shared and aren't followed. Other data (stats, animation data, ...)
/// isn't copied. Then:
///   - every sub-asset in the file loses the table's prefix ("PKa" for PKa_TransitionTable,
///     "Player" for PlayerAction_TransitionTable): "PKaChase_State" -> "Chase_State"
///   - each table's actions and conditions go into Contents folders named after the folder their file
///     was in, like Player's ("Condition/Distance/PKaAttackRange_Condition" -> folder "Distance",
///     "General/.../Action/Locomotion/..." -> "Locomotion")
/// The original files are left alone. A backup of the table's file goes to Backups/.
/// </summary>
public static class LocalizeTableTool
{
    private const string GeneralFolder = "Assets/ScriptableObject/StateMachine/General/";
    private const string ContentsFoldersProperty = "_contentsFolders";

    // Folder names that hold one kind of asset; the folder right under the last one is the Contents folder.
    private static readonly string[] KindFolders = { "Action", "Actions", "Condition", "Conditions" };

    [MenuItem("Tools/State Machine/Localize Selected Table (except General)")]
    private static void Run()
    {
        if (Selection.activeObject is not TransitionTableSO table || !AssetDatabase.IsMainAsset(table))
        {
            EditorUtility.DisplayDialog("Localize Table", "Select a Transition Table file in the Project window first.", "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(table);
        string prefix = PrefixOf(table.name);
        List<ScriptableObject> toCopy = CollectToCopy(table, path);
        List<ScriptableObject> embedded = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<ScriptableObject>()
            .Where(asset => asset != table && IsStateMachineAsset(asset))
            .ToList();

        if (!EditorUtility.DisplayDialog(
                "Localize Table",
                $"{table.name}:\n" +
                $"  copy {toCopy.Count} asset(s) from other files into this file (General stays shared)\n" +
                $"  remove a leading \"{prefix}\" from the names in this file\n" +
                "  put actions / conditions into Contents folders named after their old folders\n\n" +
                "This can't be undone. A backup of the file goes to Backups/ in the project folder.",
                "Localize", "Cancel"))
        {
            return;
        }

        // Unsaved edits go to disk first, so the backup has them.
        AssetDatabase.SaveAssets();
        BackUp(path);

        // Where each thing came from, for its Contents folder (copies lose their own path).
        var sourcePaths = new Dictionary<Object, string>();
        foreach (ScriptableObject original in toCopy)
        {
            sourcePaths[original] = AssetDatabase.GetAssetPath(original);
        }

        // Final names for everything in the file (what's there and the copies), without the prefix, unique.
        var names = new HashSet<string>(StringComparer.Ordinal) { table.name };
        var newNames = new Dictionary<ScriptableObject, string>();
        foreach (ScriptableObject asset in embedded.Concat(toCopy))
        {
            string name = UniqueName(names, WithoutPrefix(asset.name, prefix));
            names.Add(name);
            newNames[asset] = name;
        }

        var copies = new Dictionary<Object, Object>();
        foreach (ScriptableObject original in toCopy)
        {
            ScriptableObject copy = Object.Instantiate(original);
            copy.name = newNames[original];
            AssetDatabase.AddObjectToAsset(copy, table);
            copies[original] = copy;
            sourcePaths[copy] = sourcePaths[original];
        }

        foreach (ScriptableObject asset in embedded)
        {
            asset.name = newNames[asset];
            EditorUtility.SetDirty(asset);
        }

        // Everything in the file (the table, what was there, the copies) points at the copies.
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path).Where(asset => asset != null))
        {
            Remap(asset, copies);
        }

        int filed = FileIntoFolders(path, sourcePaths, prefix);

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path);

        Debug.Log(
            $"[LocalizeTableTool] {table.name}: copied {toCopy.Count}, renamed {embedded.Count + toCopy.Count}, " +
            $"put {filed} into folders.",
            table);
    }

    // State machine assets the table reaches through its fields that live in other files, General aside.
    private static List<ScriptableObject> CollectToCopy(TransitionTableSO table, string path)
    {
        var toCopy = new List<ScriptableObject>();
        var visited = new HashSet<ScriptableObject> { table };
        var pending = new Stack<ScriptableObject>();
        pending.Push(table);

        while (pending.Count > 0)
        {
            SerializedProperty property = new SerializedObject(pending.Pop()).GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue is not ScriptableObject child ||
                    !visited.Add(child) ||
                    !IsStateMachineAsset(child))
                {
                    continue;
                }

                string childPath = AssetDatabase.GetAssetPath(child);
                if (childPath.StartsWith(GeneralFolder, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (childPath != path)
                {
                    toCopy.Add(child);
                }

                pending.Push(child);
            }
        }

        return toCopy;
    }

    private static bool IsStateMachineAsset(Object asset)
    {
        return asset is StateSO ||
               asset is StateActionSO ||
               asset is StateConditionSO ||
               asset is TransitionTableSO ||
               asset is ExposedParametersSO;
    }

    // ---------- Names ----------

    // The character's name at the front of the table's name: "PKa_TransitionTable" -> "PKa",
    // "PlayerAction_TransitionTable" -> "Player" (cut where a small letter meets a capital).
    private static string PrefixOf(string tableName)
    {
        string stem = tableName.Split('_')[0];
        for (int i = 1; i < stem.Length; i++)
        {
            if (char.IsLower(stem[i - 1]) && char.IsUpper(stem[i]))
            {
                return stem.Substring(0, i);
            }
        }

        return stem;
    }

    // "PKaChase_State" -> "Chase_State". "PKanot_State" stays (the next letter isn't a capital).
    private static string WithoutPrefix(string name, string prefix)
    {
        return prefix.Length > 0 &&
               name.Length > prefix.Length &&
               name.StartsWith(prefix, StringComparison.Ordinal) &&
               char.IsUpper(name[prefix.Length])
            ? name.Substring(prefix.Length)
            : name;
    }

    private static string UniqueName(HashSet<string> names, string name)
    {
        string unique = name;
        for (int i = 2; names.Contains(unique); i++)
        {
            unique = $"{name}{i}";
        }

        return unique;
    }

    // ---------- Contents folders ----------

    // For every table in the file: each action / condition it uses that has no folder yet goes into the
    // folder named after where its file was (copies) or is (shared ones, e.g. General). Sub-state machine
    // wiring is left out, since Contents shows it under its sub-state. Returns how many were filed.
    private static int FileIntoFolders(string path, Dictionary<Object, string> sourcePaths, string prefix)
    {
        int filed = 0;

        foreach (TransitionTableSO fileTable in AssetDatabase.LoadAllAssetsAtPath(path).OfType<TransitionTableSO>())
        {
            var serializedTable = new SerializedObject(fileTable);
            SerializedProperty entries = serializedTable.FindProperty(ContentsFoldersProperty);
            if (entries == null)
            {
                continue;
            }

            var hasFolder = new HashSet<Object>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                hasFolder.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("Item").objectReferenceValue);
            }

            foreach (ScriptableObject item in TableContents.CollectUsed(fileTable))
            {
                if (item is not (StateActionSO or StateConditionSO) ||
                    item is SubStateMachineActionSO ||
                    item is ExitSubStateMachineActionSO ||
                    item is SubStateMachineExitedConditionSO ||
                    hasFolder.Contains(item))
                {
                    continue;
                }

                string source = sourcePaths.TryGetValue(item, out string copiedFrom)
                    ? copiedFrom
                    : AssetDatabase.GetAssetPath(item);
                if (source == path)
                {
                    continue;   // was already inside the file: nothing says where it belongs
                }

                string folder = FolderOf(source, prefix);
                if (folder.Length == 0)
                {
                    continue;
                }

                entries.arraySize++;
                SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("Item").objectReferenceValue = item;
                entry.FindPropertyRelative("Folder").stringValue = folder;
                hasFolder.Add(item);
                filed++;
            }

            serializedTable.ApplyModifiedPropertiesWithoutUndo();
        }

        return filed;
    }

    // The folder right under the last Action / Condition folder in the path, or "" if the file sits
    // straight in it. ".../Condition/Distance/PKaAttackRange_Condition.asset" -> "Distance".
    private static string FolderOf(string assetPath, string prefix)
    {
        string[] parts = assetPath.Split('/');
        for (int i = parts.Length - 2; i >= 0; i--)
        {
            if (KindFolders.Contains(parts[i], StringComparer.OrdinalIgnoreCase))
            {
                return i + 1 < parts.Length - 1 ? WithoutPrefix(parts[i + 1], prefix) : "";
            }
        }

        return "";
    }

    // ---------- Helpers ----------

    private static void Remap(Object asset, Dictionary<Object, Object> map)
    {
        var serializedAsset = new SerializedObject(asset);
        SerializedProperty property = serializedAsset.GetIterator();
        bool isChanged = false;

        while (property.Next(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue != null &&
                map.TryGetValue(property.objectReferenceValue, out Object replacement))
            {
                property.objectReferenceValue = replacement;
                isChanged = true;
            }
        }

        if (isChanged)
        {
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // <project>/Backups/<file>_<time>.asset (+ .meta). To restore: copy both back over the originals.
    private static void BackUp(string assetPath)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string backupFolder = Path.Combine(projectRoot, "Backups");
        Directory.CreateDirectory(backupFolder);

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{Path.GetFileNameWithoutExtension(assetPath)}_{stamp}.asset";
        string source = Path.Combine(projectRoot, assetPath);

        File.Copy(source, Path.Combine(backupFolder, fileName));
        File.Copy(source + ".meta", Path.Combine(backupFolder, fileName + ".meta"));
        Debug.Log($"[LocalizeTableTool] Backup: {Path.Combine(backupFolder, fileName)}");
    }
}
