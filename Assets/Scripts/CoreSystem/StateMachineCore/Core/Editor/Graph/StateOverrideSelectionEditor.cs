#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Unity's Inspector for a Base's state clicked in a variant table's graph: the override list
    // (VariantStatePanel, which also draws "Edit in Base").
    [CustomEditor(typeof(StateOverrideSelection))]
    public class StateOverrideSelectionEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var selection = (StateOverrideSelection)target;
            var root = new VisualElement();

            if (selection.Table == null || selection.BaseTable == null || selection.State == null)
            {
                root.Add(new HelpBox("Select the state in the graph again.", HelpBoxMessageType.Info));
                return root;
            }

            var panel = new VariantStatePanel(StateMachineGraphWindow.RefreshOverrideLabels);
            panel.Show(selection.Table, selection.BaseTable, selection.State);
            root.Add(panel);

            return root;
        }
    }
}

#endif
