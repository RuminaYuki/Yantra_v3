using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "LockMovementAction",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Locomotion/Lock Movement")]
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
        _locomotion = stateMachine.GetComponent<BaseLocomotion>();

        if (_locomotion == null)
            Debug.LogError("LockMovementAction cannot find BaseLocomotion.");
    }

    public override void OnStateEnter()
    {
        _locomotion?.LockLocomotion(
            this,
            _resetMoveAnimation);
    }

    public override void OnStateExit()
    {
        _locomotion?.UnlockLocomotion(this);
    }

    public override void OnUpdate() { }
}
