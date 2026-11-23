using System.Collections.Generic;
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
/// ช่องไล่ขนาดจากเล็ก (ซ้าย) ไปใหญ่ (ขวา) — พลังกำลังโต อยู่ในกรอบหลอดเกจ (พื้นเข้ม + เส้นขอบ) เห็นชัดทั้งฉากมืดและสว่าง
/// ได้ SP = แถบสั่น ได้เยอะทีเดียวหรือได้ถี่ ๆ ยิ่งสั่นแรง และยิ่งใกล้เต็มยิ่งแรง
/// พอเต็ม: แสงวิ่งจากซ้ายไปขวา ช่องทั้งหมดรวมเป็นแถบเดียว (ทรงเดียวกับช่อง) → เด้ง + สั่น
/// แล้วไอคอนปืนตกลงมาใต้แถบ ระหว่างรอยิงมีแสงวิ่งผ่านแถบเป็นระยะ
/// ยิงไปแล้ว (SP ลด) แถบรวมหดกลับ เหลือเป็นช่องเหมือนเดิม
///
/// โค้ดสร้างแถบเติม กรอบหลอด แถบรวม และกรอบเด้งให้เอง ไม่ต้องสร้างใน Hierarchy
/// แปะบน GameObject ที่จะเป็นแถวของช่อง (ควรมี Horizontal Layout Group)
/// </summary>
public class SkillPointBarUI : MonoBehaviour
{
    public enum SlotAlign { Bottom, Center, Top }

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

    [Header("ขนาดช่อง เล็ก → ใหญ่")]
    [Tooltip("ช่องแรก (ซ้ายสุด) หนากี่เท่าของช่องสุดท้าย — 1 = เท่ากันหมด")]
    [SerializeField, Range(0.1f, 1f)] private float _firstSlotSize = 0.45f;

    [Tooltip("โค้งของการไล่ขนาด — 1 = ไล่เท่า ๆ กัน / มากกว่า 1 = ช่องท้าย ๆ ใหญ่ขึ้นเร็ว")]
    [SerializeField, Range(0.3f, 3f)] private float _sizeCurve = 1.4f;

    [Tooltip("ช่องชิดขอบไหน — Bottom = ขอบล่างตรง / Center = เรียวทั้งบนล่าง / Top = ขอบบนตรง")]
    [SerializeField] private SlotAlign _slotAlign = SlotAlign.Center;

    [Tooltip("หลอดหนาขึ้นกี่เท่าจากขนาดช่องที่ตั้งไว้ — ไม่ต้องไปแก้ขนาดช่องทีละอัน")]
    [SerializeField, Range(0.5f, 5f)] private float _thickness = 2.5f;

    [Header("กรอบหลอดเกจ")]
    [Tooltip("มีพื้นหลังเข้ม + เส้นขอบรอบหลอด ทรงตามช่อง (เล็กไปใหญ่ก็ตาม)")]
    [SerializeField] private bool _showFrame = true;

    [Tooltip("พื้นหลังหลอด — สีเข้มเกือบทึบ ให้เห็นชัดในฉากสว่าง\n(HDRP ผสมสีแบบ Linear ค่า A ต่ำ ๆ จะดูเป็นสีเทาจาง)")]
    [SerializeField] private Color _gaugeBackColor = new(0.045f, 0.035f, 0.03f, 0.95f);

    [Tooltip("พื้นหลังเลยขอบช่องออกไปกี่พิกเซล")]
    [SerializeField, Min(0f)] private float _framePadding = 3f;

    [Tooltip("เส้นขอบรอบนอก — สีสว่างทึบ ให้เห็นชัดในฉากมืด")]
    [SerializeField] private Color _gaugeLineColor = new(0.8f, 0.68f, 0.48f, 1f);

    [Tooltip("เส้นขอบหนากี่พิกเซล — 0 = ไม่มีเส้นขอบ")]
    [SerializeField, Min(0f)] private float _borderWidth = 1.5f;

