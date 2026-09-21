using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

public abstract class RaiseEventChannelActionSO<T> : StateActionSO
{
    [SerializeField] private EventChannelSO<T> _eventChannel;
    [SerializeField] private T _value;

    public override StateAction CreateAction(
        StateMachine stateMachine)
    {
        return new RaiseEventChannelAction<T>(
            _eventChannel,
            _value);
    }
}

public class RaiseEventChannelAction<T> : StateAction
{
    private readonly EventChannelSO<T> _eventChannel;
    private readonly T _value;
    private GameObject _owner;

    public RaiseEventChannelAction(EventChannelSO<T> eventChannel, T value)
    {
        _eventChannel = eventChannel;
        _value = value;
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
        _eventChannel?.Raise(_value);
    }

    public override void OnUpdate()
    {
    }
}
