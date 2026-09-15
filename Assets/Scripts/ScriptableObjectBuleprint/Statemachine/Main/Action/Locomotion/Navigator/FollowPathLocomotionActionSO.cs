using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "FollowPathLocomotionAction",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Locomotion/Navigation/Follow Path Locomotion")]
public class FollowPathLocomotionActionSO : StateActionSO
{
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new FollowPathLocomotionAction();
    }
}

public class FollowPathLocomotionAction : StateAction
{
    private PathNavigator _pathNavigator;
    private BaseLocomotion _locomotion;

    public override void Awake(StateMachine stateMachine)
    {
        _pathNavigator = stateMachine.GetComponent<PathNavigator>();
        _locomotion = stateMachine.GetComponent<BaseLocomotion>();

        if (_pathNavigator == null)
            Debug.LogError("FollowPathLocomotionAction cannot find PathNavigator.");

        if (_locomotion == null)
            Debug.LogError("FollowPathLocomotionAction cannot find BaseLocomotion.");
    }

    public override void OnUpdate()
    {
        if (_pathNavigator == null || _locomotion == null)
            return;

        Vector3 direction = _pathNavigator.Direction;
        _locomotion.SetMovementDirection(direction);
        _locomotion.SetFacingDirection(direction);
    }

    public override void OnStateExit()
    {
        _locomotion?.ClearMovementDirection();
    }
}
