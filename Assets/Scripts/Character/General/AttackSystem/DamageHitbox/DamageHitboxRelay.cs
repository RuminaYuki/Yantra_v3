using UnityEngine;

/// <summary>
/// Added at runtime by DamageHitbox to a collider on another GameObject.
/// Forwards that collider's trigger events back to the owning hitbox.
/// </summary>
[AddComponentMenu("")]
public class DamageHitboxRelay : MonoBehaviour
{
    private DamageHitbox _owner;

    public void Init(DamageHitbox owner)
    {
        _owner = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_owner != null)
            _owner.HandleHit(other);
    }
}
