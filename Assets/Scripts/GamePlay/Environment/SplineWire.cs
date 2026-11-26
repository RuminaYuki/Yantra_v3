using UnityEngine;
using UnityEngine.Splines;

// สายไฟ / สายเคเบิลที่ไม่ขยับ: สร้างจุดของ LineRenderer จาก Spline
// - ใน Editor: อัปเดตตามที่ลาก spline (ExecuteAlways)
// - ตอน Play: คำนวณครั้งเดียวตอน OnEnable แล้วไม่ทำอะไรอีก
[ExecuteAlways]
public class SplineWire : MonoBehaviour
{
    [SerializeField] SplineContainer splineContainer;
    [SerializeField] LineRenderer lineRenderer;
    [SerializeField, Min(2)] int resolutions = 30;

    Vector3[] points;

    void OnEnable() => Rebuild();

#if UNITY_EDITOR
    void OnValidate() => Rebuild();

    void Update()
    {
        if (Application.isPlaying) return;
        Rebuild();
    }
#endif

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (splineContainer == null || lineRenderer == null) return;

        if (points == null || points.Length != resolutions)
            points = new Vector3[resolutions];

        Transform lineTransform = lineRenderer.transform;
        for (int i = 0; i < resolutions; i++)
        {
            float t = (float)i / (resolutions - 1);
            Vector3 world = splineContainer.EvaluatePosition(t);
            points[i] = lineTransform.InverseTransformPoint(world);
        }

        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = resolutions;
        lineRenderer.SetPositions(points);
    }
}
