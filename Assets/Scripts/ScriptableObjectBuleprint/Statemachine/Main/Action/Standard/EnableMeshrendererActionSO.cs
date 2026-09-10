using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewEnableMeshrenderer_Action",
    menuName = "YUKI Learning State Machine/StateMachine/Actions/Standard/Enable Mesh Renderer")]
public class EnableMeshrendererActionSO : StateActionSO
{
    [SerializeField] private bool value = true;
    [Tooltip("Change opposite Value On Exit")]
    [SerializeField] private bool resetValueOnExit = true;

    [Header("If the TargetAnchor is set, Use that anchor instead")]
    [SerializeField] private MeshRendererAnchor meshRendererAnchor;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new EnableMeshrendererAction(value, resetValueOnExit, meshRendererAnchor);
    }
}

public class EnableMeshrendererAction : StateAction
{
    private readonly MeshRendererAnchor meshRendererAnchor;
    private readonly bool value;
    private readonly bool resetValueOnExit;
    private MeshRenderer meshRenderer;

    public EnableMeshrendererAction(bool value, bool changeValueOnExit,MeshRendererAnchor meshRendererAnchor = null)
    {
        this.meshRendererAnchor = meshRendererAnchor;
        this.value = value;
        this.resetValueOnExit = changeValueOnExit;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if(meshRendererAnchor != null)
        {
            meshRenderer = meshRendererAnchor.Value;
            return;
        }
        meshRenderer = stateMachine.GetComponent<MeshRenderer>();
    }

    public override void OnStateEnter()
    {
        if (meshRenderer == null) return;
        meshRenderer.enabled = value;
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (meshRenderer == null || !resetValueOnExit) return;
        meshRenderer.enabled = !value;
    }
}
