using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(PathNavigator))]
[RequiresOwnerComponent(typeof(StandablePointValidator))]
[RequiresOwnerComponent(typeof(WaypointPath))]
[CreateAssetMenu(
    fileName = "SetRandomPositionNavRadius_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Set Random Position (Nav Radius)")]
public class SetRandomPositionNavRadiusActionSO : StateActionSO
{
    [Header("This action will use Waypoint Root at Homepoint")]
    [SerializeField, Exposed] private RingSampleParameters _ringParameters =
        new() { radius = 5f, minDistance = 1.5f, directionCount = 16 };
    [SerializeField, Min(1)] private int _maxAttempts = 10;
    [SerializeField] private bool _requireNavMeshBaked = false;

    public float Radius
    {
        get => _ringParameters.radius;
        set
        {
           if (value < 0f)
            {
                Debug.LogWarning("radius cannot be negative. Setting to 0.");
                _ringParameters.radius = 0f;
                return;
            }
            _ringParameters.radius = value; 
        }
    }

    public float MinDistance
    {
        get => _ringParameters.minDistance;
        set
        {
           if (value < 0f)
            {
                Debug.LogWarning("minDistance cannot be negative. Setting to 0.");
                _ringParameters.minDistance = 0f;
                return;
            }
            _ringParameters.minDistance = value; 
        }
    }

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetRandomPositionNavRadiusAction(
            _ringParameters,
            _maxAttempts,
            _requireNavMeshBaked);
    }
}

public class SetRandomPositionNavRadiusAction : StateAction
{
    private readonly RingSampleParameters _ringParameters;
    private readonly int _maxAttempts;
    private readonly bool _requireNavMeshBaked;

    private AnchorReference<PathNavigator> _pathNavigator;
    private AnchorReference<StandablePointValidator> _pointValidator;
    private AnchorReference<WaypointPath> _waypointPath;
    private Transform _owner;
    private Transform _destination;

    public SetRandomPositionNavRadiusAction(
        RingSampleParameters ringParameters,
        int maxAttempts,
        bool requireNavMeshBaked)
    {
        _ringParameters = ringParameters;
        _maxAttempts = Mathf.Max(1, maxAttempts);
        _requireNavMeshBaked = requireNavMeshBaked;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner.transform;
        _pathNavigator = new AnchorReference<PathNavigator>(stateMachine, this);
        _pointValidator = new AnchorReference<StandablePointValidator>(stateMachine, this);
        _waypointPath = new AnchorReference<WaypointPath>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_pathNavigator.TryGet(out PathNavigator pathNavigator) ||
            !_pointValidator.TryGet(out StandablePointValidator pointValidator) ||
            !_waypointPath.TryGet(out WaypointPath waypointPath))
            return;

        // Made on first use instead of in Awake: the components are only looked up when needed.
        CreateDestination(waypointPath);
        pathNavigator.ClearTarget();

        Transform target = waypointPath.PathRoot != null ? waypointPath.PathRoot : _owner;
        if (target == null)
            return;

        for (int i = 0; i < _maxAttempts; i++)
        {
            if (!RingPointSampler.TryGetPoint(
                    pointValidator,
                    target.position,
                    in _ringParameters,
                    out Vector3 candidate))
                continue;

            _destination.position = candidate;

            if (_requireNavMeshBaked)
            {
                if (!pathNavigator.TrySetTarget(_destination))
                    continue;
            }
            else
            {
                pathNavigator.Target = _destination;
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
        if (_pathNavigator.TryGet(out PathNavigator pathNavigator) && pathNavigator.Target == _destination)
            pathNavigator.ClearTarget();
    }

    private void CreateDestination(WaypointPath waypointPath)
    {
        if (_destination != null)
            return;

        Transform pathRoot = waypointPath.PathRoot;

        if (pathRoot != null && pathRoot.childCount > 0)
        {
            _destination = pathRoot.GetChild(0);
            return;
        }

        GameObject destinationObject = new("Random Position (Nav Radius) Destination");
        destinationObject.transform.SetParent(pathRoot != null ? pathRoot : _owner, worldPositionStays: false);
        _destination = destinationObject.transform;
    }
}
