using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "SetRandomPositionNavRadius_Action",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Locomotion/Navigation/Set Random Position (Nav Radius)")]
public class SetRandomPositionNavRadiusActionSO : StateActionSO
{
    [SerializeField] private TransformAnchor _targetAnchor;
    [SerializeField, Min(0.01f)] private float _radius = 5f;
    [SerializeField, Min(0.01f)] private float _minDistance = 1.5f;
    [SerializeField, Min(1)] private int _maxAttempts = 10;
    [SerializeField] private bool _requireNavMeshBaked = false;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetRandomPositionNavRadiusAction(
            _targetAnchor,
            _radius,
            _minDistance,
            _maxAttempts,
            _requireNavMeshBaked);
    }
}

public class SetRandomPositionNavRadiusAction : StateAction
{
    private readonly TransformAnchor _targetAnchor;
    private readonly float _radius;
    private readonly float _minDistance;
    private readonly int _maxAttempts;
    private readonly bool _requireNavMeshBaked;

    private PathNavigator _pathNavigator;
    private RandomWalkPoint _randomWalkPoint;
    private WaypointPath _waypointPath;
    private Transform _owner;
    private Transform _destination;

    public SetRandomPositionNavRadiusAction(
        TransformAnchor targetAnchor,
        float radius,
        float minDistance,
        int maxAttempts,
        bool requireNavMeshBaked)
    {
        _targetAnchor = targetAnchor;
        _radius = Mathf.Max(0.01f, radius);
        _minDistance = Mathf.Clamp(minDistance, 0.01f, _radius);
        _maxAttempts = Mathf.Max(1, maxAttempts);
        _requireNavMeshBaked = requireNavMeshBaked;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner.transform;
        _pathNavigator = stateMachine.GetComponent<PathNavigator>();
        _randomWalkPoint = stateMachine.GetComponent<RandomWalkPoint>();
        _waypointPath = stateMachine.GetComponent<WaypointPath>();

        CreateDestination();

        if (_pathNavigator == null)
            Debug.LogError("SetRandomPositionNavRadiusAction requires PathNavigator.", _owner);

        if (_randomWalkPoint == null)
            Debug.LogError("SetRandomPositionNavRadiusAction requires RandomWalkPoint.", _owner);
    }

    public override void OnStateEnter()
    {
        if (_pathNavigator == null || _randomWalkPoint == null)
            return;

        _pathNavigator.ClearTarget();

        if (!TryGetTarget(out Transform target))
            return;

        for (int i = 0; i < _maxAttempts; i++)
        {
            if (!_randomWalkPoint.TryGetRandomPoint(
                    target.position,
                    _radius,
                    _minDistance,
                    out Vector3 candidate))
                continue;

            _destination.position = candidate;

            if (_requireNavMeshBaked)
            {
                if (!_pathNavigator.TrySetTarget(_destination))
                    continue;
            }
            else
            {
                _pathNavigator.Target = _destination;
            }

            return;
        }

        Debug.LogWarning(
            $"SetRandomPositionNavRadiusAction could not find a valid position after {_maxAttempts} attempts.",
            _owner);
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (_pathNavigator != null && _pathNavigator.Target == _destination)
            _pathNavigator.ClearTarget();
    }

    private void CreateDestination()
    {
        if (_destination != null)
            return;

        Transform pathRoot = _waypointPath != null ? _waypointPath.PathRoot : null;

        if (pathRoot != null && pathRoot.childCount > 0)
        {
            _destination = pathRoot.GetChild(0);
            return;
        }

        GameObject destinationObject = new("Random Position (Nav Radius) Destination");
        destinationObject.transform.SetParent(pathRoot != null ? pathRoot : _owner, worldPositionStays: false);
        _destination = destinationObject.transform;
    }

    private bool TryGetTarget(out Transform target)
    {
        target = null;

        if (_targetAnchor == null || !_targetAnchor.IsSet)
            return false;

        target = _targetAnchor.Value;
        return target != null;
    }
}
