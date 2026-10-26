#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.Editor.Graph;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor
{
    // Like the Properties section of VisualEffect: lists every exposed parameter of the tables on the
    // StateMachineController next to it. Ticking one adds an override (starting at the value it has now:
    // a variant table's Field Override if one sets it, else the SO's); unticking removes it again.
    // Looks like prefab overrides: an overridden row has a blue bar on the left and a bold name.
    // Each table shows how many it overrides; categories fold. Overrides whose name no table has any
    // more are listed in red at the bottom.
    [CustomEditor(typeof(StateMachineParameters))]
    public class StateMachineParametersEditor : UnityEditor.Editor
    {
        private const string OverridesProperty = "_overrides";
        private const string BaseProperty = "_base";
        private const string NameProperty = "_name";
        private const string ValueProperty = "_value";
        private const string BoolProperty = "_bool";
        private const string StringProperty = "_string";
        private const string VectorProperty = "_vector";
        private const string ColorProperty = "_color";
        private const string ObjectProperty = "_object";

        private const float TickWidth = 18f;
        private const float CategoryIndent = 12f;

        private static readonly Color OverrideBarColor = new Color(0.27f, 0.6f, 1f);
        private static readonly Color TableBandColor = new Color(0f, 0f, 0f, 0.2f);
        private static readonly Color CategoryBandColor = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color MissingColor = new Color(1f, 0.45f, 0.45f);

        private SerializedProperty _overrides;

        // The controller's tables and their Bases, each variant before its Base (refreshed every draw).
        private List<TransitionTableSO> _tableChain = new List<TransitionTableSO>();

        private static GUIStyle _nameStyle;
        private static GUIStyle _overriddenNameStyle;
        private static GUIStyle _countStyle;

        private void OnEnable()
        {
            _overrides = serializedObject.FindProperty(OverridesProperty);
        }

        // Labels that cut off with the window instead of running into the value.
        private static void CreateStyles()
        {
            if (_nameStyle != null)
            {
                return;
            }

            _nameStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };
            _overriddenNameStyle = new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip };
            _countStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
        }

        public override void OnInspectorGUI()
        {
            CreateStyles();
            serializedObject.Update();

            var controller = ((Component)target).GetComponent<StateMachineController>();
            _tableChain = TableChainOf(controller);
            List<ExposedParametersSO> blackboards = BlackboardsOf(controller);
            var known = new HashSet<string>();

            if (blackboards.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No exposed parameters. Add them in the State Machine Graph's Parameters panel.",
                    MessageType.Info);
            }

            // A Base's parameters a variant removed in its Parameters panel aren't listed (unless this
            // character overrides one, so nothing that changes it is hidden).
            HashSet<string> removed = RemovedParametersOf(controller);

            foreach (ExposedParametersSO blackboard in blackboards)
            {
                known.UnionWith(blackboard.Parameters.Select(parameter => parameter.Name));
                List<ExposedParameter> shown = blackboard.Parameters
                    .Where(parameter => !removed.Contains(parameter.Name) || IndexOf(parameter.Name) >= 0)
                    .ToList();

                int overridden = shown.Count(parameter => IndexOf(parameter.Name) >= 0);
                DrawTableHeader(TableName(blackboard), overridden, shown.Count);

                // Same categories and order as the Parameters panel in the Graph: a top-level category
                // folds with its sub-categories ("RandomPosition/Teleport") inside it, each folding too.
                List<string> categories = ParameterBlackboard.CategoriesOf(blackboard);
                foreach (string top in categories.Select(TopOf).Distinct())
                {
                    List<ExposedParameter> inTop = shown
                        .Where(parameter => TopOf(ParameterBlackboard.CategoryOf(parameter)) == top)
                        .ToList();

                    if (inTop.Count == 0)
                    {
                        continue;
                    }

                    // Uncategorized folds like the others, so every row lines up.
                    if (!DrawCategoryFoldout(blackboard, top, inTop))
                    {
                        continue;
                    }

                    foreach (ExposedParameter parameter in inTop.Where(parameter => ParameterBlackboard.CategoryOf(parameter) == top))
                    {
                        DrawParameter(parameter, CategoryIndent);
                    }

                    foreach (string sub in categories.Where(category => category != top && TopOf(category) == top))
                    {
                        List<ExposedParameter> inSub = inTop
                            .Where(parameter => ParameterBlackboard.CategoryOf(parameter) == sub)
                            .ToList();
                        int depth = sub.Count(character => character == '/');

                        if (inSub.Count == 0 ||
                            !DrawCategoryFoldout(blackboard, sub, inSub, sub.Substring(sub.LastIndexOf('/') + 1), CategoryIndent * depth))
                        {
                            continue;
                        }

                        foreach (ExposedParameter parameter in inSub)
                        {
                            DrawParameter(parameter, CategoryIndent * (depth + 1));
                        }
                    }
                }

                EditorGUILayout.Space(6f);
            }

            DrawMissing(known);

            serializedObject.ApplyModifiedProperties();
        }

        // ---------- Headers ----------

        // "New_TransitionTable                 1 / 4 overridden" on a dark band.
        private static void DrawTableHeader(string tableName, int overridden, int total)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, TableBandColor);

            var inner = new Rect(rect.x + 4f, rect.y, rect.width - 8f, rect.height);
            EditorGUI.LabelField(inner, tableName, EditorStyles.boldLabel);
            EditorGUI.LabelField(inner, $"{overridden} / {total} overridden", _countStyle);

            EditorGUILayout.Space(2f);
        }

        // A light band with a fold arrow: "Attack (2)". Open or closed is remembered for this Editor session.
        // "RandomPosition" -> "RandomPosition"; "RandomPosition/Teleport" -> "RandomPosition".
        private static string TopOf(string category)
        {
            int slash = category.IndexOf('/');
            return slash < 0 ? category : category.Substring(0, slash);
        }

        // label / indent: a sub-category shows only its own part of the name, further in.
        private bool DrawCategoryFoldout(
            ExposedParametersSO blackboard,
            string category,
            List<ExposedParameter> parameters,
            string label = null,
            float indent = 0f)
        {
            string key = $"StateMachineParameters.{AssetDatabase.GetAssetPath(blackboard)}.{category}";
            bool isOpen = SessionState.GetBool(key, true);

            Rect rect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
            rect.xMin += indent;
            EditorGUI.DrawRect(rect, CategoryBandColor);

            int overridden = parameters.Count(parameter => IndexOf(parameter.Name) >= 0);
            string name = label ?? (category == string.Empty ? "Uncategorized" : category);
            string title = overridden > 0
                ? $"{name} ({parameters.Count}, {overridden} overridden)"
                : $"{name} ({parameters.Count})";

            bool open = EditorGUI.Foldout(new Rect(rect.x + 4f, rect.y, rect.width - 4f, rect.height), isOpen, title, true);
            if (open != isOpen)
            {
                SessionState.SetBool(key, open);
            }

            return open;
        }

        // ---------- Rows ----------

        // [tick] [name] [value]. Ticked: blue bar, bold name, the override's value to edit.
        // Not ticked: the value the character runs with, greyed out (a variant's Field Override, else the SO's).
        private void DrawParameter(ExposedParameter parameter, float indent)
        {
            SerializedProperty source = SourceProperty(parameter);
            SerializedProperty variantOverride = VariantOverrideOf(parameter);
            System.Type valueType = ValueTypeOf(parameter);
            int index = IndexOf(parameter.Name);
            bool isOverridden = index >= 0;

            // Hover: every place this value is set ("Chase_Condition._distance", ...).
            var label = new GUIContent(
                parameter.Name,
                parameter.Bindings.Count > 0
                    ? string.Join("\n", parameter.Bindings.Select(binding =>
                        binding.Target != null ? $"{binding.Target.name}.{binding.FieldName}" : "(missing)"))
                    : "Not bound to anything");

            Rect row = EditorGUILayout.BeginHorizontal();

            if (isOverridden && Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(row.x, row.y, 2f, row.height), OverrideBarColor);
            }

            GUILayout.Space(4f + indent);

            float nameWidth = Mathf.Max(80f, EditorGUIUtility.labelWidth - TickWidth - indent);

            if (source == null || valueType == null)
            {
                // No source (yet): keep showing that this character overrides it, so nothing looks lost.
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Toggle(isOverridden, GUILayout.Width(TickWidth));
                }

                GUILayout.Label(label, isOverridden ? _overriddenNameStyle : _nameStyle, GUILayout.Width(nameWidth));

                Color previous = GUI.contentColor;
                GUI.contentColor = MissingColor;
                GUILayout.Label(isOverridden ? "overridden, no source" : "no source", EditorStyles.miniLabel);
                GUI.contentColor = previous;

                EditorGUILayout.EndHorizontal();
                return;
            }

            bool shouldOverride = EditorGUILayout.Toggle(isOverridden, GUILayout.Width(TickWidth));
            GUILayout.Label(label, isOverridden ? _overriddenNameStyle : _nameStyle, GUILayout.Width(nameWidth));

            if (isOverridden)
            {
                DrawOverrideValue(_overrides.GetArrayElementAtIndex(index), parameter, source, valueType);
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    // Same slots as this component's overrides, so it draws the same way (read-only here).
                    if (variantOverride != null)
                    {
                        DrawOverrideValue(variantOverride, parameter, source, valueType);
                    }
                    else
                    {
                        DrawSourceValue(source, valueType);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            if (shouldOverride && !isOverridden)
            {
                AddOverride(parameter.Name, source, variantOverride);
            }
            else if (!shouldOverride && isOverridden)
            {
                _overrides.DeleteArrayElementAtIndex(index);
            }
        }

        // The SO's own value, without its [Header] / [Tooltip] decorations (PropertyField would draw
        // those inside the row).
        private static void DrawSourceValue(SerializedProperty source, System.Type valueType)
        {
            switch (source.propertyType)
            {
                case SerializedPropertyType.Float:
                    EditorGUILayout.FloatField(source.floatValue);
                    break;
                case SerializedPropertyType.Integer:
                    EditorGUILayout.IntField(source.intValue);
                    break;
                case SerializedPropertyType.Enum:
                    EditorGUILayout.EnumPopup((System.Enum)System.Enum.ToObject(valueType, source.intValue));
                    break;
                case SerializedPropertyType.Boolean:
                    EditorGUILayout.Toggle(source.boolValue);
                    break;
                case SerializedPropertyType.String:
                    EditorGUILayout.TextField(source.stringValue);
                    break;
                case SerializedPropertyType.Vector3:
                    EditorGUILayout.Vector3Field(GUIContent.none, source.vector3Value);
                    break;
                case SerializedPropertyType.Vector2:
                    EditorGUILayout.Vector2Field(GUIContent.none, source.vector2Value);
                    break;
                case SerializedPropertyType.Color:
                    EditorGUILayout.ColorField(source.colorValue);
                    break;
                case SerializedPropertyType.ObjectReference:
                    EditorGUILayout.ObjectField(source.objectReferenceValue, valueType, false);
                    break;
                default:
                    GUILayout.Label($"{source.propertyType} isn't supported", EditorStyles.miniLabel);
                    break;
            }
        }

        // The editable value of a ticked row, in the ParameterOverride slot that matches the field's type
        // (see StateMachineParameters.ConvertValue): float / int / enum -> _value, bool -> _bool,
        // string -> _string, Vector3 / Vector2 -> _vector, Color -> _color, an asset -> _object.
        private static void DrawOverrideValue(
            SerializedProperty element,
            ExposedParameter parameter,
            SerializedProperty source,
            System.Type valueType)
        {
            switch (source.propertyType)
            {
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Integer:
                    DrawNumber(element.FindPropertyRelative(ValueProperty), parameter,
                        source.propertyType == SerializedPropertyType.Integer);
                    break;

                case SerializedPropertyType.Enum:
                {
                    SerializedProperty value = element.FindPropertyRelative(ValueProperty);
                    var current = (System.Enum)System.Enum.ToObject(valueType, Mathf.RoundToInt(value.floatValue));
                    System.Enum picked = valueType.IsDefined(typeof(System.FlagsAttribute), false)
                        ? EditorGUILayout.EnumFlagsField(current)
                        : EditorGUILayout.EnumPopup(current);
                    value.floatValue = System.Convert.ToInt32(picked);
                    break;
                }

                case SerializedPropertyType.Boolean:
                {
                    SerializedProperty value = element.FindPropertyRelative(BoolProperty);
                    value.boolValue = EditorGUILayout.Toggle(value.boolValue);
                    break;
                }

                case SerializedPropertyType.String:
                {
                    SerializedProperty value = element.FindPropertyRelative(StringProperty);
                    value.stringValue = EditorGUILayout.TextField(value.stringValue);
                    break;
                }

                case SerializedPropertyType.Vector3:
                {
                    SerializedProperty value = element.FindPropertyRelative(VectorProperty);
                    value.vector3Value = EditorGUILayout.Vector3Field(GUIContent.none, value.vector3Value);
                    break;
                }

                case SerializedPropertyType.Vector2:
                {
                    SerializedProperty value = element.FindPropertyRelative(VectorProperty);
                    value.vector3Value = EditorGUILayout.Vector2Field(GUIContent.none, value.vector3Value);
                    break;
                }

                case SerializedPropertyType.Color:
                {
                    SerializedProperty value = element.FindPropertyRelative(ColorProperty);
                    value.colorValue = EditorGUILayout.ColorField(value.colorValue);
                    break;
                }

                case SerializedPropertyType.ObjectReference:
                {
                    // Only assets of the field's own type can be dropped in.
                    SerializedProperty value = element.FindPropertyRelative(ObjectProperty);
                    value.objectReferenceValue = EditorGUILayout.ObjectField(value.objectReferenceValue, valueType, false);
                    break;
                }

                default:
                    GUILayout.Label($"{source.propertyType} isn't supported", EditorStyles.miniLabel);
                    break;
            }
        }

        // float / int: [Range] -> slider, [Min] -> can't go below (the same limits the SO's own field has).
        // An int is stored as a float and shown (and set) rounded.
        private static void DrawNumber(SerializedProperty value, ExposedParameter parameter, bool isInt)
        {
            (float min, float max) = LimitsOf(parameter);
            bool isRange = !float.IsInfinity(min) && !float.IsInfinity(max);

            float picked;
            if (isInt)
            {
                int current = Mathf.RoundToInt(value.floatValue);
                picked = isRange
                    ? EditorGUILayout.IntSlider(current, Mathf.CeilToInt(min), Mathf.FloorToInt(max))
                    : EditorGUILayout.IntField(current);
            }
            else
            {
                picked = isRange
                    ? EditorGUILayout.Slider(value.floatValue, min, max)
                    : EditorGUILayout.FloatField(value.floatValue);
            }

            value.floatValue = Mathf.Clamp(picked, min, max);
        }

        // Overrides that no table has a parameter for (renamed or deleted in the Graph).
        private void DrawMissing(HashSet<string> known)
        {
            var missing = new List<int>();
            for (int i = 0; i < _overrides.arraySize; i++)
            {
                if (!known.Contains(_overrides.GetArrayElementAtIndex(i).FindPropertyRelative(NameProperty).stringValue))
                {
                    missing.Add(i);
                }
            }

            if (missing.Count == 0)
            {
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(MissingColor.r, MissingColor.g, MissingColor.b, 0.15f));
            EditorGUI.LabelField(
                new Rect(rect.x + 4f, rect.y, rect.width - 8f, rect.height),
                new GUIContent($"Missing ({missing.Count})", "No table has a parameter with these names any more."),
                EditorStyles.boldLabel);

            // Last first, so Remove doesn't shift the ones still to draw.
            for (int m = missing.Count - 1; m >= 0; m--)
            {
                int i = missing[m];
                string name = _overrides.GetArrayElementAtIndex(i).FindPropertyRelative(NameProperty).stringValue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(4f);
                    Color previous = GUI.contentColor;
                    GUI.contentColor = MissingColor;
                    GUILayout.Label(name, _nameStyle);
                    GUI.contentColor = previous;

                    if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                    {
                        _overrides.DeleteArrayElementAtIndex(i);
                    }
                }
            }
        }

        // ---------- Data ----------

        // A new override starts at the value the character runs with now, in the slot that matches its type:
        // a variant's Field Override (same slots, copied as is), else the SO's current value.
        private void AddOverride(string name, SerializedProperty source, SerializedProperty variantOverride = null)
        {
            _overrides.arraySize++;
            SerializedProperty element = _overrides.GetArrayElementAtIndex(_overrides.arraySize - 1);
            element.FindPropertyRelative(NameProperty).stringValue = name;

            if (variantOverride != null)
            {
                element.FindPropertyRelative(ValueProperty).floatValue = variantOverride.FindPropertyRelative(ValueProperty).floatValue;
                element.FindPropertyRelative(BoolProperty).boolValue = variantOverride.FindPropertyRelative(BoolProperty).boolValue;
                element.FindPropertyRelative(StringProperty).stringValue = variantOverride.FindPropertyRelative(StringProperty).stringValue;
                element.FindPropertyRelative(VectorProperty).vector3Value = variantOverride.FindPropertyRelative(VectorProperty).vector3Value;
                element.FindPropertyRelative(ColorProperty).colorValue = variantOverride.FindPropertyRelative(ColorProperty).colorValue;
                element.FindPropertyRelative(ObjectProperty).objectReferenceValue = variantOverride.FindPropertyRelative(ObjectProperty).objectReferenceValue;
                return;
            }

            switch (source.propertyType)
            {
                case SerializedPropertyType.Float:
                    element.FindPropertyRelative(ValueProperty).floatValue = source.floatValue;
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    element.FindPropertyRelative(ValueProperty).floatValue = source.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    element.FindPropertyRelative(BoolProperty).boolValue = source.boolValue;
                    break;
                case SerializedPropertyType.String:
                    element.FindPropertyRelative(StringProperty).stringValue = source.stringValue;
                    break;
                case SerializedPropertyType.Vector3:
                    element.FindPropertyRelative(VectorProperty).vector3Value = source.vector3Value;
                    break;
                case SerializedPropertyType.Vector2:
                    element.FindPropertyRelative(VectorProperty).vector3Value = source.vector2Value;
                    break;
                case SerializedPropertyType.Color:
                    element.FindPropertyRelative(ColorProperty).colorValue = source.colorValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    element.FindPropertyRelative(ObjectProperty).objectReferenceValue = source.objectReferenceValue;
                    break;
            }
        }

        private int IndexOf(string name)
        {
            for (int i = 0; i < _overrides.arraySize; i++)
            {
                if (_overrides.GetArrayElementAtIndex(i).FindPropertyRelative(NameProperty).stringValue == name)
                {
                    return i;
                }
            }

            return -1;
        }

        // The [Min] / [Range] limits of the fields this parameter sets. With several bindings, the
        // narrowest range that fits all of them. No limits = (-∞, +∞).
        private static (float Min, float Max) LimitsOf(ExposedParameter parameter)
        {
            float min = float.NegativeInfinity;
            float max = float.PositiveInfinity;

            foreach (ExposedBinding binding in parameter.Bindings)
            {
                FieldInfo field = binding.Target != null ? LeafField(binding.Target.GetType(), binding.FieldName) : null;
                if (field == null)
                {
                    continue;
                }

                if (field.GetCustomAttribute<RangeAttribute>() is RangeAttribute range)
                {
                    min = Mathf.Max(min, range.min);
                    max = Mathf.Min(max, range.max);
                }

                if (field.GetCustomAttribute<MinAttribute>() is MinAttribute minimum)
                {
                    min = Mathf.Max(min, minimum.min);
                }
            }

            // Bindings that can't agree: the lower bound wins.
            return (min, Mathf.Max(min, max));
        }

        // The field at the end of a path like "_attackParameters.damageAmount" (base classes included).
        private static FieldInfo LeafField(System.Type type, string path)
        {
            FieldInfo field = null;
            foreach (string part in (path ?? string.Empty).Split('.'))
            {
                field = null;
                for (System.Type current = type; current != null && field == null; current = current.BaseType)
                {
                    field = current.GetField(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                if (field == null)
                {
                    return null;
                }

                type = field.FieldType;
            }

            return field;
        }

        // The C# type of the field the parameter sets (from its first working binding), or null.
        private static System.Type ValueTypeOf(ExposedParameter parameter)
        {
            foreach (ExposedBinding binding in parameter.Bindings)
            {
                FieldInfo field = binding.Target != null ? LeafField(binding.Target.GetType(), binding.FieldName) : null;
                if (field != null)
                {
                    return field.FieldType;
                }
            }

            return null;
        }

        // The value of the parameter's first binding that still works (its default when not overridden),
        // or null if none does.
        private static SerializedProperty SourceProperty(ExposedParameter parameter)
        {
            foreach (ExposedBinding binding in parameter.Bindings)
            {
                SerializedProperty property = binding.Target != null
                    ? new SerializedObject(binding.Target).FindProperty(binding.FieldName)
                    : null;

                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        // The Field Override a variant table sets on the same binding SourceProperty reads, or null.
        // The most derived variant wins, like at runtime.
        private SerializedProperty VariantOverrideOf(ExposedParameter parameter)
        {
            foreach (ExposedBinding binding in parameter.Bindings)
            {
                if (binding.Target == null || new SerializedObject(binding.Target).FindProperty(binding.FieldName) == null)
                {
                    continue;
                }

                foreach (TransitionTableSO table in _tableChain)
                {
                    SerializedProperty list = new SerializedObject(table).FindProperty(StateMachineFields.FieldOverrides);
                    for (int i = list.arraySize - 1; i >= 0; i--)
                    {
                        SerializedProperty element = list.GetArrayElementAtIndex(i);
                        if (element.FindPropertyRelative("_target").objectReferenceValue == binding.Target &&
                            element.FindPropertyRelative("_fieldPath").stringValue == binding.FieldName)
                        {
                            return element;
                        }
                    }
                }

                return null;
            }

            return null;
        }

        // Each of the controller's tables, then its Base, its Base's Base ...
        private static List<TransitionTableSO> TableChainOf(StateMachineController controller)
        {
            var result = new List<TransitionTableSO>();
            if (controller == null)
            {
                return result;
            }

            SerializedProperty tables = new SerializedObject(controller).FindProperty("_transitionTables");
            for (int i = 0; i < tables.arraySize; i++)
            {
                for (var table = tables.GetArrayElementAtIndex(i).objectReferenceValue as TransitionTableSO;
                     table != null && !result.Contains(table);
                     table = new SerializedObject(table).FindProperty(BaseProperty).objectReferenceValue as TransitionTableSO)
                {
                    result.Add(table);
                }
            }

            return result;
        }

        // One Blackboard per table file. Tables are read like the Inspector does (the field is private).
        // A variant table also shows its bases' Blackboards, base first (same order as RegisterParameters).
        private static List<ExposedParametersSO> BlackboardsOf(StateMachineController controller)
        {
            var result = new List<ExposedParametersSO>();
            if (controller == null)
            {
                return result;
            }

            SerializedProperty tables = new SerializedObject(controller).FindProperty("_transitionTables");
            for (int i = 0; i < tables.arraySize; i++)
            {
                if (tables.GetArrayElementAtIndex(i).objectReferenceValue is TransitionTableSO table)
                {
                    AddBlackboards(table, result, new HashSet<TransitionTableSO>());
                }
            }

            return result;
        }

        // visited stops a loop (A's base is B, B's base is A).
        private static void AddBlackboards(
            TransitionTableSO table,
            List<ExposedParametersSO> result,
            HashSet<TransitionTableSO> visited)
        {
            if (table == null || !visited.Add(table))
            {
                return;
            }

            var baseTable = new SerializedObject(table).FindProperty(BaseProperty).objectReferenceValue as TransitionTableSO;
            AddBlackboards(baseTable, result, visited);

            ExposedParametersSO blackboard = ParameterBlackboard.Find(table);
            if (blackboard != null && !result.Contains(blackboard))
            {
                result.Add(blackboard);
            }
        }

        // Names in _removedParameters of the controller's tables and their Bases.
        private static HashSet<string> RemovedParametersOf(StateMachineController controller)
        {
            var names = new HashSet<string>();
            if (controller == null)
            {
                return names;
            }

            SerializedProperty tables = new SerializedObject(controller).FindProperty("_transitionTables");
            for (int i = 0; i < tables.arraySize; i++)
            {
                var visited = new HashSet<TransitionTableSO>();
                for (var table = tables.GetArrayElementAtIndex(i).objectReferenceValue as TransitionTableSO;
                     table != null && visited.Add(table);
                     table = new SerializedObject(table).FindProperty(BaseProperty).objectReferenceValue as TransitionTableSO)
                {
                    SerializedProperty list = new SerializedObject(table).FindProperty(StateMachineFields.RemovedParameters);
                    for (int j = 0; j < list.arraySize; j++)
                    {
                        names.Add(list.GetArrayElementAtIndex(j).stringValue);
                    }
                }
            }

            return names;
        }

        private static string TableName(ExposedParametersSO blackboard)
        {
            var table = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(blackboard));
            return table != null ? table.name : blackboard.name;
        }
    }
}

#endif
