using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Reads and writes save files. Only SaveManager calls this.
// File: <persistentDataPath>/saves/slot_<n>.json
// A save is written to .tmp first and then swapped in, keeping the previous file as .bak,
// so quitting in the middle of a write never leaves a broken save behind.
public static class SaveFileService
{
    public static string Folder => Path.Combine(Application.persistentDataPath, "saves");

    public static string PathOf(int slot) => Path.Combine(Folder, $"slot_{slot}.json");

    public static bool Exists(int slot)
    {
        string path = PathOf(slot);
        return File.Exists(path) || File.Exists(path + ".bak");
    }

    public static bool Write(int slot, GameSaveData data)
    {
        if (data == null) return false;

        string path = PathOf(slot);
        string tmp = path + ".tmp";
        string bak = path + ".bak";

        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));

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
        return TryRead(path) ?? TryRead(path + ".bak");
    }

    public static void Delete(int slot)
    {
        string path = PathOf(slot);
        foreach (string p in new[] { path, path + ".bak", path + ".tmp" })
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

    private static GameSaveData TryRead(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
            if (data != null && data.sections == null) data.sections = new List<SaveSection>();
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] Could not read {path}: {e.Message}");
            return null;
        }
    }
}
