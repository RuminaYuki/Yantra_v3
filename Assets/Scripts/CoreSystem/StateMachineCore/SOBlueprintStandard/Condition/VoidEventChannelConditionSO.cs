using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewVoidEventChannel_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Void Event Channel")]
public class VoidEventChannelConditionSO : StateConditionSO
{
    [SerializeField] private VoidEventChannelSO _eventChannel;

    public override Condition CreateCondition()
    {
        return new VoidEventChannelCondition(_eventChannel);
    }
}

// A raised event is true for exactly one state machine update (the one right after it
// was raised), however many transitions read it and in whatever order. It isn't used up
// by reading, so a transition that doesn't fire can't swallow it, and it can't linger.
public class VoidEventChannelCondition : Condition
{
    private readonly VoidEventChannelSO _eventChannel;

    private GameObject _owner;
    private bool _pending;   // raised, waiting for the next update
    private bool _active;    // true during this update

    public VoidEventChannelCondition(VoidEventChannelSO eventChannel)
    {
        _eventChannel = eventChannel;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner;

        if (_eventChannel == null)
        {
            Debug.LogError(
                "VoidEventChannelCondition has no Event Channel.",
                _owner);

            // Never raised, so it would read false forever and pass any "Expected = False" check.
            Disable();
            return;
        }

        _eventChannel.Raised += OnEventRaised;
    }

    // An event raised before this state doesn't count.
    public override void OnStateEnter()
    {
        _pending = false;
        _active = false;
    }

    // Start of every update: last update's event expires, a new one becomes visible.
    protected override void OnTick()
    {
        _active = _pending;
        _pending = false;
    }

    protected override bool Statement()
    {
        return _active;
    }

    public override void Dispose()
    {
        if (_eventChannel != null)
        {
            _eventChannel.Raised -= OnEventRaised;
        }
    }

    private void OnEventRaised()
    {
        _pending = true;
    }
}
