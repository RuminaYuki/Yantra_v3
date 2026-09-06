namespace Yuki.Learning.StateMachine
{
    public abstract class Condition
    {
        private bool _isCached;
        private bool _cachedStatement;

        public virtual void Awake(StateMachine stateMachine){}
        public virtual void OnStateEnter(){}
        protected abstract bool Statement();
        public virtual void Dispose(){}

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

        public bool IsMet()
        {
            bool actualResult =
                _condition.GetStatement();

            return actualResult == _expectedResult;
        }
        public void OnStateEnter()
        {
            _condition.OnStateEnter();
        }

        public void ClearCache()
        {
            _condition.ClearStatementCache();
        }
    }
}
