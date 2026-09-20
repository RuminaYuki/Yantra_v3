using System;
using UnityEngine;

public class TestAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private AttackSphereCast attackSphereCast;
    [SerializeField] private AttackParameters attackParameters;
    [SerializeField] private DamageTypeID damageType;
    [SerializeField] private int mouseButton = 0;

    void Update()
    {
        if (Input.GetMouseButtonDown(mouseButton))
        {
            attackSphereCast.TryToExecuteAttack(attackParameters,damageType);
        }
    }

    void OnDrawGizmos()
    {
        if (attackSphereCast == null || attackSphereCast.AttackOrigin == null)
            return;

        Transform origin = attackSphereCast.AttackOrigin;

        Gizmos.color = Color.yellow;
        Vector3 rangeEnd = origin.position + origin.forward * attackParameters.attackRange;
        Gizmos.DrawLine(origin.position, rangeEnd);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin.position, attackParameters.attackRadius);
        Gizmos.DrawWireSphere(rangeEnd, attackParameters.attackRadius);
    }
}
