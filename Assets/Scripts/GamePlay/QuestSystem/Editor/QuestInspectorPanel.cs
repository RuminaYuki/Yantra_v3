#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SDFcl.GamePlay.QuestSystem.GraphEditor
{
    // Right-hand panel: edits the selected node, or shows graph info when nothing is selected.
    // In Play Mode it also shows the live state and debug buttons for the selected quest.
    public class QuestInspectorPanel : VisualElement
    {
        private readonly QuestGraphView _view;
        private readonly ScrollView _scroll;
        private readonly VisualElement _runtimeBox;

        private QuestNodeViewBase _selected;

        public QuestInspectorPanel(QuestGraphView view)
        {
            _view = view;
            style.minWidth = 260;
            style.backgroundColor = new Color(0.19f, 0.19f, 0.19f);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            Add(_scroll);

            _runtimeBox = new VisualElement();
            _runtimeBox.style.borderTopWidth = 1;
            _runtimeBox.style.borderTopColor = new Color(0f, 0f, 0f, 0.4f);
            _runtimeBox.style.paddingLeft = 8;
            _runtimeBox.style.paddingRight = 8;
            _runtimeBox.style.paddingTop = 6;
            _runtimeBox.style.paddingBottom = 8;
            Add(_runtimeBox);

            ShowSelection(null);
        }

        // ------------------------------------------------------------------ Edit mode content

        public void ShowSelection(QuestNodeViewBase selected)
        {
            _selected = selected;
            _scroll.Clear();

            switch (selected)
            {
                case QuestNodeView qv when qv.Quest != null:
                    BuildQuest(qv);
                    break;
                case QuestNodeViewBase other:
                    BuildSerializedNode(other);
                    break;
                default:
                    BuildGraphInfo();
                    break;
            }

            RefreshRuntime();
        }

        private void BuildQuest(QuestNodeView qv)
        {
            var quest = qv.Quest;
            AddHeader(quest.Track == QuestTrack.Main ? "Main Quest" : "Sup Quest");

            var inspector = new InspectorElement(quest);
            _scroll.Add(inspector);

            var tracked = new SerializedObject(quest);
            inspector.TrackSerializedObjectValue(tracked, _ =>
            {
                // Keep the sub-asset name in sync with the title so it reads well in the Project window.
                if (quest != null && quest.name != quest.Title && !string.IsNullOrEmpty(quest.Title))
                {
                    quest.name = quest.Title;
                    EditorUtility.SetDirty(quest);
                    AssetDatabase.SaveAssetIfDirty(quest);
                }
                qv.RefreshContent();
                _view.RefreshValidation();
            });

            AddRoutes(quest);
        }

        // Gate / Reset nodes live inside the graph as [SerializeReference] items.
        private void BuildSerializedNode(QuestNodeViewBase view)
        {
            var graph = _view.Graph;
            int index = graph != null ? graph.IndexOfNode(view.Guid) : -1;
            if (index < 0) return;

            AddHeader(view is GateNodeView g ? (g.IsOr ? "OR Gate" : "AND Gate") : "Reset");
            AddHelp(view is GateNodeView gate
                ? (gate.IsOr
                    ? "Passes when ANY input passes. Loser Policy decides what happens to unfinished quests on the routes that lost."
                    : "Passes when EVERY input passes.")
                : "When the input passes, every target quest goes back to Locked and is checked again. Use it to loop a chain.");

            var so = new SerializedObject(graph);
            var element = so.FindProperty(QuestGraphSO.NodesField).GetArrayElementAtIndex(index);
            var box = new VisualElement();
            box.style.paddingLeft = 6;
            box.style.paddingRight = 6;

            var it = element.Copy();
            var end = element.GetEndProperty();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
            {
                enterChildren = false;
                if (it.name == "guid" || it.name == "position") continue;
                box.Add(new PropertyField(it.Copy()));
            }
            box.Bind(so);
            _scroll.Add(box);

            box.TrackSerializedObjectValue(so, _ =>
            {
                graph.MarkChanged();
                view.RefreshContent();
                _view.RefreshValidation();
            });

            if (view is ResetNodeView rv) AddResetTargetButton(rv, graph);
        }

        private void AddResetTargetButton(ResetNodeView rv, QuestGraphSO graph)
        {
            var button = new Button(() =>
            {
                var menu = new GenericMenu();
                foreach (var node in graph.Nodes)
                {
                    if (!(node is QuestNode qn) || qn.quest == null) continue;
                    var quest = qn.quest;
                    bool already = rv.Data.targets.Contains(quest);
                    menu.AddItem(new GUIContent(quest.Title), already, () =>
                    {
                        Undo.RecordObject(graph, "Change Reset Targets");
                        if (already) rv.Data.targets.Remove(quest);
                        else rv.Data.targets.Add(quest);
                        EditorUtility.SetDirty(graph);
                        _view.Reload();
                    });
                }
                menu.ShowAsContext();
            }) { text = "Add / remove target quest..." };
            button.style.marginTop = 6;
            _scroll.Add(button);
        }

        private void BuildGraphInfo()
        {
            var graph = _view.Graph;
            if (graph == null)
            {
                AddHelp("Double-click a Quest Graph asset in the Project window to open it.");
                return;
            }

            AddHeader(graph.name);

            int quests = 0, gates = 0, resets = 0;
            foreach (var n in graph.Nodes)
            {
                if (n is QuestNode) quests++;
                else if (n is GateNode) gates++;
                else if (n is ResetNode) resets++;
            }
            AddHelp($"{quests} quests \u00B7 {gates} gates \u00B7 {resets} reset nodes");

            var so = new SerializedObject(graph);
            var lane = new PropertyField(so.FindProperty(QuestGraphSO.LaneSplitField), "Sup lane starts at Y");
            lane.Bind(so);
            lane.TrackPropertyValue(so.FindProperty(QuestGraphSO.LaneSplitField), _ => _view.UpdateLanes());
            _scroll.Add(lane);

            AddHeader("How to use");
            AddHelp("\u2022 Space or right-click: create Main / Sup quest, AND, OR, Reset.\n" +
                    "\u2022 Drag from a node's right port to another node's left port to connect.\n" +
                    "\u2022 A quest (or Reset) takes one input. Use AND / OR to join several.\n" +
                    "\u2022 A quest with no input is Active from the start.\n" +
                    "\u2022 Delete key removes nodes or edges. Ctrl+Z works.\n" +
                    "\u2022 Double-click a quest to select its asset.");

            var issues = QuestGraphValidator.Validate(graph);
            AddHeader(issues.Count == 0 ? "No problems found" : "Problems");
            foreach (var pair in issues)
            {
                var node = graph.GetNode(pair.Key);
                string name = node is QuestNode qn && qn.quest != null ? qn.quest.Title
                    : node is GateNode gn ? gn.gateType.ToString().ToUpper()
                    : "Reset";
                foreach (var msg in pair.Value)
                {
                    var label = new Label($"\u26A0 {name}: {msg}");
                    label.style.whiteSpace = WhiteSpace.Normal;
                    label.style.color = QuestNodeViewBase.IssueColor;
                    label.style.marginLeft = 6;
                    label.style.marginBottom = 2;
                    _scroll.Add(label);
                }
            }
        }

        private void AddRoutes(QuestSO quest)
        {
            var routes = QuestRouteUtility.GetUnlockRoutes(_view.Graph, quest);
            AddHeader("Unlocks when");
            if (routes.Count == 0)
            {
                AddHelp("No input: Active from the start.");
                return;
            }
            for (int i = 0; i < routes.Count; i++)
            {
                var names = new List<string>();
                foreach (var q in routes[i]) names.Add(q.Title);
                AddHelp($"Route {i + 1}: {string.Join(" + ", names)}");
                if (i < routes.Count - 1) AddHelp("\u2014 or \u2014");
            }
        }

        private void AddHeader(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 13;
            label.style.marginTop = 8;
            label.style.marginBottom = 4;
            label.style.marginLeft = 6;
            _scroll.Add(label);
        }

        private void AddHelp(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = new Color(1f, 1f, 1f, 0.7f);
            label.style.marginLeft = 6;
            label.style.marginRight = 6;
            label.style.marginBottom = 2;
            _scroll.Add(label);
        }

        // ------------------------------------------------------------------ Play Mode box

        // Called by the window when quest states change in Play Mode.
        public void RefreshRuntime()
        {
            _runtimeBox.Clear();
            var manager = QuestManager.Instance;
            bool live = EditorApplication.isPlaying && manager != null && manager.Graph == _view.Graph;
            _runtimeBox.style.display = live ? DisplayStyle.Flex : DisplayStyle.None;
            if (!live) return;

            var title = new Label("Play Mode");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _runtimeBox.Add(title);

            if (_selected is QuestNodeView qv && qv.Quest != null)
            {
                var quest = qv.Quest;
                var state = manager.GetState(quest);
                _runtimeBox.Add(new Label($"State: {state}   Completed: x{manager.GetCompletionCount(quest)}"));

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    var obj = quest.Objectives[i];
                    int count = manager.GetObjectiveCount(quest, i);
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    var label = new Label($"{obj.id}: {count}/{obj.requiredCount}") { style = { flexGrow = 1 } };
                    string id = obj.id;
                    var plus = new Button(() => manager.ReportProgress(quest, id)) { text = "+1" };
                    plus.SetEnabled(state == QuestState.Active);
                    row.Add(label);
                    row.Add(plus);
                    _runtimeBox.Add(row);
                }

                AddRuntimeButton("Complete", () => manager.CompleteQuest(quest), state == QuestState.Active);
                AddRuntimeButton("Restart (repeatable)", () => manager.RestartQuest(quest),
                    state == QuestState.Completed && quest.IsRepeatable);
            }
            else
            {
                _runtimeBox.Add(new Label("Select a quest to debug it."));
            }

            AddRuntimeButton("Signal New Cycle", manager.SignalNewCycle, true);
            AddRuntimeButton("Force Daily Reset", () => manager.CheckDailyResets(DateTime.MaxValue), true);
        }

        private void AddRuntimeButton(string text, Action action, bool enabled)
        {
            var button = new Button(action) { text = text };
            button.SetEnabled(enabled);
            button.style.marginTop = 3;
            _runtimeBox.Add(button);
        }
    }
}

#endif
