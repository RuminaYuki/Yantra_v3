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
                ? GetStateName(fromState)
                : "Any State";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            transition.isExpanded = EditorGUILayout.Foldout(
                transition.isExpanded,
                $"Priority {transitionIndex + 1}: {fromName} -> {GetStateName(toState)}",
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
                EditorGUILayout.LabelField("WHEN", EditorStyles.boldLabel);

                for (int groupIndex = 0;
                    groupIndex < groups.arraySize;
                    groupIndex++)
                {
                    if (groupIndex > 0)
                    {
                        EditorGUILayout.Space(2f);
                        DrawCenteredLabel("OR", EditorStyles.boldLabel);
                        EditorGUILayout.Space(2f);
                    }

                    DrawConditionGroup(
                        groups,
                        groups.GetArrayElementAtIndex(groupIndex),
                        groupIndex);
                }

                if (groups.arraySize == 0)
                {
                    EditorGUILayout.HelpBox(
                        "Add at least one condition group. A transition without a group cannot run.",
                        MessageType.Warning);
                }

                string addGroupLabel = groups.arraySize == 0
                    ? "+ Add Condition Group"
                    : "+ Add OR Group";

                if (GUILayout.Button(addGroupLabel))
                {
                    int newGroupIndex = groups.arraySize;
                    groups.InsertArrayElementAtIndex(newGroupIndex);
                    groups.GetArrayElementAtIndex(newGroupIndex)
                        .FindPropertyRelative("Conditions").arraySize = 0;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawConditionGroup(
            SerializedProperty groups,
            SerializedProperty group,
            int groupIndex)
        {
            SerializedProperty conditions =
                group.FindPropertyRelative("Conditions");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"AND Group {groupIndex + 1}",
                EditorStyles.boldLabel);

            if (GUILayout.Button("Remove Group", GUILayout.Width(100f)))
            {
                groups.DeleteArrayElementAtIndex(groupIndex);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            for (int conditionIndex = 0;
                conditionIndex < conditions.arraySize;
                conditionIndex++)
            {
                if (conditionIndex > 0)
                {
                    DrawCenteredLabel("AND", EditorStyles.miniBoldLabel);
                }

                DrawCondition(
                    conditions,
                    conditions.GetArrayElementAtIndex(conditionIndex),
                    conditionIndex);
            }

            if (conditions.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "This AND group is empty and will never pass.",
                    MessageType.Warning);
            }

            string addConditionLabel = conditions.arraySize == 0
                ? "+ Add Condition"
                : "+ Add AND Condition";

            if (GUILayout.Button(addConditionLabel))
            {
                int newConditionIndex = conditions.arraySize;
                conditions.InsertArrayElementAtIndex(newConditionIndex);

                SerializedProperty newCondition =
                    conditions.GetArrayElementAtIndex(newConditionIndex);
                newCondition.FindPropertyRelative("Condition")
                    .objectReferenceValue = null;
                newCondition.FindPropertyRelative("ExpectedResult")
                    .enumValueIndex = 0;
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawCondition(
            SerializedProperty conditions,
            SerializedProperty conditionUsage,
            int conditionIndex)
        {
            SerializedProperty condition =
                conditionUsage.FindPropertyRelative("Condition");
            SerializedProperty expectedResult =
                conditionUsage.FindPropertyRelative("ExpectedResult");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(
                condition,
                GUIContent.none,
                GUILayout.MinWidth(120f));

            bool expectsTrue = expectedResult.enumValueIndex == 0;
            bool newExpectsTrue = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Expected",
                    "Checked: the condition must return true. Unchecked: it must return false."),
                expectsTrue,
                GUILayout.Width(90f));

            if (newExpectsTrue != expectsTrue)
            {
                expectedResult.enumValueIndex = newExpectsTrue ? 0 : 1;
            }

            if (GUILayout.Button("X", GUILayout.Width(24f)))
            {
                conditions.DeleteArrayElementAtIndex(conditionIndex);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawCenteredLabel(string text, GUIStyle source)
        {
            GUIStyle centeredStyle = new GUIStyle(source)
            {
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.LabelField(text, centeredStyle);
        }

        private static string GetStateName(SerializedProperty stateProperty)
        {
            return stateProperty != null && stateProperty.objectReferenceValue != null
                ? stateProperty.objectReferenceValue.name
                : "None";
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
