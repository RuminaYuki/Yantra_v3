#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.Editor.Graph;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor
{
    // Unity's own Inspector for a StateSO: its actions, then its outgoing transitions in every
    // table that uses it, in priority order with ▲ / ▼ (like the graph's side panel).
    [CustomEditor(typeof(StateSO))]
    public class StateSOEditor : UnityEditor.Editor
    {
        // Tables that use this state, found once when the Inspector opens.
        private readonly List<SerializedObject> _tables = new List<SerializedObject>();

        private void OnEnable()
        {
            _tables.Clear();
            var state = (StateSO)target;

            // Every table in each file: a sub-state machine's table can be embedded next to its parent.
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(TransitionTableSO)}"))
            {
                foreach (TransitionTableSO table in AssetDatabase
                    .LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid))
                    .OfType<TransitionTableSO>())
                {
                    if (Uses(table, state))
                    {
                        _tables.Add(new SerializedObject(table));
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (_tables.Count == 0)
            {
                EditorGUILayout.Space(10f);
                EditorGUILayout.HelpBox("Not used by any Transition Table.", MessageType.Info);
                return;
            }

            if (_tables.Count > 1)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(
                    $"Shared: used by {_tables.Count} tables. Editing actions here changes all of them.",
                    MessageType.Warning);
            }

            var state = (StateSO)target;
            foreach (SerializedObject table in _tables)
            {
                // The table was deleted while this Inspector was open.
                if (table.targetObject == null)
                {
                    continue;
                }

                table.Update();

                EditorGUILayout.Space(10f);
                EditorGUILayout.LabelField("Transitions from this state", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"In {table.targetObject.name}. Checked top to bottom, after Any State transitions.",
                    EditorStyles.miniLabel);

                DrawOrderedTransitions(table, state);
            }
        }

        // Rows for one state's transitions (state = null: the Any State transitions), in priority order.
        // ▲ / ▼ swap two of them inside the table's array; clicking a row selects its arrow in the graph.
        // Also used by TransitionSelectionEditor for the Any State node.
        internal static void DrawOrderedTransitions(SerializedObject table, StateSO state)
        {
            bool isAnyState = state == null;
            SerializedProperty transitions = table.FindProperty(isAnyState ? StateMachineFields.AnyTransitions : StateMachineFields.Transitions);
            object fromKey = isAnyState ? SpecialNodeView.Kind.AnyState : state;

            var indices = new List<int>();
            for (int i = 0; i < transitions.arraySize; i++)
            {
                if (isAnyState ||
                    transitions.GetArrayElementAtIndex(i).FindPropertyRelative("FromState").objectReferenceValue == state)
                {
                    indices.Add(i);
                }
            }

            if (indices.Count == 0)
            {
                EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
                return;
            }

            // Moving the array while it's being drawn would mix up the rows, so do it after the loop.
            int moveFrom = -1;
            int moveTo = -1;

            for (int row = 0; row < indices.Count; row++)
            {
                SerializedProperty transition = transitions.GetArrayElementAtIndex(indices[row]);
                var toState = transition.FindPropertyRelative("ToState").objectReferenceValue as StateSO;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(
                        $"{row + 1}.  →  {(toState != null ? toState.name : "None")}",
                        EditorStyles.helpBox) &&
                    toState != null)
                {
                    // Selecting the arrow swaps this Inspector, so wait until it has finished drawing.
                    var tableAsset = (TransitionTableSO)table.targetObject;
                    EditorApplication.delayCall += () =>
                        StateMachineGraphWindow.SelectTransition(tableAsset, fromKey, toState);
                }

                using (new EditorGUI.DisabledScope(row == 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(24f)))
                    {
                        moveFrom = row;
                        moveTo = row - 1;
                    }
                }

                using (new EditorGUI.DisabledScope(row == indices.Count - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(24f)))
                    {
                        moveFrom = row;
                        moveTo = row + 1;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            if (moveFrom >= 0)
            {
                SwapArrayElements(transitions, indices[moveFrom], indices[moveTo]);
                table.ApplyModifiedProperties();

                // Graph edges remember array indexes, so an open graph has to rebuild.
                EditorApplication.delayCall += StateMachineGraphWindow.ReloadAll;
            }
        }

        private static void SwapArrayElements(SerializedProperty array, int a, int b)
        {
            int low = Mathf.Min(a, b);
            int high = Mathf.Max(a, b);

            // [.. L .. H] -> [.. H L ..] -> [.. H .. L]
            array.MoveArrayElement(high, low);
            array.MoveArrayElement(low + 1, high);
        }

        private static bool Uses(TransitionTableSO table, StateSO state)
        {
            SerializedProperty property = new SerializedObject(table).GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == state)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

#endif
