using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The one and only save system. No other script reads or writes save files.
//
// Two layers of data:
//   - session : the save data in memory. Systems write into it while the game runs
//               (RecordSection, Flags). Nothing here is on disk yet.
//   - file    : a copy of the session, written only when the player uses a SavePoint.
// Loading replaces the session with the file, so everything since the last save is dropped.
//
// SaveManager never looks inside a section and does not know any game system.
// Each Saveable converts its own data (see SaveableBehaviour).
//
// Save slots: one file per slot (see SaveFileService). The slot only changes in NewGame(slot)
// and LoadSave(slot), together with the session, so one slot's game is never written into another.
//
// It creates itself before the first scene loads, so it does not need to be placed in a scene.
public class SaveManager : Singleton<SaveManager>
{
    public const string FlagsKey = "flags";

    // Number of save slots shown to the player. Valid slots are 0 .. SlotCount - 1.
    public const int SlotCount = 3;

    private GameSaveData session = new GameSaveData();
    private readonly Dictionary<string, SaveSection> sections = new Dictionary<string, SaveSection>();
    private readonly Dictionary<string, ISaveable> live = new Dictionary<string, ISaveable>();
    private readonly HashSet<object> blockers = new HashSet<object>();
    private string returnSpawnId;
    private float playTime;
    private bool inMainMenu;

    public SaveFlags Flags { get; private set; } = new SaveFlags();

    // The slot being played. SaveAt, LoadLastSave and DeleteSave() use it.
    public int CurrentSlot { get; private set; }

    // True when the current slot has a save file.
    public bool HasSave => SaveFileService.Exists(CurrentSlot);

    // False while a mini-game (or anything else) blocks saving, or while a scene is loading.
    public bool CanSave => blockers.Count == 0 && !IsSceneLoading;

    // Where the player should appear in the scene that is loading now.
    public string SpawnPointId => !string.IsNullOrEmpty(returnSpawnId) ? returnSpawnId : session.spawnPointId;

    public float PlayTimeSeconds => playTime;

    // Fired after the file was written (e.g. to show a "Game saved" message).
    public event Action Saved;

    private static bool IsSceneLoading => SceneLoader.Instance != null && SceneLoader.Instance.IsLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateOnStartup()
    {
        if (Instance != null) return;
        new GameObject("[SaveManager]").AddComponent<SaveManager>();
    }

