using UnityEngine;
using UnityEngine.UI;

// ติดบน Toggle ใน Debug panel — ขาว = เวทปลดล็อคแล้ว, เทา (Overlay โชว์) = ยังล็อค
// อ่านสถานะจริงทุกเฟรม — เก็บเวทในเกมได้ ปุ่มก็ขาวเอง
// ไม่ต้องต่อ On Value Changed เพิ่ม — component นี้ฟังเอง
[RequireComponent(typeof(Toggle))]
public class DebugSpellToggle : MonoBehaviour
{
    [Tooltip("ว่างไว้ = หาจาก parent (Debug panel) เอง")]
    [SerializeField] private PlayerDebugManager _manager;

    [Tooltip("ต้องตรงกับ spellId ใน Yant.asset: SelfHeal / Homing Missile / Push")]
    [SerializeField] private string _spellId;

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
            Debug.LogWarning($"[DebugSpellToggle] {name}: หา PlayerDebugManager ไม่เจอ", this);
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

        // isOn = Overlay เทาโชว์ → isOn ต้องตรงข้ามกับ "ปลดล็อคแล้ว"
        _toggle.SetIsOnWithoutNotify(!_manager.IsSpellUnlocked(_spellId));
    }

    private void OnToggleChanged(bool isOn)
    {
        if (_manager == null)
            return;

        _manager.SetSpellUnlocked(_spellId, !isOn);   // ขาว (isOn = false) = ปลดล็อค
    }
}
