using System;
using System.Collections;
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
    // For prefabs spawned later (async loading), put this component inside the prefab:
    // its Start() reads the current state, so no delay is needed.
    public class QuestStateBinding : MonoBehaviour
    {
        [SerializeField] private QuestSO quest;

        [Tooltip("Targets are ON while the quest is in any of these states.")]
        [SerializeField] private QuestStateMask activeWhen = QuestStateMask.Active;

        [Tooltip("Turn targets OFF in those states instead.")]
        [SerializeField] private bool invert;

        [Header("Delay (during play only)")]
        [Tooltip("Seconds before targets turn ON. With Active When = Active this is when the quest starts. 0 = instant.")]
        [SerializeField, Min(0f)] private float turnOnDelay = 0f;

        [Tooltip("Seconds before targets turn OFF. With Active When = Active this is when the quest ends. 0 = instant.")]
        [SerializeField, Min(0f)] private float turnOffDelay = 0f;

        [Tooltip("Ignore Time.timeScale, so delays also run while the game is paused.")]
        [SerializeField] private bool useRealtime = true;

        [SerializeField] private List<GameObject> targets = new List<GameObject>();

        private Coroutine applyCoroutine;

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
            if (changed == quest) Apply(state, true);
        }

        // Instant update from the current state (scene start, save load, after spawning targets).
        public void Refresh()
        {
            var manager = QuestManager.Instance;
            Apply(manager != null ? manager.GetState(quest) : QuestState.Locked, false);
        }

        private void Apply(QuestState state, bool useDelay)
        {
            // A newer state always wins over a pending delayed one.
            if (applyCoroutine != null)
            {
                StopCoroutine(applyCoroutine);
                applyCoroutine = null;
            }

            bool on = (activeWhen & (QuestStateMask)(1 << (int)state)) != 0;
            if (invert) on = !on;

            float delay = useDelay ? (on ? turnOnDelay : turnOffDelay) : 0f;

            // Coroutines can't start on an inactive object, so fall back to instant.
            if (delay > 0f && isActiveAndEnabled)
                applyCoroutine = StartCoroutine(ApplyAfterDelay(on, delay));
            else
                SetTargets(on);
        }

        private IEnumerator ApplyAfterDelay(bool on, float delay)
        {
            if (useRealtime) yield return new WaitForSecondsRealtime(delay);
            else yield return new WaitForSeconds(delay);

            applyCoroutine = null;
            SetTargets(on);
        }

        private void SetTargets(bool on)
        {
            foreach (var target in targets)
            {
                if (target != null && target.activeSelf != on) target.SetActive(on);
            }
        }

        private void OnDisable()
        {
            // Unity stops coroutines on disable; apply the pending result now so it isn't lost.
            if (applyCoroutine != null)
            {
                applyCoroutine = null;
                Refresh();
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