using UnityEngine;

[CreateAssetMenu(fileName = "NewProjectileConfig_Modifier",
    menuName = "YUKI Learning State Machine/StatsParameter/Modifiers/StatemachineList/Action/Set Projectile Config")]
public class SetProjectileConfigModifierSO : ScriptableObject, IStatModifier
{
    [SerializeField] private SetProjectileConfigActionSO _target;
    [SerializeField] private ProjectileBullet.ProjectileConfig _config;

    public void Apply()
    {
        _target.Config = _config;
    }
}
