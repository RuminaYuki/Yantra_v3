using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem
{
    // Quest log for the player. Lists Active quests with objective counters, upcoming quests with
    // their unlock routes ("Route 1 / or / Route 2"), and optionally finished ones.
    // Open / close it from your own UI code with Show(), Hide() or Toggle().
    // Labels are fields so they can be written in Thai in the Inspector.
    public class QuestLogUI : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Panel shown / hidden by Show() and Hide(). Leave empty to keep it always visible (e.g. a HUD tracker).")]
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform mainList;
        [Tooltip("Can be the same as Main List.")]
        [SerializeField] private RectTransform supList;
        [SerializeField] private QuestLogEntryUI entryPrefab;

        [Header("What to show")]
        [SerializeField] private bool showMain = true;
        [SerializeField] private bool showSup = true;
        [Tooltip("Show locked quests whose routes are in progress, with the routes that open them.")]
        [SerializeField] private bool showUpcoming = true;
        [SerializeField] private bool showCompleted;
        [Tooltip("Show repeatable quests that wait for their next round, with a timer for daily quests.")]
        [SerializeField] private bool showWaitingRepeats = true;

        [Header("Labels")]
        [SerializeField] private string routeLabel = "Route {0}";
        [SerializeField] private string orLabel = "\u2014 or \u2014";
        [SerializeField] private string upcomingPrefix = "Upcoming: ";
        [SerializeField] private string completedSuffix = " (done)";
        [SerializeField] private string availableInLabel = "Available again in {0}";
        [SerializeField] private string waitingLabel = "Available again later";
        [SerializeField] private string doneColor = "#7fd39a";
        [SerializeField] private string lostColor = "#7d848e";

        private readonly List<QuestLogEntryUI> _entries = new List<QuestLogEntryUI>();
        private bool _dirty = true;
        private float _nextTimerRefresh;

        public bool IsOpen => panel == null || panel.activeSelf;

        private void Awake()
        {
            QuestManager.QuestStateChanged += OnStateChanged;
            QuestManager.ObjectiveProgressed += OnProgress;
            QuestManager.Restored += MarkDirty;
        }

        private void OnDestroy()
        {
            QuestManager.QuestStateChanged -= OnStateChanged;
            QuestManager.ObjectiveProgressed -= OnProgress;
            QuestManager.Restored -= MarkDirty;
        }

        private void OnStateChanged(QuestSO q, QuestState s) => _dirty = true;
        private void OnProgress(QuestSO q, int i, int c) => _dirty = true;
        public void MarkDirty() => _dirty = true;

        public void Show()
        {
            if (panel != null) panel.SetActive(true);
            _dirty = true;
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        private void LateUpdate()
        {
            if (!IsOpen) return;

            // Daily timers count down without events.
            if (showWaitingRepeats && Time.unscaledTime >= _nextTimerRefresh)
            {
                _nextTimerRefresh = Time.unscaledTime + 30f;
                _dirty = true;
            }

            if (!_dirty) return;
            _dirty = false;
            Rebuild();
        }

        public void Rebuild()
        {
            foreach (var e in _entries)
            {
                if (e != null) Destroy(e.gameObject);
            }
            _entries.Clear();

            var manager = QuestManager.Instance;
            if (manager == null || manager.Graph == null || entryPrefab == null) return;

            foreach (var node in manager.Graph.Nodes)
            {
                if (!(node is QuestNode qn) || qn.quest == null) continue;
                var quest = qn.quest;
                if (quest.Track == QuestTrack.Main && !showMain) continue;
                if (quest.Track == QuestTrack.Sup && !showSup) continue;

                var list = quest.Track == QuestTrack.Main || supList == null ? mainList : supList;
                if (list == null) continue;

                switch (manager.GetState(quest))
                {
                    case QuestState.Active:
                        AddEntry(list, quest.Title, BuildActiveBody(manager, quest), false);
                        break;

                    case QuestState.Locked when showUpcoming:
                        string routes = BuildRoutesBody(manager, quest);
                        if (routes != null) AddEntry(list, upcomingPrefix + quest.Title, routes, true);
                        break;

                    case QuestState.Completed:
                        if (showWaitingRepeats && quest.IsRepeatable && IsWaitingForRepeat(manager, quest))
                            AddEntry(list, quest.Title, BuildWaitingBody(manager, quest), true);
                        else if (showCompleted)
                            AddEntry(list, quest.Title + completedSuffix, null, true);
                        break;
                }
            }
        }

        private void AddEntry(RectTransform list, string title, string body, bool dimmed)
        {
            var entry = Instantiate(entryPrefab, list);
            entry.Set(title, body, dimmed);
            _entries.Add(entry);
        }

        private string BuildActiveBody(QuestManager manager, QuestSO quest)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(quest.Description)) sb.AppendLine(quest.Description);

            for (int i = 0; i < quest.Objectives.Count; i++)
            {
                var o = quest.Objectives[i];
                int count = manager.GetObjectiveCount(quest, i);
                string text = string.IsNullOrEmpty(o.description) ? o.id : o.description;
                string counter = o.requiredCount > 1 ? $" {count}/{o.requiredCount}" : "";
                if (count >= o.requiredCount) sb.AppendLine($"<color={doneColor}><s>\u2022 {text}{counter}</s></color>");
                else sb.AppendLine($"\u2022 {text}{counter}");
            }
            return sb.ToString().TrimEnd();
        }

        // Null when no route has started yet (nothing to hint at).
        private string BuildRoutesBody(QuestManager manager, QuestSO quest)
        {
            var routes = QuestRouteUtility.GetUnlockRoutes(manager.Graph, quest);
            if (routes.Count == 0) return null;

            bool anyStarted = false;
            foreach (var route in routes)
                foreach (var q in route)
                    if (manager.GetState(q) != QuestState.Locked) anyStarted = true;
            if (!anyStarted) return null;

            var sb = new StringBuilder();
            for (int r = 0; r < routes.Count; r++)
            {
                bool lost = false;
                var parts = new List<string>();
                foreach (var q in routes[r])
                {
                    var s = manager.GetState(q);
                    if (s == QuestState.Skipped || s == QuestState.Failed) lost = true;
                    parts.Add(manager.HasPassed(q) ? $"<color={doneColor}>\u2713 {q.Title}</color>" : q.Title);
                }

                string line = (routes.Count > 1 ? string.Format(routeLabel, r + 1) + ": " : "") + string.Join(" + ", parts);
                sb.AppendLine(lost ? $"<color={lostColor}><s>{StripColor(line)}</s></color>" : line);
                if (r < routes.Count - 1) sb.AppendLine(orLabel);
            }
            return sb.ToString().TrimEnd();
        }

        private static bool IsWaitingForRepeat(QuestManager manager, QuestSO quest)
        {
            if (manager.IsWaitingForSignal(quest) || manager.GetNextReset(quest).HasValue) return true;
            return false;
        }

        private string BuildWaitingBody(QuestManager manager, QuestSO quest)
        {
            var next = manager.GetNextReset(quest);
            if (!next.HasValue) return waitingLabel;

            TimeSpan left = next.Value - DateTime.Now;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            string time = left.TotalHours >= 1 ? $"{(int)left.TotalHours}h {left.Minutes}m" : $"{Math.Max(1, left.Minutes)}m";
            return string.Format(availableInLabel, time);
        }

        private string StripColor(string text) =>
            text.Replace($"<color={doneColor}>", "").Replace("</color>", "");
    }
}
