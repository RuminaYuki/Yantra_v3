using UnityEngine;

public enum EffectType
{
    HomingMissile,
    RadialPush,
    SelfHeal
}

public enum EffectActivationMode
{
    SingleUse,
    RepeatDuringDuration,
    HoldRepeat,
    HoldCommit
}

[CreateAssetMenu(
    fileName = "EffectDefinition",
    menuName = "YUKI Learning/Spell/Effect Definition")]
public class EffectDefinition : ScriptableObject
{
    [Header("Effect")]
    [SerializeField] private EffectType effectType;
    [SerializeField] private EffectActivationMode activationMode;
    [SerializeField, Min(0f)] private float duration = 1f;
    [SerializeField, Min(0f)] private float cooldown;

    [Header("Effect Values")]
    [SerializeField, Min(0f)] private float power = 10f;
    [SerializeField, Min(0f)] private float radius = 5f;
    [SerializeField, Min(0f)] private float projectileSpeed = 20f;
    [SerializeField, Min(0f)] private float homingStrength = 10f;
    [SerializeField] private GameObject projectilePrefab;

    public EffectType Type => effectType;
    public EffectActivationMode ActivationMode => activationMode;
    public float Duration => duration;
    public float Cooldown => cooldown;
    public float Power => power;
    public float Radius => radius;
    public float ProjectileSpeed => projectileSpeed;
    public float HomingStrength => homingStrength;
    public GameObject ProjectilePrefab => projectilePrefab;
}
