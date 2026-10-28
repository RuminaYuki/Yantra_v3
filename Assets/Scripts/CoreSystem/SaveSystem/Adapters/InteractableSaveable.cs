using System;
using System.Collections.Generic;
using SDFcl.GamePlay.Interactable;
using UnityEngine;
using UnityEngine.Events;

// Remembers that this interactable was used and restores the world after loading.
// Save the cause (used), not the results: results are rebuilt from it on load.
// Must be on an object that is active when the scene starts.
[RequireComponent(typeof(SaveId))]
public class InteractableSaveable : SaveableBehaviour<InteractableSaveable.SaveData>
{
    [Serializable]
    public class SaveData
    {
        public bool used;
    }

    [Serializable]
    public struct ActiveResult
    {
        public GameObject obj;
        public bool active;
    }

    public enum RestoreMode
    {
        // Apply the same Results as when interacted.
        ReplayResults,
        // Apply Restore Results instead (e.g. skip a cutscene, show the end state).
        UseRestoreResults
    }

    [Tooltip("Block interaction after the first use.")]
    [SerializeField] private bool lockAfterUse = true;

    [Tooltip("Applied when the player interacts.")]
    [SerializeField] private List<ActiveResult> results = new();

    [Header("After Loading")]
    [Tooltip("ReplayResults: apply Results again.\n" +
             "UseRestoreResults: apply Restore Results instead (Results are not touched).")]
    [SerializeField] private RestoreMode restoreMode = RestoreMode.ReplayResults;

    [Tooltip("Used only with UseRestoreResults. Set the end state here, e.g. cutscene object off, door open.")]
    [SerializeField] private List<ActiveResult> restoreResults = new();

    [Tooltip("Extra things to do after loading, in both modes (must not need the player).")]
    [SerializeField] private UnityEvent onRestoredUsed;

    private Iinteractor interactor;
    private bool used;

    public bool Used => used;

    private void Awake()
    {
        interactor = GetComponent<Iinteractor>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (interactor == null) return;
        interactor.OnInteract += HandleInteract;
        interactor.AddCanInteractModifier(CanInteract);
    }

    protected override void OnDisable()
    {
        if (interactor != null)
        {
            interactor.OnInteract -= HandleInteract;
            interactor.RemoveCanInteractModifier(CanInteract);
        }
        base.OnDisable();
    }

    private bool CanInteract(GameObject rootplayer) => !(lockAfterUse && used);

    private void HandleInteract(GameObject rootplayer)
    {
        used = true;
        Apply(results);
        RecordNow(); // keep it even if this object gets disabled before saving
    }

    private static void Apply(List<ActiveResult> list)
    {
        foreach (ActiveResult r in list)
        {
            if (r.obj != null) r.obj.SetActive(r.active);
        }
    }

    protected override SaveData Capture() => new SaveData { used = used };

    protected override void Restore(SaveData state, int version)
    {
        if (!state.used) return;
        used = true;

        if (restoreMode == RestoreMode.ReplayResults) Apply(results);
        else Apply(restoreResults);

        onRestoredUsed?.Invoke();
    }
}
