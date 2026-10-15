using System;
using System.Collections.Generic;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem
{
    [Flags]
    public enum QuestStateMask
    {
        None = 0,
        // Bit n matches QuestState value n.
        Locked = 1,
        Active = 2,
        Completed = 4,
        Skipped = 8,
        Failed = 16
    }

    // Turns scene objects on/off from a quest's state, e.g. show the giant banana trees only while
    // "Seal the banana forest" is Active. Works after loading a save too, because it reads the state
    // instead of waiting for someone to switch it.
    // Put it on an object that starts ACTIVE in the scene (it may switch itself off later, that's fine).
    public class QuestStateBinding : MonoBehaviour
    {
        [SerializeField] private QuestSO quest;

        [Tooltip("Targets are ON while the quest is in any of these states.")]
        [SerializeField] private QuestStateMask activeWhen = QuestStateMask.Active;

        [Tooltip("Turn targets OFF in those states instead.")]
        [SerializeField] private bool invert;

        [SerializeField] private List<GameObject> targets = new List<GameObject>();

        private void Awake()
        {
            // Awake/OnDestroy (not OnEnable) so it keeps listening even if it switches itself off.
            QuestManager.QuestStateChanged += OnStateChanged;
            QuestManager.Restored += Refresh;
        }

        private void OnDestroy()
        {
            QuestManager.QuestStateChanged -= OnStateChanged;
            QuestManager.Restored -= Refresh;
        }

        private void Start() => Refresh();

        private void OnStateChanged(QuestSO changed, QuestState state)
        {
            if (changed == quest) Apply(state);
        }

        public void Refresh()
        {
            var manager = QuestManager.Instance;
            Apply(manager != null ? manager.GetState(quest) : QuestState.Locked);
        }

        private void Apply(QuestState state)
        {
            bool on = (activeWhen & (QuestStateMask)(1 << (int)state)) != 0;
            if (invert) on = !on;

            foreach (var target in targets)
            {
                if (target != null && target.activeSelf != on) target.SetActive(on);
            }
        }

        private void Reset()
        {
            // New component: control the children by default.
            targets.Clear();
            foreach (Transform child in transform) targets.Add(child.gameObject);
        }
    }
}
