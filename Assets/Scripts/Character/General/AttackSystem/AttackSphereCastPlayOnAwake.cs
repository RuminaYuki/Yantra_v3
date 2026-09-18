using UnityEngine;

[RequireComponent(typeof(AttackSphereCast))]
public class AttackSphereCastPlayOnAwake : MonoBehaviour
{
    [SerializeField] AttackParameters parameters = new();

    private void Awake()
    {
        AttackSphereCast attackSphere = GetComponent<AttackSphereCast>();

        attackSphere.TryToExecuteAttack(parameters);
    }
}
