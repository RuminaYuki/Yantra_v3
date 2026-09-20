using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
[CreateAssetMenu(
    fileName = "ExecuteParry_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/BlockSystem/Execute Parry")]
public class ExecuteParryActionSO : StateActionSO
{
    [Tooltip("Whether the parry action is enabled.")]
    [SerializeField] private bool _enable = true;
    [Tooltip("Whether to reset the parry value when exiting the state.")]
    [SerializeField] private bool _resetValueOnExit = true;
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new ExecuteParryAction(_enable, _resetValueOnExit);
    }
}

public class ExecuteParryAction : StateAction
{
    private readonly bool _enable;
    private readonly bool _resetValueOnExit;
    private BlockSystem _blockSystem;

    public ExecuteParryAction(bool enable, bool resetValueOnExit)
    {
        _enable = enable;
        _resetValueOnExit = resetValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _blockSystem = stateMachine.GetComponent<BlockSystem>();

        if (_blockSystem == null)
            Debug.LogError(
                "ExecuteParryAction requires BlockSystem.",
                stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        if (_blockSystem == null)
            return;

        _blockSystem.ExecuteParry(_enable);
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (_blockSystem == null || !_resetValueOnExit)
            return;

        _blockSystem.ExecuteParry(!_enable);
    }
}