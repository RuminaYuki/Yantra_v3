using UnityEngine;

public class Follow3D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform _target;

    [Header("Constraint")]
    [SerializeField] private bool _constrainPosition = true;
    [SerializeField] private bool _constrainRotation = true;
    [SerializeField] private bool _maintainOffset;

    [Header("Offset")]
    [SerializeField] private Vector3 _positionOffset;
    [SerializeField] private Vector3 _rotationOffset;

    [Header("Lerp Speed")]
    [Tooltip("0 = follow tightly with no lag. Higher = more lag, falls further behind the target.")]
    [SerializeField, Min(0f)] private float _positionLerpSpeed;
    [Tooltip("0 = follow tightly with no lag. Higher = more lag, falls further behind the target.")]
    [SerializeField, Min(0f)] private float _rotationLerpSpeed;

    private Vector3 _maintainedPositionOffset;
    private Quaternion _maintainedRotationOffset = Quaternion.identity;

    private void Start()
    {
        CacheMaintainedOffset();
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        if (_constrainPosition)
        {
            Vector3 localOffset = _maintainedPositionOffset + _positionOffset;
            Vector3 targetPosition = _target.position + _target.rotation * localOffset;
            float t = LagToLerpT(_positionLerpSpeed);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        }

        if (_constrainRotation)
        {
            Quaternion targetRotation = _target.rotation * _maintainedRotationOffset * Quaternion.Euler(_rotationOffset);
            float t = LagToLerpT(_rotationLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }
    }

    private static float LagToLerpT(float lag)
    {
        return lag <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / lag);
    }

    private void CacheMaintainedOffset()
    {
        if (_target == null || !_maintainOffset)
        {
            _maintainedPositionOffset = Vector3.zero;
            _maintainedRotationOffset = Quaternion.identity;
            return;
        }

        _maintainedPositionOffset = Quaternion.Inverse(_target.rotation) * (transform.position - _target.position);
        _maintainedRotationOffset = Quaternion.Inverse(_target.rotation) * transform.rotation;
    }

    #region API
    public void SetTarget(Transform target)
    {
        _target = target;
        CacheMaintainedOffset();
    }

    public void SetMaintainOffset(bool maintainOffset)
    {
        _maintainOffset = maintainOffset;
        CacheMaintainedOffset();
    }

    public void SetConstrainPosition(bool enable) => _constrainPosition = enable;
    public void SetConstrainRotation(bool enable) => _constrainRotation = enable;

    public void SetPositionOffset(Vector3 offset) => _positionOffset = offset;
    public void SetRotationOffset(Vector3 eulerOffset) => _rotationOffset = eulerOffset;

    public void SetPositionLerpSpeed(float speed) => _positionLerpSpeed = speed;
    public void SetRotationLerpSpeed(float speed) => _rotationLerpSpeed = speed;
    #endregion
}
