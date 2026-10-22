using System;

// One system's data inside the save file.
// The owner turns its own state into JSON (Capture) and back (Restore).
// SaveManager only stores the text and never looks inside it, so the save core
// does not need to know any game system (Player, Inventory, Quest, mini-games...).
[Serializable]
public class SaveSection
{
    // Fixed key for one-of-a-kind data ("player", "inventory", "quests", "flags"),
    // or the GUID from a SaveId component for objects placed in a scene.
    public string id;

    // Version of the owner's data format.
    // Given back on Restore so the owner can convert data from older saves.
    public int version;

    // The owner's data, already turned into a JSON string.
    public string json;
}
