using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Reads and writes save files. Only SaveManager calls this.
// Files: <persistentDataPath>/saves/slot_<n>.json       (the full save)
//        <persistentDataPath>/saves/slot_<n>.meta.json  (small summary for the Load Save screen)
// Every file is written to .tmp first and then swapped in, keeping the previous file as .bak,
// so quitting in the middle of a write never leaves a broken save behind.
public static class SaveFileService
{
    public static string Folder => Path.Combine(Application.persistentDataPath, "saves");

    public static string PathOf(int slot) => Path.Combine(Folder, $"slot_{slot}.json");

    public static string MetaPathOf(int slot) => Path.Combine(Folder, $"slot_{slot}.meta.json");

    public static bool Exists(int slot)
    {
        string path = PathOf(slot);
        return File.Exists(path) || File.Exists(path + ".bak");
    }

    public static bool Write(int slot, GameSaveData data)
    {
        if (data == null) return false;
        return WriteSafe(PathOf(slot), JsonUtility.ToJson(data, true));
    }

    public static bool WriteMeta(int slot, SaveSlotMeta meta)
    {
        if (meta == null) return false;
        return WriteSafe(MetaPathOf(slot), JsonUtility.ToJson(meta, true));
    }

    // Returns null when there is no meta file (e.g. a save made before meta files existed).
    public static SaveSlotMeta ReadMeta(int slot)
    {
        string path = MetaPathOf(slot);
        return TryRead<SaveSlotMeta>(path) ?? TryRead<SaveSlotMeta>(path + ".bak");
    }

    private static bool WriteSafe(string path, string text)
    {
        string tmp = path + ".tmp";
        string bak = path + ".bak";

        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(tmp, text);

            // Swap the files by hand instead of File.Replace:
            // Mono's File.Replace on Windows can crash the Editor when the target already exists.
            if (File.Exists(path))
            {
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(path, bak);   // old save -> .bak
            }
            File.Move(tmp, path);       // new save -> main file

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Could not write {path}: {e.Message}");
            return false;
        }
    }

    // Returns null when there is no save. Falls back to the .bak file if the main one is broken.
    public static GameSaveData Read(int slot)
    {
        string path = PathOf(slot);
        GameSaveData data = TryRead<GameSaveData>(path) ?? TryRead<GameSaveData>(path + ".bak");
        if (data != null && data.sections == null) data.sections = new List<SaveSection>();
        return data;
    }

    // Deletes the save and its meta file (with their .bak / .tmp).
    public static void Delete(int slot)
    {
        string path = PathOf(slot);
        string meta = MetaPathOf(slot);
        foreach (string p in new[] { path, path + ".bak", path + ".tmp", meta, meta + ".bak", meta + ".tmp" })
        {
            try
            {
                if (File.Exists(p)) File.Delete(p);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not delete {p}: {e.Message}");
            }
        }
    }

    private static T TryRead<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;

        try
        {
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] Could not read {path}: {e.Message}");
            return null;
        }
    }
}
