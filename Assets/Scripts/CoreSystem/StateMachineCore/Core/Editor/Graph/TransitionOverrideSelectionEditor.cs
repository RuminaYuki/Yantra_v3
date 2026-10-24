#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Unity's Inspector for one of the Base's arrows in a variant table's graph. For each transition
    // behind it: the Base's conditions, and what this table does with it:
    //   Same as the Base   [Override Conditions] [Remove from This Table]
    //   Overridden here    its own condition groups to edit, [Revert to Base]
    //   Removed here       [Restore]
    // Overrides live in TransitionTableSO._transitionOverrides, removals in _removedIds (both by Id).
    [CustomEditor(typeof(TransitionOverrideSelection))]
    public class TransitionOverrideSelectionEditor : UnityEditor.Editor
    {
        private const string OverridesProperty = "_transitionOverrides";
        private const string RemovedIdsProperty = "_removedIds";
        private const string TransitionIdProperty = "_transitionId";
        private const string ConditionGroupsProperty = "_conditionGroups";

        private SerializedObject _table;

        // A button's edit, done after everything is drawn (rows after it still read the lists).
        private System.Action _pendingEdit;

        private void OnEnable()
        {
            var selection = (TransitionOverrideSelection)target;
            _table = selection.Table != null ? new SerializedObject(selection.Table) : null;
        }

        public override void OnInspectorGUI()
        {
            var selection = (TransitionOverrideSelection)target;

            if (_table == null || _table.targetObject == null || selection.BaseTable == null)
            {
                EditorGUILayout.HelpBox("Select the arrow in the graph again.", MessageType.Info);
                return;
            }

            _table.Update();

            OverrideInspectorGUI.DrawHeader(selection.name, _table.targetObject.name);

            for (int i = 0; i < selection.TransitionIds.Count; i++)
            {
                // One arrow can stand for several transitions: number them only then.
                string title = selection.TransitionIds.Count > 1 ? $"Transition {i + 1} · Conditions" : "Conditions";
                DrawTransition(selection, selection.TransitionIds[i], title);
            }

            OverrideInspectorGUI.DrawEditInBase(
                $"Open {selection.BaseTable.name}. Changes there affect every table that uses it.",
                () => Selection.activeObject = selection.BaseTable);

            bool changedStructure = _pendingEdit != null;
            if (_pendingEdit != null)
            {
                _pendingEdit();
                _pendingEdit = null;
            }

            _table.ApplyModifiedProperties();

            // The arrow's color shows overridden / removed: redraw the graph once this edit is saved.
            if (changedStructure)
            {
                EditorApplication.delayCall += StateMachineGraphWindow.ReloadAll;
            }
        }

        private void DrawTransition(TransitionOverrideSelection selection, string transitionId, string title)
        {
            TransitionTableSO.ConditionGroupUsage[] baseGroups = FindBaseConditions(selection, transitionId);

            if (baseGroups == null)
            {
                OverrideInspectorGUI.DrawBoxHeader(title, "Not in the Base any more", OverrideInspectorGUI.SameColor);
                OverrideInspectorGUI.BeginBoxBody();
                OverrideInspectorGUI.DrawNote("The Base doesn't have this transition any more.", OverrideInspectorGUI.SameColor);
                OverrideInspectorGUI.EndBoxBody();
                return;
            }

            string baseSummary = Summary(baseGroups);
            SerializedProperty removedIds = _table.FindProperty(RemovedIdsProperty);
            SerializedProperty overrides = _table.FindProperty(OverridesProperty);
            int removedIndex = IndexOfString(removedIds, transitionId);
            int overrideIndex = IndexOfOverride(overrides, transitionId);

            if (removedIndex >= 0)
            {
                int clicked = OverrideInspectorGUI.DrawBoxHeader(
                    title, "Removed here", OverrideInspectorGUI.RemovedColor,
                    new GUIContent("Restore", "Take this transition again in this table"));
                if (clicked == 0)
                {
                    _pendingEdit = () => removedIds.DeleteArrayElementAtIndex(removedIndex);
                }

                OverrideInspectorGUI.BeginBoxBody();
                OverrideInspectorGUI.DrawNote($"Not taken in this table. The Base's: {baseSummary}", OverrideInspectorGUI.SameColor);
                OverrideInspectorGUI.EndBoxBody();
            }
            else if (overrideIndex >= 0)
            {
                int clicked = OverrideInspectorGUI.DrawBoxHeader(
                    title, "Overridden here", OverrideInspectorGUI.ChangedColor,
                    new GUIContent("Revert", "Use the Base's conditions again"));
                if (clicked == 0)
                {
                    _pendingEdit = () => overrides.DeleteArrayElementAtIndex(overrideIndex);
                }

                OverrideInspectorGUI.BeginBoxBody();
                SerializedProperty groups = overrides.GetArrayElementAtIndex(overrideIndex)
                    .FindPropertyRelative(ConditionGroupsProperty);
                TransitionGUI.DrawConditionGroups(groups);
                EditorGUILayout.LabelField($"The Base's: {baseSummary}", EditorStyles.miniLabel);
                OverrideInspectorGUI.EndBoxBody();
            }
            else
            {
                int clicked = OverrideInspectorGUI.DrawBoxHeader(
                    title, "Same as the Base", OverrideInspectorGUI.SameColor,
                    new GUIContent("Override", "Start from the Base's conditions and change them for this table"),
                    new GUIContent("Remove", "Don't take this transition in this table"));
                if (clicked == 0)
                {
                    _pendingEdit = () => AddOverride(overrides, transitionId, baseGroups);
                }
                else if (clicked == 1)
                {
                    _pendingEdit = () =>
                    {
                        removedIds.arraySize++;
                        removedIds.GetArrayElementAtIndex(removedIds.arraySize - 1).stringValue = transitionId;
                    };
                }

                OverrideInspectorGUI.BeginBoxBody();
                EditorGUILayout.LabelField(baseSummary, EditorStyles.wordWrappedLabel);
                OverrideInspectorGUI.EndBoxBody();
            }
        }

        // ---------- Data ----------

        // The Base's conditions for the transition (its own Base's overrides already in), or null.
        private static TransitionTableSO.ConditionGroupUsage[] FindBaseConditions(
            TransitionOverrideSelection selection,
            string transitionId)
        {
            if (selection.IsAnyState)
            {
                foreach (TransitionTableSO.AnyTransitionItem item in selection.BaseTable.EffectiveAnyTransitions)
                {
                    if (item.Id == transitionId)
                    {
                        return item.ConditionGroups ?? new TransitionTableSO.ConditionGroupUsage[0];
                    }
                }

                return null;
            }

            foreach (TransitionTableSO.TransitionItem item in selection.BaseTable.EffectiveTransitions)
            {
                if (item.Id == transitionId)
                {
                    return item.ConditionGroups ?? new TransitionTableSO.ConditionGroupUsage[0];
                }
            }

            return null;
        }

        // A copy of the Base's conditions to start from, so overriding changes nothing until edited.
        private static void AddOverride(
            SerializedProperty overrides,
            string transitionId,
            TransitionTableSO.ConditionGroupUsage[] baseGroups)
        {
            overrides.arraySize++;
            SerializedProperty entry = overrides.GetArrayElementAtIndex(overrides.arraySize - 1);
            entry.FindPropertyRelative(TransitionIdProperty).stringValue = transitionId;

            SerializedProperty groups = entry.FindPropertyRelative(ConditionGroupsProperty);
            groups.arraySize = baseGroups.Length;

            for (int g = 0; g < baseGroups.Length; g++)
            {
                TransitionTableSO.ConditionUsage[] baseConditions =
                    baseGroups[g].Conditions ?? new TransitionTableSO.ConditionUsage[0];
                SerializedProperty conditions = groups.GetArrayElementAtIndex(g).FindPropertyRelative("Conditions");
                conditions.arraySize = baseConditions.Length;

                for (int c = 0; c < baseConditions.Length; c++)
                {
                    SerializedProperty condition = conditions.GetArrayElementAtIndex(c);
                    condition.FindPropertyRelative("Condition").objectReferenceValue = baseConditions[c].Condition;
                    condition.FindPropertyRelative("ExpectedResult").enumValueIndex = (int)baseConditions[c].ExpectedResult;
                }
            }
        }

        private static int IndexOfOverride(SerializedProperty overrides, string transitionId)
        {
            for (int i = 0; i < overrides.arraySize; i++)
            {
                if (overrides.GetArrayElementAtIndex(i).FindPropertyRelative(TransitionIdProperty).stringValue == transitionId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfString(SerializedProperty list, string value)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).stringValue == value)
                {
                    return i;
                }
            }

            return -1;
        }

        // "when A and not B or C", or "always".
        private static string Summary(TransitionTableSO.ConditionGroupUsage[] groups)
        {
            var parts = new System.Collections.Generic.List<string>();

            foreach (TransitionTableSO.ConditionGroupUsage group in groups)
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (TransitionTableSO.ConditionUsage usage in group.Conditions ?? new TransitionTableSO.ConditionUsage[0])
                {
                    string name = usage.Condition != null ? usage.Condition.name : "(missing)";
                    names.Add(usage.ExpectedResult == TransitionTableSO.Result.False ? $"not {name}" : name);
                }

                if (names.Count > 0)
                {
                    parts.Add(string.Join(" and ", names));
                }
            }

            return parts.Count > 0 ? "when " + string.Join(" or ", parts) : "always";
        }
    }
}

#endif
