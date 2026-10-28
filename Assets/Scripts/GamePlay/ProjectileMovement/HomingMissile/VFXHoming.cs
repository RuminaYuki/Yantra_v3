using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.VFX;

public class VFXHoming : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] VisualEffect vfx;
    [SerializeField] string vfxPositionPropertyName = "InitializePosition";

    private void Update()
    {
        if (target == null)
        {
            Debug.LogWarning("Target is not assigned.");
            vfx.Stop();
            Destroy(gameObject, 2f);
            return;
        }
        if (vfx == null)
        {
            Debug.LogWarning("VFX is not assigned.");
            return;
        }
        vfx.SetVector3(vfxPositionPropertyName, target.position);
    }
}
