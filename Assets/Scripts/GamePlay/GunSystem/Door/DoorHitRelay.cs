using UnityEngine;

public class DoorHitRelay : MonoBehaviour
{
    [Tooltip("Empty = BaseProjectileMovement on this object.")]
    [SerializeField] private BaseProjectileMovement projectileMovement;

    private void Awake()
    {
        if (projectileMovement == null) projectileMovement = GetComponent<BaseProjectileMovement>();
    }

    private void OnEnable()
    {
        if (projectileMovement != null)
            projectileMovement._hitRegistered += HandleHit;
    }

    private void OnDisable()
    {
        if (projectileMovement != null)
            projectileMovement._hitRegistered -= HandleHit;
    }

    private void HandleHit(Collider hitCollider)
    {
        if (hitCollider == null) return;

        ShootableDoor door = hitCollider.GetComponentInParent<ShootableDoor>();
        if (door == null) return;

        ProjectileDamageApplier damageApplier = projectileMovement.GetComponent<ProjectileDamageApplier>();

        bool value = damageApplier.GetRootOwner().GetComponentInChildren<StateFlags>().Get(door.GetDoorFlag());
        Debug.Log($"Flag Value: {value}");

        if (door != null && value) door.OnShot();
    }
}
