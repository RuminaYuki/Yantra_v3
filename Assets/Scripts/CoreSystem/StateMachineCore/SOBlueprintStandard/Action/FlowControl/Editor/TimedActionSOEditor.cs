using UnityEditor;
using UnityEngine;

// Timed Action's Inspector: Start / End change with the Unit, so it's clear what the numbers mean.
//   Seconds         → "Start (sec)", plain numbers.
//   Anim Normalized → "Start (20% of clip)", a 0..1 slider, plus the Animator state fields (hidden in Seconds).
// A one-line summary under them says when the actions run, and mistakes show as warnings.
[CustomEditor(typeof(TimedActionSO))]
[CanEditMultipleObjects]
public class TimedActionSOEditor : Editor
{
    private SerializedProperty _unit;
    private SerializedProperty _start;
    private SerializedProperty _end;
    private SerializedProperty _stateName;
    private SerializedProperty _layerIndex;

    private void OnEnable()
    {
        _unit = serializedObject.FindProperty("_unit");
        _start = serializedObject.FindProperty("_start");
        _end = serializedObject.FindProperty("_end");
        _stateName = serializedObject.FindProperty("_stateName");
        _layerIndex = serializedObject.FindProperty("_layerIndex");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Every field in its usual order (Self Anchor, Actions ...), with the timing fields drawn by hand
        // right after Unit.
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            switch (property.name)
            {
                case "m_Script":
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    break;

                case "_unit":
                    EditorGUILayout.PropertyField(property);
                    DrawTiming();
                    break;

                case "_start":
                case "_end":
                case "_stateName":
                case "_layerIndex":
                    break;   // drawn in DrawTiming

                default:
                    EditorGUILayout.PropertyField(property, true);
                    break;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTiming()
    {
        // Several assets selected with different Units: the labels can't say which, so plain fields.
        if (_unit.hasMultipleDifferentValues)
        {
            EditorGUILayout.PropertyField(_start);
            EditorGUILayout.PropertyField(_end);
            EditorGUILayout.PropertyField(_stateName);
            EditorGUILayout.PropertyField(_layerIndex);
            return;
        }

        bool isAnim = _unit.enumValueIndex == (int)TimedActionSO.TimeUnit.AnimNormalized;

        EditorGUI.indentLevel++;

        if (isAnim)
        {
            DrawPercent(_start, "Start", "How far into the Animator state the actions start. 0 = its start, 1 = its end.");
            DrawPercent(_end, "End", "How far into the Animator state they stop. 0 = keep running until the state exits.");
            EditorGUILayout.PropertyField(_stateName, new GUIContent("Animator State",
                "The state's name in the Animator Controller (the box's name, not the clip's), e.g. Attack. " +
                "Case and spaces must match."));
            EditorGUILayout.PropertyField(_layerIndex, new GUIContent("Layer Index",
                "The Animator layer that state is on. 0 = the first layer (Base Layer)."));
        }
        else
        {
            EditorGUILayout.PropertyField(_start, new GUIContent("Start (sec)",
                "Seconds after entering the state when the actions start."));
            EditorGUILayout.PropertyField(_end, new GUIContent("End (sec)",
                "Seconds after entering the state when they stop. 0 = keep running until the state exits."));
        }

        EditorGUI.indentLevel--;

        DrawSummary(isAnim);
        EditorGUILayout.Space(4f);
    }

    // A 0..1 slider whose label says the percent: "Start (20% of clip)". A value already past 1
    // (a later loop of a looping clip) keeps a plain number field, so it isn't clamped by accident.
    private static void DrawPercent(SerializedProperty property, string name, string tooltip)
    {
        string percent = property.hasMultipleDifferentValues ? "—" : $"{property.floatValue * 100f:0.#}%";
        var label = new GUIContent($"{name} ({percent} of clip)", tooltip);

        if (!property.hasMultipleDifferentValues && property.floatValue > 1f)
        {
            EditorGUILayout.PropertyField(property, label);
            return;
        }

        Rect rect = EditorGUILayout.GetControlRect();
        label = EditorGUI.BeginProperty(rect, label, property);
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        float value = EditorGUI.Slider(rect, label, property.floatValue, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
            property.floatValue = value;
        EditorGUI.showMixedValue = false;
        EditorGUI.EndProperty();
    }

    // "Runs at 20% of 'Attack', until the state exits." and warnings for settings that never run.
    private void DrawSummary(bool isAnim)
    {
        if (_start.hasMultipleDifferentValues || _end.hasMultipleDifferentValues || _stateName.hasMultipleDifferentValues)
            return;

        float start = _start.floatValue;
        float end = _end.floatValue;
        string stateName = _stateName.stringValue;

        if (isAnim && string.IsNullOrWhiteSpace(stateName))
        {
            EditorGUILayout.HelpBox("Anim Normalized needs an Animator State name, otherwise the actions never run.",
                MessageType.Error);
            return;
        }

        if (end > 0f && end <= start)
        {
            EditorGUILayout.HelpBox("End must be after Start (or 0 for no end), otherwise the actions never run.",
                MessageType.Error);
            return;
        }

        string from = isAnim
            ? (start <= 0f ? $"from the start of '{stateName}'" : $"at {start * 100f:0.#}% of '{stateName}'")
            : (start <= 0f ? "as soon as the state starts" : $"{start:0.##} s after the state starts");

        string until = end > 0f
            ? (isAnim ? $"until {end * 100f:0.#}%" : $"until {end:0.##} s")
            : "until the state exits";

        EditorGUILayout.HelpBox($"Runs {from}, {until}.", MessageType.Info);

        if (isAnim && start >= 1f)
        {
            EditorGUILayout.HelpBox("Start at 1 or more only happens if the clip loops. A clip that doesn't loop stops at 1, " +
                "and the state may leave before reaching it: 0.95 is safer for \"at the end\".", MessageType.Warning);
        }
    }
}
