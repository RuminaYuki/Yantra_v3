using UnityEngine;
using UnityEngine.VFX;

public class SplineToVFX : MonoBehaviour
{
    [SerializeField] SplineToLineRenderer splineRender;
    [SerializeField] private VisualEffect vfx;

    private static readonly int InitializePositionID =
        Shader.PropertyToID("InitializePosition");

    private void Awake()
    {
        if (vfx == null)
        {
            Debug.LogWarning("VFX is not assigned.");
            gameObject.SetActive(false);
        }
        if (splineRender == null)
        {
            Debug.LogWarning("splineRender is not assigned.");
            gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        SetVFXPosition(splineRender.GetCurrentPosition());
    }

    public void SetVFXPosition(Vector3 position)
    {
        if (!vfx.HasVector3("InitializePosition"))
        {
            Debug.LogError("VFX ไม่มี Vector3 Property ชื่อ InitializePosition");
            return;
        }

        vfx.SetVector3(InitializePositionID, position);
    }
}