using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

[CustomEditor(typeof(StatsProfileSO))]
public class CharacterStatsSOEditor : Editor
{
    private readonly Dictionary<Object, bool> _gizmoFoldouts = new Dictionary<Object, bool>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var modifiers = serializedObject.FindProperty("_modifiers");

        // Default reorderable list UI (Size field, +/- buttons, drag handles) — without this,
        // there's no way to add/remove/assign elements when the list is empty or being edited.
        EditorGUILayout.PropertyField(modifiers, true);
        EditorGUILayout.Space();

        for (int i = 0; i < modifiers.arraySize; i++)
        {
            Object modifier = modifiers.GetArrayElementAtIndex(i).objectReferenceValue;
            if (modifier == null) continue;

            if (modifier is HeaderModifierSO header)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField(header.Label, EditorStyles.boldLabel);
                continue;
            }

            string displayName = modifier.name.EndsWith("_Modifier")
                ? modifier.name.Substring(0, modifier.name.Length - "_Modifier".Length)
                : modifier.name;
            EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);  // ← เหมือน Header

            var modifierSO = new SerializedObject(modifier);
            var modifierType = modifier.GetType();
            var prop = modifierSO.GetIterator();
            prop.NextVisible(true);

            // Fields under a [Header("Gizmos")] attribute get pulled into a foldout below
            // instead of drawn inline — same idea as Unity's own Header grouping, just
            // collapsible so the gizmo tuning doesn't clutter the per-stat overview.
            string currentGroup = null;
            var gizmoProps = new List<SerializedProperty>();

            while (prop.NextVisible(false))
            {
                // _target/_anchor are the same reference for every modifier of a given
                // character, so showing them here is just clutter — only the values matter.
                if (prop.name == "_target" || prop.name == "_anchor")
                    continue;

                var field = modifierType.GetField(prop.name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var headerAttr = field?.GetCustomAttribute<HeaderAttribute>();
                if (headerAttr != null)
                    currentGroup = headerAttr.header;

                if (currentGroup == "Gizmos")
                {
                    gizmoProps.Add(prop.Copy());
                    continue;
                }

                EditorGUILayout.PropertyField(prop, true);
            }

            if (gizmoProps.Count > 0)
            {
                _gizmoFoldouts.TryGetValue(modifier, out bool expanded);
                expanded = EditorGUILayout.Foldout(expanded, "Gizmos", true);
                _gizmoFoldouts[modifier] = expanded;

                if (expanded)
                {
                    EditorGUI.indentLevel++;
                    foreach (var gizmoProp in gizmoProps)
                        EditorGUILayout.PropertyField(gizmoProp, true);
                    EditorGUI.indentLevel--;
                }
            }

            modifierSO.ApplyModifiedProperties();
            EditorGUILayout.Space(4);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
