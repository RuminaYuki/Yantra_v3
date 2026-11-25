using UnityEngine;
using UnityEngine.UI;

// ติดบน Button ใน Debug panel — กดแล้ววาร์ปผู้เล่นไปจุดใน Teleport Groups ของ PlayerDebugManager
// Group = กลุ่มที่เท่าไหร่ (0 = กลุ่มแรก), Point = จุดในกลุ่ม (เริ่ม 0 ใหม่ทุกกลุ่ม)
// ไม่ต้องต่อ OnClick เพิ่ม — component นี้ฟังเอง
[RequireComponent(typeof(Button))]
public class DebugTeleportButton : MonoBehaviour
{
    [Tooltip("ว่างไว้ = หาจาก parent (Debug panel) เอง")]
    [SerializeField] private PlayerDebugManager _manager;

    [Tooltip("กลุ่มที่เท่าไหร่ใน Teleport Groups (0 = กลุ่มแรก)")]
    [SerializeField, Min(0)] private int _group;

    [Tooltip("จุดที่เท่าไหร่ในกลุ่ม (เริ่ม 0)")]
    [SerializeField, Min(0)] private int _point;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);

        if (_manager == null)
            _manager = GetComponentInParent<PlayerDebugManager>(true);
        if (_manager == null)
            _manager = FindAnyObjectByType<PlayerDebugManager>(FindObjectsInactive.Include);
        if (_manager == null)
            Debug.LogWarning($"[DebugTeleportButton] {name}: หา PlayerDebugManager ไม่เจอ", this);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if (_manager != null)
            _manager.TeleportPlayer(_group, _point);
    }
}
