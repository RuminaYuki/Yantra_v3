namespace Yuki.Learning.StateMachine
{
    public abstract class StateAction
    {
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