using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using SDFcl.GamePlay.Interactable;

[RequiresOwnerComponent(typeof(PlayerInteract), UnlessAnchorField = "anchorTarget")]
[CreateAssetMenu(
    fileName = "NewIsPlayerInteracting_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/InteractSystem/PlayerInteract/Is Player Interacting")]
public class IsPlayerInteractingConditionSO : StateConditionSO
{
    [Header("If the TargetAnchor is set, Use that anchor instead")]
    [SerializeField] private GameObjectAnchor anchorTarget;

    public override Condition CreateCondition()
    {
        return new IsPlayerInteractingCondition(anchorTarget);
    }
}

// True while PlayerInteract.GetIsInterctable is true (set by BaseInteractor on Interact / CancelInteraction).
// It's a state, not an event, so it's just read when checked: no Tick needed.
public class IsPlayerInteractingCondition : Condition
{
    private readonly GameObjectAnchor anchorTarget;
    private PlayerInteract playerInteract;

    public IsPlayerInteractingCondition(GameObjectAnchor anchorTarget = null)
    {
        this.anchorTarget = anchorTarget;
    }

    public override void Awake(StateMachine stateMachine)
    {
        anchorTarget.TryGetComponentOrOwner(stateMachine, out playerInteract, this);
    }

    protected override bool Statement()
    {
        return playerInteract.GetIsInterctable;
    }
}
