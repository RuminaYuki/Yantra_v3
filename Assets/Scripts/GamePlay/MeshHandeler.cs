using UnityEngine;

public class MeshHandeler : MonoBehaviour
{
    [SerializeField] private MeshRendererAnchor _meshRenderer;
    void Awake() { 
    }

    public void EnableMesh()
    {
        Debug.Log("Enable");
        _meshRenderer.Value.enabled = true;
    }
    public void DisableMesh()
    {
        Debug.Log("Disable");
        _meshRenderer.Value.enabled = false;
    }
}
