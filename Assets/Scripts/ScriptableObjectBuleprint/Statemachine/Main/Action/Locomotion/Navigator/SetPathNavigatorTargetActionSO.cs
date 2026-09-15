using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SetPathNavigatorTargetAction",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Locomotion/Navigation/Set Target")]
public class SetPathNavigatorTargetActionSO : StateActionSO
{
    [SerializeField] private TransformAnchor _targetAnchor;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetPathNavigatorTargetAction(_targetAnchor);
    }
}

public class SetPathNavigatorTargetAction : StateAction
{
    private readonly TransformAnchor _targetAnchor;
    private PathNavigator _pathNavigator;

    public SetPathNavigatorTargetAction(TransformAnchor targetAnchor)
    {
        _targetAnchor = targetAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _pathNavigator = stateMachine.GetComponent<PathNavigator>();

        if (_pathNavigator == null)
            Debug.LogError("SetPathNavigatorTargetAction cannot find PathNavigator.");
    }

    public override void OnStateEnter()
    {
        if (_pathNavigator == null)
            return;

        if (_targetAnchor == null)
        {
            Debug.LogError("SetPathNavigatorTargetAction has no TransformAnchor assigned.");
            return;
        }

        if (!_targetAnchor.IsSet)
        {
            Debug.LogWarning("SetPathNavigatorTargetAction target anchor is not set.");
            return;
        }

        _pathNavigator.Target = _targetAnchor.Value;
    }

    public override void OnUpdate() { }
}
