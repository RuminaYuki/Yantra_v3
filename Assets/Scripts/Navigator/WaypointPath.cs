using UnityEngine;

public class WaypointPath : MonoBehaviour
{
    [SerializeField] private Transform _pathRoot;

    private int _currentIndex;
    private int _direction = 1;
    public bool IsEnablePathGizmos = true;

    public Transform PathRoot => _pathRoot;

    private void Awake()
    {
        if (_pathRoot == null)
        {
            Debug.LogWarning("WaypointPath: Path Root is not assigned.");
            _pathRoot = transform;
        }
    }
    
    public int Count => _pathRoot != null ? _pathRoot.childCount : 0;
    public int CurrentIndex => _currentIndex;
    public int LastIndex => Count - 1;
    public bool IsAtLastPoint => Count > 0 && _currentIndex == LastIndex;

    public Transform CurrentPoint
    {
        get
        {
            if (_pathRoot == null || Count == 0)
                return null;

            return _pathRoot.GetChild(_currentIndex);
        }
    }

    public void MoveToNextPoint()
    {
        if (Count <= 1)
            return;

        if (_currentIndex >= Count - 1)
        {
            _direction = -1;
        }
        else if (_currentIndex <= 0)
        {
            _direction = 1;
        }

        _currentIndex += _direction;
    }
    
    public void ResetToFirstPoint()
    {
        _currentIndex = 0;
        _direction = 1;
    }

    public void SetCurrentPoint(Transform point)
    {
        if (point == null || _pathRoot == null)
            return;

        point.SetParent(_pathRoot);
        _currentIndex = point.GetSiblingIndex();
    }

    private void OnDrawGizmosSelected()
    {
        if (_pathRoot == null || !IsEnablePathGizmos)
            return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < _pathRoot.childCount; i++)
        {
            Transform point = _pathRoot.GetChild(i);

            Gizmos.DrawSphere(point.position, 0.15f);

            if (i >= _pathRoot.childCount - 1)
                continue;

            Transform nextPoint =
                _pathRoot.GetChild(i + 1);

            Gizmos.DrawLine(
                point.position,
                nextPoint.position);
        }
    }
}