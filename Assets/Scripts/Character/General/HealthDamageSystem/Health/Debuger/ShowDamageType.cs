using UnityEngine;

public class ShowDamageType : MonoBehaviour
{
    public Health _health;
    void OnEnable()
    {
        _health.OnHitDamageType += Showtext;
    }
    void OnDisable()
    {
        _health.OnHitDamageType -= Showtext;
    }
    void Showtext(DamageTypeID type)
    {
        Debug.Log("hit DamageType is " + type.name);
    }
}
