using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(PathNavigator))]
[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "FollowPathLocomotion_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Follow Path Locomotion")]
public class FollowPathLocomotionActionSO : StateActionSO
{
    [SerializeField] private bool _updateFacing = true;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new FollowPathLocomotionAction(_updateFacing);
    }
}

public class FollowPathLocomotionAction : StateAction
{
    private readonly bool _updateFacing;
    private PathNavigator _pathNavigator;
    private BaseLocomotion _locomotion;

    public FollowPathLocomotionAction(bool updateFacing)
    {
        _updateFacing = updateFacing;
    }

    public override void Awake(StateMachine stateMachine)
    {
        stateMachine.TryGetRequired(out _pathNavigator, this);
        stateMachine.TryGetRequired(out _locomotion, this);
    }

    public override void OnUpdate()
    {
        Vector3 direction = _pathNavigator.Direction;
        _locomotion.SetMovementDirection(direction);

        if (_updateFacing)
            _locomotion.SetFacingDirection(direction);
    }

    public override void OnStateExit()
    {
        _locomotion.ClearMovementDirection();
        if (_updateFacing)
        _locomotion.ClearFacingDirection();
    }
}
