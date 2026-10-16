using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(MeshRenderer))]
[CreateAssetMenu(
    fileName = "NewEnableMeshrenderer_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Enable Mesh Renderer")]
public class EnableMeshrendererActionSO : StateActionSO
{
    [SerializeField] private bool value = true;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool resetValueOnExit = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new EnableMeshrendererAction(value, resetValueOnExit);
    }
}

public class EnableMeshrendererAction : StateAction
{
    private readonly bool value;
    private readonly bool resetValueOnExit;
    private AnchorReference<MeshRenderer> meshRenderer;

    public EnableMeshrendererAction(bool value, bool changeValueOnExit)
    {
        this.value = value;
        this.resetValueOnExit = changeValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        meshRenderer = new AnchorReference<MeshRenderer>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!meshRenderer.TryGet(out MeshRenderer renderer)) return;
        renderer.enabled = value;
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (!resetValueOnExit || !meshRenderer.TryGet(out MeshRenderer renderer)) return;
        renderer.enabled = !value;
    }
}
