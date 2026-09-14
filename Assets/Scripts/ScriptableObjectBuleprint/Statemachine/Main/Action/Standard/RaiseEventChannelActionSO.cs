using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "RaiseEventChannel_Action",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Standard/Raise Event Channel")]
public class RaiseEventChannelActionSO : StateActionSO
{
    [SerializeField] private VoidEventChannelSO _eventChannel;

    public override StateAction CreateAction(
        StateMachine stateMachine)
    {
        return new RaiseEventChannelAction(
            _eventChannel);
    }
}

public class RaiseEventChannelAction : StateAction
{
    private readonly VoidEventChannelSO _eventChannel;
    private GameObject _owner;

    public RaiseEventChannelAction(VoidEventChannelSO eventChannel)
    {
        _eventChannel = eventChannel;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner;

        if (_eventChannel == null)
        {
            Debug.LogError(
                "RaiseEventChannelAction has no Event Channel.",
                _owner);
        }
    }

    public override void OnStateEnter()
    {
        _eventChannel?.Raise();
    }

    public override void OnUpdate()
    {
    }
}