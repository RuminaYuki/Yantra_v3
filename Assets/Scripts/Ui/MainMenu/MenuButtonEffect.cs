using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// เอฟเฟกต์ปุ่มเมนูตอนเมาส์ชี้หรือ gamepad เลือก
/// แปะบน GameObject ของปุ่มเอง — ใช้ได้กับทุกปุ่ม ไม่ต้องตั้งค่าอะไรเพิ่ม
///
/// แยก 2 สถานะอิสระ: เมาส์อยู่บนปุ่ม / gamepad เลือกปุ่มนี้
/// สว่างเมื่ออย่างใดอย่างหนึ่งเป็นจริง เมาส์ออกก็ดับทันที
///
/// ใช้ unscaled time เลยทำงานได้แม้ตอน Time.timeScale = 0
/// </summary>
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
    private Vector2 _basePosition;

    private bool _pointerInside;
    private bool _isSelected;
    private bool _isHighlighted;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _label = GetComponentInChildren<TMP_Text>();

        _baseScale = _rect.localScale;
        _basePosition = _rect.anchoredPosition;

        if (_label != null) _label.color = _normalColor;
    }

    private void OnEnable()
    {
        // รีเซ็ตตอนเปิดหน้าใหม่ ไม่งั้นปุ่มจะค้างสภาพเดิมจากรอบก่อน
        _pointerInside = false;
        _isSelected = false;
        _isHighlighted = false;

        _rect.localScale = _baseScale;
        _rect.anchoredPosition = _basePosition;
        if (_label != null) _label.color = _normalColor;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = 1f - Mathf.Exp(-_speed * dt);   // ลื่นกว่า Lerp แบบคูณ deltaTime ตรง ๆ

        float targetScale = _isHighlighted ? _hoverScale : 1f;

        if (_isHighlighted && _pulse)
            targetScale += Mathf.Sin(Time.unscaledTime * _pulseSpeed) * _pulseAmount;

        _rect.localScale = Vector3.Lerp(_rect.localScale, _baseScale * targetScale, t);

        Vector2 targetPos = _basePosition;
        if (_isHighlighted) targetPos.x += _slideDistance;
        _rect.anchoredPosition = Vector2.Lerp(_rect.anchoredPosition, targetPos, t);

        if (_label != null)
            _label.color = Color.Lerp(_label.color, _isHighlighted ? _hoverColor : _normalColor, t);
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