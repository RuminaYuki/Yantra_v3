namespace Yuki.Learning.StateMachine
{
    public abstract class Condition
    {
        public AnchorIDSO SelfAnchor {get; internal set;}
        public bool IsDisabled { get; private set; }
        public string DebugName { get; internal set; }
        internal void Disable() => IsDisabled = true;

        protected StateMachineClock Time { get; private set; }

        private bool _isCached;
        private bool _cachedStatement;
        public bool LastResult => _cachedStatement;
        public int LastEvaluatedFrame { get; private set; } = -1;
        private int _lastTickFrame = -1;

        public void Initialize(StateMachine stateMachine)
        {
            Time = stateMachine.Clock;
            Awake(stateMachine);
        }
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
                LastEvaluatedFrame = UnityEngine.Time.frameCount;
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

        public Condition Condition => _condition;
        public bool ExpectedResult => _expectedResult;

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
