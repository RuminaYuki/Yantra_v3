using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Simple reaction: wires TriggerZone Enter/Exit to UnityEvents so you can
/// hook things up in the Inspector (lights, sounds, doors...).
/// For anything with real logic, write your own component that subscribes to TriggerZone.
/// </summary>
[RequireComponent(typeof(TriggerZone))]
public class TriggerZoneEvents : MonoBehaviour
{
    [SerializeField] private UnityEvent<GameObject> onEnter;
    [SerializeField] private UnityEvent<GameObject> onExit;

    private TriggerZone _zone;

    private void Awake() => _zone = GetComponent<TriggerZone>();

    private void OnEnable()
    {
        _zone.OnZoneEnter += HandleEnter;
        _zone.OnZoneExit += HandleExit;
    }

    private void OnDisable()
    {
        _zone.OnZoneEnter -= HandleEnter;
        _zone.OnZoneExit -= HandleExit;
    }

    private void HandleEnter(GameObject who) => onEnter?.Invoke(who);
    private void HandleExit(GameObject who) => onExit?.Invoke(who);
}
