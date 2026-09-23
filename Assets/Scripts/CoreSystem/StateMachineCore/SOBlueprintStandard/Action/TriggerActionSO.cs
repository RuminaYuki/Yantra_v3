using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewTrigger_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Trigger")]
public class TriggerActionSO : StateActionSO
{
    [SerializeField] private TriggerConditionSO _triggerConditionSO;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new TriggerAction(_triggerConditionSO);
    }
}

public class TriggerAction : StateAction
{
    private readonly TriggerConditionSO _triggerConditionSO;

    public TriggerAction(TriggerConditionSO triggerConditionSO)
    {
        _triggerConditionSO = triggerConditionSO;
    }

    public override void OnStateEnter()
    {
        _triggerConditionSO.Trigger();
    }

    public override void OnUpdate() { }
}
