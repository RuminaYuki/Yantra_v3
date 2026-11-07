using UnityEngine;

// Where the target will be after Lead Time, from its current movement. Used by the teleport actions
// so the distance still feels the same when the target is walking toward or away during the
// appear animation.
[System.Serializable]
public struct TargetLeadSettings
{
    [Tooltip("Off = use where the target is now (old behaviour). On = predict where it will be.")]
    public bool usePrediction;
    [Tooltip("Seconds ahead to predict, e.g. the length of the appear animation.")]
    [Min(0f)] public float leadTime;
    [Tooltip("Caps how far the prediction can move the point, so a dash doesn't throw it too far.")]
    [Min(0f)] public float maxDistance;
}

public static class TargetLead
{
    public static Vector3 Predict(Transform target, in TargetLeadSettings settings)
    {
        Vector3 position = target.position;
        if (!settings.usePrediction || settings.leadTime <= 0f)
            return position;

        // The player moves with a CharacterController; its velocity is what the last Move() did.
        CharacterController controller = target.GetComponentInParent<CharacterController>();
        if (controller == null)
            return position;

        Vector3 velocity = controller.velocity;
        velocity.y = 0f;

        Vector3 lead = Vector3.ClampMagnitude(velocity * settings.leadTime, settings.maxDistance);
        return position + lead;
    }
}
