using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewSetFlagOnEnter_Action",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Standard/Set Flag On Enter")]
public class SetFlagOnEnterActionSO : StateActionSO
{
    [SerializeField] private FlagSO flag;
    [SerializeField] private bool value = true;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool changeValueOnExit = false;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetFlagOnEnterAction(flag, value, changeValueOnExit);
    }
}

public class SetFlagOnEnterAction : StateAction
{
    private readonly FlagSO flag;
    private readonly bool value;
    private readonly bool changeValueOnExit;
    private StateFlagsAccess stateFlags;

    public SetFlagOnEnterAction(FlagSO flag, bool value, bool changeValueOnExit)
    {
        this.flag = flag;
        this.value = value;
        this.changeValueOnExit = changeValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        stateFlags = stateMachine.GetComponent<StateFlagsAccess>();

        if (stateFlags == null)
            Debug.LogError("SetFlagOnEnterAction requires StateFlags or StateFlagReader on the StateMachine GameObject.");
    }

    public override void OnStateEnter()
    {
        if (stateFlags == null || flag == null) return;
        stateFlags.Set(flag, value);
    }

    public override void OnUpdate(){}

    public override void OnStateExit()
    {
        if (changeValueOnExit)
        {
            stateFlags.Set(flag, !value);
        }
    }
}
