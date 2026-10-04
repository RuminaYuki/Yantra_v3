using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(GameObjectHandeler), UnlessAnchorField = "handelerAnchor")]
[CreateAssetMenu(
    fileName = "NewSetGameObjHandeler_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/ReferenceHandeler/Set GameObject Handeler")]
public class SetGameObjectHandelerActionSO : StateActionSO
{
    [SerializeField] private bool value = true;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool resetValueOnExit = true;

    [Header("If the TargetAnchor is set, Use that anchor instead")]
    [SerializeField] private GameObjectAnchor handelerAnchor;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new SetGameObjectActiveAction(value, resetValueOnExit, handelerAnchor);
    }
}

public class SetGameObjectActiveAction : StateAction
{
    private readonly GameObjectAnchor handelerAnchor;
    private readonly bool value;
    private readonly bool resetValueOnExit;
    private GameObjectHandeler handeler;

    public SetGameObjectActiveAction(bool value, bool resetValueOnExit, GameObjectAnchor handelerAnchor = null)
    {
        this.handelerAnchor = handelerAnchor;
        this.value = value;
        this.resetValueOnExit = resetValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        handelerAnchor.TryGetComponentOrOwner(stateMachine, out handeler, this);
    }

    public override void OnStateEnter()
    {
        if (handeler == null) return;
        SetActive(value);
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (handeler == null || !resetValueOnExit) return;
        SetActive(!value);
    }

    private void SetActive(bool active)
    {
        if (active)
            handeler.EnableGameobject();
        else
            handeler.DisableGameobject();
    }
}
