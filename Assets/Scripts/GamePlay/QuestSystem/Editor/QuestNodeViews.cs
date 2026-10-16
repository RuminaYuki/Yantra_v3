#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SDFcl.GamePlay.QuestSystem.GraphEditor
{
    // Shared look and behaviour of every node in the quest graph.
    public abstract class QuestNodeViewBase : Node
    {
        public static readonly Color MainColor = new Color(0.55f, 0.36f, 0.25f);
        public static readonly Color SupColor = new Color(0.12f, 0.40f, 0.72f);
        public static readonly Color AndColor = new Color(0.85f, 0.25f, 0.28f);
        public static readonly Color OrColor = new Color(0.93f, 0.74f, 0.20f);
        public static readonly Color ResetColor = new Color(0.55f, 0.35f, 0.80f);

        public static readonly Color LockedColor = new Color(0.45f, 0.47f, 0.50f);
        public static readonly Color ActiveColor = new Color(0.95f, 0.71f, 0.27f);
        public static readonly Color CompletedColor = new Color(0.31f, 0.75f, 0.48f);
        public static readonly Color ClosedColor = new Color(0.85f, 0.35f, 0.35f);
        public static readonly Color IssueColor = new Color(1f, 0.6f, 0.1f);

        public string Guid { get; }
        public Port Input { get; protected set; }
        public Port Output { get; protected set; }

        private readonly Label _issueLabel;
        private readonly Label _runtimeLabel;
        private bool _hasIssues;
        private Color? _runtimeBorder;

        protected QuestNodeViewBase(string guid)
        {
            Guid = guid;
            titleButtonContainer.style.display = DisplayStyle.None;

            _runtimeLabel = MakeLabel("", 11, new Color(1f, 1f, 1f, 0.9f));
            _runtimeLabel.style.display = DisplayStyle.None;
            _runtimeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            _issueLabel = MakeLabel("", 10, IssueColor);
            _issueLabel.style.display = DisplayStyle.None;
            _issueLabel.style.whiteSpace = WhiteSpace.Normal;
            _issueLabel.style.maxWidth = 220;

            extensionContainer.Add(_runtimeLabel);
            extensionContainer.Add(_issueLabel);
            extensionContainer.style.backgroundColor = new Color(0.17f, 0.17f, 0.18f, 0.95f);
        }

        protected static Label MakeLabel(string text, int size, Color color)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.paddingLeft = 8;
            label.style.paddingRight = 8;
            label.style.paddingTop = 2;
            label.style.paddingBottom = 2;
            return label;
        }

        protected Port AddPort(Direction direction, Port.Capacity capacity, Color color)
        {
            var port = Port.Create<Edge>(Orientation.Horizontal, direction, capacity, typeof(bool));
            port.portName = "";
            port.portColor = color;
            if (direction == Direction.Input) inputContainer.Add(port);
            else outputContainer.Add(port);
            return port;
        }

        protected void SetHeaderColor(Color color)
        {
            titleContainer.style.backgroundColor = color;
        }

        public void SetIssues(List<string> issues)
        {
            _hasIssues = issues != null && issues.Count > 0;
            _issueLabel.text = _hasIssues ? "\u26A0 " + string.Join("\n\u26A0 ", issues) : "";
            _issueLabel.style.display = _hasIssues ? DisplayStyle.Flex : DisplayStyle.None;
            tooltip = _hasIssues ? string.Join("\n", issues) : null;
            ApplyBorder();
            RefreshExpandedState();
        }

        // Play Mode display. Pass null to go back to edit look.
        public void SetRuntime(Color? border, string info)
        {
            _runtimeBorder = border;
            bool show = border.HasValue && !string.IsNullOrEmpty(info);
            _runtimeLabel.text = info ?? "";
            _runtimeLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (border.HasValue) _runtimeLabel.style.color = border.Value;
            ApplyBorder();
            RefreshExpandedState();
        }

        private void ApplyBorder()
        {
            Color? color = _runtimeBorder ?? (_hasIssues ? IssueColor : (Color?)null);
            StyleFloat width = color.HasValue ? 2f : new StyleFloat(StyleKeyword.Null);
            StyleColor c = color.HasValue ? color.Value : new StyleColor(StyleKeyword.Null);

            mainContainer.style.borderTopWidth = width;
            mainContainer.style.borderBottomWidth = width;
            mainContainer.style.borderLeftWidth = width;
            mainContainer.style.borderRightWidth = width;
            mainContainer.style.borderTopColor = c;
            mainContainer.style.borderBottomColor = c;
            mainContainer.style.borderLeftColor = c;
            mainContainer.style.borderRightColor = c;
        }

        // The graph view builds the whole right-click menu.
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
        }

        public abstract void RefreshContent();
    }

    public class QuestNodeView : QuestNodeViewBase
    {
        public QuestNode Data { get; }
        public QuestSO Quest => Data.quest;

        private readonly Label _infoLabel;

        public QuestNodeView(QuestNode data) : base(data.guid)
        {
            Data = data;
            style.minWidth = 180;

            Input = AddPort(Direction.Input, Port.Capacity.Single, Color.white);
            Output = AddPort(Direction.Output, Port.Capacity.Multi, Color.white);

            _infoLabel = MakeLabel("", 11, new Color(1f, 1f, 1f, 0.65f));
            extensionContainer.Insert(0, _infoLabel);

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RefreshContent();
            RefreshPorts();
            RefreshExpandedState();
        }

        public override void RefreshContent()
        {
            var quest = Quest;
            if (quest == null)
            {
                title = "(missing quest)";
                SetHeaderColor(Color.gray);
                _infoLabel.text = "";
                return;
            }

            title = quest.Title;
            SetHeaderColor(quest.Track == QuestTrack.Main ? MainColor : SupColor);

            var parts = new List<string> { quest.Track == QuestTrack.Main ? "MAIN" : "SUP" };
            parts.Add(quest.Objectives.Count == 1 ? "1 objective" : $"{quest.Objectives.Count} objectives");
            switch (quest.RepeatMode)
            {
                case RepeatMode.Immediate: parts.Add("\u21BB repeat"); break;
                case RepeatMode.OnSignal: parts.Add("\u21BB on signal"); break;
                case RepeatMode.RealTimeDaily: parts.Add($"\u21BB daily {quest.DailyResetHour:00}:00"); break;
            }
            if (quest.IsRepeatable && quest.MaxCompletions > 0) parts.Add($"max {quest.MaxCompletions}");
            _infoLabel.text = string.Join(" \u00B7 ", parts);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.clickCount != 2 || Quest == null) return;
            Selection.activeObject = Quest;
            EditorGUIUtility.PingObject(Quest);
            evt.StopPropagation();
        }
    }

    public class GateNodeView : QuestNodeViewBase
    {
        public GateNode Data { get; }

        private readonly Label _infoLabel;

        public GateNodeView(GateNode data) : base(data.guid)
        {
            Data = data;
            style.minWidth = 90;

            Input = AddPort(Direction.Input, Port.Capacity.Multi, Color.white);
            Output = AddPort(Direction.Output, Port.Capacity.Multi, Color.white);

            _infoLabel = MakeLabel("", 10, new Color(1f, 1f, 1f, 0.6f));
            extensionContainer.Insert(0, _infoLabel);

            RefreshContent();
            RefreshPorts();
            RefreshExpandedState();
        }

        public bool IsOr => Data.gateType == GateType.Or;

        public override void RefreshContent()
        {
            title = IsOr ? "OR" : "AND";
            SetHeaderColor(IsOr ? OrColor : AndColor);
            var titleLabel = titleContainer.Q<Label>("title-label");
            if (titleLabel != null) titleLabel.style.color = IsOr ? new Color(0.15f, 0.12f, 0.02f) : Color.white;

            _infoLabel.text = IsOr ? $"lost route: {Data.loserPolicy}" : "all inputs";
            Input.portColor = IsOr ? OrColor : AndColor;
        }
    }

    public class ResetNodeView : QuestNodeViewBase
    {
        public ResetNode Data { get; }

        private readonly Label _infoLabel;

        public ResetNodeView(ResetNode data) : base(data.guid)
        {
            Data = data;
            style.minWidth = 150;

            Input = AddPort(Direction.Input, Port.Capacity.Single, ResetColor);

            _infoLabel = MakeLabel("", 10, new Color(1f, 1f, 1f, 0.7f));
            _infoLabel.style.whiteSpace = WhiteSpace.Normal;
            _infoLabel.style.maxWidth = 200;
            extensionContainer.Insert(0, _infoLabel);

            RefreshContent();
            RefreshPorts();
            RefreshExpandedState();
        }

        public override void RefreshContent()
        {
            title = "\u21BB Reset";
            SetHeaderColor(ResetColor);

            var names = new List<string>();
            foreach (var t in Data.targets) names.Add(t != null ? t.Title : "(empty)");
            _infoLabel.text = names.Count == 0 ? "no targets" : "resets: " + string.Join(", ", names);
        }
    }
}

#endif
