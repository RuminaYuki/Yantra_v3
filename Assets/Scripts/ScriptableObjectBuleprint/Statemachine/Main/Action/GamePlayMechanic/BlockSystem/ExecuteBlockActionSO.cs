using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
[CreateAssetMenu(
    fileName = "ExecuteBlock_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/BlockSystem/Execute Block")]
public class ExecuteBlockActionSO : StateActionSO
{
    [Tooltip("Whether the block action is enabled.")]
    [SerializeField] private bool _enable = true;
    [Tooltip("Whether to reset the block value when exiting the state.")]
    [SerializeField] private bool _resetValueOnExit = true;
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new ExecuteBlockAction(_enable, _resetValueOnExit);
    }
}

public class ExecuteBlockAction : StateAction
{
    private readonly bool _enable;
    private readonly bool _resetValueOnExit;
    private BlockSystem _blockSystem;

    public ExecuteBlockAction(bool enable, bool resetValueOnExit)
    {
        _enable = enable;
        _resetValueOnExit = resetValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _blockSystem = stateMachine.GetComponent<BlockSystem>();

        if (_blockSystem == null)
            Debug.LogError(
                "ExecuteBlockAction requires BlockSystem.",
                stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        if (_blockSystem == null)
            return;

        _blockSystem.ExecuteBlock(_enable);
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (_blockSystem == null || !_resetValueOnExit)
            return;

        _blockSystem.ExecuteBlock(!_enable);
    }
}