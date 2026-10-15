using System;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem
{
    public enum QuestState
    {
        Locked,     // Unlock condition not met yet.
        Active,     // Player can work on it.
        Completed,  // Done (for repeatable quests: done for this round).
        Skipped,    // Closed because another OR route won.
        Failed      // Closed as a failure because another OR route won.
    }

    // Display only. Has no effect on unlock logic.
    public enum QuestTrack
    {
        Main,
        Sup
    }

    public enum RepeatMode
    {
        None,       // Complete once.
        Immediate,  // Becomes Active again right after completing.
        OnSignal,   // Waits until QuestManager.SignalNewCycle() or RestartQuest() is called (e.g. new in-game day).
        RealTimeDaily // Becomes Active again at the next reset hour on the real-world clock.
    }

    // What happens to unfinished quests on a route that lost an OR gate.
    public enum LoserPolicy
    {
        Keep,   // Still playable (optional content).
        Skip,   // Closed quietly.
        Fail    // Closed as failed (story can react to it).
    }

    // Per-quest override of the OR gate's LoserPolicy.
    public enum LoserPolicyOverride
    {
        UseGateSetting,
        Keep,
        Skip,
        Fail
    }

    [Serializable]
    public class ObjectiveDef
    {
        [Tooltip("Id used by QuestObjectiveReporter. Keep it unique inside this quest.")]
        public string id = "objective";

        [Tooltip("Text shown in the quest log.")]
        public string description;

        [Min(1)] public int requiredCount = 1;
    }
}
