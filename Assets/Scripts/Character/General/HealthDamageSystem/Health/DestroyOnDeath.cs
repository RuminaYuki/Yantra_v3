using System.Collections;
using UnityEngine;

// Destroys the object a little after its Health dies, so a death animation / VFX can play first.
// For things spawned at runtime (e.g. minions). Enemies placed in the scene should stay (disable
// them instead): the save system and Respawn need the object to still be there.
// Healed above 0 during the delay (e.g. Respawn) = not destroyed.
public class DestroyOnDeath : MonoBehaviour
{
    [Tooltip("The Health to watch. Empty = the first one on this object or its children.")]
    [SerializeField] private Health _health;

    [Tooltip("What gets destroyed. Empty = this object.")]
    [SerializeField] private GameObject _target;

    [Tooltip("Seconds between dying and being destroyed.")]
    [SerializeField, Min(0f)] private float _delay = 2f;

    private Coroutine _pending;

    private void Awake()
    {
        if (_health == null) _health = GetComponentInChildren<Health>(true);
        if (_target == null) _target = gameObject;

        if (_health == null)
        {
            Debug.LogWarning($"[DestroyOnDeath] No Health found on '{name}'.", this);
        }
    }

    private void OnEnable()
    {
        if (_health == null) return;

        _health.OnDead += HandleDead;
        _health.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnDead -= HandleDead;
            _health.OnHealthChanged -= HandleHealthChanged;
        }

        // A disabled object's coroutines stop anyway; forget it so it can start again when re-enabled.
        _pending = null;
    }

    private void HandleDead()
    {
        if (_pending != null) return;

        _pending = StartCoroutine(DestroyAfterDelay());
    }

    // Brought back during the delay: cancel.
    private void HandleHealthChanged(float currentHP)
    {
        if (_pending == null || currentHP <= 0f) return;

        StopCoroutine(_pending);
        _pending = null;
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(_delay);

        _pending = null;
        Destroy(_target);
    }
}
