using UnityEngine;
using UnityEngine.UI;

// เส้นโค้ง Guard ใต้เป้ากลางจอ — วางไว้กลางจอที่เดียวกับเป้า สคริปต์วาดเส้นเอง
// ปกติ (ยังไม่หมด ทั้งตอนลดและตอนเติม): เส้นหนา เหลือง → แดงตอนใกล้หมด
// ใช้จนหมด (บล็อกไม่ได้): เส้นบางสีขาวกะพริบ ค่อย ๆ ยาวออก + ไอคอนบล็อกไม่ได้
// ชาร์จจากหมดจนเต็ม: เด้งกลับเป็นเส้นหนา + เงาขยายออก แล้วซ่อน
[RequireComponent(typeof(CanvasRenderer))]
public class GuardArcUI : MaskableGraphic
{
    [Header("Source")]
    [Tooltip("tag ที่ใช้ค้นหาผู้เล่น")]
    [SerializeField] private string _playerTag = "Player";

    [Header("รูปทรง")]
    [Tooltip("ระยะจากกลางจอถึงเส้น")]
    [SerializeField, Min(1f)] private float _radius = 110f;

    [Tooltip("ความกว้างของเส้นโค้งทั้งเส้น (องศา)")]
    [SerializeField, Range(10f, 360f)] private float _arcAngle = 100f;

    [Tooltip("ช่องว่างตรงกลางสำหรับไอคอน (องศา)")]
    [SerializeField, Range(0f, 90f)] private float _centerGap = 22f;

    [Tooltip("จุดกึ่งกลางของเส้นอยู่ทิศไหน — 270 = ใต้เป้า / 90 = เหนือเป้า")]
    [SerializeField, Range(0f, 360f)] private float _centerAngle = 270f;

    [Tooltip("ความหนาที่ปลายเส้น (ตอนปกติ)")]
    [SerializeField, Min(0.5f)] private float _thicknessOuter = 12f;

    [Tooltip("ความหนาที่โคนเส้นฝั่งกลาง (ตอนปกติ)")]
    [SerializeField, Min(0.5f)] private float _thicknessInner = 5f;

    [Tooltip("ความหนาเส้นตอนใช้จนหมด (รอชาร์จ)")]
    [SerializeField, Min(0.5f)] private float _chargeThickness = 3f;

    [Tooltip("ขอบสีเข้มรอบเส้น ให้มองเห็นบนฉากสว่าง — 0 = ไม่มีขอบ")]
    [SerializeField, Min(0f)] private float _outline = 1.5f;

    [Tooltip("ขอบนุ่มกี่พิกเซล กันเส้นเป็นหยัก")]
    [SerializeField, Min(0f)] private float _softEdge = 1f;

    [Header("สี")]
    [Tooltip("สีปกติ (Guard ยังเยอะ)")]
    [SerializeField] private Color _useColor = new Color(1f, 0.9f, 0.3f, 1f);

    [Tooltip("สีตอนใกล้หมด")]
    [SerializeField] private Color _lowColor = new Color(0.95f, 0.2f, 0.15f, 1f);

    [Tooltip("เหลือน้อยกว่ากี่ส่วนถึงเป็นสีใกล้หมดเต็มที่ (0.2 = 20%)")]
    [SerializeField, Range(0f, 1f)] private float _lowThreshold = 0.2f;

    [Tooltip("สีเส้นตอนใช้จนหมด (รอชาร์จ)")]
    [SerializeField] private Color _chargeColor = new Color(1f, 1f, 1f, 0.95f);

    [Tooltip("สีวาบ + สีเงาตอนชาร์จจากหมดจนเต็ม")]
    [SerializeField] private Color _flashColor = Color.white;

