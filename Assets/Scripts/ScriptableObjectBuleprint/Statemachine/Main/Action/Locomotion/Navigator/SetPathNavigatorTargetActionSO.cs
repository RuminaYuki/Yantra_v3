using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(PathNavigator))]
[CreateAssetMenu(
    fileName = "SetPathNavigatorTargetAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Set Target")]
public class SetPathNavigatorTargetActionSO : StateActionSO
{
    [SerializeField] private AnchorIDSO _targetAnchor;
    [SerializeField, Exposed] private Vector3 _offset;
    [Header("Randomize offset per axis (+-range) each time the state is entered")]
    [SerializeField, Exposed] private Vector3 _randomOffsetRange = Vector3.zero;

    /// <summary>The target as seen by this owner (through its Provider), or null if not linked.</summary>
    public Transform GetTargetTransform(GameObject owner)
    {
        return _targetAnchor != null &&
               owner.TryGetComponent(out Provider provider) &&
               provider.TryGet(_targetAnchor, out GameObject target)
            ? target.transform
            : null;
    }
    public Vector3 Offset
    {
        get => _offset;
        set => _offset = value;
    }
    public Vector3 RandomOffsetRange
    {
        get => _randomOffsetRange;
        set => _randomOffsetRange = new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetPathNavigatorTargetAction(_targetAnchor, _offset, _randomOffsetRange);
    }
}

public class SetPathNavigatorTargetAction : StateAction
{
    private readonly AnchorIDSO _targetAnchor;
    private readonly Vector3 _offset;
    private readonly Vector3 _randomOffsetRange;
    private AnchorReference<PathNavigator> _pathNavigator;
    private AnchorReference<Transform> _target;
    private Vector3 _randomOffset;

    public SetPathNavigatorTargetAction(AnchorIDSO targetAnchor, Vector3 offset, Vector3 randomOffsetRange)
    {
        _targetAnchor = targetAnchor;
        _offset = offset;
        _randomOffsetRange = new Vector3(
            Mathf.Abs(randomOffsetRange.x),
            Mathf.Abs(randomOffsetRange.y),
            Mathf.Abs(randomOffsetRange.z));
    }

    public override void Awake(StateMachine stateMachine)
    {
        _pathNavigator = new AnchorReference<PathNavigator>(stateMachine, this);
        _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);
    }

    public override void OnStateEnter()
    {
        if (_targetAnchor == null)
        {
            Debug.LogError("SetPathNavigatorTargetAction has no AnchorIDSO assigned.");
            return;
        }

        if (!_pathNavigator.TryGet(out PathNavigator pathNavigator) ||
            !_target.TryGet(out Transform target))
        {
            return;
        }

        _randomOffset = new Vector3(
            Random.Range(-_randomOffsetRange.x, _randomOffsetRange.x),
            Random.Range(-_randomOffsetRange.y, _randomOffsetRange.y),
            Random.Range(-_randomOffsetRange.z, _randomOffsetRange.z));

        // Asked from the navigator each time (it creates it once), so a re-anchored navigator still works.
        Transform offsetTarget = pathNavigator.GetOrCreateOffsetTarget();
        offsetTarget.position = target.TransformPoint(_offset + _randomOffset);
        pathNavigator.Target = offsetTarget;
    }

    public override void OnUpdate()
    {
        if (_targetAnchor == null ||
            !_pathNavigator.TryGet(out PathNavigator pathNavigator) ||
            !_target.TryGet(out Transform target))
            return;

        pathNavigator.GetOrCreateOffsetTarget().position = target.TransformPoint(_offset + _randomOffset);
    }
}