    [Header("Colors")]
    [Tooltip("สีช่องที่ยังว่าง — ใช้สีทึบ (A = 255)\nHDRP ผสมสีแบบ Linear สีโปร่งแสงจะดูจางกว่าที่ตั้งไว้มาก โดยเฉพาะในฉากสว่าง")]
    [SerializeField] private Color _emptySlotColor = new(0.29f, 0.27f, 0.25f, 1f);
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

    [Header("สั่นตอนได้ SP")]
    [Tooltip("แรงสั่นต่อ 'ความร้อน' 1 หน่วย (พิกเซล) — ได้ SP แต่ละครั้งเพิ่มความร้อน แล้วค่อย ๆ เย็นลง")]
    [SerializeField, Min(0f)] private float _gainShakeStrength = 2.5f;

    [Tooltip("สั่นแรงสุดได้แค่นี้ (พิกเซล)")]
    [SerializeField, Min(0f)] private float _gainShakeMax = 9f;

    [Tooltip("ความถี่: ได้ SP หนึ่งครั้งเพิ่มความร้อนเท่านี้ — ต่อยรัว ๆ ความร้อนสะสม ยิ่งสั่นแรง")]
    [SerializeField, Min(0f)] private float _heatPerHit = 0.6f;

    [Tooltip("จำนวน: ได้ SP 1 ช่องเต็ม เพิ่มความร้อนเท่านี้ — ได้เยอะทีเดียว ยิ่งสั่นแรง")]
    [SerializeField, Min(0f)] private float _heatPerSlot = 1f;

    [Tooltip("ความร้อนสะสมได้สูงสุดเท่านี้")]
    [SerializeField, Min(0.1f)] private float _maxHeat = 4f;

    [Tooltip("ความร้อนเย็นลงเร็วแค่ไหน (วินาที) — มาก = ต่อยห่าง ๆ ก็ยังสะสมได้")]
    [SerializeField, Min(0.05f)] private float _heatDecay = 0.35f;

    [Tooltip("ตอนแถบเกือบเต็ม สั่นแรงกว่าตอนแถบว่างกี่เท่า")]
    [SerializeField, Min(1f)] private float _shakeAtFull = 1.8f;

    [Tooltip("ตอนร้อนสุด แถบเอียงไปมาได้กี่องศา — 0 = ไม่เอียง")]
    [SerializeField, Range(0f, 8f)] private float _gainTilt = 1.5f;

    [Tooltip("ช่องที่เพิ่งได้ SP พองหนาขึ้นกี่ส่วน (0.6 = 60%)")]
    [SerializeField, Min(0f)] private float _gainSlotPunch = 0.6f;
    [SerializeField, Min(0.01f)] private float _gainPunchTime = 0.2f;

    [Header("Ready Pulse")]
    [Tooltip("ให้แถบเต้นเบา ๆ ตอนเต็ม เพื่อบอกว่าพร้อมยิงแล้ว")]
    [SerializeField] private bool _pulseWhenReady = true;
    [SerializeField] private float _pulseSpeed = 3f;
    [SerializeField] private float _pulseAmount = 0.25f;

    [Header("รวมเป็นแถบเดียวตอนเต็ม")]
    [Tooltip("ติ๊ก = SP เต็มแล้วช่องทั้งหมดรวมเป็นแถบเดียว / ไม่ติ๊ก = เป็นช่องเหมือนเดิม")]
    [SerializeField] private bool _mergeWhenFull = true;

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

    [Header("สั่นตอนเต็ม")]
    [Tooltip("ตอนรวมเสร็จ สั่นแรงแค่ไหน (พิกเซล)")]
    [SerializeField, Min(0f)] private float _shakeStrength = 6f;
    [SerializeField, Min(0.01f)] private float _shakeDuration = 0.4f;

    [Tooltip("สั่นเบา ๆ ค้างไว้ตลอดที่เต็ม (พิกเซล) — 0 = ไม่สั่น")]
    [SerializeField, Min(0f)] private float _holdShake = 0.6f;

