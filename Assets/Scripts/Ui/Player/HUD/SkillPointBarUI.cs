using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แถบ SP (Ap) — อ่านจาก SkillPoints ของผู้เล่น
///
/// ทำเป็นช่องแยกแทนแถบต่อเนื่อง เพราะ SP ไม่ใช่แค่ตัวเลข
/// ตาม GDD: Ap ถึงเกณฑ์ = ปืนยิงกระสุนต้องคำสาป ดาเมจหนัก
/// ถ้าใช้แถบยาว ๆ คนเล่นจะเดาไม่ออกว่าถึงเกณฑ์หรือยัง
/// เห็นเป็นช่องแล้วนับได้ทันทีว่าเหลืออีกกี่ครั้ง
///
/// แปะบน GameObject ที่จะเป็นแถวของช่อง (ควรมี Horizontal Layout Group)
/// </summary>
public class SkillPointBarUI : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("เว้นว่างได้ จะหาจาก tag Player ให้เอง")]
    [SerializeField] private SkillPoints _skillPoints;
    [SerializeField] private string _playerTag = "Player";

    [Header("Config")]
    [Tooltip("แบ่งเป็นกี่ช่อง — ปกติตั้งให้เท่ากับ Max Point ของ SkillPoints")]
    [SerializeField] private int _slotCount = 10;

    [Tooltip("ใช้เมื่ออ่านค่าจาก SkillPoints ไม่ได้เท่านั้น")]
    [SerializeField] private float _fallbackMaxPoints = 10f;

    [Header("Slots")]
    [Tooltip("ช่องทั้งหมด เรียงจากซ้ายไปขวา")]
    [SerializeField] private Image[] _slots;

    [Header("Colors")]
    [SerializeField] private Color _emptyColor = new(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color _filledColor = new(0.94f, 0.62f, 0.15f, 1f);

    [Tooltip("สีตอนเต็ม = ปืนพร้อมยิงกระสุนพิเศษ")]
    [SerializeField] private Color _readyColor = new(1f, 0.85f, 0.3f, 1f);

    [Header("Ready Pulse")]
    [Tooltip("ให้ช่องเต้นเบา ๆ ตอนเต็ม เพื่อบอกว่าพร้อมยิงแล้ว")]
    [SerializeField] private bool _pulseWhenReady = true;
    [SerializeField] private float _pulseSpeed = 3f;
    [SerializeField] private float _pulseAmount = 0.25f;

    private float _retryTimer;

    private void Update()
    {
        if (!TryGetSource()) return;
        if (_slots == null || _slots.Length == 0) return;

        float current = _skillPoints.CurrentSkillPoints;

        // อ่าน max จากระบบโดยตรง ไม่ hardcode
        // ถ้าทีมปรับ balance เปลี่ยนค่า max แถบจะตามเองทันที
        float max = _skillPoints.MaxSkillPoints;
        if (max <= 0f) max = _fallbackMaxPoints;

        float perSlot = max / Mathf.Max(1, _slotCount);
        bool isReady = current >= max;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            bool filled = current >= (i + 1) * perSlot;

            Color color;
            if (!filled) color = _emptyColor;
            else if (isReady) color = ApplyPulse(_readyColor);
            else color = _filledColor;

            _slots[i].color = color;
        }
    }

    private Color ApplyPulse(Color baseColor)
    {
        if (!_pulseWhenReady) return baseColor;

        float pulse = (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) * 0.5f;
        float multiplier = 1f - _pulseAmount + pulse * _pulseAmount;

        return new Color(
            baseColor.r * multiplier,
            baseColor.g * multiplier,
            baseColor.b * multiplier,
            baseColor.a);
    }

    private bool TryGetSource()
    {
        if (_skillPoints != null) return true;

        _retryTimer += Time.deltaTime;
        if (_retryTimer < 0.5f) return false;
        _retryTimer = 0f;

        var playerObj = GameObject.FindGameObjectWithTag(_playerTag);
        if (playerObj != null) _skillPoints = playerObj.GetComponent<SkillPoints>();

        return _skillPoints != null;
    }

#if UNITY_EDITOR
    /// <summary>ดึงช่องทั้งหมดจากลูกให้อัตโนมัติ — คลิกขวาที่ component แล้วเลือก</summary>
    [ContextMenu("เก็บช่องจากลูกทั้งหมด")]
    private void CollectSlots()
    {
        _slots = GetComponentsInChildren<Image>(true);
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[SkillPointBarUI] เก็บได้ {_slots.Length} ช่อง", this);
    }
#endif
}
