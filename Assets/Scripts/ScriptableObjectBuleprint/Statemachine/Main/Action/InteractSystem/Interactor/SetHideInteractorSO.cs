using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using SDFcl.GamePlay.Interactable;

[CreateAssetMenu(
    fileName = "NewSetHideInteractor_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/InteractSystem/Interactor/Set Hide Interactor")]
public class SetHideInteractorSO : StateActionSO
{
    [SerializeField] private bool value = true;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool resetValueOnExit = true;

    [Header("If the TargetAnchor is set, Use that anchor instead")]
    [SerializeField] private GameObjectAnchor anchorTarget;
    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetHideInteractor(value, resetValueOnExit, anchorTarget);
    }
}

public class SetHideInteractor : StateAction
{
    private readonly GameObjectAnchor anchorTarget;
    private readonly bool value;
    private readonly bool resetValueOnExit;
    private BaseInteractor baseInteractor;
    public SetHideInteractor(bool value, bool resetValueOnExit, GameObjectAnchor anchorTarget = null)
    {
        this.value = value;
        this.resetValueOnExit = resetValueOnExit;
        this.anchorTarget = anchorTarget;
    }

    public override void Awake(StateMachine stateMachine)
    {
        anchorTarget.TryGetComponentOrOwner(stateMachine, out baseInteractor, this);
    }
    public override void OnStateEnter()
    {
        if (baseInteractor == null) return;
        SetHide(value);
    }
    public override void OnStateExit()
    {
        if (baseInteractor == null || !resetValueOnExit) return;
        SetHide(!value);
    }
    public override void OnUpdate(){}
    private void SetHide(bool active)
    {
        if (active)
        {
            baseInteractor.SetCanInteract(false);
            baseInteractor.SetHideInteract(true);
        }
        else
        {
            baseInteractor.SetCanInteract(true);
            baseInteractor.SetHideInteract(false);
        }
    }
}
