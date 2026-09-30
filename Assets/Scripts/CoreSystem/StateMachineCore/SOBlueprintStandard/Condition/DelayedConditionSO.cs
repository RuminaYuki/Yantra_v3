using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewDelayed_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Delayed")]
public class DelayedConditionSO : StateConditionSO
{
    public enum DelayMode
    {
        [Tooltip("Inner must match for the whole delay; if it stops matching, the wait starts over.")]
        Sustained,

        [Tooltip("Inner matching once starts the delay; it keeps counting even if it stops matching.")]
        Latched
    }

    [Tooltip("The condition to wait on.")]
    [SerializeField] private StateConditionSO _innerCondition;

    [Tooltip("What the inner condition has to be: true, or false (e.g. \"not detected for 2 s\").")]
    [SerializeField] private bool _innerExpected = true;

    [SerializeField, Min(0f)] private float _delay = 1f;
    [SerializeField] private DelayMode _mode = DelayMode.Sustained;

    public override Condition CreateCondition()
    {
        // Its own copy of the inner condition, separate from any other use of that asset.
        Condition inner = _innerCondition != null ? _innerCondition.CreateCondition() : null;
        return new DelayedCondition(inner, _innerExpected, _delay, _mode);
    }
}

// Passes once the inner condition has matched for `delay` seconds (Sustained),
// or `delay` seconds after it first matched (Latched).
// Watches the inner condition in OnTick, so it works wherever it sits in a transition.
public class DelayedCondition : Condition
{
    private readonly Condition _inner;
    private readonly bool _innerExpected;
    private readonly float _delay;
    private readonly DelayedConditionSO.DelayMode _mode;

    // When the inner condition started matching, or null while it hasn't.
    private float? _matchedSince;

    public DelayedCondition(
        Condition inner,
        bool innerExpected,
        float delay,
        DelayedConditionSO.DelayMode mode)
    {
        _inner = inner;
        _innerExpected = innerExpected;
        _delay = delay;
        _mode = mode;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (_inner == null)
        {
            Debug.LogError("[DelayedCondition] has no inner condition.", stateMachine.Owner);
            Disable();
            return;
        }

        // The transition table only knows about this wrapper, so pass Awake on.
        _inner.Awake(stateMachine);

        // A broken inner condition makes this one broken too (see StateCondition.IsMet).
        if (_inner.IsDisabled)
        {
            Disable();
        }
    }

    public override void OnStateEnter()
    {
        _matchedSince = null;
        _inner.OnStateEnter();
    }

    public override void Dispose()
    {
        _inner?.Dispose();
    }

    protected override void OnTick()
    {
        // The inner condition isn't in any transition, so nothing else ticks it
        // (it matters for inner conditions that use OnTick, like OnHurt).
        _inner.Tick();

        bool matches = _inner.GetStatement() == _innerExpected;

        // Transitions only clear the cache of the conditions they hold, not this inner one.
        _inner.ClearStatementCache();

        if (matches)
        {
            _matchedSince ??= Time.time;
        }
        else if (_mode == DelayedConditionSO.DelayMode.Sustained)
        {
            _matchedSince = null;
        }
    }

    protected override bool Statement()
    {
        return _matchedSince.HasValue &&
               Time.time - _matchedSince.Value >= _delay;
    }
}
