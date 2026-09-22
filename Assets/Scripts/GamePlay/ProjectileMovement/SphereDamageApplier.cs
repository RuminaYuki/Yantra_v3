using UnityEngine;

public class SphereDamageApplier : ProjectileDamageApplier
{
    [Header("Sphere Damage Applier")]
    [SerializeField] LayerMask tragetLayer;
    [SerializeField] LayerMask obstructionLayer;
    [SerializeField] AttackParameters parameters;

    private void Awake()
    {
        SphereDamage();
    }

    private void SphereDamage()
    {
        Collider[] overlaps = Physics.OverlapSphere(transform.position, parameters.attackRadius, tragetLayer);
        foreach (Collider col in overlaps)
        {
            Vector3 targetPoint = col.ClosestPoint(transform.position);
            Vector3 direction = targetPoint - transform.position;
            float distance = direction.magnitude;

            // ถ้ามีอะไรบน obstructionLayer ขวางระหว่างทาง ให้ข้าม target นี้ไป
            if (Physics.Raycast(transform.position, direction.normalized, distance, obstructionLayer))
                continue;

            base.ApplyDamage(col);
        }
    }
}
