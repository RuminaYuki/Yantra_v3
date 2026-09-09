using UnityEngine;
using UnityEngine.Splines;

[ExecuteInEditMode]
public class SplineToLineRenderer : MonoBehaviour
{
    public SplineContainer splineContainer;
    public LineRenderer lineRenderer;

    [Range(10, 100)]
    public int resolutions = 30;

    [Range(0f, 1f)]
    public float progress = 0f;

    void Update()
    {
        if (splineContainer == null || lineRenderer == null)
            return;

        // จำนวนจุดที่ต้องใช้ในการวาดตาม Progress
        int pointCount = Mathf.Max(2, Mathf.CeilToInt(resolutions * progress));

        lineRenderer.positionCount = pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            // คำนวณ t ให้อยู่ตั้งแต่ 0 ถึง progress
            float t = progress * ((float)i / (pointCount - 1));

            Vector3 position = splineContainer.EvaluatePosition(t);

            lineRenderer.SetPosition(i, position);
        }
    }
}
