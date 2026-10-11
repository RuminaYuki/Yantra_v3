using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using SDFcl.GamePlay.Interactable;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(PlayerInteract))]
[CreateAssetMenu(
    fileName = "NewIsPlayerInteracting_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/InteractSystem/PlayerInteract/Is Player Interacting")]
public class IsPlayerInteractingConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
    {
        return new IsPlayerInteractingCondition();
    }
}

// True while PlayerInteract.GetIsInterctable is true (set by BaseInteractor on Interact / CancelInteraction).
// It's a state, not an event, so it's just read when checked: no Tick needed.
public class IsPlayerInteractingCondition : Condition
{
    private AnchorReference<PlayerInteract> playerInteract;

    public override void Awake(StateMachine stateMachine)
    {
        playerInteract = new AnchorReference<PlayerInteract>(stateMachine, this);
    }

    protected override bool Statement()
    {
        return playerInteract.TryGet(out PlayerInteract interact) && interact.GetIsInterctable;
    }
}
