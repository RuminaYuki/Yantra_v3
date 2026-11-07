using UnityEngine;
using UnityEngine.UI;

// เป้ากลางจอ — วางใต้ HUDScreen จะซ่อนเองตอนเปิดเมนู / หน้า Main Menu / Glory
// จุดกลาง = จุดที่กระสุนไปจริง อยู่นิ่งตลอด / วงรอบจุดบอกว่าพร้อมยิง: วงบานแล้วหุบเข้าหาจุด
public class AimReticleUI : MonoBehaviour
{
    [Header("ชิ้น UI")]
    [SerializeField] private Image _dot;
    [SerializeField] private Image _ring;

    [Header("จุดกลาง")]
    [Tooltip("ความชัดของจุดตอนวงบาน — 0 = ซ่อน")]
    [SerializeField, Range(0f, 1f)] private float _dotIdleAlpha = 0.4f;
    [SerializeField, Range(0f, 1f)] private float _dotAimAlpha = 1f;

    [Header("วง")]
    [Tooltip("ความชัดของวงตอนบาน — 0 = ซ่อน / ใส่ค่าถ้าอยากเห็นวงบานค้างไว้ตลอด")]
    [SerializeField, Range(0f, 1f)] private float _ringIdleAlpha = 0f;

    [Tooltip("ตอนบาน วงใหญ่กว่าตอนหุบกี่เท่า")]
    [SerializeField, Min(1f)] private float _openScale = 2.5f;

    [Tooltip("วงหุบเข้าหาจุดภายในกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _closeTime = 0.18f;

    [Tooltip("วงบานออกภายในกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _openTime = 0.12f;

    [Header("วงหุบตอนไหน")]
    [Tooltip("ติ๊ก = ถือปืนอยู่วงหุบ (ตอนนี้เกมยังไม่มีโหมดเล็ง)\nถ้ามีโหมดเล็งปืนแล้ว เอาติ๊กออก วงจะหุบตอนกดเล็งแทน")]
    [SerializeField] private bool _closeWhenHoldingGun = true;

    [Tooltip("ชื่อ Object ปืนในมือที่โดนเปิด/ปิดตอนชัก/เก็บปืน — ชื่อเดียวกับใน WeaponHUD")]
    [SerializeField] private string _heldObjectName = "R_GUN";

    [SerializeField] private bool _gunAim = true;
    [SerializeField] private bool _yantraAim = true;

    private PlayerCameraController _camera;
    private GunController _gun;
    private GameObject _heldObject;
    private float _retryTimer;
    private float _aim;

    private void OnEnable()
    {
        _aim = 0f;
        _retryTimer = 1f;
        Apply();
    }

    private void Update()
    {
        bool closed = ShouldClose();
        float duration = closed ? _closeTime : _openTime;
        _aim = Mathf.MoveTowards(_aim, closed ? 1f : 0f, Time.unscaledDeltaTime / duration);
        Apply();
    }

    private void Apply()
    {
        // ออกตัวเร็วแล้วค่อยๆ เข้าที่
        float t = 1f - (1f - _aim) * (1f - _aim);

        if (_ring != null)
        {
            _ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(_openScale, 1f, t);
            SetAlpha(_ring, Mathf.Lerp(_ringIdleAlpha, 1f, t));
        }

        if (_dot != null)
            SetAlpha(_dot, Mathf.Lerp(_dotIdleAlpha, _dotAimAlpha, t));
    }

    private bool ShouldClose()
    {
        if (!FindPlayer())
            return false;

        if (_camera.IsCutsceneMode || _camera.IsPaused)
            return false;

        if (_gunAim && _camera.IsGunAiming)
            return true;
        if (_yantraAim && _camera.IsYantraAiming)
            return true;

        return _closeWhenHoldingGun && IsHoldingGun();
    }

    // UIRoot อยู่ข้ามซีน แต่ผู้เล่นเกิดใหม่ทุกซีน — ของเดิมหายเมื่อไหร่ก็หาใหม่
    private bool FindPlayer()
    {
        if (_camera != null)
            return true;

        _retryTimer += Time.unscaledDeltaTime;
        if (_retryTimer < 0.5f)
            return false;
        _retryTimer = 0f;

        _camera = FindAnyObjectByType<PlayerCameraController>();
        if (_camera == null)
            return false;

        // หาปืนจากตัวผู้เล่นคนเดียวกับกล้อง — ปืนที่วางบนพื้นมี Object ชื่อเดียวกัน
        _gun = _camera.transform.root.GetComponentInChildren<GunController>(true);
        if (_gun == null)
            _gun = FindAnyObjectByType<GunController>(FindObjectsInactive.Include);

        _heldObject = null;
        if (_gun != null && !string.IsNullOrEmpty(_heldObjectName))
        {
            foreach (Transform child in _gun.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == _heldObjectName)
                {
                    _heldObject = child.gameObject;
                    break;
                }
            }
        }

        return true;
    }

    private bool IsHoldingGun()
    {
        if (_heldObject != null)
            return _heldObject.activeInHierarchy;
        return _gun != null && _gun.isActiveAndEnabled;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}