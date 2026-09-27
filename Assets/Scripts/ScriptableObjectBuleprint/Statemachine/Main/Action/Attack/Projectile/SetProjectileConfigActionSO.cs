using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(ProjectileShooter))]
[CreateAssetMenu(
    fileName = "SetProjectileConfig_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Attack/Projectile/Set Projectile Config")]
public class SetProjectileConfigActionSO : StateActionSO
{
    [SerializeField] private ProjectileBullet.ProjectileConfig _config;

    [Header("Bullet Type (leave Pool Tag empty to not change it)")]
    [SerializeField] private string _poolTag;
    [SerializeField] private ProjectileBullet _fallbackPrefab;

    [Header("Aim Target (leave empty for a straight shot)")]
    [SerializeField] private GameObjectAnchor _targetAnchor;

    public ProjectileBullet.ProjectileConfig Config
    {
        get => _config;
        set => _config = value;
    }

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetProjectileConfigAction(_config, _poolTag, _fallbackPrefab, _targetAnchor);
    }
}

public class SetProjectileConfigAction : StateAction
{
    private readonly ProjectileBullet.ProjectileConfig _config;
    private readonly string _poolTag;
    private readonly ProjectileBullet _fallbackPrefab;
    private readonly GameObjectAnchor _targetAnchor;

    private ProjectileShooter _shooter;

    public SetProjectileConfigAction(
        ProjectileBullet.ProjectileConfig config,
        string poolTag,
        ProjectileBullet fallbackPrefab,
        GameObjectAnchor targetAnchor)
    {
        _config = config;
        _poolTag = poolTag;
        _fallbackPrefab = fallbackPrefab;
        _targetAnchor = targetAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _shooter, this);
    }

    public override void OnStateEnter()
    {
        _shooter.SetBulletConfig(_config);

        if (!string.IsNullOrEmpty(_poolTag))
            _shooter.SetBulletType(_poolTag, _fallbackPrefab);

        UpdateTarget();
    }

    public override void OnUpdate()
    {
        UpdateTarget();
    }

    private void UpdateTarget()
    {
        if (_targetAnchor != null && _targetAnchor.IsSet)
            _shooter.SetTarget(_targetAnchor.Value.transform.position);
        else
            _shooter.ClearTarget();
    }
}
