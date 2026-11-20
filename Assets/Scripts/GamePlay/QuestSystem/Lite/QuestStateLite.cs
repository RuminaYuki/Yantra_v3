using NUnit.Framework.Internal.Execution;
using UnityEngine;
using UnityEngine.Events;

public class QuestState : MonoBehaviour
{
    [Header("Objective")]
    [SerializeField,Min(1)] int objectiveCount = 1;

    private int currentCount = 0;

    [Header("Events")]
    public UnityEvent OnComplate;
    public UnityEvent<int> OnObjectiveUpdate;

    [ContextMenu("Repost")]
    public void Repost()
    {
        currentCount++;
        OnObjectiveUpdate?.Invoke(currentCount);

        if (currentCount >= objectiveCount)
        {
            OnComplate?.Invoke();
        }
    }
}
