using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[CreateAssetMenu(
    fileName = "NewRange_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Distance")]
public class DistanceConditionSO : StateConditionSO
{
    [Tooltip("Required. Self Anchor is where the distance is measured from (empty = Owner).")]
    [SerializeField] private AnchorIDSO _targetAnchor;
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

    protected override Condition OnCreateCondition()
    {
        return new DistanceCondition(_targetAnchor, _distance);
    }
}

public class DistanceCondition : Condition
{
    private readonly AnchorIDSO _targetAnchor;
    private readonly float _distance;
    private AnchorReference<Transform> _self;
    private AnchorReference<Transform> _target;

    public DistanceCondition(
        AnchorIDSO targetAnchor,
        float distance)
    {
        _targetAnchor = targetAnchor;
        _distance = distance;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (_targetAnchor == null)
            Debug.LogError("DistanceCondition has no target AnchorIDSO assigned.", stateMachine.Owner);

        _self = new AnchorReference<Transform>(stateMachine, this);
        _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);
    }

    protected override bool Statement()
    {
        // The target may not be linked yet (e.g. the player before a sensor found it).
        if (_targetAnchor == null ||
            !_self.TryGet(out Transform self) ||
            !_target.TryGet(out Transform target))
        {
            return false;
        }

        Vector3 offset = target.position - self.position;
        offset.y = 0f;

        return offset.sqrMagnitude <= _distance * _distance;
    }
}
