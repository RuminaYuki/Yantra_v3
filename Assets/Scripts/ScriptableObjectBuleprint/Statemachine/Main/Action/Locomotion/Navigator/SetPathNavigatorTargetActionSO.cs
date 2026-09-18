using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SetPathNavigatorTargetAction",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Locomotion/Navigation/Set Target")]
public class SetPathNavigatorTargetActionSO : StateActionSO
{
    [SerializeField] private TransformAnchor _targetAnchor;
    [SerializeField] private Vector3 _offset;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetPathNavigatorTargetAction(_targetAnchor, _offset);
    }
}

public class SetPathNavigatorTargetAction : StateAction
{
    private readonly TransformAnchor _targetAnchor;
    private readonly Vector3 _offset;
    private PathNavigator _pathNavigator;
    private Transform _offsetTarget;

    public SetPathNavigatorTargetAction(TransformAnchor targetAnchor, Vector3 offset)
    {
        _targetAnchor = targetAnchor;
        _offset = offset;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _pathNavigator = stateMachine.GetComponent<PathNavigator>();

        if (_pathNavigator == null)
            Debug.LogError("SetPathNavigatorTargetAction cannot find PathNavigator.");

        _offsetTarget = new GameObject("SetPathNavigatorTargetAction_OffsetTarget").transform;
        _offsetTarget.SetParent(stateMachine.Owner.transform, false);
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

        _offsetTarget.position = _targetAnchor.Value.TransformPoint(_offset);
        _pathNavigator.Target = _offsetTarget;
    }

    public override void OnUpdate()
    {
        if (_pathNavigator == null || _targetAnchor == null || !_targetAnchor.IsSet)
            return;

        _offsetTarget.position = _targetAnchor.Value.TransformPoint(_offset);
    }
}
