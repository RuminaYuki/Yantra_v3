using UnityEngine;

namespace Yuki.Learning.StateMachine.ScriptableObjects
{
    public abstract class StateActionSO : ScriptableObject
    {
        public abstract StateAction CreateAction(
            StateMachine stateMachine);
    }
}