    // ------------------------------------------------------------------ Lifecycle

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    protected override void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnDestroy();
    }

    private void Update()
    {
        if (!inMainMenu && Time.timeScale > 0f) playTime += Time.unscaledDeltaTime;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        // A mini-game destroyed without calling Unblock must not block saving forever.
        blockers.RemoveWhere(b => b is UnityEngine.Object o && o == null);

        inMainMenu = IsMainMenu(scene.name);

        // Back at the main menu: forget the game in memory so the next New Game starts clean.
        // The file is not touched; Continue reads it again.
        if (inMainMenu) UseData(new GameSaveData());
    }

    // ------------------------------------------------------------------ Called by Saveables

    public void Register(ISaveable saveable)
    {
        if (saveable == null) return;
        string key = saveable.SaveKey;
        if (string.IsNullOrEmpty(key)) return;

        if (live.TryGetValue(key, out ISaveable other) && !ReferenceEquals(other, saveable) && !IsDestroyed(other))
        {
            Debug.LogError($"[Save] Two objects use the save id '{key}': {Describe(other)} and {Describe(saveable)}. " +
                           "Only the last one will be saved.", saveable as UnityEngine.Object);
        }
        live[key] = saveable;
    }

    public void Unregister(ISaveable saveable)
    {
        if (saveable == null) return;
        string key = saveable.SaveKey;
        if (key != null && live.TryGetValue(key, out ISaveable current) && ReferenceEquals(current, saveable))
            live.Remove(key);
    }

    public bool TryGetSection(string id, out SaveSection section)
    {
        section = null;
        return !string.IsNullOrEmpty(id) && sections.TryGetValue(id, out section);
    }

    // Writes into the in-memory save only. The file changes at the next SaveAt().
    public void RecordSection(string id, int version, string json)
    {
        if (string.IsNullOrEmpty(id)) return;

        if (!sections.TryGetValue(id, out SaveSection s))
        {
            s = new SaveSection { id = id };
            sections.Add(id, s);
            session.sections.Add(s);
        }
        s.version = version;
        s.json = json;
    }

    public void RemoveSection(string id)
    {
        if (string.IsNullOrEmpty(id) || !sections.TryGetValue(id, out SaveSection s)) return;
        sections.Remove(id);
        session.sections.Remove(s);
    }

    // ------------------------------------------------------------------ Game flow

    // Empty save in memory, then load the gameplay scene. The file is kept until the first save.
    // Uses the current slot.
    public void NewGame() => NewGame(CurrentSlot);

    // Starts a new game in this slot. An old save in the slot is only overwritten at the first save.
    public void NewGame(int slot)
    {
        if (!IsValidSlot(slot))
        {
            Debug.LogError($"[Save] NewGame: slot {slot} is out of range (0 - {SlotCount - 1}).");
            return;
        }

        CurrentSlot = slot;
        UseData(new GameSaveData());

        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadGameplay();
        else Debug.LogError("[Save] NewGame: no SceneLoader in the game.");
    }

    // Saves at this point and writes the file.
    // reloadScene = true reloads the scene afterwards, so normal enemies come back.
    public bool SaveAt(SpawnPoint point, bool reloadScene = true)
    {
        if (point == null) return false;

        if (!CanSave)
        {
            Debug.Log("[Save] Saving is blocked right now.", point);
            return false;
        }

        if (string.IsNullOrEmpty(point.Id))
            Debug.LogWarning($"[Save] {point.name} has no SaveId. The player will not be placed there after loading.", point);

        CaptureSession();
        session.version = GameSaveData.CurrentVersion;
        session.sceneName = point.gameObject.scene.name;
        session.spawnPointId = point.Id;
        session.savedAt = DateTimeOffset.Now.ToString("o");
        returnSpawnId = null;

        if (!SaveFileService.Write(CurrentSlot, session)) return false;

        Debug.Log($"[Save] Saved at '{point.name}' in '{session.sceneName}'.", point);
        Saved?.Invoke();

        if (reloadScene) LoadScene(session.sceneName);
        return true;
    }

    // Drops everything since the last save and loads the current slot's file. False if there is no save.
    public bool LoadLastSave() => LoadSave(CurrentSlot);

    // Loads this slot's file and makes it the current slot. False if the slot has no save
    // (the current slot is not changed then).
    public bool LoadSave(int slot)
    {
        if (!IsValidSlot(slot))
        {
            Debug.LogError($"[Save] LoadSave: slot {slot} is out of range (0 - {SlotCount - 1}).");
            return false;
        }

        GameSaveData data = SaveFileService.Read(slot);
        if (data == null || string.IsNullOrEmpty(data.sceneName))
        {
            Debug.Log($"[Save] No save to load in slot {slot}.");
            return false;
        }

        CurrentSlot = slot;
        UseData(data);
        LoadScene(session.sceneName);
        return true;
    }

    // Leaves the scene WITHOUT writing the file (e.g. into a mini-game scene).
    // The current state is kept in memory, so nothing is lost when coming back.
    // From the main scene: pass the point to appear at when coming back.
    // From the mini-game: call it again with no point to go back.
    public void GoToScene(string sceneName, SpawnPoint returnPoint = null)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        CaptureSession();
        if (returnPoint != null) returnSpawnId = returnPoint.Id;
        LoadScene(sceneName);
    }

    // While at least one owner blocks, SavePoints do nothing. Unblock with the same owner.
    public void BlockSaving(object owner)
    {
        if (owner != null) blockers.Add(owner);
    }

    public void Unblock(object owner)
    {
        if (owner != null) blockers.Remove(owner);
    }

    // ------------------------------------------------------------------ Slots (e.g. for a slot select screen)

    public static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

    public bool HasSaveInSlot(int slot) => IsValidSlot(slot) && SaveFileService.Exists(slot);

    // Reads a slot's file without loading it, to show savedAt / playTimeSeconds / sceneName in the UI.
    // Null when the slot has no save. Do not change the returned data: it is not used by the game.
    public GameSaveData PeekSave(int slot) => IsValidSlot(slot) ? SaveFileService.Read(slot) : null;

    // The slot saved most recently (for a "Continue" button). False when no slot has a save.
    public bool TryGetLatestSlot(out int slot)
    {
        slot = -1;
        DateTimeOffset latest = DateTimeOffset.MinValue;

        for (int i = 0; i < SlotCount; i++)
        {
            GameSaveData data = PeekSave(i);
            if (data == null || string.IsNullOrEmpty(data.sceneName)) continue;

            DateTimeOffset.TryParse(data.savedAt, out DateTimeOffset savedAt);
            if (slot < 0 || savedAt > latest)
            {
                slot = i;
                latest = savedAt;
            }
        }
        return slot >= 0;
    }

    // Deletes the current slot's file.
    public void DeleteSave() => DeleteSave(CurrentSlot);

    // Deletes this slot's file. The game in memory is not touched.
    public void DeleteSave(int slot)
    {
        if (IsValidSlot(slot)) SaveFileService.Delete(slot);
    }

    // ------------------------------------------------------------------ Internals

    // Every Saveable in the scene writes its newest state into the session.
    // Sections whose owner is not in this scene are kept as they are.
    private void CaptureSession()
    {
        foreach (ISaveable saveable in new List<ISaveable>(live.Values))
        {
            if (IsDestroyed(saveable)) continue;

            string json;
            try
            {
                json = saveable.CaptureJson();
            }
            catch (Exception e)
            {
                Debug.LogException(e, saveable as UnityEngine.Object);
                continue;
            }

            if (json != null) RecordSection(saveable.SaveKey, saveable.SaveVersion, json);
        }

        RecordSection(FlagsKey, SaveFlags.Version, Flags.ToJson());
        session.playTimeSeconds = playTime;
    }

    private void UseData(GameSaveData data)
    {
        session = data ?? new GameSaveData();
        if (session.sections == null) session.sections = new List<SaveSection>();

        sections.Clear();
        foreach (SaveSection s in session.sections)
        {
            if (s != null && !string.IsNullOrEmpty(s.id)) sections[s.id] = s;
        }

        Flags = SaveFlags.FromJson(sections.TryGetValue(FlagsKey, out SaveSection f) ? f.json : null);
        playTime = session.playTimeSeconds;
        returnSpawnId = null;
        blockers.Clear();
    }

    private static void LoadScene(string sceneName)
    {
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(sceneName);
        else SceneManager.LoadScene(sceneName);
    }

    private static bool IsMainMenu(string sceneName)
    {
        SceneLoader loader = SceneLoader.Instance;
        return loader != null && loader.Catalog != null && loader.Catalog.IsMainMenu(sceneName);
    }

    private static bool IsDestroyed(ISaveable s) => s == null || (s is UnityEngine.Object o && o == null);

    private static string Describe(ISaveable s) => s is Component c ? c.name : s.GetType().Name;

    // ------------------------------------------------------------------ Debug (right-click the component in Play Mode)

    [ContextMenu("Debug/Load Last Save")]
    private void DebugLoad() => LoadLastSave();

    [ContextMenu("Debug/Delete Save File")]
    private void DebugDelete()
    {
        DeleteSave();
        Debug.Log($"[Save] Save file of slot {CurrentSlot} deleted.");
    }

    [ContextMenu("Debug/Print Save Path")]
    private void DebugPath() => Debug.Log($"[Save] Slot {CurrentSlot}: {SaveFileService.PathOf(CurrentSlot)}");
}
