#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Unity's Inspector for a selected graph arrow: its conditions, or the Initial State for Entry.
    // Edits go straight into the table, so they save and undo like any table edit.
    [CustomEditor(typeof(TransitionSelection))]
    public class TransitionSelectionEditor : UnityEditor.Editor
    {
        private SerializedObject _table;

        private void OnEnable()
        {
            // Unity can enable this editor once more for a stand-in the graph just destroyed (a reload
            // selects a new one), when target is no longer a TransitionSelection.
            var selection = target as TransitionSelection;
            _table = selection != null && selection.Table != null ? new SerializedObject(selection.Table) : null;
        }

        // Keep the live condition results updating while playing.
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            var selection = target as TransitionSelection;

            if (selection == null || _table == null || _table.targetObject == null)
            {
                EditorGUILayout.HelpBox("Select the arrow in the graph again.", MessageType.Info);
                return;
            }

            _table.Update();

            EditorGUILayout.LabelField($"In {_table.targetObject.name}", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            if (selection.EdgeKind == TransitionSelection.Kind.Entry)
            {
                DrawEntry();
            }
            else if (selection.EdgeKind == TransitionSelection.Kind.AnyStateNode)
            {
                EditorGUILayout.LabelField("Any State transitions", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Checked top to bottom, before the current state's own transitions.",
                    EditorStyles.miniLabel);
                StateSOEditor.DrawOrderedTransitions(_table, null);
            }
            else
            {
                DrawTransitions(selection);
            }

            _table.ApplyModifiedProperties();
        }

        private void DrawEntry()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_table.FindProperty(StateMachineFields.InitialState), new GUIContent("Initial State"));

            if (EditorGUI.EndChangeCheck())
            {
                // Apply before the graph reloads, since the reload reads the asset.
                _table.ApplyModifiedProperties();
                EditorApplication.delayCall += StateMachineGraphWindow.ReloadAll;
            }
        }

        // Every transition behind this arrow (one arrow can stand for several). Found by From/To on
        // each draw, so a reorder made elsewhere can't make it edit the wrong item.
        private void DrawTransitions(TransitionSelection selection)
        {
            bool isAnyState = selection.EdgeKind == TransitionSelection.Kind.AnyStateTransition;
            SerializedProperty transitions = _table.FindProperty(isAnyState ? StateMachineFields.AnyTransitions : StateMachineFields.Transitions);

            // Priority counts only transitions checked against each other: same From state, or all Any State ones.
            int priority = 0;
            bool isFound = false;

            IReadOnlyList<StateTransition> runtimeTransitions = FindRuntimeTransitions(selection, isAnyState);

            for (int i = 0; i < transitions.arraySize; i++)
            {
                SerializedProperty transition = transitions.GetArrayElementAtIndex(i);

                if (!isAnyState &&
                    transition.FindPropertyRelative("FromState").objectReferenceValue != selection.From)
                {
                    continue;
                }

                priority++;

                if (transition.FindPropertyRelative("ToState").objectReferenceValue != selection.To)
                {
                    continue;
                }

                isFound = true;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                transition.isExpanded = EditorGUILayout.Foldout(transition.isExpanded, $"Priority {priority}", true);

                if (transition.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    // Runtime transitions keep the table's order, so Priority N is index N - 1.
                    StateTransition runtime =
                        runtimeTransitions != null && priority - 1 < runtimeTransitions.Count
                            ? runtimeTransitions[priority - 1]
                            : null;

                    TransitionGUI.DrawConditionGroups(transition.FindPropertyRelative("ConditionGroups"), runtime);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
            }

            if (!isFound)
            {
                EditorGUILayout.HelpBox("This arrow no longer exists.", MessageType.Info);
            }
        }

        // Play Mode: the running transitions this arrow belongs to, or null when they aren't checked
        // right now (not playing, nothing running this table, or the character is in another state).
        private static IReadOnlyList<StateTransition> FindRuntimeTransitions(
            TransitionSelection selection, bool isAnyState)
        {
            if (!Application.isPlaying)
            {
                return null;
            }

            StateMachine machine = StateMachineGraphWindow.FindRunningMachine(selection.Table);
            if (machine == null)
            {
                EditorGUILayout.HelpBox(
                    "Live results: keep the graph window open on a GameObject running this table.",
                    MessageType.None);
                return null;
            }

            if (isAnyState)
            {
                return machine.AnyTransitions;
            }

            State current = machine.CurrentState;
            if (current == null || selection.From == null || current.DebugName != selection.From.name)
            {
                EditorGUILayout.HelpBox(
                    $"Live results: not checked, the character is in {machine.CurrentStateName}.",
                    MessageType.None);
                return null;
            }

            return current.Transitions;
        }
    }
}

#endif
