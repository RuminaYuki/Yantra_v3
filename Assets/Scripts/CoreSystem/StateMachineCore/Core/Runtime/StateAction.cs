namespace Yuki.Learning.StateMachine
{
    public abstract class StateAction
    {
        public bool IsDisabled { get; private set; }
        internal void Disable() => IsDisabled = true;
        
        public virtual void Awake(StateMachine stateMachine)
        {
        }

        public virtual void OnStateEnter()
        {
        }

        public abstract void OnUpdate();

        public virtual void OnFixedUpdate()
        {
            
        }

        public virtual void OnStateExit()
        {
        }
    }
}