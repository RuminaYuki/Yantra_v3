using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using SDFcl.GamePlay.Interactable;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(PlayerInteract))]
[CreateAssetMenu(
    fileName = "NewSetPlayerInteractable_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/InteractSystem/PlayerInteract/Set Player Interactable")]
public class SetPlayerInteractableActionSO : StateActionSO
{
    [SerializeField] private bool value = false;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool resetValueOnExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetPlayerInteractableAction(value, resetValueOnExit);
    }
}

public class SetPlayerInteractableAction : StateAction
{
    private readonly bool value;
    private readonly bool resetValueOnExit;
    private AnchorReference<PlayerInteract> playerInteractRef;

    public SetPlayerInteractableAction(bool value, bool resetValueOnExit)
    {
        this.value = value;
        this.resetValueOnExit = resetValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        playerInteractRef = new AnchorReference<PlayerInteract>(stateMachine, this);
    }
    public override void OnStateEnter()
    {
        SetInteractable(value);
    }
    public override void OnStateExit()
    {
        if (!resetValueOnExit) return;
        SetInteractable(!value);
    }
    public override void OnUpdate(){}

    private void SetInteractable(bool interactable)
    {
        if (!playerInteractRef.TryGet(out PlayerInteract playerInteract)) return;
        playerInteract.SetIsInterctable(interactable);
    }
}
