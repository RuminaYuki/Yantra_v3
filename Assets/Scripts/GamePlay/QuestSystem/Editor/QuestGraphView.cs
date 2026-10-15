#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SDFcl.GamePlay.QuestSystem.GraphEditor
{
    // Canvas that shows a QuestGraphSO as nodes and edges and writes every change back to it (with Undo).
    public class QuestGraphView : GraphView
    {
        private const string StyleSheetName = "QuestGraph";
        private const float LaneExtent = 20000f;

        private readonly EditorWindow _window;
        private readonly QuestSearchProvider _search;
        private readonly Dictionary<string, QuestNodeViewBase> _views = new Dictionary<string, QuestNodeViewBase>();

        private QuestGraphSO _graph;
        private bool _isLoading;

        private readonly VisualElement _mainLane;
        private readonly VisualElement _supLane;
        private readonly Label _mainLaneLabel;
        private readonly Label _supLaneLabel;

        public QuestGraphSO Graph => _graph;

        // The single selected node, or null.
        public event Action<QuestNodeViewBase> SelectionChanged;

        // Any data change made from the canvas (create, delete, connect, move).
        public event Action GraphEdited;

        public QuestGraphView(EditorWindow window)
        {
            _window = window;
            graphViewChanged = OnGraphViewChanged;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ClickSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            var styleSheet = LoadStyleSheet();
            if (styleSheet != null) styleSheets.Add(styleSheet);

            // Main / Sup lanes drawn behind the nodes (display only).
            _mainLane = MakeLane(new Color(0.63f, 0.41f, 0.29f, 0.10f));
            _supLane = MakeLane(new Color(0.12f, 0.56f, 1f, 0.08f));
            _mainLaneLabel = MakeLaneLabel("MAIN", new Color(0.85f, 0.63f, 0.50f, 0.7f));
            _supLaneLabel = MakeLaneLabel("SUP", new Color(0.43f, 0.71f, 1f, 0.7f));
            contentViewContainer.Insert(0, _mainLane);
            contentViewContainer.Insert(1, _supLane);
            contentViewContainer.Insert(2, _mainLaneLabel);
            contentViewContainer.Insert(3, _supLaneLabel);

            _search = ScriptableObject.CreateInstance<QuestSearchProvider>();
            _search.hideFlags = HideFlags.HideAndDontSave;
            _search.OnPick = (kind, screenPos) => CreateNode(kind, ScreenToGraph(screenPos));
            nodeCreationRequest = ctx =>
            {
                if (_graph != null) SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), _search);
            };
        }

        // Called by the window when it closes.
        public void Dispose()
        {
            if (_search != null) UnityEngine.Object.DestroyImmediate(_search);
        }

        private static StyleSheet LoadStyleSheet()
        {
            foreach (string guid in AssetDatabase.FindAssets($"{StyleSheetName} t:StyleSheet"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == StyleSheetName)
                    return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }
            Debug.LogWarning($"{StyleSheetName}.uss not found. The quest graph will have no grid colors.");
            return null;
        }

        private static VisualElement MakeLane(Color color)
        {
            var lane = new VisualElement { pickingMode = PickingMode.Ignore };
            lane.style.position = Position.Absolute;
            lane.style.left = -LaneExtent;
            lane.style.width = LaneExtent * 2f;
            lane.style.backgroundColor = color;
            return lane;
        }

        private static Label MakeLaneLabel(string text, Color color)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.left = -180;
            label.style.fontSize = 22;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = color;
            return label;
        }

        public void UpdateLanes()
        {
            float split = _graph != null ? _graph.LaneSplitY : 0f;
            _mainLane.style.top = -LaneExtent;
            _mainLane.style.height = LaneExtent + split;
            _supLane.style.top = split;
            _supLane.style.height = LaneExtent;
            _mainLaneLabel.style.top = split - 40;
            _supLaneLabel.style.top = split + 10;

            bool show = _graph != null;
            _mainLane.style.display = _supLane.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            _mainLaneLabel.style.display = _supLaneLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ------------------------------------------------------------------ Load

        public void Load(QuestGraphSO graph)
        {
            _graph = graph;
            ClearSelection();

            _isLoading = true;
            DeleteElements(graphElements.ToList());
            _isLoading = false;
            _views.Clear();

            UpdateLanes();
            if (_graph == null) return;

            _graph.MarkChanged();
            _graph.BuildLookup();

            foreach (var node in _graph.Nodes)
            {
                if (node == null) continue;
                var view = CreateView(node);
                if (view == null) continue;
                view.SetPosition(new Rect(node.position, Vector2.zero));
                AddElement(view);
                _views[node.guid] = view;
            }

            foreach (var e in _graph.Edges)
            {
                if (!_views.TryGetValue(e.fromNode, out var from) || !_views.TryGetValue(e.toNode, out var to)) continue;
                if (from.Output == null || to.Input == null) continue;
                AddElement(from.Output.ConnectTo(to.Input));
            }

            RefreshValidation();
        }

        // Rebuild and keep the same node selected (after Undo or panel edits).
        public void Reload()
        {
            string selectedGuid = selection.Count == 1 && selection[0] is QuestNodeViewBase v ? v.Guid : null;
            Load(_graph);
            if (selectedGuid != null && _views.TryGetValue(selectedGuid, out var again)) AddToSelection(again);
        }

        private static QuestNodeViewBase CreateView(QuestGraphNode node)
        {
            switch (node)
            {
                case QuestNode q: return new QuestNodeView(q);
                case GateNode g: return new GateNodeView(g);
                case ResetNode r: return new ResetNodeView(r);
                default: return null;
            }
        }

        public void RefreshNode(string guid)
        {
            if (_views.TryGetValue(guid, out var view)) view.RefreshContent();
        }

        public void RefreshAllNodes()
        {
            foreach (var view in _views.Values) view.RefreshContent();
        }

        public void RefreshValidation()
        {
            if (_graph == null) return;
            var issues = QuestGraphValidator.Validate(_graph);
            foreach (var pair in _views)
                pair.Value.SetIssues(issues.TryGetValue(pair.Key, out var list) ? list : null);
        }

        public int CountIssues()
        {
            if (_graph == null) return 0;
            int count = 0;
            foreach (var list in QuestGraphValidator.Validate(_graph).Values) count += list.Count;
            return count;
        }

        // ------------------------------------------------------------------ Selection

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            NotifySelection();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            NotifySelection();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            NotifySelection();
        }

        private void NotifySelection()
        {
            SelectionChanged?.Invoke(selection.Count == 1 ? selection[0] as QuestNodeViewBase : null);
        }

        // ------------------------------------------------------------------ Connecting

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList()
                .Where(p => p.direction != startPort.direction && p.node != startPort.node)
                .ToList();
        }

        // ------------------------------------------------------------------ Creating

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            if (_graph == null || !(evt.target is GraphView)) return;

            Vector2 pos = contentViewContainer.WorldToLocal(evt.mousePosition);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Create/Main Quest", _ => CreateNode(QuestSearchProvider.Kind.MainQuest, pos));
            evt.menu.AppendAction("Create/Sup Quest", _ => CreateNode(QuestSearchProvider.Kind.SupQuest, pos));
            evt.menu.AppendAction("Create/AND Gate", _ => CreateNode(QuestSearchProvider.Kind.And, pos));
            evt.menu.AppendAction("Create/OR Gate", _ => CreateNode(QuestSearchProvider.Kind.Or, pos));
            evt.menu.AppendAction("Create/Reset", _ => CreateNode(QuestSearchProvider.Kind.Reset, pos));
        }

        private Vector2 ScreenToGraph(Vector2 screenPos)
        {
            VisualElement root = _window.rootVisualElement;
            Vector2 windowPos = root.ChangeCoordinatesTo(root.parent, screenPos - _window.position.position);
            return contentViewContainer.WorldToLocal(windowPos);
        }

        public void CreateNode(QuestSearchProvider.Kind kind, Vector2 position)
        {
            if (_graph == null) return;

            Undo.RecordObject(_graph, "Create Quest Node");
            QuestGraphNode node;

            switch (kind)
            {
                case QuestSearchProvider.Kind.MainQuest:
                case QuestSearchProvider.Kind.SupQuest:
                    var track = kind == QuestSearchProvider.Kind.MainQuest ? QuestTrack.Main : QuestTrack.Sup;
                    var quest = _graph.CreateQuestAsset(NextQuestName(track), track);
                    Undo.RegisterCreatedObjectUndo(quest, "Create Quest");
                    node = new QuestNode { quest = quest };
                    break;
                case QuestSearchProvider.Kind.And:
                    node = new GateNode { gateType = GateType.And };
                    break;
                case QuestSearchProvider.Kind.Or:
                    node = new GateNode { gateType = GateType.Or };
                    break;
                default:
                    node = new ResetNode();
                    break;
            }

            node.position = position;
            _graph.AddNode(node);
            SaveGraph();

            Reload();
            if (_views.TryGetValue(node.guid, out var view))
            {
                ClearSelection();
                AddToSelection(view);
            }
            GraphEdited?.Invoke();
        }

        private string NextQuestName(QuestTrack track)
        {
            string prefix = track == QuestTrack.Main ? "Main Quest" : "Sup Quest";
            int n = 1;
            foreach (var node in _graph.Nodes)
                if (node is QuestNode q && q.quest != null && q.quest.Track == track) n++;
            return $"{prefix} {n}";
        }

        // ------------------------------------------------------------------ Changes from the canvas

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_graph == null || _isLoading) return change;

            bool edited = false;

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                Undo.RecordObject(_graph, "Delete Quest Graph Elements");
                foreach (var element in change.elementsToRemove)
                {
                    switch (element)
                    {
                        case Edge edge when edge.output?.node is QuestNodeViewBase from && edge.input?.node is QuestNodeViewBase to:
                            _graph.RemoveEdge(from.Guid, to.Guid);
                            break;
                        case QuestNodeViewBase view:
                            DeleteNodeData(view);
                            break;
                    }
                }
                edited = true;
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                Undo.RecordObject(_graph, "Connect Quest Nodes");
                foreach (var edge in change.edgesToCreate)
                {
                    if (edge.output?.node is QuestNodeViewBase from && edge.input?.node is QuestNodeViewBase to)
                        _graph.AddEdge(from.Guid, to.Guid);
                }
                edited = true;
            }

            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                Undo.RecordObject(_graph, "Move Quest Nodes");
                foreach (var element in change.movedElements)
                {
                    if (element is QuestNodeViewBase view)
                    {
                        var node = _graph.GetNode(view.Guid);
                        if (node != null) node.position = view.GetPosition().position;
                    }
                }
                EditorUtility.SetDirty(_graph);
            }

            if (edited)
            {
                SaveGraph();
                // GraphView is still applying the change; validate next frame.
                schedule.Execute(() =>
                {
                    RefreshValidation();
                    GraphEdited?.Invoke();
                });
            }

            return change;
        }

        private void DeleteNodeData(QuestNodeViewBase view)
        {
            var quest = view is QuestNodeView qv ? qv.Quest : null;
            _graph.RemoveNode(view.Guid);
            if (quest == null) return;

            _graph.RemoveQuestReferences(quest);
            // Only destroy it if it really is a sub-asset of this graph.
            if (AssetDatabase.GetAssetPath(quest) == AssetDatabase.GetAssetPath(_graph))
                Undo.DestroyObjectImmediate(quest);
        }

        private void SaveGraph()
        {
            _graph.MarkChanged();
            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssetIfDirty(_graph);
        }

        // ------------------------------------------------------------------ Play Mode

        public void ApplyRuntime(QuestManager manager)
        {
            foreach (var view in _views.Values)
            {
                switch (view)
                {
                    case QuestNodeView qv when qv.Quest != null:
                        ApplyQuestRuntime(qv, manager);
                        break;
                    case GateNodeView gv:
                        bool passed = manager.IsNodePassed(gv.Guid);
                        gv.SetRuntime(passed ? QuestNodeViewBase.CompletedColor : QuestNodeViewBase.LockedColor,
                            passed ? "\u2713 passed" : "waiting");
                        break;
                    case ResetNodeView rv:
                        var inputs = _graph.GetInputs(rv.Guid);
                        bool ready = inputs.Count > 0 && manager.IsNodePassed(inputs[0]);
                        rv.SetRuntime(ready ? QuestNodeViewBase.ResetColor : QuestNodeViewBase.LockedColor,
                            ready ? "input passed" : "waiting");
                        break;
                }
            }
        }

        private static void ApplyQuestRuntime(QuestNodeView view, QuestManager manager)
        {
            var quest = view.Quest;
            var state = manager.GetState(quest);
            Color color;
            switch (state)
            {
                case QuestState.Active: color = QuestNodeViewBase.ActiveColor; break;
                case QuestState.Completed: color = QuestNodeViewBase.CompletedColor; break;
                case QuestState.Skipped:
                case QuestState.Failed: color = QuestNodeViewBase.ClosedColor; break;
                default: color = QuestNodeViewBase.LockedColor; break;
            }

            var parts = new List<string> { state.ToString() };
            if (quest.Objectives.Count > 0 && state == QuestState.Active)
            {
                int done = 0, total = 0;
                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    done += manager.GetObjectiveCount(quest, i);
                    total += quest.Objectives[i].requiredCount;
                }
                parts.Add($"{done}/{total}");
            }
            int completions = manager.GetCompletionCount(quest);
            if (completions > 0) parts.Add($"\u21BB x{completions}");
            var next = manager.GetNextReset(quest);
            if (next.HasValue) parts.Add($"next {next.Value:dd/MM HH:mm}");
            if (manager.IsWaitingForSignal(quest)) parts.Add("waiting signal");

            view.SetRuntime(color, string.Join(" \u00B7 ", parts));
        }

        public void ClearRuntime()
        {
            foreach (var view in _views.Values) view.SetRuntime(null, null);
        }
    }
}

#endif
