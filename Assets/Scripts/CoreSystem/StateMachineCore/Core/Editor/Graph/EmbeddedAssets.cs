#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Changes to what lives inside a table's file. States, Actions and Conditions go inside the
    // table's own file (sub-assets) instead of being separate files, so one enemy's state machine is
    // one file. Embedded is the default; Make Shared moves one out to its own file so other tables can
    // use it, and Make Local Copy brings a shared one in so this table can change it on its own.
    // Used by the graph and the Contents panel. Read-only questions are in TableContents.
    public static class EmbeddedAssets
    {
        // ---------- Names ----------

        // A sub-state machine's pieces are named after it, the way Yantra_v3 names them:
        //   Attack_State, AttackSubStateMachine_Action, Attack_TransitionTable, AttackStart_State
        //   exit "ToPatrol": AttackToPatrolExit_State, AttackExitToPatrolSubStateMachine_Action,
        //                    AttackToPatrolSubSMExited_Condition
        // The default exit leaves the exit id out: AttackExit_State, AttackSubSMExited_Condition.
        public const string DefaultExitId = "Default";
        private const string StateSuffix = "_State";
        private const string TableSuffix = "_TransitionTable";
        private const string ExitedMarker = "SubSMExited";

        // "Attack_State" -> "Attack"; a name without the suffix is its own prefix.
        public static string StatePrefix(string stateName) => WithoutSuffix(stateName, StateSuffix);

        // "Attack_TransitionTable" -> "Attack"
        public static string TablePrefix(string tableName) => WithoutSuffix(tableName, TableSuffix);

        private static string WithoutSuffix(string name, string suffix)
        {
            return name.EndsWith(suffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - suffix.Length) : name;
        }

        // The exit id as it appears in names: "" for the default exit, no spaces otherwise.
        private static string ExitIdPart(string exitId)
        {
            return exitId == DefaultExitId ? "" : exitId.Replace(" ", "");
        }

        // "AttackToPatrolSubSMExited" (an exit condition's name without "_Condition").
        private static string ExitConditionStem(string subStatePrefix, string exitId)
        {
            return subStatePrefix + ExitIdPart(exitId) + ExitedMarker;
        }

        // True when the condition was made for this sub-state machine (its name matches its exit id).
        public static bool IsExitConditionOf(SubStateMachineExitedConditionSO condition, string subStatePrefix)
        {
            string exitId = new SerializedObject(condition).FindProperty(StateMachineFields.ExitId).stringValue;
            return condition.name.StartsWith(ExitConditionStem(subStatePrefix, exitId), StringComparison.Ordinal);
        }

        // "New_State", "New_State 1", ... so every name in the file stays unique.
        private static string UniqueName(TransitionTableSO table, string baseName)
        {
            return ObjectNames.GetUniqueName(NamesInFile(table).ToArray(), baseName);
        }

        private static HashSet<string> NamesInFile(TransitionTableSO table)
        {
            return new HashSet<string>(AssetsInFile(table).Select(asset => asset.name));
        }

        // ---------- Create ----------
        // Each one is recorded for Undo; a caller making several things wraps them in one Undo group.

        public static T Create<T>(TransitionTableSO table, string baseName) where T : ScriptableObject
        {
            return (T)Create(table, typeof(T), baseName);
        }

        public static ScriptableObject Create(TransitionTableSO table, Type type, string baseName)
        {
            ScriptableObject asset = ScriptableObject.CreateInstance(type);
            asset.name = UniqueName(table, baseName);

            Undo.RegisterCreatedObjectUndo(asset, $"Create {asset.name}");
            AssetDatabase.AddObjectToAsset(asset, table);
            AssetDatabase.SaveAssetIfDirty(table);

            return asset;
        }

        // Ctrl+D / Ctrl+V in Contents: copies of the items in this table's file, with the next free
        // names ("Chase_Condition 1"). Nothing uses them yet. Returns original -> copy.
        // From this file: only the items (a copied state shares its actions, like Animator's Duplicate).
        // From another file: also whatever they use there, so no copy points into someone else's file.
        public static Dictionary<ScriptableObject, ScriptableObject> CopyInto(
            TransitionTableSO table,
            IEnumerable<ScriptableObject> items)
        {
            string path = AssetDatabase.GetAssetPath(table);
            var copies = new Dictionary<Object, Object>();
            var result = new Dictionary<ScriptableObject, ScriptableObject>();

            foreach (ScriptableObject item in items)
            {
                IEnumerable<ScriptableObject> needed = AssetDatabase.GetAssetPath(item) == path
                    ? new[] { item }
                    : TableContents.ReachableInSameFile(item).Where(original => original is not TransitionTableSO);

                foreach (ScriptableObject original in needed)
                {
                    if (copies.ContainsKey(original))
                    {
                        continue;
                    }

                    ScriptableObject copy = Object.Instantiate(original);
                    copy.name = UniqueName(table, original.name);
                    Undo.RegisterCreatedObjectUndo(copy, $"Copy {original.name}");
                    AssetDatabase.AddObjectToAsset(copy, table);
                    copies[original] = copy;
                }

                result[item] = (ScriptableObject)copies[item];
            }

            // Copies that use each other point at each other (only happens for another file's items).
            foreach (Object copy in copies.Values)
            {
                Remap(copy, copies);
            }

            AssetDatabase.SaveAssetIfDirty(table);
            return result;
        }

        // A ready-to-run sub-state machine, all inside the table's file and already wired:
        //   NewSub_State -> NewSubSubStateMachine_Action -> NewSub_TransitionTable (initial: NewSubStart_State)
        // A table needs an initial state to run, so the child gets an empty one to start from.
        // Returns the state to put on the graph.
        public static StateSO CreateSubStateMachine(TransitionTableSO table)
        {
            // One free prefix for all four names ("NewSub", "NewSub1", ...), so none of them gets " (1)".
            HashSet<string> takenNames = NamesInFile(table);
            string prefix = "NewSub";
            for (int number = 1;
                takenNames.Contains(prefix + StateSuffix) ||
                takenNames.Contains($"{prefix}SubStateMachine_Action") ||
                takenNames.Contains(prefix + TableSuffix) ||
                takenNames.Contains($"{prefix}Start{StateSuffix}");
                number++)
            {
                prefix = $"NewSub{number}";
            }

            StateSO state = Create<StateSO>(table, prefix + StateSuffix);
            ScriptableObject action = Create(table, typeof(SubStateMachineActionSO), $"{prefix}SubStateMachine_Action");
            TransitionTableSO childTable = Create<TransitionTableSO>(table, prefix + TableSuffix);
            StateSO startState = Create<StateSO>(table, $"{prefix}Start{StateSuffix}");

            AppendAction(state, action);
            SetReference(action, StateMachineFields.TransitionTable, childTable);
            SetReference(childTable, StateMachineFields.InitialState, startState);

            AssetDatabase.SaveAssetIfDirty(table);
            return state;
        }

        // One exit of a sub-state machine, wired on both sides with the same exit id:
        //   child table:  an exit state running an Exit Sub State Machine action
        //   parent table: a Sub State Machine Exited condition, for the arrow out of the sub-state
        // parentTable null: only the child side. Returns the exit state.
        public static StateSO CreateExit(
            TransitionTableSO parentTable,
            TransitionTableSO childTable,
            string subStatePrefix,
            string exitId)
        {
            string idPart = ExitIdPart(exitId);

            StateSO exitState = Create<StateSO>(childTable, $"{subStatePrefix}{idPart}Exit{StateSuffix}");
            ScriptableObject exitAction = Create(
                childTable,
                typeof(ExitSubStateMachineActionSO),
                $"{subStatePrefix}Exit{idPart}SubStateMachine_Action");

            SetString(exitAction, StateMachineFields.ExitId, exitId);
            AppendAction(exitState, exitAction);

            // A node position is what keeps a state with no transitions yet in its table.
            var serializedChild = new SerializedObject(childTable);
            SerializedProperty positions = serializedChild.FindProperty(StateMachineFields.NodePositions);
            positions.InsertArrayElementAtIndex(positions.arraySize);
            SerializedProperty position = positions.GetArrayElementAtIndex(positions.arraySize - 1);
            position.FindPropertyRelative("State").objectReferenceValue = exitState;
            position.FindPropertyRelative("Position").vector2Value = new Vector2(450f, 60f * positions.arraySize);
            serializedChild.ApplyModifiedProperties();

            if (parentTable != null)
            {
                CreateExitCondition(parentTable, subStatePrefix, exitId);
            }

            AssetDatabase.SaveAssetIfDirty(childTable);
            return exitState;
        }

        // A Sub State Machine Exited condition for the sub-state machine, waiting for an arrow out of it.
        // Its exit id must match the Exit Sub State Machine action of the state that leaves the child table.
        public static ScriptableObject CreateExitCondition(TransitionTableSO table, string subStatePrefix, string exitId)
        {
            ScriptableObject condition = Create(
                table,
                typeof(SubStateMachineExitedConditionSO),
                ExitConditionStem(subStatePrefix, exitId) + "_Condition");

            SetString(condition, StateMachineFields.ExitId, exitId);
            return condition;
        }

        private static void AppendAction(StateSO state, Object action)
        {
            var serializedState = new SerializedObject(state);
            SerializedProperty actions = serializedState.FindProperty(StateMachineFields.Actions);
            actions.InsertArrayElementAtIndex(actions.arraySize);
            actions.GetArrayElementAtIndex(actions.arraySize - 1).objectReferenceValue = action;
            serializedState.ApplyModifiedProperties();
        }

        private static void SetReference(Object owner, string field, Object value)
        {
            var serializedOwner = new SerializedObject(owner);
            serializedOwner.FindProperty(field).objectReferenceValue = value;
            serializedOwner.ApplyModifiedProperties();
        }

        private static void SetString(Object owner, string field, string value)
        {
            var serializedOwner = new SerializedObject(owner);
            serializedOwner.FindProperty(field).stringValue = value;
            serializedOwner.ApplyModifiedProperties();
        }

        // ---------- Shared / local ----------
        // Not undoable, like creating or deleting a file in the Project window.

        // ● -> ◆: the item becomes its own file at `path`. Everything in the table's file that used the
        // embedded one now uses the file.
        public static ScriptableObject MakeShared(TransitionTableSO table, ScriptableObject item, string path)
        {
            ScriptableObject shared = Object.Instantiate(item);
            shared.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(shared, path);

            ReplaceReferences(table, new Dictionary<Object, Object> { [item] = shared });

            AssetDatabase.RemoveObjectFromAsset(item);
            Object.DestroyImmediate(item, true);
            AssetDatabase.SaveAssets();

            return shared;
        }

        // ◆ -> ●: a copy of the shared item goes inside the table's file, and this table uses the copy.
        // Whatever the item uses from its own file comes along (e.g. a sub-state machine's table brings
        // the states, actions and conditions embedded next to it), and the copies point at each other.
        // Things in other files stay shared. The original file and the tables that use it are left alone.
        public static ScriptableObject MakeLocalCopy(TransitionTableSO table, ScriptableObject item)
        {
            var copies = new Dictionary<Object, Object>();

            foreach (ScriptableObject original in TableContents.ReachableInSameFile(item))
            {
                ScriptableObject copy = Object.Instantiate(original);
                copy.name = UniqueName(table, original.name);
                AssetDatabase.AddObjectToAsset(copy, table);
                copies[original] = copy;
            }

            // The copies point at each other, then this table's file points at the copy.
            foreach (Object copy in copies.Values)
            {
                Remap(copy, copies);
            }

            ReplaceReferences(table, new Dictionary<Object, Object> { [item] = copies[item] });
            AssetDatabase.SaveAssets();

            return (ScriptableObject)copies[item];
        }

        // In the table and everything inside its file (states, actions, conditions, folder entries,
        // node positions): every field that points at a key of `map` points at its value instead.
        private static void ReplaceReferences(TransitionTableSO table, Dictionary<Object, Object> map)
        {
            foreach (Object asset in AssetsInFile(table))
            {
                if (!map.ContainsKey(asset) && !map.ContainsValue(asset))
                {
                    Remap(asset, map);
                }
            }
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

        // ---------- Delete ----------

        // How many fields in the table's file point at the item.
        public static int CountUses(TransitionTableSO table, Object item)
        {
            int count = 0;
            foreach (Object asset in AssetsInFile(table).Where(asset => asset != item))
            {
                SerializedProperty property = new SerializedObject(asset).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == item)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        // Once `removed` are out of the table: those inside its file that nothing in the file reaches any
        // more leave the file, with whatever only they used there (a sub-state machine's action, its table
        // and the states inside it). Shared files are never touched. Recorded for Undo, the way Animator
        // deletes its states. Used when states are deleted in the graph.
        public static void DestroyUnreachable(TransitionTableSO table, IEnumerable<ScriptableObject> removed)
        {
            List<ScriptableObject> unreachable = UnreachableAfter(table, removed);
            foreach (ScriptableObject item in unreachable)
            {
                Undo.DestroyObjectImmediate(item);
            }

            if (unreachable.Count > 0)
            {
                AssetDatabase.SaveAssets();
            }
        }

        // ● / ○ in the Contents panel: the item leaves the table's file, after being taken out of
        // everything that used it (see RemoveUses), and so does whatever only it used there.
        // Not undoable, like deleting in the Project window.
        public static void Delete(TransitionTableSO table, ScriptableObject item)
        {
            RemoveUses(table, item);

            // Reachable is transitive, so one pass finds everything the item took with it.
            foreach (ScriptableObject unreachable in UnreachableAfter(table, new[] { item }))
            {
                AssetDatabase.RemoveObjectFromAsset(unreachable);
                Object.DestroyImmediate(unreachable, true);
            }

            AssetDatabase.SaveAssets();
        }

        // `removed` and what they used in the table's file, minus anything the file's main table still reaches.
        private static List<ScriptableObject> UnreachableAfter(TransitionTableSO table, IEnumerable<ScriptableObject> removed)
        {
            var candidates = new HashSet<ScriptableObject>();
            foreach (ScriptableObject item in removed)
            {
                if (item != null && TableContents.IsInSameFile(item, table))
                {
                    candidates.UnionWith(TableContents.ReachableInSameFile(item));
                }
            }

            var root = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(table)) as ScriptableObject;
            if (root == null || candidates.Count == 0)
            {
                return new List<ScriptableObject>();
            }

            var reached = new HashSet<ScriptableObject>(TableContents.ReachableInSameFile(root));
            return candidates.Where(candidate => candidate != root && !reached.Contains(candidate)).ToList();
        }

        // Takes the item out of everything in the table's file, without leaving empty slots that would
        // break at runtime: the innermost array entry holding it is removed (an action in a state's
        // list, a transition from or to it, a condition in its group, a node position, a folder entry);
        // a field outside any array (e.g. the initial state) becomes None. The item itself stays.
        // Exposed parameters are the exception: the row stays with no source (shown as Missing), so
        // characters that override it by name keep their value until it's pointed at a new source.
        public static void RemoveUses(TransitionTableSO table, Object item)
        {
            foreach (Object asset in AssetsInFile(table).Where(asset => asset != item))
            {
                var serializedAsset = new SerializedObject(asset);
                var targets = new HashSet<string>();

                SerializedProperty property = serializedAsset.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == item)
                    {
                        targets.Add(asset is ExposedParametersSO
                            ? property.propertyPath
                            : InnermostArrayElement(property.propertyPath));
                    }
                }

                if (targets.Count == 0)
                {
                    continue;
                }

                // Deepest first, and last index first inside one array, so the other paths stay valid.
                IEnumerable<string> ordered = targets
                    .OrderByDescending(path => path.Count(character => character == '['))
                    .ThenByDescending(LastIndexIn);

                foreach (string path in ordered)
                {
                    RemoveAt(serializedAsset, path);
                }

                serializedAsset.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
        }

        private const string ArrayData = ".Array.data[";

        // "_transitions.Array.data[3].ConditionGroups.Array.data[0].Conditions.Array.data[2].Condition"
        // -> "_transitions.Array.data[3].ConditionGroups.Array.data[0].Conditions.Array.data[2]"
        // A path not inside any array comes back unchanged.
        private static string InnermostArrayElement(string path)
        {
            int at = path.LastIndexOf(ArrayData, StringComparison.Ordinal);
            return at < 0 ? path : path.Substring(0, path.IndexOf(']', at) + 1);
        }

        private static int LastIndexIn(string path)
        {
            int at = path.LastIndexOf(ArrayData, StringComparison.Ordinal);
            if (at < 0)
            {
                return -1;
            }

            int start = at + ArrayData.Length;
            return int.Parse(path.Substring(start, path.IndexOf(']', start) - start));
        }

        private static void RemoveAt(SerializedObject serializedAsset, string path)
        {
            SerializedProperty target = serializedAsset.FindProperty(path);
            if (target == null)
            {
                return;
            }

            int at = path.LastIndexOf(ArrayData, StringComparison.Ordinal);
            if (at < 0 || !path.EndsWith("]", StringComparison.Ordinal))
            {
                target.objectReferenceValue = null;
                return;
            }

            // Deleting an object-reference entry that isn't empty only clears it, so clear it first.
            if (target.propertyType == SerializedPropertyType.ObjectReference)
            {
                target.objectReferenceValue = null;
            }

            serializedAsset.FindProperty(path.Substring(0, at)).DeleteArrayElementAtIndex(LastIndexIn(path));
        }

        private static IEnumerable<Object> AssetsInFile(TransitionTableSO table)
        {
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(table)).Where(asset => asset != null);
        }
    }
}

#endif
