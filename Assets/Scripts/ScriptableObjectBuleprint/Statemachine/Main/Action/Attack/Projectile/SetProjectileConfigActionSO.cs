using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(ProjectileShooter))]
[CreateAssetMenu(
    fileName = "SetProjectileConfig_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Attack/Projectile/Set Projectile Config")]
public class SetProjectileConfigActionSO : StateActionSO
{
    // Same defaults as ProjectileShooter's own config.
    [SerializeField, Exposed] private ProjectileBullet.ProjectileConfig _config = new ProjectileBullet.ProjectileConfig
    {
        aimMode = ProjectileBullet.AimMode.BySpeed,
        apexHeight = 2f,
        projectileSpeed = 15f,
        gravity = 9.81f,
        lifetime = 5f,
        damage = 10f,
        homing = new ProjectileBullet.HomingSettings { turnSpeed = 120f, duration = 1.5f, maxAngle = 70f }
    };

    [Header("Bullet Type (leave Pool Tag empty to not change it)")]
    [SerializeField] private string _poolTag;
    [SerializeField] private ProjectileBullet _fallbackPrefab;

    [Header("Aim Target (leave empty for a straight shot)")]
    [SerializeField] private AnchorIDSO _targetAnchor;
    [Tooltip("Added to the target's position (world space), for aiming and homing. The pivot is often at the feet, so e.g. (0, 1.2, 0) aims at the chest.")]
    [SerializeField, Exposed] private Vector3 _targetOffset = new Vector3(0f, 1f, 0f);

    public ProjectileBullet.ProjectileConfig Config
    {
        get => _config;
        set => _config = value;
    }

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetProjectileConfigAction(_config, _poolTag, _fallbackPrefab, _targetAnchor, _targetOffset);
    }
}

public class SetProjectileConfigAction : StateAction
{
    private readonly ProjectileBullet.ProjectileConfig _config;
    private readonly string _poolTag;
    private readonly ProjectileBullet _fallbackPrefab;
    private readonly AnchorIDSO _targetAnchor;
    private readonly Vector3 _targetOffset;

    private AnchorReference<ProjectileShooter> _shooter;
    private AnchorReference<Transform> _target;

    public SetProjectileConfigAction(
        ProjectileBullet.ProjectileConfig config,
        string poolTag,
        ProjectileBullet fallbackPrefab,
        AnchorIDSO targetAnchor,
        Vector3 targetOffset)
    {
        _config = config;
        _poolTag = poolTag;
        _fallbackPrefab = fallbackPrefab;
        _targetAnchor = targetAnchor;
        _targetOffset = targetOffset;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _shooter = new AnchorReference<ProjectileShooter>(stateMachine, this);
        if (_targetAnchor != null)
            _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);
    }

    public override void OnStateEnter()
    {
        if (!_shooter.TryGet(out ProjectileShooter shooter)) return;

        shooter.SetBulletConfig(_config);

        if (!string.IsNullOrEmpty(_poolTag))
            shooter.SetBulletType(_poolTag, _fallbackPrefab);

        UpdateTarget(shooter);
    }

    public override void OnUpdate()
    {
        if (_shooter.TryGet(out ProjectileShooter shooter))
            UpdateTarget(shooter);
    }

    // No target anchor = straight shot. The Transform itself, so a homing config can follow it.
    private void UpdateTarget(ProjectileShooter shooter)
    {
        if (_target != null && _target.TryGet(out Transform target))
            shooter.SetTarget(target, _targetOffset);
        else
            shooter.ClearTarget();
    }
}
