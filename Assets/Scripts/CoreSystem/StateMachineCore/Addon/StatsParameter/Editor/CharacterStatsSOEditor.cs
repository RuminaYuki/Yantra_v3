using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterStatsSO))]
public class CharacterStatsSOEditor : Editor
{
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
            var prop = modifierSO.GetIterator();
            prop.NextVisible(true);
            while (prop.NextVisible(false))
            {
                // _target/_anchor are the same reference for every modifier of a given
                // character, so showing them here is just clutter — only the values matter.
                if (prop.name == "_target" || prop.name == "_anchor")
                    continue;

                EditorGUILayout.PropertyField(prop, true);
            }
            modifierSO.ApplyModifiedProperties();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
