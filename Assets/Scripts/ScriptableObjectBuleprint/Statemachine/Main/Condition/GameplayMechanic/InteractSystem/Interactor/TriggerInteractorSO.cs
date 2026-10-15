using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using SDFcl.GamePlay.Interactable;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Iinteractor))]
[CreateAssetMenu(
    fileName = "NewTriggerInteractor_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/GamePlayMechanic/InteractSystem/Interactor/Trigger Interactor")]
public class TriggerInteractorSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
    {
        return new TriggerInteractor();
    }
}

public class TriggerInteractor : Condition
{
    private AnchorReference<Iinteractor> interactor;
    private Iinteractor subscribed;   // the interactor we are listening to right now
    private bool hasInteracted;

    public override void Awake(StateMachine stateMachine)
    {
        interactor = new AnchorReference<Iinteractor>(stateMachine, this);
        SyncSubscription();
    }
    public override void OnStateEnter()
    {
        SyncSubscription();
        hasInteracted = false;
    }
    protected override void OnTick()
    {
        SyncSubscription();
    }

    // Follows the anchor: if it now points at another interactor (or one appeared late), move the listener.
    private void SyncSubscription()
    {
        interactor.TryGet(out Iinteractor current);
        if (ReferenceEquals(current, subscribed))
            return;

        if (subscribed != null)
            subscribed.OnInteract -= HandleInteract;

        subscribed = current;

        if (subscribed != null)
            subscribed.OnInteract += HandleInteract;
    }
    protected override bool Statement()
    {
        return hasInteracted;
    }
    public override void Dispose()
    {
        if (subscribed != null)
        {
            subscribed.OnInteract -= HandleInteract;
        }
        subscribed = null;
    }

    private void HandleInteract(GameObject _) => hasInteracted = true;
}
