using System.Collections;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Vengeful Roots (Tanee) only: once FinishFlag turns on, wait a bit, turn every pattern child off,
/// then return to the pool. The state machine and flags are reset when it is enabled again,
/// so every spawn starts from the initial state.
/// </summary>
[RequireComponent(typeof(StateMachineController), typeof(StateFlags))]
public class TaneeThornReset : MonoBehaviour
{
    private const string PoolTag = "VengefulRoots";
    private const float DespawnDelay = 0.5f;

    [Tooltip("When this flag turns on, Vengeful Roots despawns.")]
    [SerializeField] private FlagSO _finishFlag;

    private StateMachineController _controller;
    private StateFlags _flags;
    private Coroutine _despawnRoutine;

    private void Awake()
    {
        _controller = GetComponent<StateMachineController>();
        _flags = GetComponent<StateFlags>();
    }

    private void OnEnable()
    {
        _despawnRoutine = null;
        ResetStateMachine();
    }

    private void Update()
    {
        if (_despawnRoutine != null || !_flags.Get(_finishFlag))
            return;

        _despawnRoutine = StartCoroutine(DespawnAfterDelay());
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(DespawnDelay);

        // Turn the flag off before going back to the pool, so it isn't still on at the next spawn.
        _flags.Set(_finishFlag, false);

        // No items and no thorns on every spawn; SwapSplineItemAction sets the prefab again.
        // Done before the children turn off, while SplineInstantiate can still remove its instances.
        foreach (SplineInstantiate spline in GetComponentsInChildren<SplineInstantiate>(true))
        {
            spline.itemsToInstantiate = System.Array.Empty<SplineInstantiate.InstantiableItem>();
            spline.Clear();
        }

        if (ObjectPooler.Instance != null)
            ObjectPooler.Instance.ReturnToPool(PoolTag, gameObject);
        else
            gameObject.SetActive(false);
    }

    // Reset on enable, not on disable: RestartTable enters the initial state right away,
    // so doing it here runs the initial state's enter actions on the new spawn, not on the way out.
    private void ResetStateMachine()
    {
        // Flags first, so the initial state's conditions don't see the old finish flag.
        _flags.ResetToInitialValues();

        // Thorns back to their default state (Tanee_Thorn_Down). Only active ones - an inactive
        // Animator already resets by itself when it is turned on again.
        foreach (Animator animator in GetComponentsInChildren<Animator>())
        {
            animator.Rebind();
            animator.Update(0f);
        }

        // First spawn: Start() hasn't built the state machines yet and will start fresh by itself.
        if (_controller.StateMachines == null)
            return;

        for (int i = 0; i < _controller.StateMachines.Count; i++)
            _controller.RestartTable(i);
    }
}
