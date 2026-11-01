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
/// ต่อยได้ทีละนิด (เช่น 0.2) ช่องถัดไปจะค่อย ๆ เติมจากซ้ายไปขวา และกะพริบตอนได้ SP
/// ไม่ต้องตั้งอะไรเพิ่ม — โค้ดสร้างแถบเติมในแต่ละช่องให้เอง
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

    [Header("Partial Fill")]
    [Tooltip("ช่องที่ยังเติมไม่เต็ม จางกว่าช่องเต็มเท่าไหร่ (1 = สีเดียวกัน)")]
    [Range(0f, 1f)][SerializeField] private float _partialAlpha = 0.7f;

    [Tooltip("ความเร็วที่แถบวิ่งตามค่าจริง (ช่องต่อวินาที)")]
    [SerializeField] private float _fillSpeed = 6f;

    [Header("Gain Flash")]
    [Tooltip("ช่องที่เพิ่งได้ SP สว่างวาบแป๊บนึง ให้รู้ว่าต่อยแล้วได้")]
    [SerializeField] private bool _flashOnGain = true;
    [SerializeField] private Color _flashColor = new(1f, 0.95f, 0.75f, 1f);
    [SerializeField] private float _flashTime = 0.25f;

    [Header("Ready Pulse")]
    [Tooltip("ให้ช่องเต้นเบา ๆ ตอนเต็ม เพื่อบอกว่าพร้อมยิงแล้ว")]
    [SerializeField] private bool _pulseWhenReady = true;
    [SerializeField] private float _pulseSpeed = 3f;
    [SerializeField] private float _pulseAmount = 0.25f;

    private float _retryTimer;
    private Image[] _fills;
    private float _shown = -1f;     // หน่วยเป็น "ช่อง" เช่น 3.4 = เต็ม 3 ช่อง ช่องที่ 4 ได้ 40%
    private float _lastValue = -1f;
    private float _flashTimer;
    private int _flashSlot = -1;

    private void Update()
    {
        if (!TryGetSource()) return;
        if (_slots == null || _slots.Length == 0) return;
        EnsureFills();

        float current = _skillPoints.CurrentSkillPoints;

        // อ่าน max จากระบบโดยตรง ไม่ hardcode
        // ถ้าทีมปรับ balance เปลี่ยนค่า max แถบจะตามเองทันที
        float max = _skillPoints.MaxSkillPoints;
        if (max <= 0f) max = _fallbackMaxPoints;

        float perSlot = max / Mathf.Max(1, _slotCount);
        float target = Mathf.Clamp(current / perSlot, 0f, _slots.Length);
        bool isReady = current >= max;

        if (_lastValue >= 0f && current > _lastValue + 0.0001f && _flashOnGain)
        {
            _flashSlot = Mathf.Min(_slots.Length - 1, Mathf.FloorToInt(target - 0.0001f));
            _flashTimer = _flashTime;
        }
        _lastValue = current;

        // ลดลง (ใช้ SP) = หายทันที / เพิ่มขึ้น = วิ่งตาม
        _shown = _shown < 0f || target < _shown ? target : Mathf.MoveTowards(_shown, target, _fillSpeed * Time.unscaledDeltaTime);
        _flashTimer = Mathf.Max(0f, _flashTimer - Time.unscaledDeltaTime);

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            float amount = Mathf.Clamp01(_shown - i);
            bool full = amount >= 0.999f;

            Color color = isReady ? ApplyPulse(_readyColor) : _filledColor;
            if (!full) color.a *= _partialAlpha;

            if (i == _flashSlot && _flashTimer > 0f)
                color = Color.Lerp(color, _flashColor, _flashTimer / _flashTime);

            _slots[i].color = _emptyColor;   // ตัวช่องเป็นพื้นหลัง
            _fills[i].fillAmount = amount;
            _fills[i].color = color;
            _fills[i].enabled = amount > 0f;
        }
    }

    // สร้างแถบเติมเป็นลูกของแต่ละช่อง ใช้รูปเดียวกับช่อง
    private void EnsureFills()
    {
        if (_fills != null && _fills.Length == _slots.Length) return;

        _fills = new Image[_slots.Length];
        for (int i = 0; i < _slots.Length; i++)
        {
            Image slot = _slots[i];
            if (slot == null) continue;

            var go = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(slot.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            Image fill = go.GetComponent<Image>();
            fill.sprite = slot.sprite != null ? slot.sprite : WhiteSprite();   // Filled ต้องมีรูป ไม่งั้นไม่ตัด
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            _fills[i] = fill;
        }
    }

    private static Sprite _white;

    private static Sprite WhiteSprite()
    {
        if (_white == null)
            _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return _white;
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

        _skillPoints = PlayerLookup.Find<SkillPoints>(_playerTag);
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