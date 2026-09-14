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
    public bool TryToExecuteAttack(AttackParameters parameters)
    {
        int layerMask = ~(1 << gameObject.layer);

        Collider[] overlaps = Physics.OverlapSphere(attackOrigin.position, parameters.attackRadius, layerMask);
        if (overlaps.Length > 0)
        {
            foreach (Collider col in overlaps)
            {
                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(parameters.damageAmount);
                    OnHit?.Invoke();
                    return true;
                }
            }
            return false;
        }

        if (Physics.SphereCast(attackOrigin.position, parameters.attackRadius, attackOrigin.forward, out RaycastHit hit, parameters.attackRange, layerMask))
        {
            hit.collider.GetComponent<IDamageable>()?.TakeDamage(parameters.damageAmount);
            OnHit?.Invoke();
            return true;
        }
        return false;
    }
}
[System.Serializable]
public struct AttackParameters
{
   public float damageAmount;
   public float attackRange;
   public float attackRadius;
}
