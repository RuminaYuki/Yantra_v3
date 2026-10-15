using System;
using System.Collections.Generic;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem
{
    // Runs the quest graph: tracks every quest's state, unlocks quests when their input passes,
    // handles repeatable / daily quests, Reset nodes and OR loser policies.
    [DefaultExecutionOrder(-100)]
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        // Static so scene components can subscribe before the manager wakes up.
        public static event Action<QuestSO, QuestState> QuestStateChanged;
        public static event Action<QuestSO, int, int> ObjectiveProgressed;   // quest, objective index, new count
        public static event Action<QuestSO, int> QuestCompleted;             // quest, completion count
        public static event Action<QuestSO> QuestRestarted;                  // repeatable quest became Active again
        public static event Action Restored;                                 // states replaced from a save file

        // Set by the save system before the scene starts. Returns null for a new game.
        public static Func<QuestSaveData> LoadProvider;

        [SerializeField] private QuestGraphSO graph;
        [Tooltip("Seconds between real-time daily reset checks.")]
        [SerializeField, Min(0.5f)] private float dailyCheckInterval = 5f;
        [SerializeField] private bool logChanges;

        private const int MaxEvaluatePasses = 64;
        private const int MaxDepth = 64;

        private readonly Dictionary<QuestSO, QuestRuntime> _byQuest = new Dictionary<QuestSO, QuestRuntime>();
        private readonly Dictionary<string, QuestRuntime> _byQuestId = new Dictionary<string, QuestRuntime>();
        private readonly Dictionary<string, QuestRuntime> _byNode = new Dictionary<string, QuestRuntime>();
        private readonly HashSet<string> _firedGates = new HashSet<string>();
        private readonly HashSet<string> _armedResets = new HashSet<string>();

        private bool _evaluating;
        private bool _evaluateAgain;
        private bool _started;
        private float _nextDailyCheck;

        public QuestGraphSO Graph => graph;
        public bool IsReady => _started;

        // ------------------------------------------------------------------ Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Quest] More than one QuestManager in the scene. Destroying the new one.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            BuildRuntime();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            var data = LoadProvider?.Invoke();
            if (data != null) Restore(data);
            else Evaluate();
            _started = true;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextDailyCheck) return;
            _nextDailyCheck = Time.unscaledTime + dailyCheckInterval;
            CheckDailyResets(DateTime.Now);
        }

        private void BuildRuntime()
        {
            _byQuest.Clear();
            _byQuestId.Clear();
            _byNode.Clear();
            if (graph == null)
            {
                Debug.LogError("[Quest] QuestManager has no QuestGraphSO assigned.", this);
                return;
            }

            graph.BuildLookup();
            foreach (var node in graph.Nodes)
            {
                if (!(node is QuestNode qn) || qn.quest == null) continue;
                if (_byQuest.ContainsKey(qn.quest))
                {
                    Debug.LogError($"[Quest] '{qn.quest.Title}' is used by more than one node. Only the first is used.", this);
                    continue;
                }
                var rt = new QuestRuntime(qn.quest);
                _byQuest[qn.quest] = rt;
                _byNode[qn.guid] = rt;
                if (!string.IsNullOrEmpty(qn.quest.QuestId)) _byQuestId[qn.quest.QuestId] = rt;
            }
        }

        // ------------------------------------------------------------------ Queries

        public QuestState GetState(QuestSO quest) =>
            quest != null && _byQuest.TryGetValue(quest, out var rt) ? rt.State : QuestState.Locked;

        public int GetCompletionCount(QuestSO quest) =>
            quest != null && _byQuest.TryGetValue(quest, out var rt) ? rt.CompletionCount : 0;

        public int GetObjectiveCount(QuestSO quest, int objectiveIndex)
        {
            if (quest == null || !_byQuest.TryGetValue(quest, out var rt)) return 0;
            return objectiveIndex >= 0 && objectiveIndex < rt.ObjectiveCounts.Length ? rt.ObjectiveCounts[objectiveIndex] : 0;
        }

        // Real-time daily quests: when they become Active again (null if not waiting).
        public DateTime? GetNextReset(QuestSO quest) =>
            quest != null && _byQuest.TryGetValue(quest, out var rt) && rt.NextResetTicks > 0
                ? new DateTime(rt.NextResetTicks)
                : (DateTime?)null;

        public IEnumerable<QuestSO> GetQuestsInState(QuestState state)
        {
            foreach (var rt in _byQuest.Values)
            {
                if (rt.State == state) yield return rt.Quest;
            }
        }

        // True if the quest has sent its "done" signal (stays true while a repeatable quest restarts).
        public bool HasPassed(QuestSO quest) =>
            quest != null && _byQuest.TryGetValue(quest, out var rt) && rt.OutputLatched;

        public bool IsWaitingForSignal(QuestSO quest) =>
            quest != null && _byQuest.TryGetValue(quest, out var rt) && rt.WaitingForSignal;

        // Graph editor / UI: does this node (quest or gate) currently pass its output?
        public bool IsNodePassed(string nodeGuid) => graph != null && IsSatisfied(nodeGuid, 0);

        // ------------------------------------------------------------------ Progress API

        // Adds progress to one objective of an Active quest. Completes the quest when all objectives are done.
        public void ReportProgress(QuestSO quest, string objectiveId, int amount = 1)
        {
            if (!TryGetActive(quest, out var rt)) return;

            int index = quest.FindObjectiveIndex(objectiveId);
            if (index < 0)
            {
                Debug.LogWarning($"[Quest] '{quest.Title}' has no objective '{objectiveId}'.", this);
                return;
            }

            int required = quest.Objectives[index].requiredCount;
            int newCount = Mathf.Clamp(rt.ObjectiveCounts[index] + amount, 0, required);
            if (newCount == rt.ObjectiveCounts[index]) return;

            rt.ObjectiveCounts[index] = newCount;
            ObjectiveProgressed?.Invoke(quest, index, newCount);

            if (rt.AllObjectivesDone) Complete(rt);
        }

        // Completes an Active quest directly (quests without objectives, cutscenes, debug).
        public void CompleteQuest(QuestSO quest)
        {
            if (TryGetActive(quest, out var rt)) Complete(rt);
        }

        // Makes a completed repeatable quest Active again now (e.g. talked to the NPC again).
        public void RestartQuest(QuestSO quest)
        {
            if (quest == null || !_byQuest.TryGetValue(quest, out var rt)) return;
            if (rt.State != QuestState.Completed || !rt.CanRepeatAgain) return;
            Restart(rt);
        }

        // Restarts every OnSignal quest that is waiting (call this when a new in-game day starts).
        public void SignalNewCycle()
        {
            foreach (var rt in _byQuest.Values)
            {
                if (rt.WaitingForSignal && rt.State == QuestState.Completed) Restart(rt);
            }
        }

        private bool TryGetActive(QuestSO quest, out QuestRuntime rt)
        {
            rt = null;
            if (quest == null) return false;
            if (!_byQuest.TryGetValue(quest, out rt))
            {
                Debug.LogWarning($"[Quest] '{quest.Title}' is not in graph '{(graph ? graph.name : "null")}'.", this);
                return false;
            }
            return rt.State == QuestState.Active;
        }

        // ------------------------------------------------------------------ State changes

        private void SetState(QuestRuntime rt, QuestState state)
        {
            if (rt.State == state) return;
            rt.State = state;
            if (logChanges) Debug.Log($"[Quest] {rt.Quest.Title} -> {state}", this);
            QuestStateChanged?.Invoke(rt.Quest, state);
        }

        private void Complete(QuestRuntime rt)
        {
            rt.CompletionCount++;
            rt.OutputLatched = true;
            SetState(rt, QuestState.Completed);
            QuestCompleted?.Invoke(rt.Quest, rt.CompletionCount);

            if (rt.CanRepeatAgain)
            {
                switch (rt.Quest.RepeatMode)
                {
                    case RepeatMode.Immediate:
                        Restart(rt);
                        break;
                    case RepeatMode.OnSignal:
                        rt.WaitingForSignal = true;
                        break;
                    case RepeatMode.RealTimeDaily:
                        rt.NextResetTicks = NextDailyReset(DateTime.Now, rt.Quest.DailyResetHour).Ticks;
                        break;
                }
            }

            Evaluate();
        }

        private void Restart(QuestRuntime rt)
        {
            rt.ClearProgress();
            rt.NextResetTicks = 0;
            rt.WaitingForSignal = false;
            SetState(rt, QuestState.Active);
            QuestRestarted?.Invoke(rt.Quest);
        }

        private void Close(QuestRuntime rt, QuestState closedState)
        {
            rt.NextResetTicks = 0;
            rt.WaitingForSignal = false;
            SetState(rt, closedState);
        }

        public static DateTime NextDailyReset(DateTime now, int hour)
        {
            var todayReset = now.Date.AddHours(hour);
            return now < todayReset ? todayReset : todayReset.AddDays(1);
        }

        // Public so it can be tested / forced from debug tools with a fake clock.
        public void CheckDailyResets(DateTime now)
        {
            foreach (var rt in _byQuest.Values)
            {
                if (rt.NextResetTicks > 0 && rt.State == QuestState.Completed && now.Ticks >= rt.NextResetTicks)
                    Restart(rt);
            }
        }

        // ------------------------------------------------------------------ Graph evaluation

        // Unlocks quests, applies OR loser policies and fires Reset nodes until nothing changes.
        // Safe to call from inside quest events: it just runs another pass.
        public void Evaluate()
        {
            if (graph == null) return;
            if (_evaluating)
            {
                _evaluateAgain = true;
                return;
            }

            _evaluating = true;
            try
            {
                for (int pass = 0; pass < MaxEvaluatePasses; pass++)
                {
                    _evaluateAgain = false;
                    bool changed = false;

                    foreach (var node in graph.Nodes)
                    {
                        switch (node)
                        {
                            case QuestNode qn:
                                changed |= EvaluateQuestNode(qn);
                                break;
                            case GateNode gate when gate.gateType == GateType.Or:
                                changed |= EvaluateOrGate(gate);
                                break;
                            case ResetNode reset:
                                changed |= EvaluateResetNode(reset);
                                break;
                        }
                    }

                    if (!changed && !_evaluateAgain) return;
                }
                Debug.LogWarning("[Quest] Evaluate hit the pass limit. Check the graph for a Reset loop that fires forever.", this);
            }
            finally
            {
                _evaluating = false;
            }
        }

        private bool EvaluateQuestNode(QuestNode qn)
        {
            if (!_byNode.TryGetValue(qn.guid, out var rt) || rt.State != QuestState.Locked) return false;

            var inputs = graph.GetInputs(qn.guid);
            bool open = inputs.Count == 0 || IsSatisfied(inputs[0], 0);
            if (!open) return false;

            SetState(rt, QuestState.Active);
            return true;
        }

        private bool EvaluateOrGate(GateNode gate)
        {
            bool satisfied = IsSatisfied(gate.guid, 0);
            if (satisfied && _firedGates.Add(gate.guid))
            {
                ApplyLoserPolicy(gate);
                return true;
            }
            // Re-arm when a Reset node made it unsatisfied again (looping chains).
            if (!satisfied) _firedGates.Remove(gate.guid);
            return false;
        }

        private bool EvaluateResetNode(ResetNode reset)
        {
            var inputs = graph.GetInputs(reset.guid);
            bool satisfied = inputs.Count > 0 && IsSatisfied(inputs[0], 0);

            if (!satisfied)
            {
                _armedResets.Remove(reset.guid);
                return false;
            }
            if (!_armedResets.Add(reset.guid)) return false; // Already fired for this input.

            foreach (var quest in reset.targets)
            {
                if (quest == null || !_byQuest.TryGetValue(quest, out var rt)) continue;
                rt.OutputLatched = false;
                rt.NextResetTicks = 0;
                rt.WaitingForSignal = false;
                if (reset.clearProgress) rt.ClearProgress();
                SetState(rt, QuestState.Locked);
            }
            if (logChanges) Debug.Log($"[Quest] Reset node fired ({reset.targets.Count} quests).", this);
            return true;
        }

        // Does this node currently send a "passed" signal on its output?
        private bool IsSatisfied(string guid, int depth)
        {
            if (depth > MaxDepth)
            {
                Debug.LogError("[Quest] Graph is too deep or has a cycle. Use a Reset node to loop instead.", this);
                return false;
            }

            switch (graph.GetNode(guid))
            {
                case QuestNode _:
                    return _byNode.TryGetValue(guid, out var rt) && rt.OutputLatched;

                case GateNode gate:
                    var inputs = graph.GetInputs(guid);
                    if (inputs.Count == 0) return false;
                    if (gate.gateType == GateType.And)
                    {
                        foreach (var i in inputs) if (!IsSatisfied(i, depth + 1)) return false;
                        return true;
                    }
                    foreach (var i in inputs) if (IsSatisfied(i, depth + 1)) return true;
                    return false;

                default:
                    return false; // Reset nodes have no output.
            }
        }

        // Closes unfinished quests that only lead into the OR gate's losing inputs.
        private void ApplyLoserPolicy(GateNode gate)
        {
            var losingInputs = new HashSet<string>();
            foreach (var input in graph.GetInputs(gate.guid))
            {
                if (!IsSatisfied(input, 0)) losingInputs.Add(input);
            }
            if (losingInputs.Count == 0) return;

            // Every node upstream of a losing input.
            var candidates = new HashSet<string>();
            var stack = new Stack<string>(losingInputs);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (!candidates.Add(n)) continue;
                foreach (var i in graph.GetInputs(n)) stack.Push(i);
            }

            // Keep only nodes whose every output ends up in the losing part (fixed point).
            var losing = new HashSet<string>();
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var n in candidates)
                {
                    if (losing.Contains(n)) continue;
                    bool onlyLosing = true;
                    foreach (var o in graph.GetOutputs(n))
                    {
                        bool ok = losing.Contains(o) || (o == gate.guid && losingInputs.Contains(n));
                        if (!ok) { onlyLosing = false; break; }
                    }
                    if (onlyLosing && graph.GetOutputs(n).Count > 0)
                    {
                        losing.Add(n);
                        grew = true;
                    }
                }
            }

            foreach (var n in losing)
            {
                if (!_byNode.TryGetValue(n, out var rt)) continue;
                if (rt.State != QuestState.Locked && rt.State != QuestState.Active) continue;

                var policy = ResolvePolicy(rt.Quest.WhenRouteLost, gate.loserPolicy);
                if (policy == LoserPolicy.Skip) Close(rt, QuestState.Skipped);
                else if (policy == LoserPolicy.Fail) Close(rt, QuestState.Failed);
            }
        }

        private static LoserPolicy ResolvePolicy(LoserPolicyOverride questSetting, LoserPolicy gateSetting)
        {
            switch (questSetting)
            {
                case LoserPolicyOverride.Keep: return LoserPolicy.Keep;
                case LoserPolicyOverride.Skip: return LoserPolicy.Skip;
                case LoserPolicyOverride.Fail: return LoserPolicy.Fail;
                default: return gateSetting;
            }
        }

        // ------------------------------------------------------------------ Save / Load

        public QuestSaveData Capture()
        {
            var data = new QuestSaveData();
            foreach (var rt in _byQuest.Values)
            {
                data.quests.Add(new QuestRecord
                {
                    questId = rt.Quest.QuestId,
                    state = rt.State,
                    completionCount = rt.CompletionCount,
                    outputLatched = rt.OutputLatched,
                    objectiveCounts = (int[])rt.ObjectiveCounts.Clone(),
                    nextResetTicks = rt.NextResetTicks,
                    waitingForSignal = rt.WaitingForSignal
                });
            }
            data.firedGates.AddRange(_firedGates);
            data.armedResets.AddRange(_armedResets);
            return data;
        }

        // Replaces all states without firing per-quest events (so cutscenes don't replay),
        // then fires Restored and evaluates once for quests added to the graph after the save.
        public void Restore(QuestSaveData data)
        {
            if (data == null) return;

            foreach (var record in data.quests)
            {
                if (string.IsNullOrEmpty(record.questId) || !_byQuestId.TryGetValue(record.questId, out var rt)) continue;
                rt.State = record.state;
                rt.CompletionCount = record.completionCount;
                rt.OutputLatched = record.outputLatched;
                rt.NextResetTicks = record.nextResetTicks;
                rt.WaitingForSignal = record.waitingForSignal;
                rt.ClearProgress();
                if (record.objectiveCounts != null)
                {
                    int n = Mathf.Min(record.objectiveCounts.Length, rt.ObjectiveCounts.Length);
                    Array.Copy(record.objectiveCounts, rt.ObjectiveCounts, n);
                }
            }

            _firedGates.Clear();
            _firedGates.UnionWith(data.firedGates);
            _armedResets.Clear();
            _armedResets.UnionWith(data.armedResets);

            Restored?.Invoke();
            Evaluate();
            CheckDailyResets(DateTime.Now);
        }

        // ------------------------------------------------------------------ Debug

        [ContextMenu("Debug/Log All Quests")]
        private void DebugLogAll()
        {
            foreach (var rt in _byQuest.Values)
                Debug.Log($"[Quest] {rt.Quest.Title}: {rt.State} (x{rt.CompletionCount}, latched {rt.OutputLatched})", this);
        }

        [ContextMenu("Debug/Complete All Active")]
        private void DebugCompleteAllActive()
        {
            var active = new List<QuestSO>(GetQuestsInState(QuestState.Active));
            foreach (var q in active) CompleteQuest(q);
        }

        [ContextMenu("Debug/Signal New Cycle")]
        private void DebugSignalNewCycle() => SignalNewCycle();

        [ContextMenu("Debug/Force Daily Reset")]
        private void DebugForceDaily() => CheckDailyResets(DateTime.MaxValue);
    }
}
