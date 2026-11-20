using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class QuestObjectiveEventsLite : MonoBehaviour
{
    [Serializable]
    public class CountEvent
    {
        [Min(1)] public int count = 1;
        public UnityEvent onReached;
    }

    [Tooltip("Events fired when the objective count equals the given value.")]
    [SerializeField] private List<CountEvent> events = new List<CountEvent>();

    // Hook this up to QuestState.OnObjectiveUpdate (dynamic int)
    public void HandleObjectiveUpdate(int currentCount)
    {
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].count == currentCount)
            {
                events[i].onReached?.Invoke();
            }
        }
    }
}