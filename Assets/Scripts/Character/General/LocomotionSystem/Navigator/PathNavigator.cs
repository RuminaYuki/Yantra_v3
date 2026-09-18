using UnityEngine;

public class PathNavigator : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _repathInterval = 0.25f;
    [SerializeField] private float _slowDownRadius = 1.5f;

    private IPathfinder _pathfinder;
    private float _timer;
    private Transform _offsetTarget;

    public Vector3 Direction { get; private set; }

    public Transform Target {get=> _target; set=> _target = value;}

    private void Awake()
    {
        _pathfinder = new UnityNavMeshPathfinder();
    }

    public Transform GetOrCreateOffsetTarget()
    {
        if (_offsetTarget == null)
        {
            _offsetTarget = new GameObject("PathNavigator_OffsetTarget").transform;
            _offsetTarget.SetParent(transform, false);
        }

        return _offsetTarget;
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

        Vector3 rawDirection = _pathfinder.GetDirection(transform.position);
        float distanceToTarget = Vector3.Distance(transform.position, _target.position);
        float speedScale = Mathf.Clamp01(distanceToTarget / _slowDownRadius);

        Direction = rawDirection * speedScale;
    }
}