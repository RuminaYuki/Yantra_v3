#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yuki.Learning.StateMachine.Editor
{
    // IMGUI drawing for a transition's condition groups.
    // Shared by TransitionTableSOEditor and TransitionSelectionEditor (a selected graph arrow),
    // so both always look and behave the same.
    public static class TransitionGUI
    {
        // The "WHEN" block: OR groups, each holding AND conditions.
        // `runtime` (Play Mode only) is the running transition, to show each condition's live result.
        public static void DrawConditionGroups(SerializedProperty groups, StateTransition runtime = null)
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
                    groupIndex,
                    GetRuntimeGroup(runtime, groupIndex));
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
            int groupIndex,
            IReadOnlyList<StateCondition> runtimeGroup)
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
                    conditionIndex,
                    runtimeGroup != null && conditionIndex < runtimeGroup.Count
                        ? runtimeGroup[conditionIndex]
                        : (StateCondition?)null);
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
            int conditionIndex,
            StateCondition? runtime)
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

            if (runtime.HasValue)
            {
                DrawLiveResult(runtime.Value, condition.objectReferenceValue);
            }

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

        private static IReadOnlyList<StateCondition> GetRuntimeGroup(StateTransition runtime, int groupIndex)
        {
            IReadOnlyList<IReadOnlyList<StateCondition>> runtimeGroups = runtime?.ConditionGroups;

            return runtimeGroups != null && groupIndex < runtimeGroups.Count
                ? runtimeGroups[groupIndex]
                : null;
        }

        private static GUIStyle _liveResultStyle;

        // Green = matches Expected, red = doesn't, grey = not checked this frame
        // (an earlier condition in the AND group, or an earlier transition, already decided).
        private static void DrawLiveResult(StateCondition runtime, Object conditionAsset)
        {
            _liveResultStyle ??= new GUIStyle(EditorStyles.miniLabel) { richText = true };

            Condition condition = runtime.Condition;

            // The table was edited during Play Mode, so this row no longer matches what runs.
            if (condition == null || conditionAsset == null || condition.DebugName != conditionAsset.name)
            {
                EditorGUILayout.LabelField("<color=#9E9E9E>● ?</color>", _liveResultStyle, GUILayout.Width(48f));
                return;
            }

            // Only read the recorded result. Calling GetStatement() here would run
            // Statement() outside the normal update and corrupt this frame's cache.
            // 1 frame of slack: the Inspector may repaint on the frame after Update.
            bool isFresh = !condition.IsDisabled &&
                           condition.LastEvaluatedFrame >= 0 &&
                           Time.frameCount - condition.LastEvaluatedFrame <= 1;

            string text;

            if (condition.IsDisabled)
            {
                text = "<color=#9E9E9E>● Off</color>";
            }
            else if (!isFresh)
            {
                text = "<color=#9E9E9E>● —</color>";
            }
            else
            {
                string color = condition.LastResult == runtime.ExpectedResult ? "#4CAF50" : "#E57373";
                text = $"<color={color}>● {condition.LastResult}</color>";
            }

            EditorGUILayout.LabelField(text, _liveResultStyle, GUILayout.Width(48f));
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
