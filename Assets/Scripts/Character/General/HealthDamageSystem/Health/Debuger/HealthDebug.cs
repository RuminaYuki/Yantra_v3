using UnityEngine;

[RequireComponent(typeof(Health))]
public class HealthDebug : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            if (TryGetComponent<IHeal>(out IHeal healable))
            {
                healable.Heal(1f);
            }
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            if (TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                damageable.TakeDamage(1f);
            }
        }
    }
}
