using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Object = UnityEngine.Object;

/// <summary>
/// ONE-TIME tool. Delete this file afterwards.
/// Select a Transition Table file (e.g. PlayerAction_TransitionTable) and run it: every state machine
/// asset the table reaches (states, actions, conditions, sub-state machine tables, exposed parameters,
/// all the way down) that lives in another file is copied into the table's file, and the file uses the
/// copies. Assets in the General folder stay shared and aren't followed. Other data (stats, animation
/// data, ...) isn't copied. Then every sub-asset in the file loses a leading "Player" ("PlayerGun_State"
/// -> "Gun_State"). The original files are left alone. A backup of the table's file goes to Backups/.
/// </summary>
public static class LocalizeTableTool
{
    private const string GeneralFolder = "Assets/ScriptableObject/StateMachine/General/";
    private const string Prefix = "Player";

    [MenuItem("Tools/State Machine/Localize Selected Table (except General)")]
    private static void Run()
    {
        if (Selection.activeObject is not TransitionTableSO table || !AssetDatabase.IsMainAsset(table))
        {
            EditorUtility.DisplayDialog("Localize Table", "Select a Transition Table file in the Project window first.", "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(table);
        List<ScriptableObject> toCopy = CollectToCopy(table, path);
        List<ScriptableObject> embedded = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<ScriptableObject>()
            .Where(asset => asset != table && IsStateMachineAsset(asset))
            .ToList();

        if (!EditorUtility.DisplayDialog(
                "Localize Table",
                $"{table.name}:\n" +
                $"  copy {toCopy.Count} asset(s) from other files into this file (General stays shared)\n" +
                $"  remove a leading \"{Prefix}\" from the names in this file\n\n" +
                "This can't be undone. A backup of the file goes to Backups/ in the project folder.",
                "Localize", "Cancel"))
        {
            return;
        }

        // Unsaved edits go to disk first, so the backup has them.
        AssetDatabase.SaveAssets();
        BackUp(path);

        // Final names for everything in the file (what's there and the copies), without "Player", unique.
        var names = new HashSet<string>(StringComparer.Ordinal) { table.name };
        var newNames = new Dictionary<ScriptableObject, string>();
        foreach (ScriptableObject asset in embedded.Concat(toCopy))
        {
            string name = UniqueName(names, WithoutPrefix(asset.name));
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

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path);

        Debug.Log($"[LocalizeTableTool] {table.name}: copied {toCopy.Count}, renamed {embedded.Count + toCopy.Count}.", table);
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

    // "PlayerGun_State" -> "Gun_State". "Playerish_State" stays (the next letter isn't a capital).
    private static string WithoutPrefix(string name)
    {
        return name.Length > Prefix.Length &&
               name.StartsWith(Prefix, StringComparison.Ordinal) &&
               char.IsUpper(name[Prefix.Length])
            ? name.Substring(Prefix.Length)
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