    [Tooltip("สั่นถี่แค่ไหน — ตอนได้ SP รัว ๆ จะถี่ขึ้นเองอีก")]
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

    private float _heat;
    private int _punchSlot = -1;
    private float _punchTimer;
    private float _shakePhase;

    private bool _popped;
    private float _popTimer = -1f;      // ช่วงแสงวิ่งรวมช่อง
    private float _impactTimer = -1f;   // นับจากตอนรวมเสร็จ — เด้ง / วาบ / สั่น
    private float _echoTimer = -1f;
    private float _merge;               // 0 = เป็นช่อง / 1 = รวมเป็นแถบเดียวแล้ว
    private float _shineTimer;

    private Vector3[] _slotScales;
    private Vector2[] _slotMin;         // ขอบเขตของแต่ละช่องในพิกัดของแถว
    private Vector2[] _slotMax;
    private Vector2 _spanMid;           // กลางของช่องทั้งหมด ในพิกัดของแถว
    private Vector2 _spanCenter;        // ตัวเดียวกัน แต่นับจากมุมล่างซ้ายของแถว (ใช้วางลูก)
    private Vector2 _spanSize;
    private readonly List<Vector3> _profile = new();

    // ขนาดช่องแบบไม่นับตอนพอง/เด้ง — กรอบใช้อันนี้ จะได้ไม่บวมตามตอนได้ SP
    private float[] _slotTransient;
    private Vector2[] _baseMin;
    private Vector2[] _baseMax;
    private Vector2 _baseMid;
    private Vector2 _baseCenter;
    private Vector2 _baseSize;
    private readonly List<Vector3> _frameProfile = new();

    private RectTransform _trackRect;
    private SPWedgeBar _track;

    private RectTransform _merged;
    private SPWedgeBar _bar;

    private RectTransform _echo;
    private Image[] _echoLines;

    // สั่นทั้งแถว — จำจุดเดิมไว้ ถ้า Layout ย้ายแถวไประหว่างนั้นจะนับจุดใหม่เอง
    private bool _shakeBaseSaved;
    private bool _rowShaken;
    private Vector3 _shakeBasePos;
    private Quaternion _shakeBaseRot = Quaternion.identity;
    private Vector3 _shakeSetPos;
    private Quaternion _shakeSetRot = Quaternion.identity;

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
    private static readonly System.Comparison<Vector3> _byX = (a, b) => a.x.CompareTo(b.x);

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
        if (_rowShaken)
        {
            transform.localPosition = _shakeBasePos;
            transform.localRotation = _shakeBaseRot;
            _rowShaken = false;
        }

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

        if (!firstRead && current > _lastValue + 0.0001f)
            OnGain((current - _lastValue) / perSlot, target);
        _lastValue = current;

        // ลดลง (ใช้ SP) = หายทันที / เพิ่มขึ้น = วิ่งตาม
        _shown = _shown < 0f || target < _shown ? target : Mathf.MoveTowards(_shown, target, _fillSpeed * dt);
        _flashTimer = Mathf.Max(0f, _flashTimer - dt);
        _punchTimer = Mathf.Max(0f, _punchTimer - dt);
        _heat *= Mathf.Exp(-dt / _heatDecay);
        if (_heat < 0.01f) _heat = 0f;

        // นับว่าเต็มตอนแถบวิ่งไปถึงจริง ไม่ใช่ตอนค่าเต็ม — เด้งพร้อมภาพ
        // แถบถูกซ่อนอยู่ (ยังไม่ได้ปืน / HUD ซ่อน) = ไม่เด้ง ไม่มีเสียง พอโผล่กลับมาแล้วค่อยเด้ง
        bool full = isReady && _shown >= target - 0.001f;
        UpdatePopState(full, IsGateOpen(), firstRead || Time.timeSinceLevelLoad < 1f, dt);
        full &= _popped;

        bool merged = _mergeWhenFull && _popped;
        if (!merged) _merge = Mathf.MoveTowards(_merge, 0f, dt / _breakTime);

