using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Replaces the raw "shape" list with a clickable grid in the ItemData Inspector.
[CustomEditor(typeof(ItemData))]
public class ItemDataEditor : Editor
{
    private const int GridSize = 6;
    private const float CellPixels = 24f;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "shape");
        DrawShapeGrid(serializedObject.FindProperty("shape"));
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawShapeGrid(SerializedProperty shape)
    {
        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < shape.arraySize; i++) cells.Add(shape.GetArrayElementAtIndex(i).vector2IntValue);
        if (cells.Count == 0) cells.Add(Vector2Int.zero);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shape (click cells, top-left = 0,0)", EditorStyles.boldLabel);

        bool changed = false;
        Color oldColor = GUI.backgroundColor;
        for (int y = 0; y < GridSize; y++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < GridSize; x++)
            {
                var c = new Vector2Int(x, y);
                bool on = cells.Contains(c);
                GUI.backgroundColor = on ? new Color(0.4f, 0.8f, 1f) : oldColor;
                bool now = GUILayout.Toggle(on, GUIContent.none, "Button",
                    GUILayout.Width(CellPixels), GUILayout.Height(CellPixels));
                if (now == on) continue;
                changed = true;
                if (now) cells.Add(c);
                else cells.Remove(c);
            }
            EditorGUILayout.EndHorizontal();
        }
        GUI.backgroundColor = oldColor;

        if (!changed) return;
        if (cells.Count == 0) cells.Add(Vector2Int.zero); // never allow an empty shape

        shape.arraySize = cells.Count;
        int index = 0;
        foreach (Vector2Int c in cells) shape.GetArrayElementAtIndex(index++).vector2IntValue = c;
    }
}
