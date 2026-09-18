using UnityEngine;

public class ProjectileBullet : MonoBehaviour
{
    [Header("Trajectory")]
    [SerializeField] protected float _apexHeight = 2f;
    [SerializeField] protected float _projectileSpeed = 15f;
    [SerializeField] protected float _gravity = 9.81f;
    [SerializeField] protected float _lifetime = 5f;

    protected Vector3 Velocity;

    private float _lifeTimer;

    public virtual void Launch()
    {
        float launchUpSpeed = Mathf.Sqrt(2f * _gravity * _apexHeight);
        Velocity = transform.forward * _projectileSpeed + Vector3.up * launchUpSpeed;

        _lifeTimer = 0f;
    }

    public float GetFlightTime() => 2f * Mathf.Sqrt(2f * _apexHeight / _gravity);
    public float GetRange() => _projectileSpeed * GetFlightTime();

    // Alternate mode: pick the landing point instead of the apex height — apex height becomes derived (GetApexHeight()) instead of a free input.
    public virtual void LaunchAtTarget(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        Vector3 horizontalDisplacement = new Vector3(toTarget.x, 0f, toTarget.z);
        float horizontalDistance = horizontalDisplacement.magnitude;
        float heightDifference = toTarget.y;

        float flightTime = horizontalDistance / _projectileSpeed;
        float verticalLaunchSpeed =
            (heightDifference + 0.5f * _gravity * flightTime * flightTime) / flightTime;

        Vector3 horizontalDirection = horizontalDisplacement.normalized;
        Velocity = horizontalDirection * _projectileSpeed + Vector3.up * verticalLaunchSpeed;

        _lifeTimer = 0f;
    }

    // Works after either Launch() or LaunchAtTarget() — reads back the apex height implied by the current vertical velocity.
    public float GetApexHeight() => (Velocity.y * Velocity.y) / (2f * _gravity);

    protected virtual void Update()
    {
        _lifeTimer += Time.deltaTime;
        if (_lifetime > 0f && _lifeTimer >= _lifetime)
        {
            Destroy(gameObject);
            return;
        }

        Velocity += Vector3.down * _gravity * Time.deltaTime;
        transform.position += Velocity * Time.deltaTime;

        if (Velocity.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(Velocity);
    }
}
