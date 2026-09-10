using UnityEngine;

public class MeshHandeler : MonoBehaviour
{
    [SerializeField] private MeshRenderer _meshRenderer;

    public void EnableMesh()
    {
        _meshRenderer.enabled = true;
    }
    public void DisableMesh()
    {
        _meshRenderer.enabled = false;
    }
}
