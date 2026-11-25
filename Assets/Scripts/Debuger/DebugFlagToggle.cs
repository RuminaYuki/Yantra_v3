using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ติดบน Toggle ใน Debug panel — ขาว = flag เปิด, เทา (Overlay โชว์) = flag ปิด
// อ่านค่าจริงตลอด เกมเปลี่ยน flag เอง (เช่น เก็บปืน) ปุ่มก็เปลี่ยนตาม
// ไม่ต้องต่อ On Value Changed เพิ่ม — component นี้ฟังเอง
[RequireComponent(typeof(Toggle))]
public class DebugFlagToggle : MonoBehaviour
{
    [Tooltip("ใส่ได้หลายตัว (เช่น Combat = Attack + Block) — ขาวเมื่อทุกตัวเปิด, กดแล้วตั้งทุกตัวพร้อมกัน")]
    [SerializeField] private FlagSO[] _flags;

    [Tooltip("อ่านค่า flag จริงมาอัปเดตสีปุ่มทุกกี่วินาที")]
    [SerializeField] private float _refreshInterval = 0.2f;

    private Toggle _toggle;
    private readonly List<StateFlags> _owners = new List<StateFlags>();
    private readonly List<StateFlags> _result = new List<StateFlags>();
    private float _nextRefresh;

    private void Awake()
    {
        _toggle = GetComponent<Toggle>();
        _toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void OnDestroy()
    {
        if (_toggle != null)
            _toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh)
            return;
        _nextRefresh = Time.unscaledTime + _refreshInterval;

        // isOn = Overlay เทาโชว์ → isOn ต้องตรงข้ามกับ "เปิดอยู่"
        _toggle.SetIsOnWithoutNotify(!AllEnabled());
    }

    private void OnToggleChanged(bool isOn)
    {
        bool enable = !isOn;   // ขาว (isOn = false) = เปิด
        int count = 0;

        foreach (FlagSO flag in _flags)
        {
            foreach (StateFlags owner in Owners(flag))
            {
                owner.Set(flag, enable);
                count++;
            }
        }

        if (count == 0)
            Debug.LogWarning($"[DebugFlagToggle] {name}: ไม่มี StateFlags ไหนในฉากที่มี flag ของปุ่มนี้", this);
        else
            Debug.Log($"[DebugFlagToggle] {name}: {(enable ? "Enable" : "Disable")}", this);

        _nextRefresh = 0f;   // อัปเดตสีตามค่าจริงทันทีเฟรมหน้า
    }

    private bool AllEnabled()
    {
        bool found = false;
        foreach (FlagSO flag in _flags)
        {
            foreach (StateFlags owner in Owners(flag))
            {
                found = true;
                if (!owner.Get(flag))
                    return false;
            }
        }
        return found;   // ไม่เจอ flag ในฉาก = ถือว่าปิด (เทา)
    }

    // จำ StateFlags ในฉากไว้ — ตัวไหนถูกทำลาย (เปลี่ยนฉาก) หรือหา flag ไม่เจอ ค่อยหาใหม่
    private List<StateFlags> Owners(FlagSO flag)
    {
        _result.Clear();
        if (flag == null)
            return _result;

        _owners.RemoveAll(o => o == null);
        Collect(flag);

        if (_result.Count == 0)
        {
            _owners.Clear();
            _owners.AddRange(FindObjectsByType<StateFlags>());
            Collect(flag);
        }
        return _result;
    }

    private void Collect(FlagSO flag)
    {
        foreach (StateFlags owner in _owners)
        {
            if (owner.Contains(flag))
                _result.Add(owner);
        }
    }
}
