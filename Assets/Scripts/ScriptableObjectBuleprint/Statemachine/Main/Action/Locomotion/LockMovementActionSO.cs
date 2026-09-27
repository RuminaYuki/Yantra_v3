using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "LockMovementAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Lock Movement")]
public class LockMovementActionSO : StateActionSO
{
    [SerializeField] private bool _resetMoveAnimation = true;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new LockMovementAction(_resetMoveAnimation);
    }
}

public class LockMovementAction : StateAction
{
    private readonly bool _resetMoveAnimation;
    private BaseLocomotion _locomotion;

    public LockMovementAction(bool resetMoveAnimation)
    {
        _resetMoveAnimation = resetMoveAnimation;
    }

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _locomotion, this);
    }

    public override void OnStateEnter()
    {
        if (_locomotion != null)
        {
            _locomotion.LockLocomotion(
                this,
                _resetMoveAnimation);
        }
    }

    public override void OnStateExit()
    {
        if (_locomotion != null)
        {
            _locomotion.UnlockLocomotion(this);
        }
    }

    public override void OnUpdate() { }
}
