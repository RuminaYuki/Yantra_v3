using UnityEditor;
using UnityEngine;

public static class ComponentGroupMenu
{
    [MenuItem("Tools/Component Group/Show All Components")]
    static void ShowAll()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null) continue;

                Undo.RecordObject(c, "Show All Components");

                if (c is ComponentGroupHeader header)
                    header.collapsed = false;   // กางทุกกลุ่ม

                c.hideFlags &= ~HideFlags.HideInInspector;   // แกะป้ายทุกตัว
                EditorUtility.SetDirty(c);
            }
        }

        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }
}
