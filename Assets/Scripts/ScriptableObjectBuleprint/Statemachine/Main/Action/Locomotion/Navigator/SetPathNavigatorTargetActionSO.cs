using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SetPathNavigatorTargetAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Set Target")]
public class SetPathNavigatorTargetActionSO : StateActionSO
{
    [SerializeField] private TransformAnchor _targetAnchor;
    [SerializeField] private Vector3 _offset;
    [Header("Randomize offset per axis (+-range) each time the state is entered")]
    [SerializeField] private Vector3 _randomOffsetRange = Vector3.zero;

    public Transform TargetTransform
    {
        get => _targetAnchor.Value;
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

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetPathNavigatorTargetAction(_targetAnchor, _offset, _randomOffsetRange);
    }
}

public class SetPathNavigatorTargetAction : StateAction
{
    private readonly TransformAnchor _targetAnchor;
    private readonly Vector3 _offset;
    private readonly Vector3 _randomOffsetRange;
    private PathNavigator _pathNavigator;
    private Transform _offsetTarget;
    private Vector3 _randomOffset;

    public SetPathNavigatorTargetAction(TransformAnchor targetAnchor, Vector3 offset, Vector3 randomOffsetRange)
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
        _pathNavigator = stateMachine.GetComponent<PathNavigator>();

        if (_pathNavigator == null)
        {
            Debug.LogError("SetPathNavigatorTargetAction cannot find PathNavigator.");
            return;
        }

        _offsetTarget = _pathNavigator.GetOrCreateOffsetTarget();
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

        _randomOffset = new Vector3(
            Random.Range(-_randomOffsetRange.x, _randomOffsetRange.x),
            Random.Range(-_randomOffsetRange.y, _randomOffsetRange.y),
            Random.Range(-_randomOffsetRange.z, _randomOffsetRange.z));

        _offsetTarget.position = _targetAnchor.Value.TransformPoint(_offset + _randomOffset);
        _pathNavigator.Target = _offsetTarget;
    }

    public override void OnUpdate()
    {
        if (_pathNavigator == null || _targetAnchor == null || !_targetAnchor.IsSet)
            return;

        _offsetTarget.position = _targetAnchor.Value.TransformPoint(_offset + _randomOffset);
    }
}
