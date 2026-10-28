using UnityEngine;

/// <summary>
/// Spawns a VFX prefab (VFX Graph / ParticleSystem) when a projectile (e.g. HomingMissile)
/// hits an IDamageable. The prefab is spawned in the world (not under the projectile),
/// so it keeps playing after the projectile is destroyed (destroyed 0.1s after the hit).
/// Lifetime / speed / fade out are handled by VFXPlayback, same as PlayVFXOnSpawn.
/// </summary>
public class ProjectileHitVFXPlayer : MonoBehaviour
{
    public enum ImpactPosition
    {
        /// <summary>Point on the target surface closest to the projectile. Most accurate.</summary>
        ContactPointOnTarget,

        /// <summary>Center of the target bounds. Use for aura-like effects that wrap the body.</summary>
        TargetCenter,

        /// <summary>Projectile position at the moment of the hit.</summary>
        ProjectilePosition
    }

    public enum VFXRotationMode
    {
        /// <summary>No rotation, keep the pose the effect was authored with.</summary>
        None,

        /// <summary>Face back toward where the projectile came from. Good for splash/burst effects.</summary>
        AwayFromTarget,

        /// <summary>Follow the projectile's flight direction.</summary>
        ProjectileDirection,

        /// <summary>Face the camera.</summary>
        FaceCamera
    }

    [Header("Event Source")]
    [Tooltip("Can be left empty. Will search this GameObject, children and parents.")]
    [SerializeField] private BaseProjectileMovement _projectile;

    [Header("Effect")]
    [Tooltip("Prefab that contains a VFX Graph (VisualEffect) or ParticleSystem.")]
    [SerializeField] private GameObject _vfxPrefab;

    [Tooltip("How long the effect plays (seconds) before it stops emitting and fades out.\n" +
        "0 = play until the effect ends by itself (VFX Graph is capped by VFXPlayback's safety lifetime).")]
    [SerializeField] private float _duration = 1f;

    [Tooltip("Playback speed. 2 = twice as fast / 0.5 = half speed.")]
    [SerializeField] private float _speed = 1f;

    [Tooltip("After emitting stops, seconds to let remaining particles fade before the object is destroyed.")]
    [SerializeField] private float _fadeOut = 1f;

    [Tooltip("Parent the effect to the hit target so it follows the enemy while it moves.\n" +
        "Off = the effect stays where the hit happened.")]
    [SerializeField] private bool _attachToTarget = false;

    [Header("Position")]
    [SerializeField] private ImpactPosition _positionMode = ImpactPosition.ContactPointOnTarget;

    [Tooltip("Offset applied to the resolved position (world space).\n" +
        "Positive Y = raise it, use when the effect is sunk inside the target.")]
    [SerializeField] private Vector3 _worldOffset = Vector3.zero;

    [Header("Rotation")]
    [SerializeField] private VFXRotationMode _rotationMode = VFXRotationMode.None;

    [Tooltip("Extra rotation on top of the mode above (degrees).\nTry steps of 90 first: Y=90 / Y=180 / X=90 / X=-90")]
    [SerializeField] private Vector3 _rotationOffset = Vector3.zero;

    [Tooltip("Can be left empty. Camera.main is used.")]
    [SerializeField] private Camera _cameraOverride;

    [Header("Options")]
    [Tooltip("Minimum seconds between effects. Prevents stacking when Repeat Mode = AllowRepeat.")]
    [SerializeField] private float _minInterval = 0.05f;

    [Header("Debug")]
    [Tooltip("Log which target was hit and where the effect was spawned.")]
    [SerializeField] private bool _logPlays = false;

    [Tooltip("Draw the spawn point in the Scene view for 2 seconds.")]
    [SerializeField] private bool _drawDebugPoint = false;

    private float _lastPlayTime = -999f;

    private void Awake()
    {
        if (_projectile == null) _projectile = ResolveProjectile();

        if (_projectile == null)
        {
            Debug.LogError(
                "[ProjectileHitVFX] BaseProjectileMovement not found - " +
                "assign it in the Projectile field or move this script next to it.", this);
            enabled = false;
        }
    }

