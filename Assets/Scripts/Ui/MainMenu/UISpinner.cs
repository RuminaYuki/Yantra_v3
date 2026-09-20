using UnityEngine;

/// <summary>
/// หมุน RectTransform ไปเรื่อย ๆ — ใช้กับ loading spinner
/// แปะบน Image ที่จะหมุน
///
/// ใช้ unscaledDeltaTime เพราะตอนโหลด scene อาจมีการตั้ง timeScale = 0
/// ถ้าใช้ deltaTime ปกติตัวหมุนจะค้างนิ่ง ดูเหมือนเกมแฮงก์
/// </summary>
public class UISpinner : MonoBehaviour
{
    [Tooltip("องศาต่อวินาที — ลบ = หมุนตามเข็ม")]
    [SerializeField] private float _speed = -180f;

    [Header("Stepped (แบบนาฬิกา)")]
    [Tooltip("เปิด = กระตุกเป็นช่อง ๆ แบบ spinner คลาสสิก, ปิด = หมุนลื่น")]
    [SerializeField] private bool _stepped = false;

    [Tooltip("แบ่งเป็นกี่ช่องต่อรอบ")]
    [SerializeField] private int _steps = 12;

    private RectTransform _rect;
    private float _angle;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        _angle = 0f;
        if (_rect != null) _rect.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        if (_rect == null) return;

        _angle += _speed * Time.unscaledDeltaTime;
        _angle = Mathf.Repeat(_angle, 360f);

        float applied = _angle;

        if (_stepped && _steps > 0)
        {
            float stepSize = 360f / _steps;
            applied = Mathf.Floor(_angle / stepSize) * stepSize;
        }

        _rect.localRotation = Quaternion.Euler(0f, 0f, applied);
    }
}
