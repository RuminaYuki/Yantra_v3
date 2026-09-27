#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor
{
    [CustomEditor(typeof(TransitionTableSO))]
    public class TransitionTableSOEditor : UnityEditor.Editor
    {
        private SerializedProperty _initialState;
        private SerializedProperty _anyTransitions;
        private SerializedProperty _transitions;

        private void OnEnable()
        {
            _initialState = serializedObject.FindProperty("_initialState");
            _anyTransitions = serializedObject.FindProperty("_anyTransitions");
            _transitions = serializedObject.FindProperty("_transitions");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_initialState);
            EditorGUILayout.Space();

            DrawTransitionList(
                _anyTransitions,
                "Any State Transitions",
                "Any State transitions are checked before local transitions. Top entries have higher priority.",
                "+ Add Any State Transition",
                false);

            EditorGUILayout.Space(10f);

            DrawTransitionList(
                _transitions,
                "Local Transitions",
                "Local transitions are checked from top to bottom. The first valid transition is used.",
                "+ Add Local Transition",
                true);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTransitionList(
            SerializedProperty transitions,
            string title,
            string helpText,
            string addButtonLabel,
            bool hasFromState)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(helpText, MessageType.Info);

            for (int transitionIndex = 0;
                transitionIndex < transitions.arraySize;
                transitionIndex++)
            {
                SerializedProperty transition =
                    transitions.GetArrayElementAtIndex(transitionIndex);

                DrawTransition(
                    transitions,
                    transition,
                    transitionIndex,
                    hasFromState);

                EditorGUILayout.Space(6f);
            }

            if (GUILayout.Button(addButtonLabel, GUILayout.Height(26f)))
            {
                int newIndex = transitions.arraySize;
                transitions.InsertArrayElementAtIndex(newIndex);
                ClearTransition(
                    transitions.GetArrayElementAtIndex(newIndex),
                    hasFromState);
            }
        }

        private void DrawTransition(
            SerializedProperty transitions,
            SerializedProperty transition,
            int transitionIndex,
            bool hasFromState)
        {
            SerializedProperty fromState = hasFromState
                ? transition.FindPropertyRelative("FromState")
                : null;
            SerializedProperty toState =
                transition.FindPropertyRelative("ToState");
            SerializedProperty groups =
                transition.FindPropertyRelative("ConditionGroups");

            string fromName = hasFromState
                ? TransitionGUI.GetStateName(fromState)
                : "Any State";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            transition.isExpanded = EditorGUILayout.Foldout(
                transition.isExpanded,
                $"Priority {transitionIndex + 1}: {fromName} -> {TransitionGUI.GetStateName(toState)}",
                true);

            if (GUILayout.Button("Up", GUILayout.Width(38f)) &&
                transitionIndex > 0)
            {
                transitions.MoveArrayElement(
                    transitionIndex,
                    transitionIndex - 1);
            }

            if (GUILayout.Button("Down", GUILayout.Width(48f)) &&
                transitionIndex < transitions.arraySize - 1)
            {
                transitions.MoveArrayElement(
                    transitionIndex,
                    transitionIndex + 1);
            }

            if (GUILayout.Button("Delete", GUILayout.Width(54f)))
            {
                transitions.DeleteArrayElementAtIndex(transitionIndex);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            if (transition.isExpanded)
            {
                EditorGUI.indentLevel++;

                if (hasFromState)
                {
                    EditorGUILayout.PropertyField(
                        fromState,
                        new GUIContent("From"));
                }

                EditorGUILayout.PropertyField(toState, new GUIContent("To"));
                EditorGUILayout.Space(4f);
                TransitionGUI.DrawConditionGroups(groups);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private static void ClearTransition(
            SerializedProperty transition,
            bool hasFromState)
        {
            if (hasFromState)
            {
                transition.FindPropertyRelative("FromState")
                    .objectReferenceValue = null;
            }

            transition.FindPropertyRelative("ToState")
                .objectReferenceValue = null;
            transition.FindPropertyRelative("ConditionGroups")
                .arraySize = 0;
        }
    }
}

#endif
