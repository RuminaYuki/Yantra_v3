using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(StateFlagsAccess))]
[CreateAssetMenu(
    fileName = "NewSetFlagOnEnter_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Set Flag On Enter")]
public class SetFlagOnEnterActionSO : StateActionSO
{
    [SerializeField] private FlagSO flag;
    [SerializeField] private bool value = true;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool changeValueOnExit = false;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetFlagOnEnterAction(flag, value, changeValueOnExit);
    }
}

public class SetFlagOnEnterAction : StateAction
{
    private readonly FlagSO flag;
    private readonly bool value;
    private readonly bool changeValueOnExit;
    private AnchorReference<StateFlagsAccess> stateFlags;

    public SetFlagOnEnterAction(FlagSO flag, bool value, bool changeValueOnExit)
    {
        this.flag = flag;
        this.value = value;
        this.changeValueOnExit = changeValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        stateFlags = new AnchorReference<StateFlagsAccess>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (flag == null || !stateFlags.TryGet(out StateFlagsAccess flags)) return;
        flags.Set(flag, value);
    }

    public override void OnUpdate(){}

    public override void OnStateExit()
    {
        if (changeValueOnExit && flag != null && stateFlags.TryGet(out StateFlagsAccess flags))
        {
            flags.Set(flag, !value);
        }
    }
}
