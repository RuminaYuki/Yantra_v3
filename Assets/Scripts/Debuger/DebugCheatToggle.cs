using UnityEngine;
using UnityEngine.UI;

// ติดบน Toggle ใน Debug panel — ขาว = โหมดเปิดอยู่, เทา (Overlay โชว์) = ปิด (แบบเดียวกับ DebugFlagToggle)
// อ่านสถานะจริงจาก PlayerDebugManager ทุกเฟรม — ติ๊กใน Inspector / ผู้เล่นเกิดใหม่ ปุ่มก็ตรงเสมอ
// ไม่ต้องต่อ On Value Changed เพิ่ม — component นี้ฟังเอง
[RequireComponent(typeof(Toggle))]
public class DebugCheatToggle : MonoBehaviour
{
    [Tooltip("ว่างไว้ = หาจาก parent (Debug panel) เอง")]
    [SerializeField] private PlayerDebugManager _manager;
    [SerializeField] private PlayerDebugManager.Cheat _cheat;

    private Toggle _toggle;

    private void Awake()
    {
        _toggle = GetComponent<Toggle>();
        _toggle.onValueChanged.AddListener(OnToggleChanged);

        if (_manager == null)
            _manager = GetComponentInParent<PlayerDebugManager>(true);
        if (_manager == null)
            _manager = FindAnyObjectByType<PlayerDebugManager>(FindObjectsInactive.Include);
        if (_manager == null)
            Debug.LogWarning($"[DebugCheatToggle] {name}: หา PlayerDebugManager ไม่เจอ", this);
    }

    private void OnDestroy()
    {
        if (_toggle != null)
            _toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void Update()
    {
        if (_manager == null)
            return;

        // isOn = Overlay เทาโชว์ → isOn ต้องตรงข้ามกับ "เปิดอยู่"
        _toggle.SetIsOnWithoutNotify(!_manager.IsOn(_cheat));
    }

    private void OnToggleChanged(bool isOn)
    {
        if (_manager == null)
            return;

        _manager.Set(_cheat, !isOn);   // ขาว (isOn = false) = เปิด
    }
}
