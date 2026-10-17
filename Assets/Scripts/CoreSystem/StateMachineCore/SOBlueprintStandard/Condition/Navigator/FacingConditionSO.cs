using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[CreateAssetMenu(
    fileName = "Facing_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Navigator/Facing")]
public class FacingConditionSO : StateConditionSO
{
    [Tooltip("Required. Self Anchor is the one that should be facing (empty = Owner).")]
    [SerializeField] private AnchorIDSO _targetAnchor;
    [Tooltip("Max angle (degrees) between the owner's forward and the direction to the target to be considered \"facing\" it.")]
    [SerializeField, Range(0f, 180f)] private float _angleTolerance = 10f;

    protected override Condition OnCreateCondition()
    {
        return new FacingCondition(_targetAnchor, _angleTolerance);
    }
}

public class FacingCondition : Condition
{
    private readonly AnchorIDSO _targetAnchor;
    private readonly float _angleTolerance;
    private AnchorReference<Transform> _self;
    private AnchorReference<Transform> _target;

    public FacingCondition(
        AnchorIDSO targetAnchor,
        float angleTolerance)
    {
        _targetAnchor = targetAnchor;
        _angleTolerance = angleTolerance;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (_targetAnchor == null)
            Debug.LogError("FacingCondition has no target AnchorIDSO assigned.", stateMachine.Owner);

        _self = new AnchorReference<Transform>(stateMachine, this);
        _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);
    }

    protected override bool Statement()
    {
        if (_targetAnchor == null ||
            !_self.TryGet(out Transform self) ||
            !_target.TryGet(out Transform target))
        {
            return false;
        }

        Vector3 toTarget = target.position - self.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        Vector3 forward = self.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        float angle = Vector3.Angle(forward, toTarget);
        return angle <= _angleTolerance;
    }
}
