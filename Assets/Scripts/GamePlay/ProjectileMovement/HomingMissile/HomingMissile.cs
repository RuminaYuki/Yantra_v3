using UnityEngine;

public enum TargetLostBehaviour
{
    ContinueForward,
    SearchAgain,
    Destroy
}

public class HomingMissile : BaseProjectileMovement
{
    [SerializeField] private float turnSpeed = 360f;
    [SerializeField] private float maxTrackingAngle = 100f;

    [Header("Target")]
    [SerializeField] private TargetDetector targetDetector;
    [SerializeField] private TargetLostBehaviour targetLostBehaviour = TargetLostBehaviour.SearchAgain;
    [SerializeField] Transform target;
    private bool hadTarget;
    private bool targetLossHandled;

    protected override void Awake()
    {
        if (targetDetector == null)
            targetDetector = GetComponent<TargetDetector>();
    }

    public void SetTarget(Transform target)
    {
        this.target = target;
        hadTarget = target != null;
        targetLossHandled = false;
    }

    protected override void Update()
    {
        base.Update();

        if (target == null)
        {
            if (!hadTarget && targetDetector != null)
            {
                target = targetDetector.FindTarget(transform);
                hadTarget = target != null;
            }
            else
            {
                HandleMissingTarget();
            }

            if (targetLostBehaviour == TargetLostBehaviour.Destroy && targetLossHandled)
                return;
        }
        else if (!target.gameObject.activeInHierarchy || !CanTrackTarget(target))
        {
            target = null;
            HandleMissingTarget();
        }

        if (target != null)
            SteerTowards(target.position);

        MoveForward();
    }

    private void HandleMissingTarget()
    {
        if (targetLossHandled && targetLostBehaviour != TargetLostBehaviour.SearchAgain)
            return;

        switch (targetLostBehaviour)
        {
            case TargetLostBehaviour.ContinueForward:
                targetLossHandled = true;
                break;
            case TargetLostBehaviour.SearchAgain:
                target = targetDetector == null ? null : targetDetector.FindTarget(transform);
                if (target != null)
                {
                    hadTarget = true;
                    targetLossHandled = false;
                }
                break;
            case TargetLostBehaviour.Destroy:
                if (hadTarget)
                {
                    targetLossHandled = true;
                    Destroy(gameObject);
                }
                break;
        }
    }

    private bool CanTrackTarget(Transform candidate)
    {
        Vector3 direction = candidate.position - transform.position;
        return direction.sqrMagnitude > 0.001f &&
               Vector3.Angle(transform.forward, direction) <= maxTrackingAngle;
    }

    private void SteerTowards(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );
    }

    private void MoveForward()
    {
        transform.position +=
            transform.forward * moveSpeed * Time.deltaTime;
    }
}