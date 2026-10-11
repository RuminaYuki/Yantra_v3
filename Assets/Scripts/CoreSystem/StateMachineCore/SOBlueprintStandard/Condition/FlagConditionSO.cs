using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(StateFlagsAccess))]
[CreateAssetMenu(
    fileName = "NewFlag_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Check Flag")]
public class FlagConditionSO : StateConditionSO
{
    [SerializeField] private FlagSO flag;

    protected override Condition OnCreateCondition()
    {
        return new FlagCondition(flag);
    }
}

public class FlagCondition : Condition
{
    private readonly FlagSO flag;
    private AnchorReference<StateFlagsAccess> stateFlags;

    public FlagCondition(FlagSO flag)
    {
        this.flag = flag;
    }

    public override void Awake(StateMachine stateMachine)
    {
        // Self Anchor empty = this state machine's own flags; set = another object's flags.
        stateFlags = new AnchorReference<StateFlagsAccess>(stateMachine, this);
    }

    protected override bool Statement()
    {
        // The anchored object may not be linked yet; the box looks again when the Provider changes.
        return flag != null &&
               stateFlags.TryGet(out StateFlagsAccess flags) &&
               flags.Get(flag);
    }
}
