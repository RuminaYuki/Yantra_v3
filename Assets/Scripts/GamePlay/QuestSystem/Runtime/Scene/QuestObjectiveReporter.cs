using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem
{
    // Sends objective progress to the QuestManager. Hook Report() to an existing UnityEvent,
    // e.g. InteractorEvent.OnInteract or TriggerZoneEvents.onEnter, so no other script needs changing.
    public class QuestObjectiveReporter : MonoBehaviour
    {
        [SerializeField] private QuestSO quest;
        [Tooltip("Objective id from the QuestSO.")]
        [SerializeField] private string objectiveId;
        [SerializeField, Min(1)] private int amount = 1;

        [Tooltip("Count this object only once per quest round (e.g. each banana tree counts once). " +
                 "Resets when the quest becomes Active again.")]
        [SerializeField] private bool oncePerRound = true;

        private bool _reported;

        public QuestSO Quest => quest;
        public string ObjectiveId => objectiveId;

        private void Awake() => QuestManager.QuestStateChanged += OnStateChanged;
        private void OnDestroy() => QuestManager.QuestStateChanged -= OnStateChanged;

        private void OnStateChanged(QuestSO changed, QuestState state)
        {
            if (changed == quest && state == QuestState.Active) _reported = false;
        }

        // For UnityEvent with no parameter.
        public void Report()
        {
            var manager = QuestManager.Instance;
            if (manager == null || quest == null) return;
            if (oncePerRound && _reported) return;
            if (manager.GetState(quest) != QuestState.Active) return;

            _reported = true;
            manager.ReportProgress(quest, objectiveId, amount);
        }

        // For UnityEvent<GameObject> (InteractorEvent, TriggerZoneEvents).
        public void Report(GameObject who) => Report();

        // Finishes the quest directly (quests without objectives, e.g. after a cutscene).
        public void CompleteQuest()
        {
            var manager = QuestManager.Instance;
            if (manager != null && quest != null) manager.CompleteQuest(quest);
        }

        public void CompleteQuest(GameObject who) => CompleteQuest();

        private void OnValidate()
        {
            if (quest != null && !string.IsNullOrEmpty(objectiveId) && quest.FindObjectiveIndex(objectiveId) < 0)
                Debug.LogWarning($"[Quest] '{quest.Title}' has no objective '{objectiveId}'.", this);
        }
    }
}
