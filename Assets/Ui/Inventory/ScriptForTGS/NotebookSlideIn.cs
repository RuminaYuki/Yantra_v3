using UnityEngine;

// ใส่ที่ Notebook — ทุกครั้งที่เปิดกระเป๋า สมุดจะเลื่อนออกมาจากหลังกระเป๋า
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class NotebookSlideIn : MonoBehaviour
{
    [Tooltip("ตอนเริ่ม สมุดมุดอยู่หลังกระเป๋าลึกกี่หน่วย (เลื่อนไปทางขวา)")]
    [SerializeField] private float _slideDistance = 300f;

    [Tooltip("เลื่อนออกมานานกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _duration = 0.3f;

    [Tooltip("เปิดกระเป๋าแล้วรอกี่วินาทีค่อยเริ่มเลื่อน")]
    [SerializeField, Min(0f)] private float _delay = 0.05f;

    [Tooltip("เอียงกี่องศาตอนเริ่ม แล้วค่อยๆ ตั้งตรง — 0 = ไม่เอียง")]
    [SerializeField] private float _startTilt = 3f;

    [Tooltip("ช่วงแรกของการเลื่อนที่ค่อยๆ ชัดขึ้น กันขอบสมุดโผล่พ้นกระเป๋าตอนเริ่ม — 0 = ไม่จาง")]
    [SerializeField, Range(0f, 1f)] private float _fadePortion = 0.35f;

    private RectTransform _rect;
    private CanvasGroup _group;
    private Vector2 _homePosition;
    private Quaternion _homeRotation;
    private float _time;
    private bool _playing;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _group = GetComponent<CanvasGroup>();
        _homePosition = _rect.anchoredPosition;
        _homeRotation = _rect.localRotation;
    }

    private void OnEnable()
    {
        _time = -_delay;
        _playing = true;
        Apply(0f);
    }

    private void OnDisable()
    {
        _playing = false;
        Apply(1f);
    }

    private void Update()
    {
        if (!_playing)
            return;

        // เวลาจริง เผื่อมี HitStop / หยุดเกมอยู่ตอนเปิดกระเป๋า
        _time += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_time / _duration);
        Apply(t);

        if (t >= 1f)
            _playing = false;
    }

    private void Apply(float t)
    {
        float remain = Mathf.Pow(1f - t, 3f);   // ออกเร็วแล้วค่อยๆ ช้าลงตอนเข้าที่

        _rect.anchoredPosition = _homePosition + Vector2.right * (_slideDistance * remain);
        _rect.localRotation = _homeRotation * Quaternion.Euler(0f, 0f, _startTilt * remain);
        _group.alpha = _fadePortion > 0f ? Mathf.Clamp01(t / _fadePortion) : 1f;
    }
}
