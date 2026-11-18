using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แถบ SP (Ap) — อ่านจาก SkillPoints ของผู้เล่น
///
/// ทำเป็นช่องแยกแทนแถบต่อเนื่อง เพราะ SP ไม่ใช่แค่ตัวเลข
/// ตาม GDD: Ap ถึงเกณฑ์ = ปืนยิงกระสุนต้องคำสาป ดาเมจหนัก
/// เห็นเป็นช่องแล้วนับได้ทันทีว่าเหลืออีกกี่ครั้ง
///
/// พอเต็ม: แสงวิ่งจากซ้ายไปขวา ช่องทั้งหมดรวมเป็นแถบเดียว → เด้ง + สั่น
/// แล้วไอคอนปืนตกลงมาใต้แถบ บอกว่ายิงได้แล้ว ระหว่างรอยิงมีแสงวิ่งผ่านแถบเป็นระยะ
/// ยิงไปแล้ว (SP ลด) แถบรวมหดกลับ เหลือเป็นช่องเหมือนเดิม
///
/// โค้ดสร้างแถบเติม แถบรวม แสงวิ่ง และกรอบเด้งให้เอง ไม่ต้องสร้างใน Hierarchy
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
    [Tooltip("ให้แถบเต้นเบา ๆ ตอนเต็ม เพื่อบอกว่าพร้อมยิงแล้ว")]
    [SerializeField] private bool _pulseWhenReady = true;
    [SerializeField] private float _pulseSpeed = 3f;
    [SerializeField] private float _pulseAmount = 0.25f;

    [Header("รวมเป็นแถบเดียวตอนเต็ม")]
    [Tooltip("ติ๊ก = SP เต็มแล้วช่องทั้งหมดรวมเป็นแถบเดียว / ไม่ติ๊ก = เป็นช่องเหมือนเดิม")]
    [SerializeField] private bool _mergeWhenFull = true;

    [Tooltip("รูปของแถบรวม — เว้นว่าง = ใช้รูปเดียวกับช่อง")]
    [SerializeField] private Sprite _mergedSprite;

    [Tooltip("แสงวิ่งรวมช่องจากซ้ายไปขวาภายในกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _mergeTime = 0.22f;

    [Tooltip("ยิงแล้ว แถบรวมหดหายภายในกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _breakTime = 0.2f;

    [Header("Ready Pop")]
    [Tooltip("รวมเสร็จแล้วเด้ง + วาบ + มีกรอบกระจายออก")]
    [SerializeField] private bool _popOnReady = true;

    [Tooltip("ตอนเด้ง แถบพองหนาขึ้นกี่เท่า")]
    [SerializeField] private float _popThickness = 2.4f;

    [Tooltip("ตอนเด้ง แถบยืดยาวขึ้นกี่เท่า (เฉพาะแถบรวม)")]
    [SerializeField] private float _popStretch = 1.06f;
    [SerializeField] private float _popDuration = 0.45f;
    [SerializeField] private float _popFlashTime = 0.25f;

    [Tooltip("กรอบรอบแถบที่กระจายออกแล้วจางหาย")]
    [SerializeField] private float _echoDuration = 0.8f;

    [Tooltip("กระจายออกไปกี่พิกเซล — X = ซ้ายขวา, Y = บนล่าง (Y น้อยไว้ ไม่ให้ชนหลอดเลือด)")]
    [SerializeField] private Vector2 _echoSpread = new(18f, 6f);
    [SerializeField] private float _echoThickness = 2.5f;
    [SerializeField] private Color _echoColor = new(1f, 0.93f, 0.67f, 0.85f);

    [Header("สั่น (แถบรวม + ไอคอน)")]
    [Tooltip("ตอนรวมเสร็จ สั่นแรงแค่ไหน (พิกเซล)")]
    [SerializeField, Min(0f)] private float _shakeStrength = 6f;
    [SerializeField, Min(0.01f)] private float _shakeDuration = 0.4f;

    [Tooltip("สั่นเบา ๆ ค้างไว้ตลอดที่เต็ม (พิกเซล) — 0 = ไม่สั่น")]
    [SerializeField, Min(0f)] private float _holdShake = 0.6f;

    [Tooltip("สั่นถี่แค่ไหน")]
    [SerializeField, Min(1f)] private float _shakeFrequency = 28f;

    [Header("แสงวิ่งผ่านแถบ")]
    [Tooltip("ตอนเต็ม แสงวิ่งผ่านแถบทุกกี่วินาที — 0 = ไม่มี")]
    [SerializeField, Min(0f)] private float _shineInterval = 1.4f;

    [Tooltip("แสงวิ่งจากซ้ายสุดไปขวาสุดภายในกี่วินาที")]
    [SerializeField, Min(0.05f)] private float _shineDuration = 0.45f;

    [Tooltip("แสงกว้างแค่ไหน เทียบกับความยาวแถบ")]
    [SerializeField, Range(0.05f, 0.6f)] private float _shineWidth = 0.15f;
    [SerializeField, Range(0f, 1f)] private float _shineAlpha = 0.65f;

    [Tooltip("แสงเอียงกี่องศา")]
    [SerializeField, Range(0f, 60f)] private float _shineAngle = 25f;

    [Header("ไอคอนปืนใต้แถบ (ไม่ใส่ก็ได้)")]
    [Tooltip("รูปปืนที่ตกลงมาใต้แถบตอน SP เต็ม เช่น HUD_GunIcon\nวางไว้ตรงจุดที่อยากให้อยู่ตอนจบ / ห้ามเป็นลูกของแถวนี้ ไม่งั้นจะถูกนับเป็นช่อง")]
    [SerializeField] private Image _readyIcon;

    [Tooltip("ติ๊ก = ย้อมไอคอนเป็นสีตอนเต็ม / ไม่ติ๊ก = ใช้สีตามรูป")]
    [SerializeField] private bool _tintIcon = true;

    [Tooltip("ไอคอนตกลงมาจากสูงกว่าจุดจริงกี่พิกเซล")]
    [SerializeField] private float _iconDrop = 30f;
    [SerializeField, Min(0.01f)] private float _iconDropTime = 0.35f;

    [Tooltip("ตอนตกลงมา ไอคอนเริ่มจากใหญ่กี่เท่า")]
    [SerializeField] private float _iconPopScale = 1.5f;

    [Tooltip("ยิงไปแล้ว ไอคอนจางหายในกี่วินาที")]
    [SerializeField] private float _iconFadeOut = 0.3f;

    [Tooltip("ตอนพร้อมยิง ไอคอนเต้นใหญ่ขึ้นกี่ส่วน (0.04 = 4%)")]
    [SerializeField] private float _iconPulseScale = 0.04f;

    [Header("ข้อความข้างไอคอน (ไม่ใส่ก็ได้)")]
    [Tooltip("TextMeshPro เช่นคำว่า Shoot !! — ห้ามเป็นลูกของแถวนี้")]
    [SerializeField] private TMP_Text _readyText;

    [Tooltip("ขึ้นช้ากว่าไอคอนกี่วินาที")]
    [SerializeField, Min(0f)] private float _textDelay = 0.1f;

    [Tooltip("ตอนโผล่ ตัวหนังสือเริ่มจากใหญ่กี่เท่า")]
    [SerializeField] private float _textPopScale = 1.8f;

    [Tooltip("ตอนรอยิง กะพริบจางสุดแค่ไหน (1 = ไม่กะพริบ)")]
    [SerializeField, Range(0f, 1f)] private float _textBlinkMin = 0.5f;

    [Header("Center Callout (ไม่ใส่ก็ได้)")]
    [Tooltip("ไอคอนกลางจอใกล้เป้าเล็ง เด้งขึ้นแป๊บเดียวตอน SP เต็ม\nผู้เล่นมองกลางจอ ไม่ได้มองมุมจอ — แบบเดียวกับที่ Guard อยู่ใต้เป้าเล็ง")]
    [SerializeField] private Image _calloutIcon;

    [Tooltip("ค้างไว้กี่วินาทีก่อนจาง")]
    [SerializeField] private float _calloutHold = 0.7f;
    [SerializeField] private float _calloutFade = 0.4f;
    [SerializeField] private float _calloutPopScale = 1.4f;

    [Tooltip("ตอนจาง ลอยขึ้นกี่พิกเซล")]
    [SerializeField] private float _calloutRise = 14f;

    [Header("Sound (เว้นว่าง = ไม่มีเสียง)")]
    [Tooltip("เสียงตอน SP เต็ม — ดังตอนแถบรวมเสร็จ ผู้เล่นไม่ได้มองมุมจอ เสียงช่วยให้รู้ตัวได้ดีที่สุด")]
    [SerializeField] private SoundID _readySound;

    [Header("Debug")]
    [Tooltip("ขึ้นข้อความใน Console ทุกครั้งที่เด้ง — ไว้เช็คว่าทำงานจริงไหม")]
    [SerializeField] private bool _logPop = true;

    [Tooltip("ติ๊กตอน Play = แกล้งให้ SP เต็ม ไว้ดูเอฟเฟกต์ / เอาติ๊กออก = กลับเป็นค่าจริง (เหมือนยิงไปแล้ว)")]
    [SerializeField] private bool _debugForceFull;

    private float _retryTimer;
    private Image[] _fills;
    private float _shown = -1f;     // หน่วยเป็น "ช่อง" เช่น 3.4 = เต็ม 3 ช่อง ช่องที่ 4 ได้ 40%
    private float _lastValue = -1f;
    private float _flashTimer;
    private int _flashSlot = -1;

    private bool _popped;
    private float _popTimer = -1f;      // ช่วงแสงวิ่งรวมช่อง
    private float _impactTimer = -1f;   // นับจากตอนรวมเสร็จ — เด้ง / วาบ / สั่น
    private float _echoTimer = -1f;
    private float _merge;               // 0 = เป็นช่อง / 1 = รวมเป็นแถบเดียวแล้ว
    private float _shineTimer;

    private Vector3[] _slotScales;
    private Vector2 _spanCenter;
    private Vector2 _spanSize;

    private RectTransform _merged;
    private Image _bar;
    private RectTransform _barRect;
    private Image _shine;
    private RectTransform _shineRect;

    private RectTransform _echo;
    private Image[] _echoLines;

    private float _iconAlpha;
    private float _iconTimer = -1f;
    private Vector3 _iconBaseScale = Vector3.one;
    private Vector2 _iconBasePos;
    private bool _iconSaved;

    private Vector3 _textBaseScale = Vector3.one;
    private Vector2 _textBasePos;
    private Color _textBaseColor = Color.white;
    private bool _textSaved;

    private float _calloutTimer = -1f;
    private Vector3 _calloutBaseScale = Vector3.one;
    private Vector2 _calloutBasePos;
    private bool _calloutSaved;

    private CanvasGroup _gateGroup;
    private bool _gateSearched;

    private static readonly Vector3[] _corners = new Vector3[4];

    private void Awake()
    {
        // ซ่อนไอคอน/ข้อความไว้ก่อน กันแวบขึ้นมาตอนเข้าฉาก
        SaveIconBase();
        SaveTextBase();
        if (_readyIcon != null) _readyIcon.enabled = false;
        if (_readyText != null) _readyText.enabled = false;
    }

    private void OnDisable()
    {
        // ปิดกลางคันตอนกำลังสั่น — คืนตำแหน่งเดิมไว้
        if (_iconSaved && _readyIcon != null)
        {
            _readyIcon.rectTransform.anchoredPosition = _iconBasePos;
            _readyIcon.rectTransform.localScale = _iconBaseScale;
        }

        if (_textSaved && _readyText != null)
        {
            _readyText.rectTransform.anchoredPosition = _textBasePos;
            _readyText.rectTransform.localScale = _textBaseScale;
        }
    }

    private void Update()
    {
        if (!TryGetSource()) return;
        if (_slots == null || _slots.Length == 0) return;
        EnsureFills();

        float dt = Time.unscaledDeltaTime;

        // อ่าน max จากระบบโดยตรง ไม่ hardcode
        // ถ้าทีมปรับ balance เปลี่ยนค่า max แถบจะตามเองทันที
        float max = _skillPoints.MaxSkillPoints;
        if (max <= 0f) max = _fallbackMaxPoints;
        float current = _debugForceFull ? max : _skillPoints.CurrentSkillPoints;

        float perSlot = max / Mathf.Max(1, _slotCount);
        float target = Mathf.Clamp(current / perSlot, 0f, _slots.Length);
        bool isReady = current >= max;
        bool firstRead = _lastValue < 0f;

        if (!firstRead && current > _lastValue + 0.0001f && _flashOnGain)
        {
            _flashSlot = Mathf.Min(_slots.Length - 1, Mathf.FloorToInt(target - 0.0001f));
            _flashTimer = _flashTime;
        }
        _lastValue = current;

        // ลดลง (ใช้ SP) = หายทันที / เพิ่มขึ้น = วิ่งตาม
        _shown = _shown < 0f || target < _shown ? target : Mathf.MoveTowards(_shown, target, _fillSpeed * dt);
        _flashTimer = Mathf.Max(0f, _flashTimer - dt);

        // นับว่าเต็มตอนแถบวิ่งไปถึงจริง ไม่ใช่ตอนค่าเต็ม — เด้งพร้อมภาพ
        // แถบถูกซ่อนอยู่ (ยังไม่ได้ปืน / HUD ซ่อน) = ไม่เด้ง ไม่มีเสียง พอโผล่กลับมาแล้วค่อยเด้ง
        bool full = isReady && _shown >= target - 0.001f;
        UpdatePopState(full, IsGateOpen(), firstRead || Time.timeSinceLevelLoad < 1f, dt);
        full &= _popped;

        bool merged = _mergeWhenFull && _popped;
        if (!merged) _merge = Mathf.MoveTowards(_merge, 0f, dt / _breakTime);

        MeasureSpan();
        Vector2 shake = ShakeOffset();
        float bump = PopBump();
        float popFlash = _impactTimer >= 0f && _impactTimer < _popFlashTime ? 1f - _impactTimer / _popFlashTime : 0f;

        // แถบรวมเด้งทั้งหนาและยาว / ถ้าไม่รวม ช่องพองหนาแบบเดิม
        Vector2 barScale = new(1f + (_popStretch - 1f) * bump, 1f + (_popThickness - 1f) * bump);
        float slotThickness = _mergeWhenFull ? 1f : barScale.y;

        // รวมเสร็จแล้วซ่อนช่องไว้ข้างใต้ — ใช้สีใสแทนการปิด Image ไม่งั้น Layout คำนวณขนาดช่องใหม่
        bool hideSlots = merged && _merge >= 1f;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            float amount = Mathf.Clamp01(_shown - i);
            bool slotFull = amount >= 0.999f;

            Color color = isReady ? ApplyPulse(_readyColor) : _filledColor;
            if (!slotFull) color.a *= _partialAlpha;

            if (i == _flashSlot && _flashTimer > 0f)
                color = Color.Lerp(color, _flashColor, _flashTimer / _flashTime);

            if (popFlash > 0f)
                color = Color.Lerp(color, Color.white, popFlash);

            _slots[i].color = hideSlots ? Color.clear : _emptyColor;   // ตัวช่องเป็นพื้นหลัง
            _fills[i].fillAmount = amount;
            _fills[i].color = color;
            _fills[i].enabled = !hideSlots && amount > 0f;

            Vector3 scale = _slotScales[i];
            scale.y *= slotThickness;
            _slots[i].rectTransform.localScale = scale;
        }

        UpdateMerged(dt, merged, barScale, popFlash, shake);
        UpdateEcho(barScale, shake);
        UpdateReadyIcon(full, dt, shake);
        UpdateReadyText(full, shake);
        UpdateCallout();
    }

    // ---------- เต็ม: วิ่งรวม → เด้ง ----------

    // เด้งครั้งเดียวต่อการเต็มหนึ่งรอบ — เปิดเมนูแล้วปิด HUD กลับมาก็ไม่เด้งซ้ำ
    private void UpdatePopState(bool full, bool visible, bool skipPop, float dt)
    {
        if (full && !_popped && visible)
        {
            _popped = true;
            _shineTimer = 0f;

            // เข้าฉากมาเต็มอยู่แล้ว (โหลดเซฟ) ไม่ต้องเด้ง โชว์สภาพพร้อมยิงเลย
            if (skipPop || !_popOnReady)
            {
                _merge = 1f;
                _popTimer = -1f;
                _iconAlpha = 1f;
                _iconTimer = 999f;
            }
            else
            {
                _merge = 0f;
                _popTimer = 0f;
            }
        }

        if (!full)
        {
            _popped = false;
            _popTimer = -1f;
            _impactTimer = -1f;
            _echoTimer = -1f;
            _calloutTimer = -1f;
        }

        if (_impactTimer >= 0f)
        {
            _impactTimer += dt;
            if (_impactTimer >= Mathf.Max(_popDuration, Mathf.Max(_popFlashTime, _shakeDuration))) _impactTimer = -1f;
        }

        if (_echoTimer >= 0f)
        {
            _echoTimer += dt;
            if (_echoTimer >= _echoDuration) _echoTimer = -1f;
        }

        if (_calloutTimer >= 0f)
        {
            _calloutTimer += dt;
            if (_calloutTimer >= 0.3f + _calloutHold + _calloutFade) _calloutTimer = -1f;
        }

        // แสงวิ่งรวมช่องจนสุดปลายก่อน แล้วค่อยเด้ง
        if (_popTimer >= 0f)
        {
            _popTimer += dt;
            float mergeTime = _mergeWhenFull ? _mergeTime : 0f;
            _merge = mergeTime > 0f ? Mathf.Clamp01(_popTimer / mergeTime) : 1f;

            if (_popTimer >= mergeTime)
            {
                _popTimer = -1f;
                Impact();
            }
        }
    }

    private void Impact()
    {
        _impactTimer = 0f;
        _echoTimer = 0f;
        _calloutTimer = _calloutIcon != null ? 0f : -1f;
        _iconAlpha = 1f;
        _iconTimer = 0f;
        _shineTimer = 0f;
        PlayReadySound();

        if (_logPop)
            Debug.Log("[SkillPointBarUI] SP เต็ม — รวมแถบ + เด้ง", this);
    }

    // เด้งแบบเยลลี่: พองขึ้นเร็ว ยุบเลยนิดนึง แล้วเข้าที่ — คืนค่าประมาณ -0.25 ถึง 1
    private float PopBump()
    {
        if (_impactTimer < 0f || _impactTimer >= _popDuration) return 0f;

        float k = _impactTimer / Mathf.Max(0.01f, _popDuration);
        float decay = (1f - k) * (1f - k);
        return Mathf.Sin(k * Mathf.PI * 2.5f) * decay / 0.64f;
    }

    // สั่นแรงตอนรวมเสร็จแล้วค่อย ๆ เบาลง / ระหว่างรอยิงสั่นเบา ๆ ค้างไว้
    private Vector2 ShakeOffset()
    {
        float amp = 0f;

        if (_impactTimer >= 0f && _impactTimer < _shakeDuration)
        {
            float k = 1f - _impactTimer / _shakeDuration;
            amp = _shakeStrength * k * k;
        }

        if (_popped && _popTimer < 0f)
            amp = Mathf.Max(amp, _holdShake);

        if (amp <= 0f) return Vector2.zero;

        float t = Time.unscaledTime * _shakeFrequency;
        return new Vector2(Mathf.PerlinNoise(t, 0.37f) - 0.5f, Mathf.PerlinNoise(0.71f, t) - 0.5f) * (2f * amp);
    }

    // ---------- แถบรวม ----------

    private void UpdateMerged(float dt, bool merged, Vector2 barScale, float popFlash, Vector2 shake)
    {
        bool show = _mergeWhenFull && (merged || _merge > 0.001f);
        if (!show)
        {
            if (_merged != null && _merged.gameObject.activeSelf) _merged.gameObject.SetActive(false);
            return;
        }

        EnsureMerged();
        if (!_merged.gameObject.activeSelf) _merged.gameObject.SetActive(true);

        _merged.anchoredPosition = _spanCenter + shake;
        _merged.sizeDelta = _spanSize;
        _merged.localScale = new Vector3(barScale.x, barScale.y, 1f);

        // วิ่งจากซ้ายไปขวาตอนรวม / หดจากขวาไปซ้ายตอนยิงแล้ว
        float reveal = Smooth(_merge);
        _barRect.anchorMax = new Vector2(reveal, 1f);

        Color color = ApplyPulse(_readyColor);
        float whiten = merged && _merge < 1f ? 0.45f : popFlash;   // ตอนวิ่งรวมสว่างกว่าปกติ
        if (whiten > 0f) color = Color.Lerp(color, Color.white, whiten);
        if (!merged) color.a *= reveal;
        _bar.color = color;

        UpdateShine(dt, merged, reveal);
    }

    private void UpdateShine(float dt, bool merged, float reveal)
    {
        float width = _spanSize.x;
        float shineWidth = Mathf.Max(4f, width * _shineWidth);
        _shineRect.sizeDelta = new Vector2(shineWidth, _spanSize.y * 4f);

        float x = 0f;
        float alpha = 0f;

        if (merged && _merge < 1f)
        {
            // หัวแสงนำหน้าตอนวิ่งรวมช่อง
            x = reveal * width - shineWidth * 0.25f;
            alpha = 1f;
        }
        else if (merged && _shineInterval > 0f)
        {
            // วิ่งผ่านเป็นระยะ — รอบแรกเว้นไว้ก่อน ไม่ให้ซ้อนกับตอนวิ่งรวม
            float period = Mathf.Max(_shineInterval, _shineDuration + 0.05f);
            _shineTimer = (_shineTimer + dt) % period;
            float p = (_shineTimer - (period - _shineDuration)) / _shineDuration;
            if (p >= 0f)
            {
                x = Mathf.Lerp(-shineWidth, width + shineWidth, p);
                alpha = _shineAlpha;
            }
        }

        _shineRect.anchoredPosition = new Vector2(x, 0f);
        _shine.color = new Color(1f, 1f, 1f, alpha);
        _shine.enabled = alpha > 0.001f;
    }

    // ขอบเขตของช่องทั้งหมดในพิกัดของแถว — แถบรวมกับกรอบเด้งวางทับตรงนี้
    private void MeasureSpan()
    {
        var row = transform as RectTransform;
        if (row == null) return;

        Vector2 min = new(float.MaxValue, float.MaxValue);
        Vector2 max = new(float.MinValue, float.MinValue);
        bool any = false;

        foreach (Image slot in _slots)
        {
            if (slot == null) continue;

            slot.rectTransform.GetWorldCorners(_corners);
            for (int c = 0; c < 4; c++)
            {
                Vector2 p = row.InverseTransformPoint(_corners[c]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            any = true;
        }

        if (!any)
        {
            min = row.rect.min;
            max = row.rect.max;
        }

        _spanSize = max - min;
        _spanCenter = (min + max) * 0.5f - row.rect.min;
    }

    // แถบรวมกับแสงวิ่งอยู่ใน RectMask2D แสงจะได้ไม่ล้นออกนอกแถบ / Ignore Layout ไม่ให้ Layout Group นับเป็นช่อง
    private void EnsureMerged()
    {
        if (_merged != null) return;

        var go = new GameObject("SPMerged", typeof(RectTransform), typeof(LayoutElement), typeof(RectMask2D));
        go.layer = gameObject.layer;
        go.GetComponent<LayoutElement>().ignoreLayout = true;

        _merged = (RectTransform)go.transform;
        _merged.SetParent(transform, false);
        _merged.anchorMin = Vector2.zero;
        _merged.anchorMax = Vector2.zero;
        _merged.pivot = new Vector2(0.5f, 0.5f);
        _merged.SetAsLastSibling();
        if (_echo != null) _echo.SetAsLastSibling();

        Sprite sprite = _mergedSprite != null ? _mergedSprite : FirstSlotSprite();
        _bar = CreateImage("Bar", _merged, sprite);
        _bar.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        _barRect = _bar.rectTransform;
        _barRect.anchorMin = Vector2.zero;
        _barRect.anchorMax = Vector2.one;
        _barRect.pivot = new Vector2(0f, 0.5f);
        _barRect.offsetMin = Vector2.zero;
        _barRect.offsetMax = Vector2.zero;

        _shine = CreateImage("Shine", _merged, ShineSprite());
        _shineRect = _shine.rectTransform;
        _shineRect.anchorMin = new Vector2(0f, 0.5f);
        _shineRect.anchorMax = new Vector2(0f, 0.5f);
        _shineRect.pivot = new Vector2(0.5f, 0.5f);
        _shineRect.localRotation = Quaternion.Euler(0f, 0f, -_shineAngle);
        _shine.enabled = false;
    }

    private Image CreateImage(string objectName, Transform parent, Sprite sprite)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private Sprite FirstSlotSprite()
    {
        foreach (Image slot in _slots)
            if (slot != null && slot.sprite != null) return slot.sprite;
        return WhiteSprite();
    }

    // ---------- กรอบเด้ง ----------

    private void UpdateEcho(Vector2 barScale, Vector2 shake)
    {
        if (_echoTimer < 0f)
        {
            if (_echo != null && _echo.gameObject.activeSelf) _echo.gameObject.SetActive(false);
            return;
        }

        EnsureEcho();
        if (!_echo.gameObject.activeSelf) _echo.gameObject.SetActive(true);

        float e = Mathf.Clamp01(_echoTimer / Mathf.Max(0.01f, _echoDuration));
        float ease = 1f - (1f - e) * (1f - e);

        Vector2 spread = new(1f + _echoSpread.x * ease, 1f + _echoSpread.y * ease);
        _echo.anchoredPosition = _spanCenter + shake;
        _echo.sizeDelta = Vector2.Scale(_spanSize, barScale) + spread * 2f;

        Color color = _echoColor;
        color.a *= 1f - e;
        foreach (Image line in _echoLines)
            line.color = color;
    }

    // กรอบ 4 เส้นรอบแถบ — ไม่ใช้รูป เส้นคมทุกขนาด / Ignore Layout ไม่ให้ Layout Group นับเป็นช่อง
    private void EnsureEcho()
    {
        if (_echo != null) return;

        var go = new GameObject("ReadyEcho", typeof(RectTransform), typeof(LayoutElement));
        go.layer = gameObject.layer;
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        _echo = (RectTransform)go.transform;
        _echo.SetParent(transform, false);
        _echo.SetAsLastSibling();
        _echo.anchorMin = Vector2.zero;
        _echo.anchorMax = Vector2.zero;
        _echo.pivot = new Vector2(0.5f, 0.5f);

        float t = _echoThickness;
        _echoLines = new[]
        {
            CreateLine("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -t), Vector2.zero),
            CreateLine("Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, t)),
            CreateLine("Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(t, 0f)),
            CreateLine("Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-t, 0f), Vector2.zero),
        };
    }

    private Image CreateLine(string lineName, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Image image = CreateImage(lineName, _echo, null);
        RectTransform rt = image.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return image;
    }

    // ---------- ไอคอนปืน + ข้อความ ----------

    private void UpdateReadyIcon(bool full, float dt, Vector2 shake)
    {
        if (_readyIcon == null) return;
        SaveIconBase();

        if (!full)
            _iconAlpha = Mathf.MoveTowards(_iconAlpha, 0f, dt / Mathf.Max(0.01f, _iconFadeOut));

        if (_iconAlpha <= 0.001f) _iconTimer = -1f;
        else if (_iconTimer >= 0f) _iconTimer += dt;

        float scale = 1f;
        float appear = 1f;
        Vector2 offset = shake * 0.5f;
        Color color = _tintIcon ? ApplyPulse(_readyColor) : Color.white;

        if (_iconTimer >= 0f && _iconTimer < _iconDropTime)
        {
            // ตกลงมาเลยจุดนิดนึงแล้วเด้งกลับ + หดจากใหญ่ + วาบขาว
            float k = _iconTimer / _iconDropTime;
            offset.y += _iconDrop * (1f - EaseOutBack(k));
            scale = Mathf.Lerp(_iconPopScale, 1f, Smooth(k));
            appear = Mathf.Clamp01(k * 4f);
            color = Color.Lerp(color, Color.white, 1f - k);
        }
        else if (full && _pulseWhenReady)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) * 0.5f;
            scale = 1f + _iconPulseScale * pulse;
        }

        color.a *= _iconAlpha * appear;
        _readyIcon.color = color;
        _readyIcon.enabled = color.a > 0.001f;

        RectTransform rt = _readyIcon.rectTransform;
        rt.localScale = _iconBaseScale * scale;
        rt.anchoredPosition = _iconBasePos + offset;
    }

    private void UpdateReadyText(bool full, Vector2 shake)
    {
        if (_readyText == null) return;
        SaveTextBase();

        const float popTime = 0.25f;
        float t = _iconTimer - _textDelay;
        float alpha = _iconTimer >= 0f && t >= 0f ? _iconAlpha : 0f;
        float scale = 1f;
        Color color = _textBaseColor;

        if (t >= 0f && t < popTime)
        {
            // กระแทกลงมาจากตัวใหญ่ + วาบขาว
            float k = t / popTime;
            scale = Mathf.LerpUnclamped(_textPopScale, 1f, EaseOutBack(k));
            color = Color.Lerp(Color.white, color, k);
        }
        else if (full)
        {
            alpha *= Mathf.Lerp(_textBlinkMin, 1f, (Mathf.Sin(Time.unscaledTime * _pulseSpeed * 2f) + 1f) * 0.5f);
        }

        color.a = _textBaseColor.a * alpha;
        _readyText.color = color;
        _readyText.enabled = alpha > 0.001f;

        RectTransform rt = _readyText.rectTransform;
        rt.localScale = _textBaseScale * scale;
        rt.anchoredPosition = _textBasePos + shake * 0.5f;
    }

    private void SaveIconBase()
    {
        if (_iconSaved || _readyIcon == null) return;
        _iconBaseScale = _readyIcon.rectTransform.localScale;
        _iconBasePos = _readyIcon.rectTransform.anchoredPosition;
        _iconSaved = true;
    }

    private void SaveTextBase()
    {
        if (_textSaved || _readyText == null) return;
        _textBaseScale = _readyText.rectTransform.localScale;
        _textBasePos = _readyText.rectTransform.anchoredPosition;
        _textBaseColor = _readyText.color;
        _textSaved = true;
    }

    // ---------- กลางจอ ----------

    private void UpdateCallout()
    {
        if (_calloutIcon == null) return;

        RectTransform rt = _calloutIcon.rectTransform;
        if (!_calloutSaved)
        {
            _calloutBaseScale = rt.localScale;
            _calloutBasePos = rt.anchoredPosition;
            _calloutSaved = true;
        }

        if (_calloutTimer < 0f)
        {
            _calloutIcon.enabled = false;
            rt.anchoredPosition = _calloutBasePos;
            return;
        }

        float t = _calloutTimer;
        float scale;
        if (t < 0.3f)
        {
            float k = t / 0.3f;
            scale = k < 0.4f
                ? Mathf.Lerp(0.5f, _calloutPopScale, Smooth(k / 0.4f))
                : Mathf.Lerp(_calloutPopScale, 1f, Smooth((k - 0.4f) / 0.6f));
        }
        else scale = 1f;

        float fade = Mathf.Clamp01((t - 0.3f - _calloutHold) / Mathf.Max(0.01f, _calloutFade));

        Color color = Color.Lerp(_readyColor, Color.white, Mathf.Clamp01(1f - t / 0.25f));
        color.a = 1f - Smooth(fade);

        _calloutIcon.enabled = true;
        _calloutIcon.color = color;
        rt.localScale = _calloutBaseScale * scale;
        rt.anchoredPosition = _calloutBasePos + Vector2.up * (_calloutRise * Smooth(fade));
    }

    // ---------- อื่น ๆ ----------

    private bool IsGateOpen()
    {
        if (!_gateSearched)
        {
            _gateSearched = true;
            // ตัวที่ใกล้สุด = ของ HUDFlagGate ถ้ามี / ไม่มีก็เป็นของ HUD เอง (ซ่อนตอน cutscene ก็ไม่เด้งเหมือนกัน)
            _gateGroup = GetComponentInParent<CanvasGroup>();
        }

        return _gateGroup == null || _gateGroup.alpha > 0.5f;
    }

    private void PlayReadySound()
    {
        if (_readySound == null || SoundManager.Instance == null) return;
        SoundManager.Instance.PlayEventSFX(_readySound);
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // ไปเลยปลายทางนิดนึงแล้วเด้งกลับ
    private static float EaseOutBack(float t)
    {
        const float c1 = 2.2f;
        const float c3 = c1 + 1f;
        t = Mathf.Clamp01(t) - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    // สร้างแถบเติมเป็นลูกของแต่ละช่อง ใช้รูปเดียวกับช่อง
    private void EnsureFills()
    {
        if (_fills != null && _fills.Length == _slots.Length) return;

        _fills = new Image[_slots.Length];
        _slotScales = new Vector3[_slots.Length];
        for (int i = 0; i < _slots.Length; i++)
        {
            Image slot = _slots[i];
            if (slot == null) continue;

            _slotScales[i] = slot.rectTransform.localScale;

            Image fill = CreateImage("Fill", slot.transform, slot.sprite != null ? slot.sprite : WhiteSprite());   // Filled ต้องมีรูป ไม่งั้นไม่ตัด
            RectTransform rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            _fills[i] = fill;
        }
    }

    private static Sprite _white;
    private static Sprite _shineSprite;

    private static Sprite WhiteSprite()
    {
        if (_white == null)
            _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return _white;
    }

    // แสงขาวนุ่ม ๆ จางสองข้าง — สร้างเองไม่ต้องมีไฟล์รูป
    private static Sprite ShineSprite()
    {
        if (_shineSprite != null) return _shineSprite;

        const int width = 64;
        var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false)
        {
            name = "SPShine",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        for (int x = 0; x < width; x++)
        {
            float t = x / (width - 1f) * 2f - 1f;
            float a = 1f - t * t;
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply();

        _shineSprite = Sprite.Create(tex, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f));
        return _shineSprite;
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