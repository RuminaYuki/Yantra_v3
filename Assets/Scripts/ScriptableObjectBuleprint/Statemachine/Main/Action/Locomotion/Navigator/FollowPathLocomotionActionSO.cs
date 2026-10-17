using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(PathNavigator))]
[RequiresOwnerComponent(typeof(BaseLocomotion))]
[CreateAssetMenu(
    fileName = "FollowPathLocomotion_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Locomotion/Navigation/Follow Path Locomotion")]
public class FollowPathLocomotionActionSO : StateActionSO
{
    [SerializeField] private bool _updateFacing = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new FollowPathLocomotionAction(_updateFacing);
    }
}

public class FollowPathLocomotionAction : StateAction
{
    private readonly bool _updateFacing;
    // Both live on the same character, so both follow the one Self Anchor.
    private AnchorReference<PathNavigator> _pathNavigator;
    private AnchorReference<BaseLocomotion> _locomotion;

    public FollowPathLocomotionAction(bool updateFacing)
    {
        _updateFacing = updateFacing;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _pathNavigator = new AnchorReference<PathNavigator>(stateMachine, this);
        _locomotion = new AnchorReference<BaseLocomotion>(stateMachine, this);
    }

    public override void OnUpdate()
    {
        if (!_pathNavigator.TryGet(out PathNavigator pathNavigator) ||
            !_locomotion.TryGet(out BaseLocomotion locomotion))
            return;

        Vector3 direction = pathNavigator.Direction;
        locomotion.SetMovementDirection(direction);

        if (_updateFacing)
            locomotion.SetFacingDirection(direction);
    }

    public override void OnStateExit()
    {
        if (!_locomotion.TryGet(out BaseLocomotion locomotion)) return;
        locomotion.ClearMovementDirection();
        if (_updateFacing)
            locomotion.ClearFacingDirection();
    }
}
