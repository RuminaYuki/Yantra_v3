using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ProjectileBullet : MonoBehaviour
{
    public enum AimMode { BySpeed, ByApexHeight }

    // Turns the bullet toward a moving target after launch. Gravity is off while it homes.
    [System.Serializable]
    public struct HomingSettings
    {
        public bool enabled;
        [Tooltip("Degrees per second. Lower = wider turns, easier to dodge.")]
        public float turnSpeed;
        [Tooltip("Seconds it homes for, then flies on. 0 = until it hits or expires.")]
        public float duration;
        [Tooltip("Stops homing once the target is further off its heading than this, e.g. after flying past " +
                 "a dodge. 0 = no limit.")]
        public float maxAngle;
    }

    [System.Serializable]
    public struct ProjectileConfig
    {
        public AimMode aimMode;
        public float apexHeight;
        public float projectileSpeed;
        public float gravity;
        public float lifetime;
        public float damage;
        public DamageTypeID damageType;
        public HomingSettings homing;
    }

    [Header("Trajectory")]
    [Tooltip("BySpeed: speed is the input, flight time/apex are derived.\nByApexHeight: apex height is the input, speed is derived.")]
    [SerializeField] private AimMode _aimMode = AimMode.BySpeed;
    [SerializeField] private float _apexHeight = 2f;
    [SerializeField] private float _projectileSpeed = 15f;
    [SerializeField] private float _gravity = 9.81f;
    [SerializeField] private float _lifetime = 5f;

    [Header("Damage")]
    [SerializeField] private float _damage = 10f;
    [SerializeField] private DamageTypeID _damageType;

    [Header("Homing")]
    [SerializeField] private HomingSettings _homing = new HomingSettings { turnSpeed = 120f, duration = 1.5f, maxAngle = 70f };

    [HideInInspector] public string myPoolTag;

    private Rigidbody _rb;
    private Vector3 _velocity;
    private float _lifeTimer;
    private Transform _owner;

    private Transform _homingTarget;
    private Vector3 _homingOffset;
    private bool _isHoming;

    private void Awake()
    {
        // Movement must go through the Rigidbody (MovePosition), not raw transform assignment,
        // otherwise Unity treats it as a teleport and Continuous Collision Detection never kicks in.
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    public void Launch()
    {
        float launchUpSpeed = Mathf.Sqrt(2f * _gravity * _apexHeight);
        _velocity = transform.forward * _projectileSpeed + Vector3.up * launchUpSpeed;

        _lifeTimer = 0f;
        StartHoming();
    }

    // Aim-at-target mode: always lands exactly on target. Whichever value _aimMode treats as input, the other becomes derived.
    public void LaunchAtTarget(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        Vector3 horizontalDisplacement = new Vector3(toTarget.x, 0f, toTarget.z);
        float horizontalDistance = horizontalDisplacement.magnitude;
        float heightDifference = toTarget.y;
        Vector3 horizontalDirection = horizontalDisplacement.normalized;

        float horizontalSpeed;
        float verticalLaunchSpeed;

        if (_aimMode == AimMode.ByApexHeight)
        {
            // Apex must stay above the target, otherwise no real arc reaches it.
            float apex = Mathf.Max(_apexHeight, heightDifference + 0.01f);

            verticalLaunchSpeed = Mathf.Sqrt(2f * _gravity * apex);
            float timeUp = verticalLaunchSpeed / _gravity;
            float timeDown = Mathf.Sqrt(2f * (apex - heightDifference) / _gravity);

            horizontalSpeed = horizontalDistance / (timeUp + timeDown);
        }
        else
        {
            horizontalSpeed = _projectileSpeed;
            float flightTime = horizontalDistance / horizontalSpeed;
            verticalLaunchSpeed = (heightDifference + 0.5f * _gravity * flightTime * flightTime) / flightTime;
        }

        _velocity = horizontalDirection * horizontalSpeed + Vector3.up * verticalLaunchSpeed;

        _lifeTimer = 0f;
        StartHoming();
    }

    private void FixedUpdate()
    {
        _lifeTimer += Time.fixedDeltaTime;
        if (_lifetime > 0f && _lifeTimer >= _lifetime)
        {
            ReturnOrDestroy();
            return;
        }

        if (_isHoming)
            SteerTowardsTarget();
        else
            _velocity += Vector3.down * _gravity * Time.fixedDeltaTime;

        _rb.MovePosition(_rb.position + _velocity * Time.fixedDeltaTime);

        if (_velocity.sqrMagnitude > 0.0001f)
            _rb.MoveRotation(Quaternion.LookRotation(_velocity));
    }

    // ---------- Homing ----------

    // Set by the shooter before Launch. Null = no homing this shot (also clears a pooled bullet's old target).
    // Offset: world-space, from the target's pivot (e.g. up to the chest, since the pivot is often at the feet).
    public void SetHomingTarget(Transform target, Vector3 offset = default)
    {
        _homingTarget = target;
        _homingOffset = offset;
    }

    private void StartHoming()
    {
        _isHoming = _homing.enabled && _homingTarget != null && _homing.turnSpeed > 0f;
    }

    // Keeps the speed, only turns the heading. Once it stops (time up, target gone or behind it),
    // it doesn't start again: gravity takes over from the current velocity.
    private void SteerTowardsTarget()
    {
        bool timeUp = _homing.duration > 0f && _lifeTimer >= _homing.duration;
        if (timeUp || _homingTarget == null || !_homingTarget.gameObject.activeInHierarchy)
        {
            _isHoming = false;
            return;
        }

        Vector3 toTarget = _homingTarget.position + _homingOffset - _rb.position;
        if (toTarget.sqrMagnitude < 0.0001f || _velocity.sqrMagnitude < 0.0001f)
            return;

        if (_homing.maxAngle > 0f && Vector3.Angle(_velocity, toTarget) > _homing.maxAngle)
        {
            _isHoming = false;
            return;
        }

        float maxRadians = _homing.turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime;
        _velocity = Vector3.RotateTowards(_velocity, toTarget.normalized * _velocity.magnitude, maxRadians, 0f);
    }

    private void ReturnOrDestroy()
    {
        _homingTarget = null;
        _isHoming = false;

        if (ObjectPooler.Instance != null && !string.IsNullOrEmpty(myPoolTag))
            ObjectPooler.Instance.ReturnToPool(myPoolTag, gameObject);
        else
            Destroy(gameObject);
    }

    // Sets the shooter so the bullet won't hit itself or its own children.
    public void SetOwner(Transform owner)
    {
        _owner = owner;
    }

    // Lets a shooter override this bullet's defaults per shot (e.g. multiple weapons sharing one prefab with different damage/speed).
    public void Configure(ProjectileConfig config)
    {
        _aimMode = config.aimMode;
        _apexHeight = config.apexHeight;
        _projectileSpeed = config.projectileSpeed;
        _gravity = config.gravity;
        _lifetime = config.lifetime;
        _damage = config.damage;
        _damageType = config.damageType;
        _homing = config.homing;
    }

    // Both are handled since it depends on whether the bullet's/target's collider is set to Is Trigger.
    private void OnTriggerEnter(Collider other) => ProcessHit(other);
    private void OnCollisionEnter(Collision collision) => ProcessHit(collision.collider);

    private void ProcessHit(Collider other)
    {
        if (IsOwner(other))
            return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(_damage);
            if (_damageType != null)
                damageable.DamageType(_damageType);
        }

        ReturnOrDestroy();
    }

    // Matches Hitbox.CheckOwner: skip the shooter, its children, and anything else tagged the same as the shooter.
    private bool IsOwner(Collider other)
    {
        if (_owner == null)
            return false;

        if (other.transform == _owner || other.transform.IsChildOf(_owner))
            return true;

        return other.CompareTag(_owner.tag);
    }
}
