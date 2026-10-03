using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ComponentGroupHeader))]
public class ComponentGroupHeaderEditor : Editor
{
    SerializedProperty groupName;
    SerializedProperty color;
    SerializedProperty collapsed;

    // เรียกตอน Inspector เริ่มวาด component นี้ (เช่น ตอนคลิกเลือก GameObject)
    void OnEnable()
    {
        groupName = serializedObject.FindProperty("groupName");
        color = serializedObject.FindProperty("color");
        collapsed = serializedObject.FindProperty("collapsed");

        ApplyVisibility(((ComponentGroupHeader)target).gameObject);   // ★ ใหม่
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // แถบสี
        Rect bar = EditorGUILayout.GetControlRect(false, 22);
        EditorGUI.DrawRect(bar, color.colorValue);

        // ลูกศรพับ/กาง + ชื่อกลุ่ม วางทับบนแถบสี
        Rect foldoutRect = new Rect(bar.x + 4, bar.y + 3, bar.width - 4, bar.height - 6);
        bool expanded = EditorGUI.Foldout(foldoutRect, !collapsed.boolValue, groupName.stringValue, true);
        bool collapsedChanged = collapsed.boolValue == expanded;   // ★ ใหม่: เช็คว่าเพิ่งกดลูกศรหรือเปล่า
        collapsed.boolValue = !expanded;

        // ช่องแก้ชื่อกับสี
        EditorGUILayout.PropertyField(groupName, new GUIContent("Name"));
        EditorGUILayout.PropertyField(color);

        serializedObject.ApplyModifiedProperties();

        // ★ ใหม่: กดลูกศรแล้ว → ไปแปะ/แกะป้ายให้ component ข้างล่าง
        if (collapsedChanged)
            ApplyVisibility(((ComponentGroupHeader)target).gameObject);
    }

    // ★ ใหม่: ไล่ component จากบนลงล่าง แล้วแปะป้ายตามที่คั่น
    public static void ApplyVisibility(GameObject go)
    {
        bool hide = false;      // ตอนนี้อยู่ในกลุ่มที่พับหรือเปล่า
        bool changed = false;

        foreach (Component c in go.GetComponents<Component>())
        {
            if (c == null) continue;          // Missing Script
            if (c is Transform) continue;     // Transform ไม่ยุ่ง

            // เจอที่คั่น → เริ่มกลุ่มใหม่ จำไว้ว่ากลุ่มนี้พับหรือเปล่า
            if (c is ComponentGroupHeader header)
            {
                hide = header.collapsed;
                continue;
            }

            HideFlags newFlags = hide
                ? c.hideFlags | HideFlags.HideInInspector     // แปะป้าย
                : c.hideFlags & ~HideFlags.HideInInspector;   // แกะป้าย

            if (c.hideFlags != newFlags)
            {
                c.hideFlags = newFlags;
                EditorUtility.SetDirty(c);    // บอก Unity ว่ามีการเปลี่ยน ต้อง save
                changed = true;
            }
        }

        // สั่งให้ Inspector วาดใหม่ (แทนการคลิกออกแล้วคลิกกลับ)
        if (changed)
            EditorApplication.delayCall += () => ActiveEditorTracker.sharedTracker.ForceRebuild();
    }
}
