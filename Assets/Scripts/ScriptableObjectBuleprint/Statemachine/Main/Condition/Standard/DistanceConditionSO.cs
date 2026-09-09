using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "Distance_Condition",
    menuName = "YUKI Learning State Machine/StateMachine/Conditions/Standard/Distance")]
public class DistanceConditionSO : StateConditionSO
{
    [SerializeField] private TransformAnchor _targetAnchor;
    [Header("if have FloatDataSO, it will override the value")]
    [SerializeField, Min(0f)] private FloatDataSO _distanceData;
    [SerializeField, Min(0f)] private float _distance = 1f;
    public float Distance => _distance;

    public override Condition CreateCondition()
    {
        float distance = _distanceData != null ? _distanceData.Value : _distance;
        return new DistanceCondition(_targetAnchor, distance);
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
