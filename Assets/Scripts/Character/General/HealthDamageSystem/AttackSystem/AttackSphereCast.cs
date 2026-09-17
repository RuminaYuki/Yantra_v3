using UnityEngine;
using System;

public class AttackSphereCast : MonoBehaviour
{
    // Default values for the attack parameters
    [Header("Attack Parameters")]
    [SerializeField] private Transform attackOrigin;
    

    #region Properties

    public Transform AttackOrigin
    {
        get => attackOrigin;
        set
        {
            if (value == null)
            {
                Debug.LogWarning("Attack origin cannot be null. Keeping the previous value.");
                return;
            }
            attackOrigin = value;
        }
    }

    public event Action OnHit;
    #endregion

    /// <summary>
    /// Attempts to execute an attack using a sphere cast.
    /// </summary>
    public bool TryToExecuteAttack(AttackParameters parameters, DamageTypeID damageType = null)
    {
        int layerMask = ~(1 << gameObject.layer);

        Collider[] overlaps = Physics.OverlapSphere(attackOrigin.position, parameters.attackRadius, layerMask);
        foreach (Collider col in overlaps)
        {
            if (TryApplyDamage(col, parameters, damageType))
                return true;
        }

        if (Physics.SphereCast(attackOrigin.position, parameters.attackRadius, attackOrigin.forward, out RaycastHit hit, parameters.attackRange, layerMask))
        {
            return TryApplyDamage(hit.collider, parameters, damageType);
        }

        return false;
    }

    private bool TryApplyDamage(Collider col, AttackParameters parameters, DamageTypeID damageType)
    {
        IDamageable damageable = col.GetComponent<IDamageable>();
        if (damageable == null)
            return false;

        damageable.TakeDamage(parameters.damageAmount);
        OnHit?.Invoke();
        if (damageType != null)
            damageable.DamageType(damageType);

        return true;
    }

}
[System.Serializable]
public struct AttackParameters
{
   public float damageAmount;
   public float attackRange;
   public float attackRadius;
}
