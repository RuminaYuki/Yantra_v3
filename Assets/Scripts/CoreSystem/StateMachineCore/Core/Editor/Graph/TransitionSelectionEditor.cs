#if UNITY_EDITOR

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
            var selection = (TransitionSelection)target;
            _table = selection.Table != null ? new SerializedObject(selection.Table) : null;
        }

        public override void OnInspectorGUI()
        {
            var selection = (TransitionSelection)target;

            if (_table == null || _table.targetObject == null)
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
                    TransitionGUI.DrawConditionGroups(transition.FindPropertyRelative("ConditionGroups"));
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
    }
}

#endif
