#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The graph window's .uss files (StateMachineGraph.uss, ContentsPanel.uss).
    internal static class GraphStyleSheets
    {
        // Adds the stylesheet to the element. Found by name, so the Graph folder can be moved without
        // breaking a path. A missing file only costs the looks, so it warns instead of failing.
        public static void AddTo(VisualElement element, string name)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{name} t:StyleSheet"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                {
                    element.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(path));
                    return;
                }
            }

            Debug.LogWarning($"{name}.uss not found next to the state machine graph scripts.");
        }
    }
}

#endif
