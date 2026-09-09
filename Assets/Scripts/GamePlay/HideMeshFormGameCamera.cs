using UnityEngine;
using UnityEngine.Rendering;

public class HideMeshFormGameCamera : MonoBehaviour
{
    private SkinnedMeshRenderer bodyRenderer;
    void Awake()
    {
        bodyRenderer = GetComponent<SkinnedMeshRenderer>();
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam.cameraType == CameraType.Game)
            bodyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam.cameraType == CameraType.Game)
            bodyRenderer.shadowCastingMode = ShadowCastingMode.On;
    }
}
