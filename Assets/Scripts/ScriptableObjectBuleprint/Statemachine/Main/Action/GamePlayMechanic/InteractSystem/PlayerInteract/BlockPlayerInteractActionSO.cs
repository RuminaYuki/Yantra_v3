using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using SDFcl.GamePlay.Interactable;

// Put this on a state where the player must not start an interaction
// (e.g. Spell_State: picking or casting a spell). Interact works again when the state exits.
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(PlayerInteract))]
[CreateAssetMenu(
    fileName = "NewBlockPlayerInteract_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/InteractSystem/PlayerInteract/Block Player Interact")]
public class BlockPlayerInteractActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new BlockPlayerInteractAction();
    }
}

public class BlockPlayerInteractAction : StateAction
{
    private AnchorReference<PlayerInteract> playerInteractRef;

    public override void Awake(StateMachine stateMachine)
    {
        playerInteractRef = new AnchorReference<PlayerInteract>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!playerInteractRef.TryGet(out PlayerInteract playerInteract)) return;
        playerInteract.AddInteractBlocker(this);
    }

    public override void OnStateExit()
    {
        if (!playerInteractRef.TryGet(out PlayerInteract playerInteract)) return;
        playerInteract.RemoveInteractBlocker(this);
    }

    public override void OnUpdate() { }
}
