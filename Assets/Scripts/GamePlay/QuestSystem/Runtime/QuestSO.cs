using System.Collections.Generic;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem
{
    // One quest. Lives as a sub-asset inside a QuestGraphSO (created by the Quest Graph editor),
    // so scene components can still reference it directly.
    public class QuestSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id written to save files. Generated automatically, do not change after release.")]
        [SerializeField] private string questId;
        [SerializeField] private string title = "New Quest";
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private QuestTrack track = QuestTrack.Main;

        [Header("Objectives")]
        [Tooltip("All objectives done -> quest completed. Leave empty to complete it from code / events only.")]
        [SerializeField] private List<ObjectiveDef> objectives = new List<ObjectiveDef>();

        [Header("Repeat")]
        [SerializeField] private RepeatMode repeatMode = RepeatMode.None;
        [Tooltip("Total completions allowed. 0 = unlimited. Ignored when Repeat Mode is None.")]
        [SerializeField, Min(0)] private int maxCompletions;
        [Tooltip("RealTimeDaily only: local hour (0-23) when the quest becomes available again.")]
        [SerializeField, Range(0, 23)] private int dailyResetHour = 4;

        [Header("OR Routes")]
        [Tooltip("What happens to this quest if it is unfinished on a route that lost an OR gate.")]
        [SerializeField] private LoserPolicyOverride whenRouteLost = LoserPolicyOverride.UseGateSetting;

        public string QuestId => questId;
        public string Title => title;
        public string Description => description;
        public QuestTrack Track => track;
        public IReadOnlyList<ObjectiveDef> Objectives => objectives;
        public RepeatMode RepeatMode => repeatMode;
        public int MaxCompletions => maxCompletions;
        public int DailyResetHour => dailyResetHour;
        public LoserPolicyOverride WhenRouteLost => whenRouteLost;

        public bool IsRepeatable => repeatMode != RepeatMode.None;

        public int FindObjectiveIndex(string objectiveId)
        {
            for (int i = 0; i < objectives.Count; i++)
            {
                if (objectives[i].id == objectiveId) return i;
            }
            return -1;
        }

#if UNITY_EDITOR
        // Used by the graph editor.
        public void EditorInit(string newTitle, QuestTrack newTrack)
        {
            title = newTitle;
            track = newTrack;
            EnsureId();
        }

        private void OnValidate() => EnsureId();

        private void EnsureId()
        {
            if (string.IsNullOrEmpty(questId))
            {
                questId = System.Guid.NewGuid().ToString("N");
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
