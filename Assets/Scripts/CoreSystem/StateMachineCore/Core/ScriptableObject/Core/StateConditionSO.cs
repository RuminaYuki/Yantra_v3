using UnityEngine;

namespace Yuki.Learning.StateMachine.ScriptableObjects
{
    public abstract class StateConditionSO : StateElementSO
    {
        public Condition CreateCondition()
        {
            Condition condition = OnCreateCondition();
            condition.SelfAnchor = SelfAnchor;
            return condition;
        }
        protected abstract Condition OnCreateCondition();
    }
}