using UnityEngine;

public class ProjectileDamageApplier : MonoBehaviour
{
    [SerializeField] BaseProjectileMovement projectileMovement;
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] protected Transform owner;

    private void OnEnable()
    {
        if (projectileMovement != null)
            projectileMovement._hitRegistered += ApplyDamage;
    }

    private void OnDisable()
    {
        if (projectileMovement != null)
            projectileMovement._hitRegistered -= ApplyDamage;
    }

    public void SetOwner(Transform newOwner)
    {
        owner = newOwner;
    }

    public void ApplyDamage(Collider hitCollider)
    {
        if (hitCollider == null || IsOwner(hitCollider.transform))
            return;

        IDamageable damageable = FindDamageable(hitCollider);
        if (damageable == null)
            return;

        damageable.TakeDamage(damageAmount);
    }

    private bool IsOwner(Transform hitTransform)
    {
        return owner != null &&
               (hitTransform == owner || hitTransform.IsChildOf(owner));
    }

    private IDamageable FindDamageable(Collider hitCollider)
    {
        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            IDamageable damageable = behaviour as IDamageable;
            if (damageable != null)
                return damageable;
        }

        return null;
    }

    public void SetDamge(float amount)
    {
        damageAmount = amount;
    }
}
