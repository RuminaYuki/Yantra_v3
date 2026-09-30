#if UNITY_EDITOR

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor
{
    // Adds a "missing components" report under the normal StateMachineController Inspector,
    // built from [RequiresOwnerComponent] on every action / condition its tables use.
    [CustomEditor(typeof(StateMachineController))]
    public class StateMachineControllerEditor : UnityEditor.Editor
    {
        private struct Issue
        {
            public System.Type Component;
            public ScriptableObject Source;   // the action / condition asset that needs it
        }

        private List<Issue> _issues;

        private void OnEnable()
        {
            _issues = FindIssues((StateMachineController)target);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(6f);

            if (_issues.Count == 0)
            {
                EditorGUILayout.HelpBox("All required components are present.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Missing components. These actions / conditions will be disabled in Play Mode:",
                    MessageType.Warning);

                foreach (Issue issue in _issues)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(issue.Component.Name, EditorStyles.boldLabel, GUILayout.Width(160f));

                    // Click to find the asset in the Project window.
                    if (GUILayout.Button(issue.Source.name, EditorStyles.linkLabel))
                    {
                        EditorGUIUtility.PingObject(issue.Source);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            if (GUILayout.Button("Re-check"))
            {
                _issues = FindIssues((StateMachineController)target);
            }
        }

        private static List<Issue> FindIssues(StateMachineController controller)
        {
            var issues = new List<Issue>();
            var visitedTables = new HashSet<TransitionTableSO>();

            using var serializedController = new SerializedObject(controller);
            SerializedProperty tables = serializedController.FindProperty("_transitionTables");

            for (int i = 0; i < tables.arraySize; i++)
            {
                if (tables.GetArrayElementAtIndex(i).objectReferenceValue is TransitionTableSO table)
                {
                    CheckTable(table, controller.gameObject, visitedTables, issues);
                }
            }

            return issues;
        }

        // Walks a table: its states' actions, its transitions' conditions, and any sub-state tables.
        private static void CheckTable(
            TransitionTableSO table, GameObject owner,
            HashSet<TransitionTableSO> visitedTables, List<Issue> issues)
        {
            // A table can reach itself through sub-state machines.
            if (!visitedTables.Add(table))
            {
                return;
            }

            using var serializedTable = new SerializedObject(table);
            var states = new HashSet<StateSO>();
            var conditions = new HashSet<StateConditionSO>();

            AddState(serializedTable.FindProperty("_initialState"), states);

            foreach (string arrayName in new[] { "_transitions", "_anyTransitions" })
            {
                SerializedProperty transitions = serializedTable.FindProperty(arrayName);
                for (int i = 0; i < transitions.arraySize; i++)
                {
                    SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
                    AddState(transition.FindPropertyRelative("FromState"), states);
                    AddState(transition.FindPropertyRelative("ToState"), states);
                    AddConditions(transition.FindPropertyRelative("ConditionGroups"), conditions);
                }
            }

            foreach (StateSO state in states)
            {
                using var serializedState = new SerializedObject(state);
                SerializedProperty actions = serializedState.FindProperty("_actions");

                for (int i = 0; i < actions.arraySize; i++)
                {
                    var action = actions.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableObject;
                    if (action == null)
                    {
                        continue;
                    }

                    CheckAsset(action, owner, issues);

                    // Sub-state machine: its table runs on the same owner.
                    if (action is SubStateMachineActionSO)
                    {
                        using var serializedAction = new SerializedObject(action);
                        if (serializedAction.FindProperty("_transitionTable").objectReferenceValue is TransitionTableSO subTable)
                        {
                            CheckTable(subTable, owner, visitedTables, issues);
                        }
                    }
                }
            }

            foreach (StateConditionSO condition in conditions)
            {
                CheckAsset(condition, owner, issues);
            }
        }

        // `visited` guards against wrappers that (indirectly) contain themselves.
        private static void CheckAsset(
            ScriptableObject asset, GameObject owner, List<Issue> issues,
            HashSet<ScriptableObject> visited = null)
        {
            visited ??= new HashSet<ScriptableObject>();
            if (!visited.Add(asset))
            {
                return;
            }

            foreach (RequiresOwnerComponentAttribute requirement in
                     asset.GetType().GetCustomAttributes<RequiresOwnerComponentAttribute>(true))
            {
                if (IsAnchorAssigned(asset, requirement.UnlessAnchorField))
                {
                    continue;
                }

                if (owner.GetComponent(requirement.ComponentType) == null &&
                    !issues.Exists(issue => issue.Source == asset && issue.Component == requirement.ComponentType))
                {
                    issues.Add(new Issue { Component = requirement.ComponentType, Source = asset });
                }
            }

            CheckNestedAssets(asset, owner, issues, visited);
        }

        // An action / condition can hold other actions / conditions in its fields (a wrapper such as
        // a delayed condition); those run on the same owner, so check them too. Found by field type,
        // so this editor doesn't need to know any wrapper class.
        private static void CheckNestedAssets(
            ScriptableObject asset, GameObject owner, List<Issue> issues,
            HashSet<ScriptableObject> visited)
        {
            using var serializedAsset = new SerializedObject(asset);
            SerializedProperty property = serializedAsset.GetIterator();

            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (property.objectReferenceValue is StateConditionSO or StateActionSO)
                {
                    CheckAsset((ScriptableObject)property.objectReferenceValue, owner, issues, visited);
                }
            }
        }

        private static bool IsAnchorAssigned(ScriptableObject asset, string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                return false;
            }

            using var serializedAsset = new SerializedObject(asset);
            return serializedAsset.FindProperty(fieldName)?.objectReferenceValue != null;
        }

        private static void AddState(SerializedProperty property, HashSet<StateSO> states)
        {
            if (property?.objectReferenceValue is StateSO state)
            {
                states.Add(state);
            }
        }

        private static void AddConditions(SerializedProperty groups, HashSet<StateConditionSO> conditions)
        {
            for (int g = 0; g < groups.arraySize; g++)
            {
                SerializedProperty usages = groups.GetArrayElementAtIndex(g).FindPropertyRelative("Conditions");
                for (int c = 0; c < usages.arraySize; c++)
                {
                    if (usages.GetArrayElementAtIndex(c).FindPropertyRelative("Condition").objectReferenceValue
                        is StateConditionSO condition)
                    {
                        conditions.Add(condition);
                    }
                }
            }
        }
    }
}

#endif
