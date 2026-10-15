using System;
using System.Collections.Generic;

namespace SDFcl.GamePlay.QuestSystem
{
    // Live state of one quest. Owned by QuestManager.
    public class QuestRuntime
    {
        public readonly QuestSO Quest;
        public QuestState State = QuestState.Locked;
        public int CompletionCount;

        // True once the quest has signalled its outputs. Stays true while a repeatable quest
        // restarts, so quests after it do not lock again. Only a ResetNode clears it.
        public bool OutputLatched;

        public readonly int[] ObjectiveCounts;

        // RealTimeDaily: DateTime ticks (local) when it becomes Active again. 0 = not waiting.
        public long NextResetTicks;

        // OnSignal: completed and waiting for SignalNewCycle().
        public bool WaitingForSignal;

        public QuestRuntime(QuestSO quest)
        {
            Quest = quest;
            ObjectiveCounts = new int[quest.Objectives.Count];
        }

        public bool AllObjectivesDone
        {
            get
            {
                for (int i = 0; i < ObjectiveCounts.Length; i++)
                {
                    if (ObjectiveCounts[i] < Quest.Objectives[i].requiredCount) return false;
                }
                return true;
            }
        }

        public bool CanRepeatAgain =>
            Quest.IsRepeatable && (Quest.MaxCompletions == 0 || CompletionCount < Quest.MaxCompletions);

        public void ClearProgress() => Array.Clear(ObjectiveCounts, 0, ObjectiveCounts.Length);
    }

    // ---- Save data (plain classes, JsonUtility friendly) ----

    [Serializable]
    public class QuestRecord
    {
        public string questId;
        public QuestState state;
        public int completionCount;
        public bool outputLatched;
        public int[] objectiveCounts;
        public long nextResetTicks;
        public bool waitingForSignal;
    }

    [Serializable]
    public class QuestSaveData
    {
        public List<QuestRecord> quests = new List<QuestRecord>();
        public List<string> firedGates = new List<string>();   // OR gates that already applied their loser policy.
        public List<string> armedResets = new List<string>();  // Reset nodes that already fired for the current input.
    }
}
