namespace Yuki.Learning.StateMachine
{
    public class StateTransition
    {
        private readonly State _targetState;
        private readonly StateCondition[][] _conditionGroups;

        public StateTransition(
            State targetState,
            StateCondition[][] conditionGroups)
        {
            _targetState = targetState;
            _conditionGroups = conditionGroups;
        }
        public void OnStateEnter()
        {
            if (_conditionGroups == null)
            {
                return;
            }

            foreach (StateCondition[] group in _conditionGroups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (StateCondition condition in group)
                {
                    condition.OnStateEnter();
                }
            }
        }
        public bool TryGetNextState(out State nextState)
        {
            if (IsAnyGroupMet())
            {
                nextState = _targetState;
                return true;
            }

            nextState = null;
            return false;
        }

        private bool IsAnyGroupMet()
        {
            if (_conditionGroups == null ||
                _conditionGroups.Length == 0)
            {
                return false;
            }

            foreach (StateCondition[] group in _conditionGroups)
            {
                if (AreAllConditionsMet(group))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AreAllConditionsMet(
            StateCondition[] group)
        {
            if (group == null || group.Length == 0)
            {
                return false;
            }

            foreach (StateCondition condition in group)
            {
                if (!condition.IsMet())
                {
                    return false;
                }
            }

            return true;
        }

        public void ClearConditionsCache()
        {
            foreach (StateCondition[] group in _conditionGroups)
            {
                foreach (StateCondition condition in group)
                {
                    condition.ClearCache();
                }
            }
        }
    }
}