    private BaseProjectileMovement ResolveProjectile()
    {
        var found = GetComponent<BaseProjectileMovement>();
        if (found != null) return found;

        found = GetComponentInChildren<BaseProjectileMovement>(true);
        if (found != null) return found;

        return GetComponentInParent<BaseProjectileMovement>(true);
    }

    private void OnEnable()
    {
        if (_projectile != null) _projectile._hitRegistered += HandleHit;
        _lastPlayTime = -999f;
    }

    /// <summary>
    /// Always unsubscribe - Domain Reload is disabled in this project,
    /// so a leftover subscriber would double the effect on the next Play.
    /// </summary>
    private void OnDisable()
    {
        if (_projectile != null) _projectile._hitRegistered -= HandleHit;
    }

    private void HandleHit(Collider hitCollider)
    {
        if (hitCollider == null) return;

        if (Time.time - _lastPlayTime < _minInterval) return;
        _lastPlayTime = Time.time;

        if (_vfxPrefab == null)
        {
            if (_logPlays) Debug.Log("[ProjectileHitVFX] Hit! - but no VFX prefab assigned", this);
            return;
        }

        Transform origin = _projectile.transform;
        Vector3 position = ResolvePosition(hitCollider, origin.position) + _worldOffset;
        Quaternion rotation = BuildRotation(origin);

        if (_drawDebugPoint)
        {
            Debug.DrawLine(position + Vector3.up * 0.3f, position - Vector3.up * 0.3f, Color.magenta, 2f);
            Debug.DrawLine(position + Vector3.right * 0.3f, position - Vector3.right * 0.3f, Color.magenta, 2f);
            Debug.DrawLine(position + Vector3.forward * 0.3f, position - Vector3.forward * 0.3f, Color.magenta, 2f);
        }

        if (_logPlays)
            Debug.Log($"[ProjectileHitVFX] Hit! - {_vfxPrefab.name} on {hitCollider.name}", this);

        // Never parent to the projectile: it is destroyed right after the hit.
        GameObject spawned = Instantiate(_vfxPrefab, position, rotation);

        if (_attachToTarget)
            spawned.transform.SetParent(ResolveAttachParent(hitCollider), true);

        VFXPlayback.Manage(spawned, _duration, _speed, _fadeOut);
    }

    /// <summary>
    /// Attach to the IDamageable owner rather than the collider itself,
    /// so the effect follows the enemy root/body instead of a single bone collider.
    /// </summary>
    private static Transform ResolveAttachParent(Collider hitCollider)
    {
        IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
        Component component = damageable as Component;
        return component != null ? component.transform : hitCollider.transform;
    }

    private Vector3 ResolvePosition(Collider hitCollider, Vector3 projectilePosition)
    {
        switch (_positionMode)
        {
            case ImpactPosition.ProjectilePosition:
                return projectilePosition;

            case ImpactPosition.TargetCenter:
                return hitCollider.bounds.center;

            default:
                // ClosestPoint only works on Box/Sphere/Capsule/convex Mesh colliders.
                // Fall back to bounds for anything else (e.g. non-convex MeshCollider).
                if (hitCollider is MeshCollider mesh && !mesh.convex)
                    return hitCollider.ClosestPointOnBounds(projectilePosition);

                return hitCollider.ClosestPoint(projectilePosition);
        }
    }

    private Quaternion BuildRotation(Transform origin)
    {
        Quaternion baseRotation;

        switch (_rotationMode)
        {
            case VFXRotationMode.AwayFromTarget:
                // Projectile is usually at/inside the target on hit,
                // so use the reverse flight direction.
                baseRotation = Quaternion.LookRotation(-origin.forward);
                break;

            case VFXRotationMode.ProjectileDirection:
                baseRotation = origin.rotation;
                break;

            case VFXRotationMode.FaceCamera:
                Camera cam = _cameraOverride != null ? _cameraOverride : Camera.main;
                baseRotation = cam != null ? cam.transform.rotation : Quaternion.identity;
                break;

            default:
                baseRotation = Quaternion.identity;
                break;
        }

        return baseRotation * Quaternion.Euler(_rotationOffset);
    }
}
