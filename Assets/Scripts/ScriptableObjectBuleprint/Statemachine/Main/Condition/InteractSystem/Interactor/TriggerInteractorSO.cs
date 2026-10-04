using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using SDFcl.GamePlay.Interactable;

[RequiresOwnerComponent(typeof(Iinteractor), UnlessAnchorField = "anchorTarget")]
[CreateAssetMenu(
    fileName = "NewTriggerInteractor_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/InteractSystem/Interactor/Trigger Interactor")]
public class TriggerInteractorSO : StateConditionSO
{
    [Header("If the TargetAnchor is set, Use that anchor instead")]
    [SerializeField] private GameObjectAnchor anchorTarget;
    public override Condition CreateCondition()
    {
        return new TriggerInteractor(anchorTarget);
    }
}

public class TriggerInteractor : Condition
{
    private readonly GameObjectAnchor anchorTarget;
    private Iinteractor interactor;
    private bool hasInteracted;
    public TriggerInteractor(GameObjectAnchor anchorTarget = null)
    {
        this.anchorTarget = anchorTarget;
    }
    public override void Awake(StateMachine stateMachine)
    {
        anchorTarget.TryGetComponentOrOwner(stateMachine, out interactor, this);
        if (interactor != null)
        {
            interactor.OnInteract += HandleInteract;
        }
    }
    public override void OnStateEnter()
    {
        hasInteracted = false;
    }
    protected override bool Statement()
    {
        return hasInteracted;
    }
    public override void Dispose()
    {
        if (interactor != null)
        {
            interactor.OnInteract -= HandleInteract;
        }
    }

    private void HandleInteract(GameObject _) => hasInteracted = true;
}
