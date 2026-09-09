using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewVoidEventChannel_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/Standard/Void Event Channel")]
public class VoidEventChannelConditionSO : StateConditionSO
{
    [SerializeField] private VoidEventChannelSO _eventChannel;

    public override Condition CreateCondition()
    {
        return new VoidEventChannelCondition(_eventChannel);
    }
}

public class VoidEventChannelCondition : Condition
{
    private readonly VoidEventChannelSO _eventChannel;

    private GameObject _owner;
    private bool _wasRaised;

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
            return;
        }

        _eventChannel.Raised += OnEventRaised;
    }

    public override void OnStateEnter()
    {
        _wasRaised = false;
    }

    protected override bool Statement()
    {
        bool result = _wasRaised;
        _wasRaised = false;
        return result;
    }

    public override void Dispose()
    {
        if (_eventChannel != null)
        {
            _eventChannel.Raised -= OnEventRaised;
        }

        _wasRaised = false;
    }

    private void OnEventRaised()
    {
        _wasRaised = true;
    }
}
