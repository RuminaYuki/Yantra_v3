using System;
using System.Collections.Generic;

// Everything written to one save slot.
// Plain data only: no MonoBehaviour and no reference to any game system.
// Every system (Player, Inventory, Quest, flags, a teammate's mini-game...) stores its data
// as a SaveSection, so this class does not change when a new system starts saving.
[Serializable]
public class GameSaveData
{
    // Bump when the layout of THIS class changes. Each section has its own version.
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;

    // Local time with offset (ISO 8601), e.g. 2026-10-21T14:30:00.0000000+07:00
    public string savedAt;

    public float playTimeSeconds;

    // Unity scene name the save was made in. Loaded with SceneLoader.LoadScene().
    public string sceneName;

    // Id of the SpawnPoint / SavePoint the player is placed at after loading.
    public string spawnPointId;

    public List<SaveSection> sections = new List<SaveSection>();
}
