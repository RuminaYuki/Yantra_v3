using System.Collections;
using UnityEngine;

public class CameraShotStep : SequenceStep
{
    [SerializeField] PlayerCameraController cameraController;
    [SerializeField] Transform cameraPoint;
    [SerializeField] Transform lookTarget;
    [SerializeField] float moveDuration = 1.5f;
    [SerializeField] float holdDuration = 2f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] bool setCutsceneMode = true;

    public override IEnumerator Run()
    {
        if (cameraController == null)
            cameraController = FindFirstObjectByType<PlayerCameraController>();

        if (cameraController == null || cameraPoint == null || lookTarget == null)
        {
            Debug.LogWarning($"{nameof(CameraShotStep)} on {name} needs a camera controller, camera point, and look target.");
            yield break;
        }

        Transform cam = cameraController.transform;
        Vector3 startPos = cam.position;
        Quaternion startRot = cam.rotation;

        Vector3 endPos = cameraPoint.position;
        Vector3 lookDirection = lookTarget.position - endPos;
        Quaternion endRot = lookDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(lookDirection)
            : cameraPoint.rotation;
        bool wasCutsceneMode = cameraController.IsCutsceneMode;
        if (setCutsceneMode)
            cameraController.IsCutsceneMode = true;

        try
        {
            float duration = Mathf.Max(0f, moveDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float k = ease != null ? ease.Evaluate(t) : t;
                cam.position = Vector3.Lerp(startPos, endPos, k);
                cameraController.SetLookRotation(Quaternion.Slerp(startRot, endRot, k));
                yield return null;
            }
            cam.position = endPos;
            cameraController.SetLookRotation(endRot);

            if (holdDuration > 0f)
                yield return new WaitForSeconds(holdDuration);
        }
        finally
        {
            if (setCutsceneMode)
                cameraController.IsCutsceneMode = wasCutsceneMode;
        }

    }
}