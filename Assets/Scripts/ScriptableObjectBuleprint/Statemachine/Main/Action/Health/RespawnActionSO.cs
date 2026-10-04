using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(Health))]
[CreateAssetMenu(
    fileName = "NewRespawn_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Health/Respawn")]
public class RespawnActionSO : StateActionSO
{
    [Tooltip("If true, Health Amount is a percent of Max Health (0-100). If false, it is a flat HP value.")]
    [SerializeField] private bool usePercent = true;
    [Tooltip("HP to respawn with. Clamped to 0..MaxHealth.")]
    [SerializeField, Min(0f)] private float healthAmount = 100f;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new RespawnAction(usePercent, healthAmount);
    }
}

// Sets the owner's HP on state enter. Works on a dead owner too: once HP is above 0, IsDead is false again.
public class RespawnAction : StateAction
{
    private readonly bool usePercent;
    private readonly float healthAmount;
    private Health health;

    public RespawnAction(bool usePercent, float healthAmount)
    {
        this.usePercent = usePercent;
        this.healthAmount = healthAmount;
    }

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out health, this);
    }

    public override void OnStateEnter()
    {
        float targetHP = usePercent
            ? health.MaxHealth * (healthAmount / 100f)
            : healthAmount;

        if (targetHP <= 0f)
        {
            Debug.LogWarning($"[RespawnAction] Respawn HP is 0 on '{health.name}', so it stays dead.", health);
        }

        health.SetCurrentHealth(targetHP);
    }

    public override void OnUpdate() { }

    public override void OnStateExit() { }
}
