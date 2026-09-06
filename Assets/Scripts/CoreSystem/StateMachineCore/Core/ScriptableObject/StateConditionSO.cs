using UnityEngine;

namespace Yuki.Learning.StateMachine.ScriptableObjects
{
    public abstract class StateConditionSO : ScriptableObject
    {
        public abstract Condition CreateCondition();
    }
}