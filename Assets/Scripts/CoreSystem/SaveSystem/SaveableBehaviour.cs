using System;
using UnityEngine;

// Base class for anything that saves its own data. A child class only writes two methods:
//
//     [Serializable] public class MyState { public int round; }
//
//     public class MySaveable : SaveableBehaviour<MyState>
//     {
//         protected override MyState Capture() => new MyState { round = round };
//         protected override void Restore(MyState state, int version) => round = state.round;
//     }
//
// How it works:
//   - OnEnable  : registers with SaveManager, so it is captured when the player saves.
//   - Start     : asks SaveManager for its own section and restores it (pull model).
//                 Start runs after every Awake in the scene, so default values set in Awake
//                 are already there and the restored values are not overwritten.
//   - RecordNow : writes to the in-memory save right away. Use it for changes that may be gone
//                 by the time the player saves (an enemy that died, an item that was picked up).
//
// Section id: the fixed key (DefaultKey or the Inspector field), otherwise the SaveId GUID.
public abstract class SaveableBehaviour<TState> : MonoBehaviour, ISaveable where TState : class
{
    [Tooltip("Section id for one-of-a-kind data (e.g. \"player\").\n" +
             "Leave empty to use the SaveId component on this object.")]
    [SerializeField] private string fixedKey;

    private SaveId saveId;
    private bool registered;

    // Fixed key used when the Inspector field is empty. Override for one-of-a-kind scripts.
    protected virtual string DefaultKey => null;

    // Bump when TState changes, then convert old data in Restore().
    public virtual int SaveVersion => 1;

    public string SaveKey
    {
        get
        {
            if (!string.IsNullOrEmpty(fixedKey)) return fixedKey;
            if (!string.IsNullOrEmpty(DefaultKey)) return DefaultKey;
            if (saveId == null) saveId = GetComponent<SaveId>();
            return saveId != null && saveId.HasId ? saveId.Id : null;
        }
    }

    // Current state. Return null to skip saving this time.
    protected abstract TState Capture();

    // Put the state back. `version` is the SaveVersion the data was written with.
    protected abstract void Restore(TState state, int version);

    // ---------- ISaveable ----------

    public string CaptureJson()
    {
        TState state = Capture();
        return state == null ? null : JsonUtility.ToJson(state);
    }

    public void RestoreJson(string json, int version)
    {
        TState state;
        try
        {
            state = JsonUtility.FromJson<TState>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] '{SaveKey}' on {name}: could not read saved data ({e.Message}).", this);
            return;
        }

        if (state != null) Restore(state, version);
    }

    // ---------- Lifecycle ----------

    protected virtual void OnEnable()
    {
        string key = SaveKey;
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning($"[Save] {GetType().Name} on {name} has no save id. " +
                             "Add a SaveId component or set a fixed key. It will not be saved.", this);
            return;
        }

        SaveManager manager = SaveManager.Instance;
        if (manager == null) return;

        manager.Register(this);
        registered = true;
    }

    protected virtual void OnDisable()
    {
        if (!registered) return;
        registered = false;

        SaveManager manager = SaveManager.Instance;
        if (manager != null) manager.Unregister(this);
    }

    protected virtual void Start()
    {
        RestoreFromSave();
    }

    // Returns true if saved data was found and restored.
    protected bool RestoreFromSave()
    {
        SaveManager manager = SaveManager.Instance;
        string key = SaveKey;
        if (manager == null || string.IsNullOrEmpty(key)) return false;
        if (!manager.TryGetSection(key, out SaveSection section)) return false;

        try
        {
            RestoreJson(section.json, section.version);
            return true;
        }
        catch (Exception e)
        {
            // One broken section must not stop the rest of the scene from loading.
            Debug.LogException(e, this);
            return false;
        }
    }

    // Write the current state into the in-memory save now (not to the file).
    protected void RecordNow()
    {
        SaveManager manager = SaveManager.Instance;
        string key = SaveKey;
        if (manager == null || string.IsNullOrEmpty(key)) return;

        string json = CaptureJson();
        if (json != null) manager.RecordSection(key, SaveVersion, json);
    }
}
