using UnityEngine;

namespace Yuki.Learning.StateMachine.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "NewState",
        menuName = "YUKI Learning State Machine/StateMachine/State")]
    public class StateSO : ScriptableObject
    {
        [SerializeField]
        private StateActionSO[] _actions;

        public State CreateState(StateMachine stateMachine)
        {
            StateAction[] runtimeActions =
                new StateAction[_actions.Length];

            for (int i = 0; i < _actions.Length; i++)
            {
                runtimeActions[i] =
                    _actions[i].CreateAction(stateMachine);
            }

            State state = new State(
                name,
                runtimeActions,
                stateMachine
            );

            return state;
        }
    }
}
