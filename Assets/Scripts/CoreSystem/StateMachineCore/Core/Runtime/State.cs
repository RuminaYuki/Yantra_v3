namespace Yuki.Learning.StateMachine
{
    public class State
    {
        private readonly StateAction[] _actions;
        private StateTransition[] _transitions;

        public string DebugName { get; }

        public State(string debugName, StateAction[] actions, StateMachine stateMachine)
        {
            DebugName = debugName;
            _actions = actions;
            _transitions = new StateTransition[0];


            foreach (StateAction action in _actions)
            {
                action.Awake(stateMachine);
            }
        }
        public void SetTransitions(StateTransition[] transitions)
        {
            _transitions = transitions;
        }
        
        public bool TryGetTransition(out State nextState)
        {
            nextState = null;

            foreach (StateTransition transition in _transitions)
            {
                if (transition.TryGetNextState(out nextState))
                {
                    break;
                }
            }

            foreach (StateTransition transition in _transitions)
            {
                transition.ClearConditionsCache();
            }

            return nextState != null;
        }

        public void OnStateEnter()
        {
            foreach (StateTransition transition in _transitions)
            {
                transition.OnStateEnter();
            }
            
            foreach (StateAction action in _actions)
            {
                action.OnStateEnter();
            }
        }
        public void OnUpdate()
        {
            foreach (StateAction action in _actions)
            {
                action.OnUpdate();
            }
        }
        public void OnFixedUpdate()
        {
            foreach (StateAction action in _actions)
            {
                action.OnFixedUpdate();
            }
        }
        public void OnStateExit()
        {
            foreach (StateAction action in _actions)
            {
                action.OnStateExit();
            }
        }
    }
}

