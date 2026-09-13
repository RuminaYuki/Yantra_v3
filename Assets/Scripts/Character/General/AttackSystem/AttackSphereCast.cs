using UnityEngine;
using UnityEngine.InputSystem;

public class AttackSphereCast : MonoBehaviour
{
    // Default values for the attack parameters
    [Header("Attack Parameters")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private float damageAmount = 1f;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackRadius = 0.5f;

    [Header("Debug Settings")]
    [SerializeField] private bool showGizmos = false;
    [SerializeField] private bool showDebugLogs = false;
    public InputActionReference attackInputAction;
    

    #region Properties
    public float DamageAmount
    {
        get => damageAmount;
        set
        {
            if (value < 0)
            {
                Debug.LogWarning("Damage amount cannot be negative. Setting to 0.");
                damageAmount = 0;
                return;
            }
            damageAmount = value;
        } 
    }

    public float AttackRange
    {
        get => attackRange;
        set
        {
            if (value < 0)
            {
                Debug.LogWarning("Attack range cannot be negative. Setting to 0.");
                attackRange = 0;
                return;
            }
            attackRange = value;
        }
    }

    public float AttackRadius
    {
        get => attackRadius;
        set
        {
            if (value < 0)
            {
                Debug.LogWarning("Attack radius cannot be negative. Setting to 0.");
                attackRadius = 0;
                return;
            }
            attackRadius = value;
        }
    }

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
    #endregion


    void Update()
    {
        InputAction action = attackInputAction?.action;
        if (action != null && action.WasPerformedThisFrame())
        {
            TryToExecuteAttack();
        }
    }
    public bool TryToExecuteAttack()
    {
        int layerMask = ~(1 << gameObject.layer);

        Collider[] overlaps = Physics.OverlapSphere(attackOrigin.position, attackRadius, layerMask);
        if (overlaps.Length > 0)
        {
            foreach (Collider col in overlaps)
            {
                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damageAmount);
                    if (showDebugLogs)
                        Debug.Log($"{gameObject.name} attack origin overlapping {col.name} directly, damage applied.");
                    return true;
                }
            }

            if (showDebugLogs)
                Debug.Log($"{gameObject.name} attack origin blocked by non-damageable overlap ({overlaps[0].name}).");
            return false;
        }

        if (Physics.SphereCast(attackOrigin.position, attackRadius, attackOrigin.forward, out RaycastHit hit, attackRange, layerMask))
        {
            hit.collider.GetComponent<IDamageable>()?.TakeDamage(damageAmount);
            if (showDebugLogs)
            {
                Debug.Log($"{gameObject.name} attacked {hit.collider.name} (layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}) at distance {hit.distance:F2} for {damageAmount} damage.", hit.collider.gameObject);
            }
            return true;
        }
        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} SphereCast hit nothing (range {attackRange}, radius {attackRadius}).");
        }
        return false;
    }

    public void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(attackOrigin.position, attackOrigin.position + attackOrigin.forward * attackRange);
        Gizmos.DrawWireSphere(attackOrigin.position + attackOrigin.forward * attackRange, attackRadius);
    }
}
