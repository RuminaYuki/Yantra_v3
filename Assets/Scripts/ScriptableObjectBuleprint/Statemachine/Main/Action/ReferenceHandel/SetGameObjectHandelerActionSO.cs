using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(GameObjectHandeler))]
[CreateAssetMenu(
    fileName = "NewSetGameObjHandeler_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/ReferenceHandeler/Set GameObject Handeler")]
public class SetGameObjectHandelerActionSO : StateActionSO
{
    [SerializeField] private bool value = true;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool resetValueOnExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetGameObjectActiveAction(value, resetValueOnExit);
    }
}

public class SetGameObjectActiveAction : StateAction
{
    private readonly bool value;
    private readonly bool resetValueOnExit;
    private AnchorReference<GameObjectHandeler> handelerRef;

    public SetGameObjectActiveAction(bool value, bool resetValueOnExit)
    {
        this.value = value;
        this.resetValueOnExit = resetValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        handelerRef = new AnchorReference<GameObjectHandeler>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        SetActive(value);
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (!resetValueOnExit) return;
        SetActive(!value);
    }

    private void SetActive(bool active)
    {
        if (!handelerRef.TryGet(out GameObjectHandeler handeler)) return;

        if (active)
            handeler.EnableGameobject();
        else
            handeler.DisableGameobject();
    }
}
