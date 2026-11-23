using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ป้าย "กด F ค้างไว้" — วางบน Panel ใน Canvas ของ HUD ไม่ต้องผูกกับผีตัวไหน โผล่เองตอนเวลาช้าลง
// รอกด: ปุ่มเต้นตุบ ๆ / กดค้างอยู่: ปุ่มยุบ + เปลี่ยนสี / กันได้: ขึ้นข้อความแป๊บนึงแล้วหาย
public class BlockQTEPromptUI : MonoBehaviour
{
    [Header("ชิ้น UI")]
    [Tooltip("CanvasGroup ของทั้งก้อน — ใช้ซ่อน / โชว์")]
    [SerializeField] private CanvasGroup _group;

    [Tooltip("ไอคอนปุ่ม F")]
    [SerializeField] private RectTransform _key;

    [Tooltip("รูปปุ่ม — เปลี่ยนสีตอนกดค้าง (ว่างได้)")]
    [SerializeField] private Graphic _keyGraphic;

    [SerializeField] private TMP_Text _label;

    [Tooltip("วงนับถอยหลัง — Image แบบ Image Type = Filled (ใช้ RingFill เดิมได้) หมดวง = ผีตีโดน / ว่างได้")]
    [SerializeField] private Image _ring;
    [SerializeField] private Color _ringFullColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color _ringEmptyColor = new Color(0.85f, 0.15f, 0.1f, 1f);

    [Header("ข้อความ")]
    [SerializeField] private string _pressText = "HOLD  F  TO BLOCK";
    [SerializeField] private string _holdText = "HOLD...";
    [SerializeField] private string _blockedText = "BLOCKED!";

    /// <summary>ก้อนที่ต้องโชว์ — คัตซีนใช้หาว่าห้ามซ่อนอันไหนตอนซ่อน HUD</summary>
    public Transform Visual => _group != null ? _group.transform : transform;

    [Tooltip("โชว์ข้อความกันได้ค้างกี่วินาที — 0 = ไม่โชว์")]
    [SerializeField, Min(0f)] private float _resultTime = 0.8f;

    [Header("ตัวหนังสือ (ให้อ่านออกบนฉากสว่าง)")]
    [Tooltip("ขอบตัวหนังสือ — 0 = ไม่มี / 0.1-0.2 กำลังดี (ตัวหนังสือจะอ้วนขึ้นเท่าขอบ ขอบเลยอยู่ข้างนอก ไม่กินเนื้อจนตัวกลวง)")]
    [SerializeField, Range(0f, 0.5f)] private float _textOutline = 0.15f;
    [SerializeField] private Color _textOutlineColor = Color.black;

