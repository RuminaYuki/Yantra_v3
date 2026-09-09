using UnityEngine;
using UnityEngine.Splines;
using static UnityEngine.Rendering.DebugUI;

[ExecuteInEditMode]
public class SplineToLineRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] SplineContainer splineContainer;
    [SerializeField] LineRenderer lineRenderer;

    [Header("Settings")]
    [Range(10, 100)]
    [SerializeField] int resolutions = 30;
    [Range(0.01f, 100f)]
    [SerializeField] float nextKnotProgressDelta = 0.01f;
    [SerializeField] float offset = 0f;

    [Header("Debug")]
    [Range(0f, 1f)]
    [SerializeField] float currentProgress = 0f;
    public float CurrentProgress 
    {
        get => Mathf.Clamp01(currentProgress + offset);
        set => currentProgress = Mathf.Clamp01(value + offset);
    }

    [SerializeField] Vector3 currentKnotPosition;
    public Vector3 CurrentKnotPosition
    {
        get => splineContainer.EvaluatePosition(CurrentProgress);
    }

    [SerializeField] Vector3 nextKnotPosition;
    public Vector3 NextKnotPosition
    {
        get
        {
            float nextProgress = Mathf.Clamp01(CurrentProgress + nextKnotProgressDelta);
            return splineContainer.EvaluatePosition(nextProgress);
        }
    }

    void Update()
    {
        if (splineContainer == null || lineRenderer == null)
            return;

        // จำนวนจุดที่ต้องใช้ในการวาดตาม Progress
        int pointCount = Mathf.Max(2, Mathf.CeilToInt(resolutions * CurrentProgress));

        lineRenderer.positionCount = pointCount;
        for (int i = 0; i < pointCount; i++)
        {
            // คำนวณ t ให้อยู่ตั้งแต่ 0 ถึง progress
            float t = CurrentProgress * ((float)i / (pointCount - 1));

            Vector3 position = splineContainer.EvaluatePosition(t);

            lineRenderer.SetPosition(i, position);
        }
    }

    #region API
    public void SetProgress(float progress) => CurrentProgress = progress;
    public void AddProgress() => CurrentProgress += nextKnotProgressDelta;
    public float GetProgress() => CurrentProgress;
    public Vector2 GetSplineDirectionScreenSpace(Camera cam)
    {
        Vector3 currentWorld = CurrentKnotPosition;
        Vector3 nextWorld = NextKnotPosition;

        Vector3 currentScreen = cam.WorldToScreenPoint(currentWorld);
        Vector3 nextScreen = cam.WorldToScreenPoint(nextWorld);

        return (new Vector2(nextScreen.x, nextScreen.y) -
                new Vector2(currentScreen.x, currentScreen.y)).normalized;
    }
    #endregion
}
