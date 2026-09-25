using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "GianSkillOnHitPointAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/SkillPoint/Gian Skill On Hit Point")]
public class GianSkillOnHitPointActionSO : StateActionSO
{
    [SerializeField] private float gianSkillPointAmount = 1f;

    public float GianSkillPointAmount
    {
        get => gianSkillPointAmount;
        set
        {
            if (value < 0f)
            {
                Debug.LogWarning("Gian skill point amount cannot be negative. Setting to 0.");
                gianSkillPointAmount = 0f;
                return;
            }
            gianSkillPointAmount = value;
        }
    }

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new GianSkillOnHitPointAction(gianSkillPointAmount);
    }
}

public class GianSkillOnHitPointAction : StateAction
{
    private readonly float gianSkillPointAmount;
    private AttackSphereCast attackSphereCast;
    private SkillPoints skillPoints;
    public GianSkillOnHitPointAction(float gianSkillPointAmount)
    {
        this.gianSkillPointAmount = gianSkillPointAmount;
    }
    public override void Awake(StateMachine stateMachine)
    {
        attackSphereCast = stateMachine.GetComponent<AttackSphereCast>();
        skillPoints = stateMachine.GetComponent<SkillPoints>();
    }
    public override void OnStateEnter()
    {
        if (attackSphereCast == null) return;
        attackSphereCast.OnHit += OnHit;
    }

    public override void OnUpdate(){}
    
    public override void OnStateExit()
    {
        if (attackSphereCast == null) return;
        attackSphereCast.OnHit -= OnHit;
    }
    
    private void OnHit()
    {
        skillPoints?.gaint(gianSkillPointAmount);
    }
}