        float bump = PopBump();
        float popFlash = _impactTimer >= 0f && _impactTimer < _popFlashTime ? 1f - _impactTimer / _popFlashTime : 0f;

        // แถบรวมเด้งทั้งหนาและยาว / ถ้าไม่รวม ช่องพองหนาแบบเดิม
        Vector2 barScale = new(1f + (_popStretch - 1f) * bump, 1f + (_popThickness - 1f) * bump);
        float popThickness = _mergeWhenFull ? 1f : barScale.y;

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

            _slots[i].color = hideSlots ? Color.clear : _emptySlotColor;   // ตัวช่องเป็นพื้นหลัง
            _fills[i].fillAmount = amount;
            _fills[i].color = color;
            _fills[i].enabled = !hideSlots && amount > 0f;

            float transient = popThickness * SlotPunch(i);
            _slotTransient[i] = transient;

            Vector3 scale = _slotScales[i];
            scale.y *= SlotSize(i) * _thickness * transient;
            _slots[i].rectTransform.localScale = scale;
        }

        MeasureSlots();
        UpdateFrame();
        Vector2 shake = UpdateShake(dt);
        UpdateMerged(dt, merged, barScale, popFlash);
        UpdateEcho(barScale);
        UpdateReadyIcon(full, dt, shake);
        UpdateReadyText(full, shake);
        UpdateCallout();
    }

    // ---------- ได้ SP ----------

    // ได้เยอะทีเดียว (จำนวน) หรือได้ถี่ ๆ (ความถี่) = ความร้อนขึ้นมาก = สั่นแรง
    private void OnGain(float slotsGained, float target)
    {
        int slot = Mathf.Clamp(Mathf.FloorToInt(target - 0.0001f), 0, _slots.Length - 1);

        if (_flashOnGain)
        {
            _flashSlot = slot;
            _flashTimer = _flashTime;
        }

        _punchSlot = slot;
        _punchTimer = _gainPunchTime;
        _heat = Mathf.Min(_maxHeat, _heat + _heatPerHit + Mathf.Max(0f, slotsGained) * _heatPerSlot);
    }

    // ช่องซ้ายเล็ก ไล่ไปขวาใหญ่
    private float SlotSize(int i)
    {
        if (_slots.Length <= 1) return 1f;
        float t = i / (_slots.Length - 1f);
        return Mathf.Lerp(_firstSlotSize, 1f, Mathf.Pow(t, _sizeCurve));
    }

    private float SlotPunch(int i)
    {
        if (i != _punchSlot || _punchTimer <= 0f) return 1f;
        float k = _punchTimer / _gainPunchTime;
        return 1f + _gainSlotPunch * k * k;
    }

    // สั่นทั้งแถว: ได้ SP (ตามความร้อน) + ตอนรวมเสร็จ + สั่นค้างตอนรอยิง
    private Vector2 UpdateShake(float dt)
    {
        float fill = Mathf.Clamp01(_shown / _slots.Length);
        float heat01 = Mathf.Clamp01(_heat / _maxHeat);

        float amp = Mathf.Min(_gainShakeMax, _gainShakeStrength * _heat * Mathf.Lerp(1f, _shakeAtFull, fill));

        float fullAmp = 0f;
        if (_impactTimer >= 0f && _impactTimer < _shakeDuration)
        {
            float k = 1f - _impactTimer / _shakeDuration;
            fullAmp = _shakeStrength * k * k;
        }
        if (_popped && _popTimer < 0f)
            fullAmp = Mathf.Max(fullAmp, _holdShake);

        amp += fullAmp;

        // ร้อนมาก = สั่นถี่ขึ้นด้วย
        _shakePhase += dt * _shakeFrequency * (1f + 0.6f * heat01);
        if (_shakePhase > 10000f) _shakePhase -= 10000f;

        Vector2 offset = Vector2.zero;
        float tilt = 0f;
        if (amp > 0.001f)
        {
            float p = _shakePhase;
            offset = new Vector2(Mathf.PerlinNoise(p, 0.37f) - 0.5f, Mathf.PerlinNoise(0.71f, p) - 0.5f) * (2f * amp);
            tilt = (Mathf.PerlinNoise(p * 0.8f, 5.3f) - 0.5f) * 2f * _gainTilt * heat01;
        }

        ApplyRowShake(offset, tilt);
        return offset;
    }

    private void ApplyRowShake(Vector2 offset, float tilt)
    {
        bool shaking = offset.sqrMagnitude > 0.000001f || Mathf.Abs(tilt) > 0.0001f;
        if (!shaking && !_rowShaken) return;   // นิ่งอยู่แล้ว ไม่ต้องเขียนทับทุกเฟรม

        Transform t = transform;

        // Layout ย้ายแถวไประหว่างสั่น → ใช้ตำแหน่งใหม่เป็นจุดเดิม
        if (!_shakeBaseSaved || t.localPosition != _shakeSetPos) _shakeBasePos = t.localPosition;
        if (!_shakeBaseSaved || t.localRotation != _shakeSetRot) _shakeBaseRot = t.localRotation;
        _shakeBaseSaved = true;

        _shakeSetPos = _shakeBasePos + new Vector3(offset.x, offset.y, 0f);
        _shakeSetRot = _shakeBaseRot * Quaternion.Euler(0f, 0f, tilt);
        t.localPosition = _shakeSetPos;
        t.localRotation = _shakeSetRot;
        _rowShaken = shaking;
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

    // ---------- แถบรวม ----------

    private void UpdateMerged(float dt, bool merged, Vector2 barScale, float popFlash)
    {
        bool show = _mergeWhenFull && (merged || _merge > 0.001f);
        if (!show)
        {
            if (_merged != null && _merged.gameObject.activeSelf) _merged.gameObject.SetActive(false);
            return;
        }

        EnsureMerged();
        if (!_merged.gameObject.activeSelf) _merged.gameObject.SetActive(true);

        _merged.anchoredPosition = _spanCenter;
        _merged.sizeDelta = _spanSize;
        _merged.localScale = new Vector3(barScale.x, barScale.y, 1f);

        // วิ่งจากซ้ายไปขวาตอนรวม / หดจากขวาไปซ้ายตอนยิงแล้ว
        float reveal = Smooth(_merge);

        Color color = ApplyPulse(_readyColor);
        float whiten = merged && _merge < 1f ? 0.45f : popFlash;   // ตอนวิ่งรวมสว่างกว่าปกติ
        if (whiten > 0f) color = Color.Lerp(color, Color.white, whiten);
        if (!merged) color.a *= reveal;
        _bar.color = color;

        float shinePos = -1f;
        float shineAlpha = 0f;

        if (merged && _merge < 1f)
        {
            // หัวแสงนำหน้าตอนวิ่งรวมช่อง
            shinePos = reveal - _shineWidth * 0.25f;
            shineAlpha = 1f;
        }
        else if (merged && _shineInterval > 0f)
        {
            // วิ่งผ่านเป็นระยะ — รอบแรกเว้นไว้ก่อน ไม่ให้ซ้อนกับตอนวิ่งรวม
            float period = Mathf.Max(_shineInterval, _shineDuration + 0.05f);
            _shineTimer = (_shineTimer + dt) % period;
            float p = (_shineTimer - (period - _shineDuration)) / _shineDuration;
            if (p >= 0f)
            {
                shinePos = Mathf.Lerp(-_shineWidth, 1f + _shineWidth, p);
                shineAlpha = _shineAlpha;
            }
        }

        float slant = Mathf.Tan(_shineAngle * Mathf.Deg2Rad) * _spanSize.y * 0.5f / Mathf.Max(1f, _spanSize.x);

        BuildProfile(_profile, _slotMin, _slotMax, _spanMid, _spanSize.x * 0.5f, 0f);
        _bar.SetState(_profile, reveal, shinePos, _shineWidth, shineAlpha, slant);
    }

    // ขอบเขตของแต่ละช่องในพิกัดของแถว — แถบรวมกับกรอบเด้งใช้ตรงนี้
    private void MeasureSlots()
    {
        var row = transform as RectTransform;
        if (row == null) return;

        if (_slotMin == null || _slotMin.Length != _slots.Length)
        {
            _slotMin = new Vector2[_slots.Length];
            _slotMax = new Vector2[_slots.Length];
            _baseMin = new Vector2[_slots.Length];
            _baseMax = new Vector2[_slots.Length];
        }

        Vector2 min = new(float.MaxValue, float.MaxValue);
        Vector2 max = new(float.MinValue, float.MinValue);
        Vector2 baseMin = min;
        Vector2 baseMax = max;
        bool any = false;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            Vector2 slotMin = new(float.MaxValue, float.MaxValue);
            Vector2 slotMax = new(float.MinValue, float.MinValue);
            _slots[i].rectTransform.GetWorldCorners(_corners);
            for (int c = 0; c < 4; c++)
            {
                Vector2 p = row.InverseTransformPoint(_corners[c]);
                slotMin = Vector2.Min(slotMin, p);
                slotMax = Vector2.Max(slotMax, p);
            }

            _slotMin[i] = slotMin;
            _slotMax[i] = slotMax;
            min = Vector2.Min(min, slotMin);
            max = Vector2.Max(max, slotMax);

            // ถอดส่วนที่พองชั่วคราวออก — หดกลับเข้าหาขอบที่เลือกชิดไว้
            float transient = Mathf.Max(0.01f, _slotTransient[i]);
            float height = (slotMax.y - slotMin.y) / transient;
            Vector2 bMin = slotMin;
            Vector2 bMax = slotMax;
            if (_slotAlign == SlotAlign.Bottom) bMax.y = slotMin.y + height;
            else if (_slotAlign == SlotAlign.Top) bMin.y = slotMax.y - height;
            else
            {
                float mid = (slotMin.y + slotMax.y) * 0.5f;
                bMin.y = mid - height * 0.5f;
                bMax.y = mid + height * 0.5f;
            }

            _baseMin[i] = bMin;
            _baseMax[i] = bMax;
            baseMin = Vector2.Min(baseMin, bMin);
            baseMax = Vector2.Max(baseMax, bMax);
            any = true;
        }

        if (!any)
        {
            min = baseMin = row.rect.min;
            max = baseMax = row.rect.max;
        }

        _spanSize = max - min;
        _spanMid = (min + max) * 0.5f;
        _spanCenter = _spanMid - row.rect.min;

        _baseSize = baseMax - baseMin;
        _baseMid = (baseMin + baseMax) * 0.5f;
        _baseCenter = _baseMid - row.rect.min;
    }

    // ทรงตามช่อง = ลากผ่านกลางทุกช่อง ขอบบนล่างตามช่องจริง (เล็กไปใหญ่ก็ตามได้) / expand = ขยายออกรอบด้าน
    private void BuildProfile(List<Vector3> output, Vector2[] mins, Vector2[] maxs, Vector2 mid, float halfWidth, float expand)
    {
        output.Clear();

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            Vector2 min = mins[i] - mid;
            Vector2 max = maxs[i] - mid;
            output.Add(new Vector3((min.x + max.x) * 0.5f, min.y - expand, max.y + expand));
        }

        if (output.Count == 0) return;

        output.Sort(_byX);

        // ต่อปลายซ้ายขวาให้ถึงขอบช่องแรก / ช่องสุดท้าย
        Vector3 head = output[0];
        Vector3 tail = output[^1];
        float half = halfWidth + expand;
        output.Insert(0, new Vector3(-half, head.y, head.z));
        output.Add(new Vector3(half, tail.y, tail.z));
    }

    // ---------- กรอบหลอด ----------

    // พื้นหลังเข้ม (เห็นในฉากสว่าง) + เส้นขอบสว่าง (เห็นในฉากมืด) วางไว้หลังช่อง
    // ขอบกับพื้นอยู่ใน mesh เดียวกัน — ขอบอยู่ข้างหลังพื้นเสมอ
    private void UpdateFrame()
    {
        if (!_showFrame)
        {
            if (_trackRect != null && _trackRect.gameObject.activeSelf) _trackRect.gameObject.SetActive(false);
            return;
        }

        EnsureFrame();
        if (!_trackRect.gameObject.activeSelf) _trackRect.gameObject.SetActive(true);

        PlaceFrame(_trackRect, _framePadding + _borderWidth);
        BuildProfile(_frameProfile, _baseMin, _baseMax, _baseMid, _baseSize.x * 0.5f, _framePadding);
        _track.color = _gaugeBackColor;
        _track.SetOutline(_borderWidth, _gaugeLineColor);
        _track.SetState(_frameProfile, 1f, -1f, 0.1f, 0f, 0f);
    }

    private void PlaceFrame(RectTransform rt, float expand)
    {
        rt.anchoredPosition = _baseCenter;
        rt.sizeDelta = _baseSize + Vector2.one * (expand * 2f);
    }

    private void EnsureFrame()
    {
        if (_trackRect != null) return;

        // ลูกตัวแรก = วาดก่อน อยู่หลังช่องทั้งหมด
        _track = CreateWedge("SPGauge", out _trackRect);
        _trackRect.SetAsFirstSibling();
    }

    private SPWedgeBar CreateWedge(string objectName, out RectTransform rect)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(LayoutElement), typeof(SPWedgeBar));
        go.layer = gameObject.layer;
        go.GetComponent<LayoutElement>().ignoreLayout = true;   // ไม่ให้ Layout Group นับเป็นช่อง

        rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        var wedge = go.GetComponent<SPWedgeBar>();
        wedge.raycastTarget = false;
        return wedge;
    }

    private void EnsureMerged()
    {
        if (_merged != null) return;

        _bar = CreateWedge("SPMerged", out _merged);
        _merged.SetAsLastSibling();
        if (_echo != null) _echo.SetAsLastSibling();
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

    // ---------- กรอบเด้ง ----------

    private void UpdateEcho(Vector2 barScale)
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
        _echo.anchoredPosition = _spanCenter;
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

    // สร้างแถบเติมเป็นลูกของแต่ละช่อง ใช้รูปเดียวกับช่อง + ตั้งจุดหมุนตามที่เลือกชิดขอบ
    private void EnsureFills()
    {
        if (_fills != null && _fills.Length == _slots.Length) return;

        float pivotY = _slotAlign == SlotAlign.Bottom ? 0f : _slotAlign == SlotAlign.Top ? 1f : 0.5f;

        _fills = new Image[_slots.Length];
        _slotScales = new Vector3[_slots.Length];
        _slotTransient = new float[_slots.Length];
        for (int i = 0; i < _slots.Length; i++)
        {
            Image slot = _slots[i];
            if (slot == null) continue;

            _slotScales[i] = slot.rectTransform.localScale;
            SetPivotYKeepPosition(slot.rectTransform, pivotY);

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

    // ย่อช่องแล้วให้หดเข้าหาขอบที่เลือก — ย้ายจุดหมุนโดยที่ช่องไม่ขยับ
    private static void SetPivotYKeepPosition(RectTransform rt, float pivotY)
    {
        Vector2 pivot = rt.pivot;
        if (Mathf.Approximately(pivot.y, pivotY)) return;

        float dy = (pivotY - pivot.y) * rt.rect.height * rt.localScale.y;
        rt.pivot = new Vector2(pivot.x, pivotY);
        rt.localPosition += rt.localRotation * new Vector3(0f, dy, 0f);
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

    /// <summary>ลองสั่นเหมือนได้ SP 1 ช่อง — คลิกขวาที่ component ตอน Play กดรัว ๆ ดูความแรงตอนต่อยถี่ได้</summary>
    [ContextMenu("ทดสอบ: สั่นเหมือนได้ SP 1 ช่อง")]
    private void DebugGain()
    {
        if (_slots == null || _slots.Length == 0) return;
        OnGain(1f, Mathf.Max(1f, _shown));
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