// Anything that keeps its own section in the save file.
// Most scripts should inherit SaveableBehaviour<TState> instead of using this directly.
public interface ISaveable
{
    // Section id: a fixed key ("player") or the GUID of a SaveId component.
    string SaveKey { get; }

    // Version of this section's data format.
    int SaveVersion { get; }

    // Current state as a JSON string. Return null to skip this section.
    string CaptureJson();

    // Put back a state written by CaptureJson. `version` is the SaveVersion it was written with.
    void RestoreJson(string json, int version);
}
