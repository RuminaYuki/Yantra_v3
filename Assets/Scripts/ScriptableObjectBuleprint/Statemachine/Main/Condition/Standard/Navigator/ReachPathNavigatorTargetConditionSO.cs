using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "ReachPathNavigatorTarget_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Navigator/Reach Path Navigator Target")]
public class ReachPathNavigatorTargetConditionSO : StateConditionSO
{
    [SerializeField, Min(0.01f)] private float _arrivalDistance = 0.5f;

    public float ArrivalDistance
    {
        get => _arrivalDistance;
        set
        {
            if (value < 0.01f)
            {
                Debug.LogWarning("Arrival distance cannot be below 0.01. Setting to 0.01.");
                _arrivalDistance = 0.01f;
                return;
            }
            _arrivalDistance = value;
        }
    }

    public override Condition CreateCondition()
    {
        return new ReachPathNavigatorTargetCondition(_arrivalDistance);
    }
}

public class ReachPathNavigatorTargetCondition : Condition
{
    private readonly float _arrivalDistance;

    private Transform _owner;
    private PathNavigator _pathNavigator;

    public ReachPathNavigatorTargetCondition(float arrivalDistance)
    {
        _arrivalDistance = Mathf.Max(0.01f,arrivalDistance);
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner.transform;
        _pathNavigator = stateMachine.GetComponent<PathNavigator>();

        if (_pathNavigator == null)
            Debug.LogError("ReachPathNavigatorTargetCondition requires PathNavigator.");
    }

    protected override bool Statement()
    {
        if (_owner == null ||
            _pathNavigator == null ||
            _pathNavigator.Target == null)
        {
            return false;
        }

        Vector3 offset = _pathNavigator.Target.position - _owner.position;
        offset.y = 0f;

        return offset.sqrMagnitude <= _arrivalDistance * _arrivalDistance;
    }
}
