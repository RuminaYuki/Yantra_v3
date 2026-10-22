using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Simple key-value results that need no class of their own,
// e.g. "minigame.banana.cleared" = true or "minigame.banana.best" = 120.
// Use SaveManager.Instance.Flags. Stored in the save file as the "flags" section.
// Prefix keys with your system name ("minigame.<name>.") so they never clash.
public class SaveFlags
{
    public const int Version = 1;

    private readonly FlagList data;
    private readonly Dictionary<string, FlagValue> map = new Dictionary<string, FlagValue>();

    public SaveFlags() : this(new FlagList()) { }

    private SaveFlags(FlagList data)
    {
        this.data = data ?? new FlagList();
        if (this.data.items == null) this.data.items = new List<FlagValue>();

        foreach (FlagValue f in this.data.items)
        {
            if (f != null && !string.IsNullOrEmpty(f.key)) map[f.key] = f;
        }
    }

    public static SaveFlags FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return new SaveFlags();

        try
        {
            return new SaveFlags(JsonUtility.FromJson<FlagList>(json));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] Could not read flags: {e.Message}");
            return new SaveFlags();
        }
    }

    public string ToJson() => JsonUtility.ToJson(data);

    public bool Has(string key) => key != null && map.ContainsKey(key);

    public void Remove(string key)
    {
        if (key == null || !map.TryGetValue(key, out FlagValue f)) return;
        map.Remove(key);
        data.items.Remove(f);
    }

    // ---------- Bool ----------

    public void SetBool(string key, bool value) => Set(key, value ? "true" : "false");

    public bool GetBool(string key, bool fallback = false) =>
        TryGetRaw(key, out string raw) ? raw == "true" : fallback;

    // ---------- Int ----------

    public void SetInt(string key, int value) => Set(key, value.ToString(CultureInfo.InvariantCulture));

    public int GetInt(string key, int fallback = 0) =>
        TryGetRaw(key, out string raw) &&
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    // ---------- Float ----------

    public void SetFloat(string key, float value) => Set(key, value.ToString("R", CultureInfo.InvariantCulture));

    public float GetFloat(string key, float fallback = 0f) =>
        TryGetRaw(key, out string raw) &&
        float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

    // ---------- String ----------

    public void SetString(string key, string value) => Set(key, value ?? string.Empty);

    public string GetString(string key, string fallback = null) =>
        TryGetRaw(key, out string raw) ? raw : fallback;

    // ---------- Internals ----------

    private bool TryGetRaw(string key, out string raw)
    {
        raw = null;
        if (key == null || !map.TryGetValue(key, out FlagValue f)) return false;
        raw = f.value;
        return true;
    }

    private void Set(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[Save] Flag key is empty.");
            return;
        }

        if (!map.TryGetValue(key, out FlagValue f))
        {
            f = new FlagValue { key = key };
            map.Add(key, f);
            data.items.Add(f);
        }
        f.value = value;
    }
}

// JsonUtility cannot store a Dictionary, so flags are saved as a list of key + text value.
[Serializable]
public class FlagValue
{
    public string key;
    public string value;
}

[Serializable]
public class FlagList
{
    public List<FlagValue> items = new List<FlagValue>();
}
