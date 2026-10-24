using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yuki.Learning.StateMachine.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "New_State",
        menuName = "YUKI Learning State Machine/StateMachineList/State")]
    public class StateSO : ScriptableObject
    {
        [SerializeField]
        private StateActionSO[] _actions = Array.Empty<StateActionSO>();

        public State CreateState(StateMachine stateMachine)
        {
            var runtimeActions = new List<StateAction>();

            // action ของ state นี้ หลัง Variant สลับ / ลบ / เพิ่ม (ElementResolver.ActionsFor)
            foreach (StateActionSO action in stateMachine.Resolver.ActionsFor(this, _actions))
            {
                runtimeActions.Add(
                    stateMachine.Resolver.Resolve(action).CreateAction(stateMachine));
            }

            State state = new State(
                name,
                runtimeActions.ToArray(),
                stateMachine
            );

            return state;
        }
    }
}
