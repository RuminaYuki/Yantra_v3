using System;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteInEditMode]
public class SplineToLineRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] SplineContainer splineContainer;
    [SerializeField] LineRenderer lineRenderer;

    [Header("Settings")]
    [SerializeField, Min(10f)] int resolutions = 30;
    [Range(0.01f, 100f)]
    [SerializeField] float nextKnotProgressDelta = 0.01f;
    [SerializeField] float offset = 0f;

    [Header("Debug")]
    #region Debug
    public bool debugSpell = true;
    #endregion
    [Range(0f, 1f)]
    [SerializeField] float currentProgress = 0f;
    private bool finishedSpellRaised;
    Vector3 currentPosition = Vector3.zero;

    [Header("Event Actions")]
    [SerializeField] FloatEventChannelSO SpellProgress;

    public float CurrentProgress 
    {
        get
        {
            float CP = Mathf.Clamp01(currentProgress + offset);
            return CP;
        }
        set 
        {
            currentProgress = Mathf.Clamp01(value + offset);
            if (currentProgress < 1f)
            {
                finishedSpellRaised = false;
            }
            progressIsChange();
            SpellProgress.Raise(currentProgress);
        }
    }

    public Vector3 CurrentKnotPosition
    {
        get => splineContainer.EvaluatePosition(CurrentProgress);
    }

    public Vector3 NextKnotPosition
    {
        get
        {
            float nextProgress = Mathf.Clamp01(CurrentProgress + nextKnotProgressDelta);
            return splineContainer.EvaluatePosition(nextProgress);
        }
    }

    private void Update()
    {
        progressIsChange();
    }

    void progressIsChange()
    {
        if (splineContainer == null || lineRenderer == null)
            return;

        // จำนวนจุดที่ต้องใช้ในการร่ายตาม Progress
        int pointCount = Mathf.Max(2, Mathf.CeilToInt(resolutions * CurrentProgress));

        lineRenderer.positionCount = pointCount;
        for (int i = 0; i < pointCount; i++)
        {
            // คำนวณ t ให้อยู่ตั้งแต่ 0 ถึง progress
            float t = CurrentProgress * ((float)i / (pointCount - 1));

            currentPosition = splineContainer.EvaluatePosition(t);

            lineRenderer.SetPosition(i, currentPosition);
            if (t >= 1f && !finishedSpellRaised)
            {
                finishedSpellRaised = true;
            }
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
    public Vector3 GetCurrentPosition() => currentPosition;
    #endregion
}
