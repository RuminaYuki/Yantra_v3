using UnityEngine;

public class PathNavigator : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _repathInterval = 0.25f;

    private IPathfinder _pathfinder;
    private float _timer;

    public Vector3 Direction { get; private set; }

    public Transform Target {get=> _target; set=> _target = value;}

    private void Awake()
    {
        _pathfinder = new UnityNavMeshPathfinder();
    }

    public bool TrySetTarget(Transform target)
    {
        if (target == null || _pathfinder == null)
            return false;

        if (!_pathfinder.TryCalculatePath(transform.position,
                target.position, out Vector3 resolvedPosition))
        {
            return false;
        }

        target.position = resolvedPosition;
        _target = target;
        _timer = _repathInterval;

        return true;
    }

    public void ClearTarget()
    {
        _target = null;
        Direction = Vector3.zero;
    }

    private void Update()
    {
        if (_target == null)
        {
            Direction = Vector3.zero;
            return;
        }

        _timer -= Time.deltaTime;

        if (_timer <= 0f)
        {
            _timer = _repathInterval;

            if (!_pathfinder.TryCalculatePath(transform.position,
                    _target.position, out Vector3 resolvedPosition))
            {
                Direction = Vector3.zero;
                return;
            }

            _target.position = resolvedPosition;
        }

        Direction = _pathfinder.GetDirection(transform.position);
    }
}