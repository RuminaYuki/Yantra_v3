using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewSequence_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Sequence")]
public class SequenceConditionSO : StateConditionSO
{
    [Tooltip("Waited on first. Once it matches it's done with: it doesn't have to stay matching.")]
    [SerializeField] private StateConditionSO _firstCondition;
    [SerializeField] private bool _firstExpected = true;

    [Tooltip("Only starts (OnStateEnter, ticking) once First has matched, so its timers count from then.")]
    [SerializeField] private StateConditionSO _thenCondition;
    [SerializeField] private bool _thenExpected = true;

    protected override Condition OnCreateCondition()
    {
        // Their own copies, separate from any other use of those assets.
        Condition first = _firstCondition != null ? _firstCondition.CreateCondition() : null;
        Condition then = _thenCondition != null ? _thenCondition.CreateCondition() : null;
        return new SequenceCondition(first, _firstExpected, then, _thenExpected);
    }
}

// Passes when Then matches, but Then is only started after First has matched once since entering the
// state. Every condition in a transition is ticked from the state's start (even after an earlier one in
// its group fails), so "A and B" can't make B's timers wait for A; this can.
public class SequenceCondition : Condition
{
    private readonly Condition _first;
    private readonly bool _firstExpected;
    private readonly Condition _then;
    private readonly bool _thenExpected;

    // False: waiting on First. True: First matched, Then is running.
    private bool _isOnThen;
    private bool _thenMatches;

    public SequenceCondition(Condition first, bool firstExpected, Condition then, bool thenExpected)
    {
        _first = first;
        _firstExpected = firstExpected;
        _then = then;
        _thenExpected = thenExpected;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (_first == null || _then == null)
        {
            Debug.LogError("[SequenceCondition] needs both a First and a Then condition.", stateMachine.Owner);
            Disable();
            return;
        }

        // The transition table only knows about this wrapper, so pass Awake on.
        _first.Awake(stateMachine);
        _then.Awake(stateMachine);

        // A broken inner condition makes this one broken too (see StateCondition.IsMet).
        if (_first.IsDisabled || _then.IsDisabled)
        {
            Disable();
        }
    }

    public override void OnStateEnter()
    {
        _isOnThen = false;
        _thenMatches = false;
        _first.OnStateEnter();
    }

    public override void Dispose()
    {
        _first?.Dispose();
        _then?.Dispose();
    }

    protected override void OnTick()
    {
        // The inner conditions aren't in any transition, so nothing else ticks them or clears their cache.
        if (!_isOnThen)
        {
            _first.Tick();
            bool firstMatches = _first.GetStatement() == _firstExpected;
            _first.ClearStatementCache();

            if (firstMatches)
            {
                // Then starts fresh now; it's ticked from the next frame.
                _isOnThen = true;
                _then.OnStateEnter();
            }

            return;
        }

        _then.Tick();
        _thenMatches = _then.GetStatement() == _thenExpected;
        _then.ClearStatementCache();
    }

    protected override bool Statement()
    {
        return _isOnThen && _thenMatches;
    }
}
