using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewRange_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Distance")]
public class DistanceConditionSO : StateConditionSO
{
    [SerializeField] private TransformAnchor _targetAnchor;
    [SerializeField, Min(0f)] private float _distance = 1f;

    public float Distance
    {
        get => _distance;
        set
        {
            if (value < 0f)
            {
                Debug.LogWarning("Distance cannot be negative. Setting to 0.");
                _distance = 0f;
                return;
            }
            _distance = value;
        }
    }

    public override Condition CreateCondition()
    {
        return new DistanceCondition(_targetAnchor, _distance);
    }
}

public class DistanceCondition : Condition
{
    private readonly TransformAnchor _targetAnchor;
    private readonly float _distance;
    private Transform _owner;

    public DistanceCondition(
        TransformAnchor targetAnchor,
        float distance)
    {
        _targetAnchor = targetAnchor;
        _distance = distance;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.GetComponent<Transform>();

        if (_targetAnchor == null)
            Debug.LogError("TargetInRangeCondition has no TransformAnchor assigned.");
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

        Vector3 offset = _targetAnchor.Value.position - _owner.position;
        offset.y = 0f;

        return offset.sqrMagnitude <= _distance * _distance;
    }
}
