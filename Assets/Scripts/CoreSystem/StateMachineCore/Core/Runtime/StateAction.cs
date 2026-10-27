namespace Yuki.Learning.StateMachine
{
    public abstract class StateAction
    {
        public AnchorIDSO SelfAnchor {get; internal set;}
        public bool IsDisabled { get; private set; }
        internal void Disable() => IsDisabled = true;

        protected StateMachineClock Time { get; private set; }
        
        public void Initialize(StateMachine stateMachine)
        {
            Time = stateMachine.Clock;
            Awake(stateMachine);
        }
        public virtual void Awake(StateMachine stateMachine){}
        public virtual void OnStateEnter(){}
        public abstract void OnUpdate();
        public virtual void OnFixedUpdate(){}
        public virtual void OnStateExit(){}
    }
}