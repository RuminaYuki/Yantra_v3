using System;
using System.Collections.Generic;
using UnityEngine;

// เก็บว่าผู้เล่นปลดล็อคการร่ายตัวไหนไปแล้ว (อ้างด้วย RadialMenuButtonData.spellId) และเซฟลงไฟล์
// ใส่ไว้ที่ player root (ข้างๆ InventorySaveable)
// เรียก Unlock("Push") ได้จาก UnlockSpellAddition, UnityEvent, เควส หรือโค้ดอื่น
[DisallowMultipleComponent]
public class SpellUnlocks : SaveableBehaviour<SpellUnlocks.SaveData>
{
    [Serializable]
    public class SaveData
    {
        public List<string> ids = new List<string>();
    }

    [Tooltip("แคตตาล็อกการร่าย (Yant.asset) ใช้เช็คว่า id ที่ส่งมามีอยู่จริงไหม")]
    [SerializeField] RadialMenuDataSO _catalog;

    readonly HashSet<string> _unlocked = new();

    public event Action<string> Unlocked;
    public event Action<string> Locked;

    protected override string DefaultKey => "spellUnlocks";

    public bool IsUnlocked(string spellId) =>
        !string.IsNullOrEmpty(spellId) && _unlocked.Contains(spellId);

    public bool IsUnlocked(RadialMenuButtonData data) =>
        data != null && (data.unlockedByDefault || IsUnlocked(data.spellId));

    public void Unlock(string spellId) => Unlock(spellId, null);

    // source = วัตถุที่สั่งปลดล็อค (ใช้แค่ตอน log เพื่อให้รู้ว่าใครส่ง id ผิดมา)
    public void Unlock(string spellId, UnityEngine.Object source)
    {
        if (!IsKnownId(spellId, "Unlock", source)) return;
        if (!_unlocked.Add(spellId)) return;
        RecordNow();
        Unlocked?.Invoke(spellId);
    }

    public void Lock(string spellId) => Lock(spellId, null);

    public void Lock(string spellId, UnityEngine.Object source)
    {
        if (!IsKnownId(spellId, "Lock", source)) return;
        if (!_unlocked.Remove(spellId)) return;
        RecordNow();
        Locked?.Invoke(spellId);
    }

    protected override SaveData Capture() => new SaveData { ids = new List<string>(_unlocked) };

    protected override void Restore(SaveData state, int version)
    {
        _unlocked.Clear();
        if (state.ids == null) return;

        foreach (string id in state.ids)
        {
            if (IsKnownId(id, "Load save")) _unlocked.Add(id);
        }
    }

    // เช็คว่า id มีใน _catalog ไหม ถ้าไม่มีจะ log บอกว่าใช้ id อะไรได้บ้าง
    bool IsKnownId(string spellId, string from, UnityEngine.Object source = null)
    {
        UnityEngine.Object context = source != null ? source : this;
        if (source != null) from = $"{from} (from {source.name})";

        if (string.IsNullOrEmpty(spellId))
        {
            Debug.LogWarning($"[SpellUnlocks] {from}: spellId is empty.", context);
            return false;
        }

        if (_catalog == null)
        {
            Debug.LogWarning($"[SpellUnlocks] {from} '{spellId}': no Catalog set on {name}, so the id cannot be checked.", context);
            return true; // ยังไม่ได้ใส่ catalog ก็ยอมให้ผ่านไปก่อน
        }

        if (_catalog.TryGetById(spellId, out _)) return true;

        var valid = new List<string>();
        foreach (RadialMenuButtonData d in _catalog.GetListData())
        {
            if (d != null && !string.IsNullOrEmpty(d.spellId)) valid.Add(d.spellId);
        }

        Debug.LogWarning($"[SpellUnlocks] {from}: spellId '{spellId}' is not in {_catalog.name}. " +
                         $"Valid ids: {string.Join(", ", valid)}", context);
        return false;
    }
}
