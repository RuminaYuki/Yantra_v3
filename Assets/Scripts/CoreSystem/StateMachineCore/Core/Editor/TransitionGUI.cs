#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Yuki.Learning.StateMachine.Editor
{
    // IMGUI drawing for a transition's condition groups.
    // Shared by TransitionTableSOEditor (Inspector) and the graph's GraphInspectorPanel,
    // so both always look and behave the same.
    public static class TransitionGUI
    {
        // The "WHEN" block: OR groups, each holding AND conditions.
        public static void DrawConditionGroups(SerializedProperty groups)
        {
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
        }

        public static string GetStateName(SerializedProperty stateProperty)
        {
            return stateProperty != null && stateProperty.objectReferenceValue != null
                ? stateProperty.objectReferenceValue.name
                : "None";
        }

        private static void DrawConditionGroup(
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
    }
}

#endif
