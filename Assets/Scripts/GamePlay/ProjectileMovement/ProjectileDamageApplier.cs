using UnityEngine;

public class ProjectileDamageApplier : MonoBehaviour
{
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private Transform owner;

    public void SetOwner(Transform newOwner)
    {
        owner = newOwner;
    }

    public bool ApplyDamage(Collider hitCollider)
    {
        if (hitCollider == null || IsOwner(hitCollider.transform))
            return false;

        IDamageable damageable = FindDamageable(hitCollider);
        if (damageable == null)
            return false;

        damageable.TakeDamage(damageAmount);
        return true;
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

    public void SetDamge(float amount) => damageAmount = amount;
}