    [SerializeField] private Color _outlineColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("เส้นพื้นหลัง (บอกความยาวเต็ม)")]
    [Tooltip("0 = ไม่มีเส้นพื้นหลัง")]
    [SerializeField, Min(0f)] private float _trackThickness = 3f;
    [SerializeField] private Color _trackColor = new Color(0f, 0f, 0f, 0.35f);

    [Tooltip("สีช่วง Min Floor บนเส้นพื้นหลัง (โชว์เมื่อเกมตั้ง Min Guard ไว้)")]
    [SerializeField] private Color _floorZoneColor = new Color(0.8f, 0.1f, 0.1f, 0.8f);

    [Header("ไอคอนตรงกลาง (ไม่ใส่ก็ได้)")]
    [Tooltip("ไอคอนตอนบล็อกได้")]
    [SerializeField] private Sprite _readyIcon;

    [Tooltip("ไอคอนตอน Guard หมด บล็อกไม่ได้")]
    [SerializeField] private Sprite _brokenIcon;
    [SerializeField, Min(1f)] private float _iconSize = 28f;
    [SerializeField] private Color _iconColor = Color.white;
    [SerializeField] private Color _brokenIconColor = new Color(1f, 0.3f, 0.25f, 1f);

    [Header("จังหวะ")]
    [Tooltip("ความเร็วที่เส้นวิ่งตามค่าจริง")]
    [SerializeField, Min(0.1f)] private float _fillSpeed = 3f;

    [Tooltip("Guard เต็มครบกี่วินาทีแล้วซ่อน")]
    [SerializeField, Min(0f)] private float _hideDelay = 0.35f;

    [Tooltip("ความเร็วจางหาย / โผล่")]
    [SerializeField, Min(0.1f)] private float _fadeSpeed = 8f;

    [Tooltip("ความเร็วกะพริบตอนใช้จนหมด (รอชาร์จ)")]
    [SerializeField, Min(0f)] private float _blinkSpeed = 10f;

    [Tooltip("กะพริบจางสุดแค่ไหน (1 = ไม่กะพริบ)")]
    [SerializeField, Range(0f, 1f)] private float _blinkMinAlpha = 0.35f;

    [SerializeField, Min(0.01f)] private float _flashTime = 0.25f;

    [Tooltip("เส้นเด้งออกกี่เท่าตอนโดนตี / หมด / ชาร์จเต็ม")]
    [SerializeField, Min(0f)] private float _punchAmount = 0.12f;
    [SerializeField, Min(0.01f)] private float _punchTime = 0.18f;

    [Tooltip("Guard ลดทีเดียวเกินกี่ส่วนถึงนับว่าโดนตี (0.03 = 3%)")]
    [SerializeField, Range(0.005f, 0.5f)] private float _hitThreshold = 0.03f;

    [Header("เด้งตอนเต็ม")]
    [Tooltip("ติ๊ก = เติมเต็มแบบไม่ได้ใช้จนหมดก็เด้งด้วย / ไม่ติ๊ก = เด้งเฉพาะตอนชาร์จจากหมด")]
    [SerializeField] private bool _popOnRefill = true;

    [Tooltip("ตอนเด้ง เส้นขยายออก + หนาขึ้นแค่ไหน")]
    [SerializeField, Min(0f)] private float _popAmount = 0.3f;

    [Tooltip("เด้งนานกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _popDuration = 0.35f;

    [Tooltip("เด้งแล้วโชว์ค้างกี่วินาทีก่อนซ่อน")]
    [SerializeField, Min(0f)] private float _popHold = 0.8f;

    [Tooltip("เงาขยายออกนานกี่วินาที — 0 = ไม่มีเงา")]
    [SerializeField, Min(0f)] private float _echoDuration = 0.55f;

    [Tooltip("เงาขยายออกไปอีกกี่เท่าของระยะเส้น")]
    [SerializeField, Min(0f)] private float _echoSpread = 0.5f;

    [Header("Debug")]
    [Tooltip("ติ๊ก = โชว์เส้นตลอด แม้ Guard เต็ม")]
    [SerializeField] private bool _alwaysShow;

    [Tooltip("ติ๊ก = ขึ้น log สถานะใน Console ทุก 1 วินาที")]
    [SerializeField] private bool _logState;

    [Header("ตัวอย่างใน Editor (ไม่มีผลตอนเล่น)")]
    [SerializeField, Range(0f, 1f)] private float _previewAmount = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _previewFloor = 0f;
    [SerializeField] private bool _previewCharging;

    private BlockSystem _block;
    private Image _icon;
    private float _retryTimer = 1f;
    private float _shown = -1f;
    private float _alpha;
    private float _thin;            // 0 = เส้นหนา (ปกติ) / 1 = เส้นบาง (หมด รอชาร์จ)
    private float _floor;
    private float _lastTarget = 1f;
    private bool _charging;
    private bool _wasReady = true;
    private bool _wasFull = true;
    private float _hideDelayNow;
    private float _popTimer;
    private float _fullTimer;
    private float _flashTimer;
    private float _punchTimer;
    private float _echoTimer;
    private Color _flashColorNow = Color.white;
    private Color _fillColorNow = Color.white;
    private float _logTimer;
    private int _vertexCount;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        float dt = Time.unscaledDeltaTime;
        bool wasVisible = _alpha > 0f;

        if (!FindBlock() || _block.MaxGuardPoints <= 0f)
        {
            LogState(dt, "ยังหา BlockSystem ของผู้เล่นไม่เจอ");
            _alpha = Mathf.MoveTowards(_alpha, 0f, _fadeSpeed * dt);
            UpdateIcon(true, 1f, 0f);
            if (wasVisible) SetVerticesDirty();
            return;
        }

        float target = Mathf.Clamp01(_block.CurrentGuardPoints / _block.MaxGuardPoints);
        bool ready = _block.IsReady;
        bool full = ready && target >= 0.999f;
        _floor = Mathf.Clamp01(_block.MinGuardPercent / 100f);

        if (_shown < 0f)
        {
            _shown = target;
            _lastTarget = target;
            _wasReady = ready;
            _wasFull = full;
            _thin = ready ? 0f : 1f;
            _hideDelayNow = _hideDelay;
            _fullTimer = full ? 999f : 0f;   // เต็มอยู่แล้วตั้งแต่เริ่ม ไม่ต้องโผล่มาโชว์
        }

        // เส้นบางสีขาว = ใช้จนหมดเท่านั้น (IsReady กลับมา true ตอนเต็ม) / ยังไม่หมดแล้วเติม = เส้นปกติ
        _charging = !ready;

        if (!full) _hideDelayNow = _hideDelay;

        if (full && !_wasFull && (!_wasReady || _popOnRefill)) Pop();
        else if (_wasReady && !ready) Flash(_lowColor);
        else if (target < _lastTarget - _hitThreshold) _punchTimer = _punchTime;

        _wasReady = ready;
        _wasFull = full;
        _lastTarget = target;

        _shown = Mathf.MoveTowards(_shown, target, _fillSpeed * dt);
        _thin = Mathf.MoveTowards(_thin, _charging ? 1f : 0f, 10f * dt);
        _flashTimer = Mathf.Max(0f, _flashTimer - dt);
        _punchTimer = Mathf.Max(0f, _punchTimer - dt);
        _echoTimer = Mathf.Max(0f, _echoTimer - dt);
        _popTimer = Mathf.Max(0f, _popTimer - dt);

        float blink = Mathf.Lerp(_blinkMinAlpha, 1f, (Mathf.Sin(Time.unscaledTime * _blinkSpeed) + 1f) * 0.5f);

        Color fill = Color.Lerp(UseColor(_shown, _floor), _chargeColor, _thin);
        fill.a *= Mathf.Lerp(1f, blink, _thin);
        if (_flashTimer > 0f)
            fill = Color.Lerp(fill, _flashColorNow, _flashTimer / _flashTime);
        _fillColorNow = fill;

        _fullTimer = full ? _fullTimer + dt : 0f;
        bool show = _alwaysShow || !full || _fullTimer < _hideDelayNow;
        _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, _fadeSpeed * dt);

        UpdateIcon(ready, blink, Mathf.Max(_punchTimer / _punchTime, _popTimer / _popDuration));
        LogState(dt, $"Guard {_block.CurrentGuardPoints:0}/{_block.MaxGuardPoints:0} | บล็อกได้ {ready} | {(_charging ? "หมด รอชาร์จ" : "ปกติ")}");

        if (wasVisible || _alpha > 0f)
            SetVerticesDirty();
    }

    // เหลือเยอะ = สีปกติ / ใกล้หมด (หรือต่ำกว่า Min Floor) = สีใกล้หมด
    private Color UseColor(float amount, float floor)
    {
        float lowAt = Mathf.Max(_lowThreshold, floor);
        return Color.Lerp(_lowColor, _useColor, Mathf.InverseLerp(lowAt, lowAt * 2f + 0.0001f, amount));
    }

    // เต็มแล้ว: ตึง! เส้นเด้ง + วาบ + เงาขยายออก แล้วโชว์ค้างแป๊บนึงก่อนซ่อน
    private void Pop()
    {
        Flash(_flashColor);
        _popTimer = _popDuration;
        _echoTimer = _echoDuration;
        _hideDelayNow = Mathf.Max(_hideDelay, _popHold);
    }

    private void Flash(Color flashColor)
    {
        _flashColorNow = flashColor;
        _flashTimer = _flashTime;
        _punchTimer = _punchTime;
    }

    private void UpdateIcon(bool ready, float blink, float punch)
    {
        if (_icon == null)
        {
            if (_readyIcon == null && _brokenIcon == null)
                return;

            var iconObject = new GameObject("GuardIcon", typeof(RectTransform), typeof(Image));
            iconObject.layer = gameObject.layer;
            iconObject.transform.SetParent(transform, false);
            _icon = iconObject.GetComponent<Image>();
            _icon.raycastTarget = false;
            _icon.preserveAspect = true;
        }

        Sprite sprite = ready || _brokenIcon == null ? _readyIcon : _brokenIcon;
        _icon.enabled = sprite != null && _alpha > 0f;
        if (!_icon.enabled)
            return;

        float rad = _centerAngle * Mathf.Deg2Rad;
        RectTransform iconRect = _icon.rectTransform;
        iconRect.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * _radius;
        iconRect.sizeDelta = Vector2.one * _iconSize;
        iconRect.localScale = Vector3.one * (1f + 0.35f * punch * punch);

        Color iconColor = ready ? _iconColor : _brokenIconColor;
        iconColor.a *= _alpha * (ready ? 1f : blink);
        _icon.sprite = sprite;
        _icon.color = iconColor;
    }

    private void LogState(float dt, string status)
    {
        if (!_logState)
            return;

        _logTimer += dt;
        if (_logTimer < 1f)
            return;
        _logTimer = 0f;

        float groupAlpha = 1f;
        foreach (CanvasGroup group in GetComponentsInParent<CanvasGroup>())
            groupAlpha *= group.alpha;

        Debug.Log($"[GuardArc] {status} | ความชัดเส้น {_alpha:0.00} | ความชัด HUD {groupAlpha:0.00} | " +
                  $"จุดที่วาด {_vertexCount} | Canvas {(canvas != null ? canvas.name : "ไม่มี")} | " +
                  $"CanvasRenderer {(GetComponent<CanvasRenderer>() != null ? "มี" : "ไม่มี")}", this);
    }

    // UIRoot อยู่ข้ามซีน แต่ผู้เล่นเกิดใหม่ทุกซีน — ของเดิมหายเมื่อไหร่ก็หาใหม่
    private bool FindBlock()
    {
        if (_block != null)
            return true;

        _shown = -1f;
        _retryTimer += Time.unscaledDeltaTime;
        if (_retryTimer < 0.5f)
            return false;
        _retryTimer = 0f;

        _block = PlayerLookup.Find<BlockSystem>(_playerTag);
        return _block != null;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        _vertexCount = 0;

        float amount = _shown;
        float alpha = _alpha;
        float thin = _thin;
        float floor = _floor;
        Color fill = _fillColorNow;
        float punch = _punchTimer > 0f ? _punchTimer / _punchTime : 0f;
        float pop = _popTimer > 0f ? _popTimer / _popDuration : 0f;
        float echo = _echoDuration > 0f && _echoTimer > 0f ? _echoTimer / _echoDuration : 0f;

        if (!Application.isPlaying)
        {
            amount = _previewAmount;
            alpha = 1f;
            floor = _previewFloor;
            thin = _previewCharging ? 1f : 0f;
            fill = _previewCharging ? _chargeColor : UseColor(amount, floor);
            punch = 0f;
            pop = 0f;
            echo = 0f;
        }

        alpha *= color.a;
        if (alpha <= 0.001f)
            return;

        amount = Mathf.Clamp01(amount);
        float radius = _radius * (1f + _punchAmount * punch * punch + _popAmount * pop * pop);
        float thicken = 1f + _popAmount * 2f * pop * pop;
        float inner = Mathf.Lerp(_thicknessInner, _chargeThickness, thin) * thicken;
        float outer = Mathf.Lerp(_thicknessOuter, _chargeThickness, thin) * thicken;
        Vector2 center = rectTransform.rect.center;

        for (int side = -1; side <= 1; side += 2)
        {
            if (_trackThickness > 0f)
            {
                if (floor > 0f)
                    AddArc(vh, center, side, 0f, floor, _radius, _trackThickness, _trackThickness, Fade(_floorZoneColor, alpha));
                AddArc(vh, center, side, floor, 1f, _radius, _trackThickness, _trackThickness, Fade(_trackColor, alpha));
            }

            if (_outline > 0f)
                AddArc(vh, center, side, 0f, amount, radius, inner + _outline * 2f, outer + _outline * 2f, Fade(_outlineColor, alpha * fill.a));
            AddArc(vh, center, side, 0f, amount, radius, inner, outer, Fade(fill, alpha));

            // เงาเด้งตอนเต็ม — เส้นเต็มขยายออกแล้วจาง
            if (echo > 0f)
            {
                float echoRadius = _radius * (1f + _echoSpread * (1f - echo * echo));
                if (_outline > 0f)
                    AddArc(vh, center, side, 0f, 1f, echoRadius, _thicknessInner + _outline * 2f,
                        _thicknessOuter + _outline * 2f, Fade(_outlineColor, alpha * echo));
                AddArc(vh, center, side, 0f, 1f, echoRadius, _thicknessInner, _thicknessOuter, Fade(_flashColor, alpha * echo));
            }
        }

        _vertexCount = vh.currentVertCount;
    }

    // from / to = 0 ถึง 1 นับจากกลางเส้นออกไปปลาย — เส้นเลยหดเข้าหากลาง
    private void AddArc(VertexHelper vh, Vector2 center, int side, float from, float to, float radius,
        float thicknessFrom, float thicknessTo, Color arcColor)
    {
        if (to - from <= 0.0001f || arcColor.a <= 0f)
            return;

        float half = _arcAngle * 0.5f;
        float gap = Mathf.Min(_centerGap * 0.5f, half);
        float startDeg = _centerAngle + side * Mathf.Lerp(gap, half, from);
        float endDeg = _centerAngle + side * Mathf.Lerp(gap, half, to);

        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(endDeg - startDeg) / 3f));
        Color32 solid = arcColor;
        Color32 clear = new Color(arcColor.r, arcColor.g, arcColor.b, 0f);
        int first = vh.currentVertCount;

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float rad = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
            float halfThickness = Mathf.Lerp(thicknessFrom, thicknessTo, t) * 0.5f;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            AddVertex(vh, center + dir * (radius + halfThickness + _softEdge), clear);
            AddVertex(vh, center + dir * (radius + halfThickness), solid);
            AddVertex(vh, center + dir * (radius - halfThickness), solid);
            AddVertex(vh, center + dir * (radius - halfThickness - _softEdge), clear);
        }

        for (int i = 0; i < steps; i++)
        {
            int a = first + i * 4;
            int b = a + 4;
            for (int k = 0; k < 3; k++)
            {
                vh.AddTriangle(a + k, a + k + 1, b + k + 1);
                vh.AddTriangle(a + k, b + k + 1, b + k);
            }
        }
    }

    private static void AddVertex(VertexHelper vh, Vector2 position, Color32 vertexColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vh.AddVert(vertex);
    }

    private static Color Fade(Color c, float alpha)
    {
        c.a *= alpha;
        return c;
    }
}