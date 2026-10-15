using UnityEngine;

namespace Yuki.Learning.StateMachine.ScriptableObjects
{
    public abstract class StateActionSO : StateElementSO
    {
        public StateAction CreateAction(StateMachine stateMachine)
        {
            StateAction action = OnCreateAction(stateMachine);
            action.SelfAnchor = SelfAnchor;
            return action;
        }
        protected abstract StateAction OnCreateAction(StateMachine stateMachine);
    }
}