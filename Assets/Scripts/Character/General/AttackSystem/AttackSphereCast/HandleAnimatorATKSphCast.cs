using UnityEngine;

public class HandleAnimatorATKSphCast : MonoBehaviour
{
    [SerializeField] private AttackSphereCast _attackSphereCast;
    public void CastAttackFromAnimation()
    {
        _attackSphereCast.CastAttack();
    }
}
