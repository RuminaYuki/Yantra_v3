using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;
using SDFcl.GamePlay.Interactable;

[UseSelfAnchor]
[CreateAssetMenu(
    fileName = "NewSetHideInteractor_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/InteractSystem/Interactor/Set Hide Interactor")]
public class SetHideInteractorSO : StateActionSO
{
    [SerializeField] private bool value = true;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool resetValueOnExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetHideInteractor(value, resetValueOnExit);
    }
}

public class SetHideInteractor : StateAction
{
    private readonly bool value;
    private readonly bool resetValueOnExit;
    private AnchorReference<BaseInteractor> interactorRef;
    public SetHideInteractor(bool value, bool resetValueOnExit)
    {
        this.value = value;
        this.resetValueOnExit = resetValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        interactorRef = new AnchorReference<BaseInteractor>(stateMachine, this);
    }
    public override void OnStateEnter()
    {
        SetHide(value);
    }
    public override void OnStateExit()
    {
        if (!resetValueOnExit) return;
        SetHide(!value);
    }
    public override void OnUpdate(){}
    private void SetHide(bool active)
    {
        if (!interactorRef.TryGet(out BaseInteractor baseInteractor)) return;

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
