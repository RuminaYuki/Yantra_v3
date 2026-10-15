#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SDFcl.GamePlay.QuestSystem.GraphEditor
{
    // Editor window for a QuestGraphSO. Opened by double-clicking the asset or from the menu.
    // In Play Mode it shows the running QuestManager's state on the nodes.
    public class QuestGraphWindow : EditorWindow
    {
        // Serialized so the window keeps its graph after a script recompile.
        [SerializeField] private QuestGraphSO _graph;

        private QuestGraphView _graphView;
        private QuestInspectorPanel _panel;
        private Label _graphLabel;
        private Label _statusLabel;
        private QuestNodeViewBase _selected;

        private bool _runtimeDirty;
        private double _nextRuntimeRefresh;

        [MenuItem("Window/Yantra/Quest Graph")]
        public static void OpenEmpty()
        {
            var window = GetWindow<QuestGraphWindow>();
            window.titleContent = new GUIContent("Quest Graph");
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(EntityId entityId, int line)
        {
            if (!(EditorUtility.EntityIdToObject(entityId) is QuestGraphSO graph)) return false;
            Open(graph);
            return true;
        }

        public static void Open(QuestGraphSO graph)
        {
            var window = GetWindow<QuestGraphWindow>();
            window.titleContent = new GUIContent("Quest Graph");
            window.SetGraph(graph);
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += OnEditorUpdate;
            QuestManager.QuestStateChanged += OnQuestStateChanged;
            QuestManager.ObjectiveProgressed += OnObjectiveProgressed;
            QuestManager.QuestRestarted += OnQuestEvent;
            QuestManager.Restored += MarkRuntimeDirty;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= OnEditorUpdate;
            QuestManager.QuestStateChanged -= OnQuestStateChanged;
            QuestManager.ObjectiveProgressed -= OnObjectiveProgressed;
            QuestManager.QuestRestarted -= OnQuestEvent;
            QuestManager.Restored -= MarkRuntimeDirty;
            _graphView?.Dispose();
        }

        private void CreateGUI()
        {
            var toolbar = new Toolbar();
            _graphLabel = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 6, marginRight = 12, unityTextAlign = TextAnchor.MiddleLeft } };
            toolbar.Add(_graphLabel);
            toolbar.Add(new ToolbarButton(() => _graphView?.FrameAll()) { text = "Frame All" });
            toolbar.Add(new ToolbarButton(Validate) { text = "Validate" });
            toolbar.Add(new ToolbarButton(PingGraph) { text = "Select Asset" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            _statusLabel = new Label { style = { unityTextAlign = TextAnchor.MiddleRight, marginRight = 8 } };
            toolbar.Add(_statusLabel);
            rootVisualElement.Add(toolbar);

            _graphView = new QuestGraphView(this);
            _panel = new QuestInspectorPanel(_graphView);
            _graphView.SelectionChanged += OnSelectionChanged;
            _graphView.GraphEdited += OnGraphEdited;

            var split = new TwoPaneSplitView(1, 320f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            split.Add(_graphView);
            split.Add(_panel);
            rootVisualElement.Add(split);

            ShowGraph();
        }

        private void SetGraph(QuestGraphSO graph)
        {
            _graph = graph;
            ShowGraph();
        }

        private void ShowGraph()
        {
            if (_graphView == null) return;
            _graphLabel.text = _graph != null ? _graph.name : "No Quest Graph (double-click one in the Project window)";
            _graphView.Load(_graph);
            _panel.ShowSelection(null);
            UpdateStatus();
            MarkRuntimeDirty();

            // Center the graph after the first layout pass.
            _graphView.schedule.Execute(() => _graphView.FrameAll()).ExecuteLater(50);
        }

        private void OnSelectionChanged(QuestNodeViewBase selected)
        {
            _selected = selected;
            _panel.ShowSelection(selected);
        }

        private void OnGraphEdited()
        {
            if (_selected == null) _panel.ShowSelection(null);
            UpdateStatus();
        }

        private void OnUndoRedo()
        {
            if (_graphView == null || _graph == null) return;
            _graphView.Reload();
            _panel.ShowSelection(_selected);
            UpdateStatus();
        }

        private void Validate()
        {
            if (_graphView == null) return;
            _graphView.RefreshValidation();
            UpdateStatus();
            if (_selected == null) _panel.ShowSelection(null);
        }

        private void PingGraph()
        {
            if (_graph == null) return;
            Selection.activeObject = _graph;
            EditorGUIUtility.PingObject(_graph);
        }

        private void UpdateStatus()
        {
            if (_statusLabel == null) return;
            if (_graph == null)
            {
                _statusLabel.text = "";
                return;
            }

            int issues = _graphView.CountIssues();
            string issueText = issues == 0 ? "\u2713 no problems" : $"\u26A0 {issues} problem(s)";
            string playText = IsLive() ? "  \u00B7  \u25CF LIVE" : (EditorApplication.isPlaying ? "  \u00B7  Play Mode (no QuestManager using this graph)" : "");
            _statusLabel.text = issueText + playText;
            _statusLabel.style.color = issues == 0 ? new Color(0.6f, 0.9f, 0.6f) : QuestNodeViewBase.IssueColor;
        }

        // ------------------------------------------------------------------ Play Mode

        private bool IsLive()
        {
            var manager = QuestManager.Instance;
            return EditorApplication.isPlaying && manager != null && _graph != null && manager.Graph == _graph;
        }

        private void OnQuestStateChanged(QuestSO quest, QuestState state) => MarkRuntimeDirty();
        private void OnObjectiveProgressed(QuestSO quest, int index, int count) => MarkRuntimeDirty();
        private void OnQuestEvent(QuestSO quest) => MarkRuntimeDirty();
        private void MarkRuntimeDirty() => _runtimeDirty = true;

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            MarkRuntimeDirty();
            if (change == PlayModeStateChange.EnteredEditMode) _graphView?.ClearRuntime();
        }

        private void OnEditorUpdate()
        {
            if (_graphView == null) return;

            // Daily timers change without events, so refresh now and then while playing.
            if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= _nextRuntimeRefresh)
            {
                _nextRuntimeRefresh = EditorApplication.timeSinceStartup + 2.0;
                _runtimeDirty = true;
            }

            if (!_runtimeDirty) return;
            _runtimeDirty = false;

            if (IsLive()) _graphView.ApplyRuntime(QuestManager.Instance);
            else _graphView.ClearRuntime();

            _panel.RefreshRuntime();
            UpdateStatus();
        }
    }
}

#endif
