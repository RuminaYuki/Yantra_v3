using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(AnimAddition))]
public class AnimAddition_Editor : Editor
{
    static readonly Dictionary<string, string> showIf = new Dictionary<string, string>
    {
        { "animationDataOnInteract",       "IsPlayOnInteract" },
        { "animationDataOnCancelInteract", "IsPlayOnCancelInteract" }
    };

    // สร้าง style ครั้งเดียว ไม่สร้างใหม่ทุกรอบ
    static GUIStyle headerStyle;
    static GUIStyle HeaderStyle => headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
    {
        fontSize = 14,
        normal = { textColor = new Color(1f, 0.7f, 0.2f) }
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            // struct ที่มีคู่
            if (showIf.TryGetValue(prop.name, out string boolName))
            {
                if (!serializedObject.FindProperty(boolName).boolValue)
                    continue;

                // กล่อง + เยื้อง ให้ดูเป็นลูกของ bool ข้างบน
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(prop, true);
                }
                continue;
            }

            // bool ที่เป็นหัวข้อ
            if (showIf.ContainsValue(prop.name))
            {
                EditorGUILayout.Space(8);
                DrawHeaderToggle(prop);
                continue;
            }

            using (new EditorGUI.DisabledScope(prop.name == "m_Script"))
            {
                EditorGUILayout.PropertyField(prop, true);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    // Toggle ตัวหนา checkbox อยู่ซ้าย ยังกดได้และรองรับ Undo/Prefab override
    void DrawHeaderToggle(SerializedProperty prop)
    {
        Rect r = EditorGUILayout.GetControlRect(false, 22);
        EditorGUI.BeginProperty(r, GUIContent.none, prop);
        EditorGUI.showMixedValue = prop.hasMultipleDifferentValues;

        bool value = EditorGUI.ToggleLeft(r, prop.displayName, prop.boolValue, HeaderStyle);
        if (value != prop.boolValue) prop.boolValue = value;

        EditorGUI.showMixedValue = false;
        EditorGUI.EndProperty();
    }
}