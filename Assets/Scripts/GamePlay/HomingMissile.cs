using UnityEngine;

public enum TargetLostBehaviour
{
    ContinueForward,
    SearchAgain,
    Destroy
}

public class HomingMissile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float turnSpeed = 360f;
    [SerializeField] private float maxTrackingAngle = 100f;
    [SerializeField] private float lifetime = 10f;

    [Header("Target")]
    [SerializeField] private TargetDetector targetDetector;
    [SerializeField] private TargetLostBehaviour targetLostBehaviour = TargetLostBehaviour.SearchAgain;
    [SerializeField] private MissileDamageApplier damageApplier;

    [SerializeField] Transform target;
    private float lifeTimer;
    private bool hadTarget;
    private bool targetLossHandled;

    private void Awake()
    {
        if (targetDetector == null)
            targetDetector = GetComponent<TargetDetector>();
        if (damageApplier == null)
            damageApplier = GetComponent<MissileDamageApplier>();
    }

    public void SetTarget(Transform target)
    {
        this.target = target;
        hadTarget = target != null;
        targetLossHandled = false;
    }

    public void SetOwner(Transform owner)
    {
        if (damageApplier != null)
            damageApplier.SetOwner(owner);
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifetime > 0f && lifeTimer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

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

    public void NotifyTargetHit()
    {
        Destroy(gameObject);
    }
}