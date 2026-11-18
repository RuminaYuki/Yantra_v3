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

    [Header("Ready Pop (แบบเดียวกับ Guard)")]
    [Tooltip("ตอนเต็ม แถบพองหนา สว่างวาบ แล้วมีกรอบกระจายออก")]
    [SerializeField] private bool _popOnReady = true;

    [Tooltip("ช่องพองหนาขึ้นกี่เท่าตอนเด้ง")]
    [SerializeField] private float _popThickness = 2.4f;
    [SerializeField] private float _popDuration = 0.45f;
    [SerializeField] private float _popFlashTime = 0.25f;

    [Tooltip("กรอบรอบแถบที่กระจายออกแล้วจางหาย")]
    [SerializeField] private float _echoDuration = 0.8f;

    [Tooltip("กระจายออกไปกี่พิกเซล — X = ซ้ายขวา, Y = บนล่าง (Y น้อยไว้ ไม่ให้ชนหลอดเลือด)")]
    [SerializeField] private Vector2 _echoSpread = new(18f, 6f);
    [SerializeField] private float _echoThickness = 2.5f;
    [SerializeField] private Color _echoColor = new(1f, 0.93f, 0.67f, 0.85f);

    [Header("Ready Icon (ไม่ใส่ก็ได้)")]
    [Tooltip("ไอคอนที่โผล่ข้างแถบตอน SP เต็ม เช่น UltIcon_B_FireShot\nห้ามเป็นลูกของแถวนี้ ไม่งั้นจะถูกนับเป็นช่อง")]
    [SerializeField] private Image _readyIcon;

    [Tooltip("ตอนโผล่ ไอคอนขยายใหญ่สุดกี่เท่า")]
    [SerializeField] private float _iconPopScale = 1.5f;

    [Tooltip("ยิงไปแล้ว ไอคอนจางหายในกี่วินาที")]
    [SerializeField] private float _iconFadeOut = 0.3f;

    [Tooltip("ตอนพร้อมยิง ไอคอนเต้นใหญ่ขึ้นกี่ส่วน (0.04 = 4%)")]
    [SerializeField] private float _iconPulseScale = 0.04f;

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
    [Tooltip("เสียงตอน SP เต็ม — ผู้เล่นไม่ได้มองมุมจอ เสียงช่วยให้รู้ตัวได้ดีที่สุด")]
    [SerializeField] private SoundID _readySound;

    [Header("Debug")]
    [Tooltip("ขึ้นข้อความใน Console ทุกครั้งที่เด้ง — ไว้เช็คว่าทำงานจริงไหม")]
    [SerializeField] private bool _logPop = true;

    private float _retryTimer;
    private Image[] _fills;
    private float _shown = -1f;     // หน่วยเป็น "ช่อง" เช่น 3.4 = เต็ม 3 ช่อง ช่องที่ 4 ได้ 40%
    private float _lastValue = -1f;
    private float _flashTimer;
    private int _flashSlot = -1;

    private bool _popped;
    private float _popTimer = -1f;
    private float _echoTimer = -1f;
    private float _iconAlpha;

    private Vector3[] _slotScales;
    private RectTransform _echo;
    private Image[] _echoLines;
    private Vector3 _iconBaseScale = Vector3.one;
    private bool _iconScaleSaved;

    private float _calloutTimer = -1f;
    private Vector3 _calloutBaseScale = Vector3.one;
    private Vector2 _calloutBasePos;
    private bool _calloutSaved;

    private CanvasGroup _gateGroup;
    private bool _gateSearched;

    private void Update()
    {
        if (!TryGetSource()) return;
        if (_slots == null || _slots.Length == 0) return;
        EnsureFills();

        float dt = Time.unscaledDeltaTime;
        float current = _skillPoints.CurrentSkillPoints;

        // อ่าน max จากระบบโดยตรง ไม่ hardcode
        // ถ้าทีมปรับ balance เปลี่ยนค่า max แถบจะตามเองทันที
        float max = _skillPoints.MaxSkillPoints;
        if (max <= 0f) max = _fallbackMaxPoints;

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

        float popFlash = _popTimer >= 0f && _popTimer < _popFlashTime ? 1f - _popTimer / _popFlashTime : 0f;
        float thickness = Mathf.Lerp(1f, _popThickness, PopBump());

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

            _slots[i].color = _emptyColor;   // ตัวช่องเป็นพื้นหลัง
            _fills[i].fillAmount = amount;
            _fills[i].color = color;
            _fills[i].enabled = amount > 0f;

            Vector3 scale = _slotScales[i];
            scale.y *= thickness;
            _slots[i].rectTransform.localScale = scale;
        }

        UpdateEcho();
        UpdateReadyIcon(full, dt);
        UpdateCallout();
    }

    // ---------- เด้งตอนเต็ม ----------

    // เด้งครั้งเดียวต่อการเต็มหนึ่งรอบ — เปิดเมนูแล้วปิด HUD กลับมาก็ไม่เด้งซ้ำ
    private void UpdatePopState(bool full, bool visible, bool skipPop, float dt)
    {
        if (full && !_popped && visible)
        {
            _popped = true;

            // เข้าฉากมาเต็มอยู่แล้ว (โหลดเซฟ) ไม่ต้องเด้ง แค่โชว์ไอคอนเลย
            if (skipPop || !_popOnReady)
                _iconAlpha = 1f;
            else
            {
                _popTimer = 0f;
                _echoTimer = 0f;
                _calloutTimer = _calloutIcon != null ? 0f : -1f;
                _iconAlpha = 1f;
                PlayReadySound();

                if (_logPop)
                    Debug.Log("[SkillPointBarUI] SP เต็ม — เด้ง", this);
            }
        }

        if (!full)
        {
            _popped = false;
            _popTimer = -1f;
            _echoTimer = -1f;
            _calloutTimer = -1f;
        }

        if (_popTimer >= 0f)
        {
            _popTimer += dt;
            if (_popTimer >= Mathf.Max(_popDuration, _popFlashTime)) _popTimer = -1f;
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
    }

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

    // 0 → 1 → 0 พองขึ้นเร็วแล้วค่อยยุบ
    private float PopBump()
    {
        if (_popTimer < 0f || _popTimer >= _popDuration) return 0f;

        float k = _popTimer / Mathf.Max(0.01f, _popDuration);
        return k < 0.3f ? Smooth(k / 0.3f) : 1f - Smooth((k - 0.3f) / 0.7f);
    }

    private void UpdateEcho()
    {
        if (_echoTimer < 0f)
        {
            if (_echo != null) _echo.gameObject.SetActive(false);
            return;
        }

        EnsureEcho();
        _echo.gameObject.SetActive(true);

        float e = Mathf.Clamp01(_echoTimer / Mathf.Max(0.01f, _echoDuration));
        float ease = 1f - (1f - e) * (1f - e);

        Vector2 spread = new(1f + _echoSpread.x * ease, 1f + _echoSpread.y * ease);
        _echo.offsetMin = -spread;
        _echo.offsetMax = spread;

        Color color = _echoColor;
        color.a *= 1f - e;
        foreach (Image line in _echoLines)
            line.color = color;
    }

    private void UpdateReadyIcon(bool full, float dt)
    {
        if (_readyIcon == null) return;

        if (!_iconScaleSaved)
        {
            _iconBaseScale = _readyIcon.rectTransform.localScale;
            _iconScaleSaved = true;
        }

        if (!full)
            _iconAlpha = Mathf.MoveTowards(_iconAlpha, 0f, dt / Mathf.Max(0.01f, _iconFadeOut));

        float scale = 1f;
        Color color = ApplyPulse(_readyColor);

        if (_popTimer >= 0f && _popTimer < _popDuration)
        {
            float k = _popTimer / Mathf.Max(0.01f, _popDuration);
            scale = k < 0.35f
                ? Mathf.Lerp(0.6f, _iconPopScale, Smooth(k / 0.35f))
                : Mathf.Lerp(_iconPopScale, 1f, Smooth((k - 0.35f) / 0.65f));
            color = Color.Lerp(color, Color.white, 1f - k);
        }
        else if (full && _pulseWhenReady)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) * 0.5f;
            scale = 1f + _iconPulseScale * pulse;
        }

        color.a *= _iconAlpha;
        _readyIcon.color = color;
        _readyIcon.enabled = _iconAlpha > 0.001f;
        _readyIcon.rectTransform.localScale = _iconBaseScale * scale;
    }

    // กรอบ 4 เส้นรอบแถว — ไม่ใช้รูป เส้นคมทุกขนาด / Ignore Layout ไม่ให้ Layout Group นับเป็นช่อง
    private void EnsureEcho()
    {
        if (_echo != null) return;

        var go = new GameObject("ReadyEcho", typeof(RectTransform), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        _echo = (RectTransform)go.transform;
        _echo.SetParent(transform, false);
        _echo.SetAsLastSibling();
        _echo.anchorMin = Vector2.zero;
        _echo.anchorMax = Vector2.one;

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
        var go = new GameObject(lineName, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_echo, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // ---------- เดิม ----------

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