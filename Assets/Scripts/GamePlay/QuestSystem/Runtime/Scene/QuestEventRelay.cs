using UnityEngine;
using UnityEngine.Events;

namespace SDFcl.GamePlay.QuestSystem
{
    // One-shot reactions to a quest: play a cutscene, give an item, play a sound...
    // These do NOT fire when a save is loaded, so cutscenes don't play again.
    // For things that should simply be on/off with the quest, use QuestStateBinding instead.
    public class QuestEventRelay : MonoBehaviour
    {
        [SerializeField] private QuestSO quest;

        [Tooltip("Quest became Active (first time, after a Reset node, and after each repeat).")]
        [SerializeField] private UnityEvent onActivated;

        [Tooltip("Quest completed. Fires every round for repeatable quests. Int = completion count.")]
        [SerializeField] private UnityEvent<int> onCompleted;

        [Tooltip("Repeatable quest became Active again.")]
        [SerializeField] private UnityEvent onRestarted;

        [Tooltip("Closed because another OR route won (policy Skip).")]
        [SerializeField] private UnityEvent onSkipped;

        [Tooltip("Closed as failed because another OR route won (policy Fail).")]
        [SerializeField] private UnityEvent onFailed;

        private void Awake()
        {
            QuestManager.QuestStateChanged += OnStateChanged;
            QuestManager.QuestCompleted += OnCompleted;
            QuestManager.QuestRestarted += OnRestarted;
        }

        private void OnDestroy()
        {
            QuestManager.QuestStateChanged -= OnStateChanged;
            QuestManager.QuestCompleted -= OnCompleted;
            QuestManager.QuestRestarted -= OnRestarted;
        }

        private void OnStateChanged(QuestSO changed, QuestState state)
        {
            if (changed != quest) return;
            switch (state)
            {
                case QuestState.Active: onActivated?.Invoke(); break;
                case QuestState.Skipped: onSkipped?.Invoke(); break;
                case QuestState.Failed: onFailed?.Invoke(); break;
            }
        }

        private void OnCompleted(QuestSO changed, int count)
        {
            if (changed == quest) onCompleted?.Invoke(count);
        }

        private void OnRestarted(QuestSO changed)
        {
            if (changed == quest) onRestarted?.Invoke();
        }
    }
}
