using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonEffect : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Scale")]
    [SerializeField] private float _hoverScale = 1.12f;
    [SerializeField] private float _speed = 12f;

    [Header("Slide")]
    [Tooltip("เลื่อนไปทางขวากี่พิกเซลตอนถูกเลือก")]
    [SerializeField] private float _slideDistance = 16f;

    [Header("Color")]
    [SerializeField] private Color _normalColor = new(0.65f, 0.65f, 0.65f, 1f);
    [SerializeField] private Color _hoverColor = Color.white;

    [Header("Pulse")]
    [Tooltip("ให้ตัวที่เลือกอยู่เต้นเบา ๆ แบบ GTA")]
    [SerializeField] private bool _pulse = true;
    [SerializeField] private float _pulseAmount = 0.03f;
    [SerializeField] private float _pulseSpeed = 4f;

    private TMP_Text _label;
    private RectTransform _rect;
    private Vector3 _baseScale;

    // ตำแหน่งปุ่มเป็นของ Layout Group — เราแค่ยืมไปเลื่อนตอนถูกเลือก แล้วคืนให้
    private Vector2 _basePosition;
    private float _slide;
    private int _enabledFrame;

    private bool _pointerInside;
    private bool _isSelected;
    private bool _isHighlighted;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _label = GetComponentInChildren<TMP_Text>();
        _baseScale = _rect.localScale;
    }

    private void OnEnable()
    {
        // รีเซ็ตตอนเปิดหน้าใหม่ ไม่งั้นปุ่มจะค้างสภาพเดิมจากรอบก่อน
        _pointerInside = false;
        _isSelected = false;
        _isHighlighted = false;

        _rect.localScale = _baseScale;
        if (_label != null) _label.color = _normalColor;

        _slide = 0f;
        _enabledFrame = Time.frameCount;
    }

    private void OnDisable()
    {
        // ปิดหน้าตอนปุ่มยังเลื่อนค้าง — คืนที่เดิมก่อน ไม่งั้นเปิดรอบหน้าปุ่มจะเบี้ยว
        if (_slide != 0f) _rect.anchoredPosition = _basePosition;
        _slide = 0f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = 1f - Mathf.Exp(-_speed * dt);   // ลื่นกว่า Lerp แบบคูณ deltaTime ตรง ๆ

        float targetScale = _isHighlighted ? _hoverScale : 1f;

        if (_isHighlighted && _pulse)
            targetScale += Mathf.Sin(Time.unscaledTime * _pulseSpeed) * _pulseAmount;

        _rect.localScale = Vector3.Lerp(_rect.localScale, _baseScale * targetScale, t);

        if (_label != null)
            _label.color = Color.Lerp(_label.color, _isHighlighted ? _hoverColor : _normalColor, t);

        UpdateSlide(t);
    }

    private void UpdateSlide(float t)
    {
        // Layout Group จัดตำแหน่งตอนท้ายเฟรม เฟรมแรกหลังเปิดค่ายังไม่ถูก รอไปก่อน
        if (Time.frameCount <= _enabledFrame) return;

        if (!_isHighlighted && _slide < 0.01f)
        {
            // กลับถึงที่แล้ว ปล่อยตำแหน่งคืนให้ Layout Group ไม่ไปแตะอีก
            if (_slide != 0f)
            {
                _rect.anchoredPosition = _basePosition;
                _slide = 0f;
            }
            return;
        }

        // เพิ่งเริ่มเลื่อน — จำจุดตั้งต้นจากที่ Layout Group วางไว้ตอนนี้
        if (_slide == 0f) _basePosition = _rect.anchoredPosition;

        _slide = Mathf.Lerp(_slide, _isHighlighted ? _slideDistance : 0f, t);
        _rect.anchoredPosition = _basePosition + new Vector2(_slide, 0f);
    }

    // ---------- เมาส์ ----------

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
        UpdateHighlight();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
        UpdateHighlight();
    }

    // ---------- Gamepad / คีย์บอร์ด ----------

    public void OnSelect(BaseEventData eventData)
    {
        _isSelected = true;
        UpdateHighlight();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        _isHighlighted = _pointerInside || _isSelected;
    }
}