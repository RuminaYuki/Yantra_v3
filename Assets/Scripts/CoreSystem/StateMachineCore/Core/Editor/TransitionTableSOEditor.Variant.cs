#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.Editor.Graph;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor
{
    // Variant part of the table Inspector: the Base table, the inherited initial state, and the
    // field overrides (pick an action / condition the table runs, pick one of its [Exposed] fields, set it).
    public partial class TransitionTableSOEditor
    {
        private const string BaseProperty = "_base";
        private const string TargetProperty = "_target";
        private const string FieldPathProperty = "_fieldPath";
        private const string ValueProperty = "_value";
        private const string BoolProperty = "_bool";
        private const string StringProperty = "_string";
        private const string VectorProperty = "_vector";
        private const string ColorProperty = "_color";
        private const string ObjectProperty = "_object";

        private static readonly Color MissingColor = new Color(1f, 0.45f, 0.45f);

        private string _baseError;

        // Set when something the graph draws from the Base changed (Base, removed transitions).
        private bool _graphNeedsReload;

        // ---------- Base ----------

        // A Base that would loop back to this table (A -> B -> A) is refused: the runtime would never stop.
        private void DrawBase()
        {
            var table = (TransitionTableSO)target;
            UnityEngine.Object previous = _base.objectReferenceValue;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _base,
                new GUIContent("Base", "Uses everything the Base has, plus this table's own transitions and overrides."));

            if (EditorGUI.EndChangeCheck())
            {
                var picked = _base.objectReferenceValue as TransitionTableSO;
                _baseError = null;
                _graphNeedsReload = true;

                if (CreatesLoop(table, picked))
                {
                    _baseError = $"{picked.name} can't be the Base: it already uses {table.name} (directly or through its own Base).";
                    _base.objectReferenceValue = previous;
                }
            }

            if (_baseError != null)
            {
                EditorGUILayout.HelpBox(_baseError, MessageType.Error);
            }

            if (GUILayout.Button(new GUIContent("Create Variant", "A new table next to this one, with this one as its Base")))
            {
                TransitionTableVariantMenu.CreateVariant(table);
            }
        }

        // Empty initial state on a variant: say which one it gets from the Base.
        private void DrawInheritedInitialState()
        {
            if (_initialState.objectReferenceValue != null ||
                !(_base.objectReferenceValue is TransitionTableSO baseTable) ||
                HasLoop((TransitionTableSO)target))
            {
                return;
            }

            StateSO inherited = baseTable.EffectiveInitialState;
            EditorGUILayout.HelpBox(
                inherited != null
                    ? $"Empty: uses the Base's initial state, {inherited.name}."
                    : "Empty, and no Base has an initial state either.",
                inherited != null ? MessageType.Info : MessageType.Warning);
        }

        // Following the candidate's Base, then that one's Base ... comes back to this table = a loop.
        private static bool CreatesLoop(TransitionTableSO table, TransitionTableSO candidate)
        {
            var visited = new HashSet<TransitionTableSO>();
            for (TransitionTableSO current = candidate; current != null; current = BaseOf(current))
            {
                if (current == table || !visited.Add(current))
                {
                    return true;
                }
            }

            return false;
        }

        // A loop saved before this check existed (set in Debug mode, say).
        private static bool HasLoop(TransitionTableSO table)
        {
            return CreatesLoop(table, BaseOf(table));
        }

        private static TransitionTableSO BaseOf(TransitionTableSO table)
        {
            return new SerializedObject(table).FindProperty(BaseProperty).objectReferenceValue as TransitionTableSO;
        }

        // ---------- Inherited transitions ----------

        // The Base's transitions this table gets, greyed out, above its own (they're checked first).
        // Remove / Restore puts the ID into / takes it out of _removedIds.
        private void DrawInheritedTransitions(bool local)
        {
            if (!(_base.objectReferenceValue is TransitionTableSO baseTable) || HasLoop((TransitionTableSO)target))
            {
                return;
            }

            List<(string Id, string Label)> rows = local
                ? baseTable.EffectiveTransitions
                    .Select(item => (item.Id, $"{StateName(item.FromState)} -> {StateName(item.ToState)}{ConditionSummary(item.ConditionGroups)}"))
                    .ToList()
                : baseTable.EffectiveAnyTransitions
                    .Select(item => (item.Id, $"Any State -> {StateName(item.ToState)}{ConditionSummary(item.ConditionGroups)}"))
                    .ToList();

            if (rows.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField($"From {baseTable.name} ({rows.Count})", EditorStyles.miniBoldLabel);

            foreach ((string id, string label) in rows)
            {
                int removedIndex = IndexOfRemoved(id);
                bool removed = removedIndex >= 0;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                Color previous = GUI.contentColor;
                if (removed)
                {
                    GUI.contentColor = MissingColor;
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.LabelField(new GUIContent(removed ? $"(removed) {label}" : label, label));
                }

                GUI.contentColor = previous;

                // A Base that was never opened since IDs were added has none yet.
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(id)))
                {
                    var button = new GUIContent(
                        removed ? "Restore" : "Remove",
                        string.IsNullOrEmpty(id) ? "No ID yet: select the Base table once so it gets one." : null);

                    if (GUILayout.Button(button, GUILayout.Width(64f)))
                    {
                        _graphNeedsReload = true;

                        if (removed)
                        {
                            _removedIds.DeleteArrayElementAtIndex(removedIndex);
                        }
                        else
                        {
                            _removedIds.arraySize++;
                            _removedIds.GetArrayElementAtIndex(_removedIds.arraySize - 1).stringValue = id;
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4f);
        }

        // Removed IDs whose transition is gone from the Base (deleted there, or the Base changed).
        private void DrawStaleRemovedIds()
        {
            if (_removedIds.arraySize == 0 || HasLoop((TransitionTableSO)target))
            {
                return;
            }

            var baseIds = new HashSet<string>();
            if (_base.objectReferenceValue is TransitionTableSO baseTable)
            {
                baseIds.UnionWith(baseTable.EffectiveTransitions.Select(item => item.Id));
                baseIds.UnionWith(baseTable.EffectiveAnyTransitions.Select(item => item.Id));
            }

            var stale = new List<int>();
            for (int i = 0; i < _removedIds.arraySize; i++)
            {
                if (!baseIds.Contains(_removedIds.GetArrayElementAtIndex(i).stringValue))
                {
                    stale.Add(i);
                }
            }

            if (stale.Count == 0)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox($"{stale.Count} removed transition(s) no longer exist in the Base.", MessageType.Warning);
            if (GUILayout.Button("Clean Up", GUILayout.Width(72f), GUILayout.Height(38f)))
            {
                _graphNeedsReload = true;

                // Last first, so the earlier indexes stay valid.
                for (int i = stale.Count - 1; i >= 0; i--)
                {
                    _removedIds.DeleteArrayElementAtIndex(stale[i]);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private int IndexOfRemoved(string id)
        {
            for (int i = 0; i < _removedIds.arraySize; i++)
            {
                if (_removedIds.GetArrayElementAtIndex(i).stringValue == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string StateName(StateSO state)
        {
            return state != null ? state.name : "(missing)";
        }

        // "  when A and not B or C" (groups are OR, conditions in a group are AND).
        private static string ConditionSummary(TransitionTableSO.ConditionGroupUsage[] groups)
        {
            List<string> parts = (groups ?? Array.Empty<TransitionTableSO.ConditionGroupUsage>())
                .Select(group => string.Join(" and ",
                    (group.Conditions ?? Array.Empty<TransitionTableSO.ConditionUsage>()).Select(usage =>
                        (usage.ExpectedResult == TransitionTableSO.Result.False ? "not " : string.Empty) +
                        (usage.Condition != null ? usage.Condition.name : "(missing)"))))
                .Where(part => part.Length > 0)
                .ToList();

            return parts.Count == 0 ? string.Empty : "  when " + string.Join(" or ", parts);
        }

        // ---------- Field overrides ----------

        private void DrawFieldOverrides()
        {
            var table = (TransitionTableSO)target;

            // Only a variant overrides: a table without a Base just edits its actions' values directly.
            // (Shown anyway if it still has some, e.g. its Base was cleared, so they can be removed.)
            if (_base.objectReferenceValue == null && _fieldOverrides.arraySize == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Field Overrides", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sets an [Exposed] field of an action / condition for this table only; the asset itself doesn't change. " +
                "A character's own override (State Machine Parameters) still wins.",
                MessageType.Info);

            if (HasLoop(table))
            {
                EditorGUILayout.HelpBox("The Base chain loops back to this table. Pick another Base.", MessageType.Error);
                return;
            }

            List<(string MenuPath, StateElementSO Element)> candidates = ElementsUsedBy(table);

            for (int i = 0; i < _fieldOverrides.arraySize; i++)
            {
                SerializedProperty element = _fieldOverrides.GetArrayElementAtIndex(i);
                bool keep = IsSubVariantOverride(element)
                    ? DrawSubVariantOverride(element)
                    : DrawFieldOverride(element, candidates);

                if (!keep)
                {
                    _fieldOverrides.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.Space(4f);
            }

            if (GUILayout.Button("+ Add Field Override", GUILayout.Height(26f)))
            {
                _fieldOverrides.arraySize++;
                SerializedProperty added = _fieldOverrides.GetArrayElementAtIndex(_fieldOverrides.arraySize - 1);
                added.FindPropertyRelative(TargetProperty).objectReferenceValue = null;
                added.FindPropertyRelative(FieldPathProperty).stringValue = string.Empty;
            }
        }

        // Target / Field dropdowns, then one value box of the field's type. false = Remove was clicked.
        private bool DrawFieldOverride(
            SerializedProperty element,
            List<(string MenuPath, StateElementSO Element)> candidates)
        {
            var overrideTarget = element.FindPropertyRelative(TargetProperty).objectReferenceValue as StateElementSO;
            string path = element.FindPropertyRelative(FieldPathProperty).stringValue;
            Type valueType = overrideTarget != null && !string.IsNullOrEmpty(path)
                ? ParameterBlackboard.FindExposedPath(overrideTarget.GetType(), path)
                : null;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Target
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Target");
            var targetLabel = new GUIContent(overrideTarget != null ? overrideTarget.name : "Pick an action / condition");
            if (EditorGUILayout.DropdownButton(targetLabel, FocusType.Keyboard))
            {
                ShowTargetMenu(GUILayoutUtility.GetLastRect(), element.propertyPath, overrideTarget, candidates);
            }

            bool remove = GUILayout.Button("Remove", GUILayout.Width(64f));
            EditorGUILayout.EndHorizontal();

            if (overrideTarget != null && candidates.All(candidate => candidate.Element != overrideTarget))
            {
                DrawProblem("This table doesn't run it (any more). The override does nothing.");
            }

            // Field
            using (new EditorGUI.DisabledScope(overrideTarget == null))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Field");
                var fieldLabel = new GUIContent(
                    string.IsNullOrEmpty(path) ? "Pick a field"
                    : valueType != null ? $"{path}  ({TypeName(valueType)})"
                    : $"{path}  (missing)");

                if (EditorGUILayout.DropdownButton(fieldLabel, FocusType.Keyboard))
                {
                    ShowFieldMenu(GUILayoutUtility.GetLastRect(), element.propertyPath, overrideTarget, path);
                }

                EditorGUILayout.EndHorizontal();
            }

            // Value
            if (valueType != null)
            {
                DrawValue(element, valueType);
            }
            else if (overrideTarget != null && !string.IsNullOrEmpty(path))
            {
                DrawProblem("Field not found: renamed, deleted, or no longer [Exposed].");
            }

            EditorGUILayout.EndVertical();
            return !remove;
        }

        // Made by Make Sub Variant in the graph: a Base's Sub State Machine action runs another table here.
        // _transitionTable isn't [Exposed] (it's not a value to tune), so it isn't edited like the others.
        private static bool IsSubVariantOverride(SerializedProperty element)
        {
            return element.FindPropertyRelative(TargetProperty).objectReferenceValue is SubStateMachineActionSO &&
                   element.FindPropertyRelative(FieldPathProperty).stringValue == StateMachineFields.TransitionTable;
        }

        //   Sub-State Machine                                   [Remove]
        //   AttackSubStateMachine_Action runs AttackVariant_TransitionTable here
        //   (Base: Attack_TransitionTable)
        // false = Remove was clicked.
        private static bool DrawSubVariantOverride(SerializedProperty element)
        {
            var action = (SubStateMachineActionSO)element.FindPropertyRelative(TargetProperty).objectReferenceValue;
            var subTable = element.FindPropertyRelative(ObjectProperty).objectReferenceValue as TransitionTableSO;
            var baseSubTable = new SerializedObject(action)
                .FindProperty(StateMachineFields.TransitionTable).objectReferenceValue as TransitionTableSO;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sub-State Machine", EditorStyles.boldLabel);
            bool remove = GUILayout.Button(
                new GUIContent("Remove", "Run the Base's sub-state machine here again. The Variant table stays in the file, unused."),
                GUILayout.Width(64f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"{action.name} runs {(subTable != null ? subTable.name : "(missing)")} here",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                $"Base: {(baseSubTable != null ? baseSubTable.name : "(none)")}",
                EditorStyles.miniLabel);

            if (subTable == null)
            {
                DrawProblem("The table is missing. Remove this and Make Sub Variant again.");
            }

            EditorGUILayout.EndVertical();
            return !remove;
        }

        private static void DrawProblem(string message)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = MissingColor;
            EditorGUILayout.LabelField(message, EditorStyles.miniLabel);
            GUI.contentColor = previous;
        }

        // "Idle_State/IdleDebugLog_Action", "Conditions/Alpha1Down_Condition".
        private void ShowTargetMenu(
            Rect rect,
            string elementPath,
            StateElementSO current,
            List<(string MenuPath, StateElementSO Element)> candidates)
        {
            var menu = new GenericMenu();

            if (candidates.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No action / condition here has an [Exposed] field"));
            }

            foreach ((string menuPath, StateElementSO candidate) in candidates)
            {
                menu.AddItem(new GUIContent(menuPath), candidate == current, () => EditElement(elementPath, element =>
                {
                    element.FindPropertyRelative(TargetProperty).objectReferenceValue = candidate;

                    // Keep the field when the new target has it too, start from the new target's value.
                    SerializedProperty fieldPath = element.FindPropertyRelative(FieldPathProperty);
                    if (ParameterBlackboard.FindExposedPath(candidate.GetType(), fieldPath.stringValue) == null)
                    {
                        fieldPath.stringValue = string.Empty;
                    }
                    else
                    {
                        CopySourceValue(element, candidate, fieldPath.stringValue);
                    }
                }));
            }

            menu.DropDown(rect);
        }

        private void ShowFieldMenu(Rect rect, string elementPath, StateElementSO overrideTarget, string current)
        {
            var menu = new GenericMenu();

            foreach ((string path, Type type) in ParameterBlackboard.ExposedPathsOf(overrideTarget.GetType()))
            {
                menu.AddItem(new GUIContent($"{path}  ({TypeName(type)})"), path == current, () => EditElement(elementPath, element =>
                {
                    element.FindPropertyRelative(FieldPathProperty).stringValue = path;
                    CopySourceValue(element, overrideTarget, path);
                }));
            }

            menu.DropDown(rect);
        }

        // Menu clicks arrive after OnInspectorGUI, so they edit through their own Update / Apply.
        private void EditElement(string elementPath, Action<SerializedProperty> edit)
        {
            serializedObject.Update();
            edit(serializedObject.FindProperty(elementPath));
            serializedObject.ApplyModifiedProperties();
        }

        // The value box, in the OverrideValue slot FieldPathUtility reads for this type:
        // float / int / enum -> _value, bool -> _bool, string -> _string, Vector3 / Vector2 -> _vector,
        // Color -> _color, an asset -> _object.
        // Also used by the Parameters panel for a Base's parameter overridden in a variant (label: none).
        internal static void DrawValue(SerializedProperty element, Type type, GUIContent label = null)
        {
            label = label ?? new GUIContent("Value");

            if (type == typeof(float))
            {
                SerializedProperty value = element.FindPropertyRelative(ValueProperty);
                value.floatValue = EditorGUILayout.FloatField(label, value.floatValue);
            }
            else if (type == typeof(int))
            {
                SerializedProperty value = element.FindPropertyRelative(ValueProperty);
                value.floatValue = EditorGUILayout.IntField(label, Mathf.RoundToInt(value.floatValue));
            }
            else if (type.IsEnum)
            {
                SerializedProperty value = element.FindPropertyRelative(ValueProperty);
                var current = (Enum)Enum.ToObject(type, Mathf.RoundToInt(value.floatValue));
                Enum picked = type.IsDefined(typeof(FlagsAttribute), false)
                    ? EditorGUILayout.EnumFlagsField(label, current)
                    : EditorGUILayout.EnumPopup(label, current);
                value.floatValue = Convert.ToInt32(picked);
            }
            else if (type == typeof(bool))
            {
                SerializedProperty value = element.FindPropertyRelative(BoolProperty);
                value.boolValue = EditorGUILayout.Toggle(label, value.boolValue);
            }
            else if (type == typeof(string))
            {
                SerializedProperty value = element.FindPropertyRelative(StringProperty);
                value.stringValue = EditorGUILayout.TextField(label, value.stringValue);
            }
            else if (type == typeof(Vector3))
            {
                SerializedProperty value = element.FindPropertyRelative(VectorProperty);
                value.vector3Value = EditorGUILayout.Vector3Field(label, value.vector3Value);
            }
            else if (type == typeof(Vector2))
            {
                SerializedProperty value = element.FindPropertyRelative(VectorProperty);
                value.vector3Value = EditorGUILayout.Vector2Field(label, value.vector3Value);
            }
            else if (type == typeof(Color))
            {
                SerializedProperty value = element.FindPropertyRelative(ColorProperty);
                value.colorValue = EditorGUILayout.ColorField(label, value.colorValue);
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                // Only assets of the field's own type can be dropped in.
                SerializedProperty value = element.FindPropertyRelative(ObjectProperty);
                value.objectReferenceValue = EditorGUILayout.ObjectField(label, value.objectReferenceValue, type, false);
            }
        }

        // A picked field starts at the asset's current value, so nothing changes until it's edited.
        internal static void CopySourceValue(SerializedProperty element, StateElementSO source, string path)
        {
            SerializedProperty from = new SerializedObject(source).FindProperty(path);
            if (from == null)
            {
                return;
            }

            switch (from.propertyType)
            {
                case SerializedPropertyType.Float:
                    element.FindPropertyRelative(ValueProperty).floatValue = from.floatValue;
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    element.FindPropertyRelative(ValueProperty).floatValue = from.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    element.FindPropertyRelative(BoolProperty).boolValue = from.boolValue;
                    break;
                case SerializedPropertyType.String:
                    element.FindPropertyRelative(StringProperty).stringValue = from.stringValue;
                    break;
                case SerializedPropertyType.Vector3:
                    element.FindPropertyRelative(VectorProperty).vector3Value = from.vector3Value;
                    break;
                case SerializedPropertyType.Vector2:
                    element.FindPropertyRelative(VectorProperty).vector3Value = from.vector2Value;
                    break;
                case SerializedPropertyType.Color:
                    element.FindPropertyRelative(ColorProperty).colorValue = from.colorValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    element.FindPropertyRelative(ObjectProperty).objectReferenceValue = from.objectReferenceValue;
                    break;
            }
        }

        // Every action and condition the table runs (its Bases' included) that has an [Exposed] field.
        // Actions are listed under their state, conditions under "Conditions".
        private static List<(string MenuPath, StateElementSO Element)> ElementsUsedBy(TransitionTableSO table)
        {
            var states = new List<StateSO>();
            var conditions = new List<StateConditionSO>();

            void AddState(StateSO state)
            {
                if (state != null && !states.Contains(state))
                {
                    states.Add(state);
                }
            }

            void AddConditions(TransitionTableSO.ConditionGroupUsage[] groups)
            {
                foreach (TransitionTableSO.ConditionGroupUsage group in groups ?? Array.Empty<TransitionTableSO.ConditionGroupUsage>())
                foreach (TransitionTableSO.ConditionUsage usage in group.Conditions ?? Array.Empty<TransitionTableSO.ConditionUsage>())
                {
                    if (usage.Condition != null && !conditions.Contains(usage.Condition))
                    {
                        conditions.Add(usage.Condition);
                    }
                }
            }

            AddState(table.EffectiveInitialState);

            foreach (TransitionTableSO.TransitionItem item in table.EffectiveTransitions)
            {
                AddState(item.FromState);
                AddState(item.ToState);
                AddConditions(item.ConditionGroups);
            }

            foreach (TransitionTableSO.AnyTransitionItem item in table.EffectiveAnyTransitions)
            {
                AddState(item.ToState);
                AddConditions(item.ConditionGroups);
            }

            var result = new List<(string MenuPath, StateElementSO Element)>();
            var seen = new HashSet<StateElementSO>();

            // What each state runs in this table: its actions after the table's action changes
            // (replaced / added ones can be overridden too).
            foreach (StateSO state in states)
            {
                IEnumerable<StateActionSO> stateActions = TableContents.ActionsOf(state).OfType<StateActionSO>();
                List<StateActionSO> actionsRun = ElementResolver.BuildActionList(
                    state, stateActions, table.EffectiveActionChanges, table.EffectiveActionOrders);

                foreach (StateActionSO action in actionsRun)
                {
                    if (HasExposedField(action) && seen.Add(action))
                    {
                        result.Add(($"{state.name}/{action.name}", action));
                    }
                }
            }

            foreach (StateConditionSO condition in conditions)
            {
                if (HasExposedField(condition) && seen.Add(condition))
                {
                    result.Add(($"Conditions/{condition.name}", condition));
                }
            }

            return result;
        }

        private static bool HasExposedField(StateElementSO element)
        {
            return ParameterBlackboard.ExposedPathsOf(element.GetType()).Any();
        }

        private static string TypeName(Type type)
        {
            if (type == typeof(float)) return "float";
            if (type == typeof(int)) return "int";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            return type.Name;
        }
    }
}

#endif
