namespace Yuki.Learning.StateMachine
{
    public abstract class Condition
    {
        public bool IsDisabled { get; private set; }
        internal void Disable() => IsDisabled = true;

        private bool _isCached;
        private bool _cachedStatement;
        private int _lastTickFrame = -1;

        public virtual void Awake(StateMachine stateMachine){}
        public virtual void OnStateEnter(){}
        protected abstract bool Statement();
        public virtual void Dispose(){}
        protected virtual void OnTick(){}
        internal void Tick()
        {
            if (IsDisabled || _lastTickFrame == UnityEngine.Time.frameCount)
            {
                return;
            }

            _lastTickFrame = UnityEngine.Time.frameCount;
            OnTick();
        }
        public bool GetStatement()
        {
            if (!_isCached)
            {
                _cachedStatement = Statement();
                _isCached = true;
            }

            return _cachedStatement;
        }

        public void ClearStatementCache()
        {
            _isCached = false;
        }
    }

    public readonly struct StateCondition
    {
        private readonly Condition _condition;
        private readonly bool _expectedResult;

        public StateCondition(
            Condition condition,
            bool expectedResult)
        {
            _condition = condition;
            _expectedResult = expectedResult;
        }
        public void Tick()
        {
            _condition.Tick();
        }
        public bool IsMet()
        {
            if (_condition.IsDisabled) return false;

            bool actualResult = _condition.GetStatement();
            return actualResult == _expectedResult;
        }
        public void OnStateEnter()
        {
            if (_condition.IsDisabled) return;
            _condition.OnStateEnter();
        }

        public void ClearCache()
        {
            _condition.ClearStatementCache();
        }
    }
}
