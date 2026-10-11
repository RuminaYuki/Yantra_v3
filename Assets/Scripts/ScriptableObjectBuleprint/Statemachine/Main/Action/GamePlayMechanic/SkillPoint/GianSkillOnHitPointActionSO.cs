using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(AttackSphereCast))]
[RequiresOwnerComponent(typeof(SkillPoints))]
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

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new GianSkillOnHitPointAction(gianSkillPointAmount);
    }
}

public class GianSkillOnHitPointAction : StateAction
{
    private readonly float gianSkillPointAmount;
    private AnchorReference<AttackSphereCast> attackSphereCast;
    private AnchorReference<SkillPoints> skillPoints;
    // The one we subscribed to, so exit unsubscribes the same object even if the anchor changed.
    private AttackSphereCast subscribedSphereCast;
    public GianSkillOnHitPointAction(float gianSkillPointAmount)
    {
        this.gianSkillPointAmount = gianSkillPointAmount;
    }
    public override void Awake(StateMachine stateMachine)
    {
        attackSphereCast = new AnchorReference<AttackSphereCast>(stateMachine, this);
        skillPoints = new AnchorReference<SkillPoints>(stateMachine, this);
    }
    public override void OnStateEnter()
    {
        if (!attackSphereCast.TryGet(out subscribedSphereCast)) return;
        subscribedSphereCast.OnHit += OnHit;
    }

    public override void OnUpdate(){}
    
    public override void OnStateExit()
    {
        if (subscribedSphereCast != null)
            subscribedSphereCast.OnHit -= OnHit;
        subscribedSphereCast = null;
    }
    
    private void OnHit()
    {
        if (skillPoints.TryGet(out SkillPoints points))
            points.gaint(gianSkillPointAmount);
    }
}