    [Tooltip("เงาฟุ้งดำหลังตัวหนังสือ")]
    [SerializeField] private bool _textShadow = true;
    [SerializeField] private Color _textShadowColor = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField, Range(0f, 1f)] private float _textShadowSize = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _textShadowSoftness = 0.6f;

    [Header("หน้าตา")]
    [SerializeField] private Color _idleColor = Color.white;
    [SerializeField] private Color _holdColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField, Min(0.1f)] private float _fadeSpeed = 10f;

    [Tooltip("ปุ่มเต้นแรงแค่ไหนตอนรอกด")]
    [SerializeField, Min(0f)] private float _pulse = 0.12f;
    [SerializeField, Min(0f)] private float _pulseSpeed = 8f;

    [Tooltip("ปุ่มยุบเหลือกี่เท่าตอนกดค้าง")]
    [SerializeField, Range(0.5f, 1f)] private float _pressedScale = 0.88f;

    private BlockQTE _qte;
    private float _resultTimer;
    private float _keyScale = 1f;
    private float _ringShown = 1f;

    private void Awake()
    {
        if (_group == null)
            _group = GetComponent<CanvasGroup>();
        SetAlpha(0f);
        StyleLabel();
    }

    // ขอบ + เงา ทำผ่าน material ของตัวหนังสือตัวนี้ตัวเดียว (ไม่ไปแก้ฟอนต์ที่ใช้ร่วมกับ UI อื่น)
    private void StyleLabel()
    {
        if (_label == null)
            return;

        if (_textOutline > 0f)
        {
            _label.outlineWidth = _textOutline;
            _label.outlineColor = _textOutlineColor;

            // ขอบของ TMP อยู่ครึ่งในครึ่งนอกตัวอักษร ฟอนต์ผอม ๆ เลยโดนกินจนเหลือแต่เส้นกลวง
            // ขยายเนื้อออกเท่ากัน ขอบจะไปอยู่ข้างนอกทั้งหมด
            _label.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, _textOutline);
        }

        if (_textShadow)
        {
            Material material = _label.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, _textShadowColor);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.4f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, _textShadowSize);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, _textShadowSoftness);
        }

        _label.UpdateMeshPadding();
    }

    private void OnEnable()
    {
        BlockQTE.AnyStarted += HandleStarted;
        BlockQTE.AnyEnded += HandleEnded;
    }

    private void OnDisable()
    {
        BlockQTE.AnyStarted -= HandleStarted;
        BlockQTE.AnyEnded -= HandleEnded;
        _qte = null;
        _resultTimer = 0f;
        SetAlpha(0f);
    }

    private void HandleStarted(BlockQTE qte)
    {
        _qte = qte;
        _resultTimer = 0f;
        _ringShown = 1f;
    }

    private void HandleEnded(BlockQTE qte)
    {
        if (qte != _qte)
            return;

        _resultTimer = qte.LastResult == BlockQTE.Result.Blocked ? _resultTime : 0f;
        _qte = null;
    }

    // ใช้เวลาจริง — ตอนสโลว์ / hitstop ป้ายยังขยับปกติ
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _resultTimer = Mathf.Max(0f, _resultTimer - dt);

        bool waiting = _qte != null && _qte.IsSlowing;
        bool holding = waiting && _qte.IsHolding;
        bool showResult = _resultTimer > 0f;
        bool active = waiting || showResult;

        if (_group != null)
            SetAlpha(Mathf.MoveTowards(_group.alpha, active ? 1f : 0f, _fadeSpeed * dt));

        // กำลังจางหาย — ค้างหน้าตาสุดท้ายไว้ ไม่งั้นข้อความเด้งกลับเป็น "HOLD F" แวบนึงก่อนหาย
        if (!active)
            return;

        if (_label != null)
        {
            string text = showResult ? _blockedText : holding ? _holdText : _pressText;
            if (_label.text != text)
                _label.text = text;
        }

        float targetScale = holding || showResult
            ? _pressedScale
            : 1f + _pulse * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * _pulseSpeed));
        _keyScale = Mathf.Lerp(_keyScale, targetScale, 1f - Mathf.Exp(-20f * dt));

        if (_key != null)
            _key.localScale = Vector3.one * _keyScale;

        if (_keyGraphic != null)
            _keyGraphic.color = holding || showResult ? _holdColor : _idleColor;

        if (_ring != null)
        {
            // กดค้าง = กันอยู่แล้ว วงเติมเต็มเป็นสีทอง (ปลอดภัย) / ปล่อย = กลับไปนับถอยหลังต่อ
            bool safe = holding || showResult;
            float target = safe ? 1f : _qte != null ? _qte.Remaining01 : _ringShown;
            _ringShown = Mathf.Lerp(_ringShown, target, 1f - Mathf.Exp(-25f * dt));
            _ring.fillAmount = _ringShown;
            _ring.color = safe ? _holdColor : Color.Lerp(_ringEmptyColor, _ringFullColor, _ringShown);
        }
    }

    private void SetAlpha(float alpha)
    {
        if (_group == null)
            return;
        _group.alpha = alpha;
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }
}