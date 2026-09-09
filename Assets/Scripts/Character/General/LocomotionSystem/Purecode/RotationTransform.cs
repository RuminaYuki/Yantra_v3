using UnityEngine;
public class RotationTransform
{
    private readonly Transform _transform;

    private float _speed;

    public RotationTransform(Transform transform, float speed = 1)
    {
        _transform = transform;
        _speed = speed;
    }

    public void Rotate(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction, Vector3.up);

        Quaternion nextRotation = Quaternion.Slerp(_transform.rotation,
            targetRotation,_speed * Time.fixedDeltaTime);

        _transform.rotation = nextRotation;
    }

    #region Secondary API
    public float Speed{get=>_speed; set => _speed = value;}
    #endregion
}