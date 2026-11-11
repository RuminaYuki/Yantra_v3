#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor
{
    // A list for building Create menus, used by the Contents panel and Create > State Machine SO...:
    // every ScriptableObject script under Assets/Scripts,
    // grouped by the folder it is in. The folders are read each time, so moving a script moves its entry.
    // A class with [MenuGroup] goes in the group that says instead.
    public static class FolderMenu
    {
        private const string ScriptsRoot = "Assets/Scripts";

        // One entry of a Create menu.
        public struct Entry
        {
            public Type Type;
            public string MenuPath;   // "CoreSystem/SOBlueprintStandard/Animator/PlayAnimatorStateAction"
            public string BaseName;   // "New_PlayAnimatorState_Action"
        }

        private class Folder
        {
            public readonly List<Type> Types = new List<Type>();
            public readonly SortedDictionary<string, Folder> Subfolders =
                new SortedDictionary<string, Folder>(StringComparer.OrdinalIgnoreCase);
        }

        // Every script whose class passes belongs, e.g. "is an Action".
        public static List<Entry> Find(Func<Type, bool> belongs)
        {
            var top = new Folder();
            var chosenPlace = new List<Entry>();
            var found = new HashSet<Type>();

            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { ScriptsRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Editor/"))
                {
                    continue;
                }

                Type type = AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass();
                if (!IsCreatable(type) || !belongs(type))
                {
                    continue;
                }

                found.Add(type);

                var attribute = (MenuGroupAttribute)Attribute.GetCustomAttribute(type, typeof(MenuGroupAttribute), false);
                if (attribute != null)
                {
                    chosenPlace.Add(NewEntry(type, $"{attribute.Group}/{ScriptFolderPart(path)}{NameWithoutSO(type)}"));
                }
                else
                {
                    FolderOf(top, path).Types.Add(type);
                }
            }

            WarnAboutClassesWithoutScript(belongs, found);

            var entries = new List<Entry>();
            AddEntries(top, "", entries);
            entries.AddRange(chosenPlace.OrderBy(entry => entry.MenuPath, StringComparer.OrdinalIgnoreCase));
            return entries;
        }

        // What a [MenuGroup] entry keeps of its script's place: every folder after Action / Condition.
        //   ".../Action/Animator/PlayAnimatorStateActionSO.cs"     -> "Animator/"
        //   ".../Action/Addon/EventChanel/RaiseVoid...ActionSO.cs" -> "Addon/EventChanel/"
        //   ".../Action/DebugLogActionSO.cs"                       -> ""
        // "Action" and "Condition" only repeat the menu they're in, so they're left out.
        // A script not under either keeps only the folder it is directly in.
        private static string ScriptFolderPart(string scriptPath)
        {
            string[] folders = Path.GetDirectoryName(scriptPath).Replace('\\', '/').Split('/');

            int start = Array.FindLastIndex(folders, name => name == "Action" || name == "Condition");
            if (start < 0)
            {
                string folder = folders[folders.Length - 1];
                return folder == Path.GetFileName(ScriptsRoot) ? "" : $"{folder}/";
            }

            string rest = string.Join("/", folders.Skip(start + 1));
            return rest.Length > 0 ? $"{rest}/" : "";
        }

        private static Entry NewEntry(Type type, string menuPath)
        {
            return new Entry { Type = type, MenuPath = menuPath, BaseName = BaseNameOf(type) };
        }

        // The asset name pattern is Name_Type_ElementType, so only "New" is left to replace:
        //   PlayAnimatorStateActionSO -> New_PlayAnimatorState_Action
        //   CooldownSO                -> New_Cooldown
        private static string BaseNameOf(Type type)
        {
            string name = NameWithoutSO(type);

            if (typeof(StateActionSO).IsAssignableFrom(type))
            {
                return $"New_{WithoutEnding(name, "Action")}_Action";
            }

            if (typeof(StateConditionSO).IsAssignableFrom(type))
            {
                return $"New_{WithoutEnding(name, "Condition")}_Condition";
            }

            return $"New_{name}";
        }

        private static bool IsCreatable(Type type)
        {
            return type != null &&
                   !type.IsAbstract &&
                   !type.IsGenericType &&
                   typeof(ScriptableObject).IsAssignableFrom(type);
        }

        // "Assets/Scripts/TestCompoent/HealthDamageSystem/X.cs" -> the folder top > TestCompoent > HealthDamageSystem.
        private static Folder FolderOf(Folder top, string scriptPath)
        {
            string directory = Path.GetDirectoryName(scriptPath).Replace('\\', '/');
            string relative = directory.Substring(ScriptsRoot.Length);

            Folder folder = top;
            foreach (string name in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!folder.Subfolders.TryGetValue(name, out Folder subfolder))
                {
                    subfolder = new Folder();
                    folder.Subfolders[name] = subfolder;
                }

                folder = subfolder;
            }

            return folder;
        }

        // A folder's scripts first, then its subfolders. A folder that only leads to one other folder
        // adds a click and nothing to choose, so it shows as one level, under the upper folder's name:
        // CoreSystem/StateMachineCore/(Addon, Core, ...) -> CoreSystem/(Addon, Core, ...)
        private static void AddEntries(Folder folder, string menuPath, List<Entry> entries)
        {
            foreach (Type type in folder.Types.OrderBy(NameWithoutSO, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(NewEntry(type, menuPath + NameWithoutSO(type)));
            }

            foreach (KeyValuePair<string, Folder> subfolder in folder.Subfolders)
            {
                Folder shown = subfolder.Value;
                while (shown.Types.Count == 0 && shown.Subfolders.Count == 1)
                {
                    shown = shown.Subfolders.Values.First();
                }

                AddEntries(shown, $"{menuPath}{subfolder.Key}/", entries);
            }
        }

        // PlayAnimatorStateActionSO -> PlayAnimatorStateAction
        private static string NameWithoutSO(Type type)
        {
            return WithoutEnding(type.Name, "SO");
        }

        private static string WithoutEnding(string name, string ending)
        {
            if (name.EndsWith(ending, StringComparison.Ordinal))
            {
                return name.Substring(0, name.Length - ending.Length);
            }

            return name;
        }

        // GetClass only finds a class in the file of the same name, so any other class would just be
        // missing from the menu. Unity can't save such a class as an asset either, so it's listed here.
        private static void WarnAboutClassesWithoutScript(Func<Type, bool> belongs, HashSet<Type> found)
        {
            List<string> missing = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(type => type.Assembly.GetName().Name == "Assembly-CSharp" &&
                               IsCreatable(type) && belongs(type) && !found.Contains(type))
                .Select(type => type.Name)
                .OrderBy(name => name)
                .ToList();

            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    $"[State Machine SO] Not in the Create menu, because no file under {ScriptsRoot} has the class's name: " +
                    string.Join(", ", missing));
            }
        }
    }
}

#endif
