using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "Facing_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Navigator/Facing")]
public class FacingConditionSO : StateConditionSO
{
    [SerializeField] private TransformAnchor _targetAnchor;
    [Tooltip("Max angle (degrees) between the owner's forward and the direction to the target to be considered \"facing\" it.")]
    [SerializeField, Range(0f, 180f)] private float _angleTolerance = 10f;

    public override Condition CreateCondition()
    {
        return new FacingCondition(_targetAnchor, _angleTolerance);
    }
}

public class FacingCondition : Condition
{
    private readonly TransformAnchor _targetAnchor;
    private readonly float _angleTolerance;
    private Transform _owner;

    public FacingCondition(
        TransformAnchor targetAnchor,
        float angleTolerance)
    {
        _targetAnchor = targetAnchor;
        _angleTolerance = angleTolerance;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner.transform;

        if (_targetAnchor == null)
            Debug.LogError("FacingCondition has no target TransformAnchor assigned.");
    }

    protected override bool Statement()
    {
        if (_owner == null ||
            _targetAnchor == null ||
            !_targetAnchor.IsSet ||
            _targetAnchor.Value == null)
        {
            return false;
        }

        Vector3 toTarget = _targetAnchor.Value.position - _owner.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        Vector3 forward = _owner.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        float angle = Vector3.Angle(forward, toTarget);
        return angle <= _angleTolerance;
    }
}
