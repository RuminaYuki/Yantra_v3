using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Gives skill points once, as soon as the state is entered. No hit needed (e.g. a Glory kill).
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(SkillPoints))]
[CreateAssetMenu(
    fileName = "GianSkillPointAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/SkillPoint/Gian Skill Point")]
public class GianSkillPointActionSO : StateActionSO
{
    [SerializeField, Exposed, Min(0f)] private float gianSkillPointAmount = 1f;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new GianSkillPointAction(gianSkillPointAmount);
    }
}

public class GianSkillPointAction : StateAction
{
    private readonly float gianSkillPointAmount;
    private AnchorReference<SkillPoints> skillPoints;

    public GianSkillPointAction(float gianSkillPointAmount)
    {
        this.gianSkillPointAmount = gianSkillPointAmount;
    }

    public override void Awake(StateMachine stateMachine)
    {
        skillPoints = new AnchorReference<SkillPoints>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (skillPoints.TryGet(out SkillPoints points))
            points.gaint(gianSkillPointAmount);
    }

    public override void OnUpdate() { }
    public override void OnStateExit() { }
}
