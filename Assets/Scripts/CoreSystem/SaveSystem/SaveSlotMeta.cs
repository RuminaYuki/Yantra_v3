using System;

// Small summary of one save slot, written next to the save file as slot_<n>.meta.json.
// The Load Save screen reads only this file, so it does not have to load every full save.
// The background image is not saved: locationId is looked up in LocationDatabase.
[Serializable]
public class SaveSlotMeta
{
    // Bump when the layout of this class changes.
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int slotIndex;

    // Save time in UTC ticks. Use SavedAtLocal to show it.
    public long savedAtUtcTicks;

    public float playTimeSeconds;
    public string sceneName;

    // LocationData.locationId of the SavePoint used. Empty when unknown (old saves).
    public string locationId;

    public bool HasSaveTime => savedAtUtcTicks > 0;

    public DateTime SavedAtLocal => new DateTime(savedAtUtcTicks, DateTimeKind.Utc).ToLocalTime();

    public TimeSpan PlayTime => TimeSpan.FromSeconds(playTimeSeconds);
}
