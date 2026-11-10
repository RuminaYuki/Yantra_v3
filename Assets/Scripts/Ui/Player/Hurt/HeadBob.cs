using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Head bobbing ตอนเดิน — เป็น Cinemachine Extension
///
/// แปะบน GameObject เดียวกับ Cinemachine Camera (MainCamera ใต้ Rin)
/// ไม่ต้องแปะบน Player
///
/// ทำไมไม่ผ่าน ShakePivot: vcam ตาม ShakePivot ซึ่งเป็นลูกของตัวเอง
/// offset เลยถูกป้อนกลับเข้าตัวกล้องแล้วขยาย/หน่วง
/// Extension นี้แก้ที่ผลลัพธ์ท้ายสุดของกล้อง (Finalize) ไม่ขยับ Transform ใดเลย
/// เหมือนที่ CameraRecoil ทำผ่าน Recomposer
///
/// จังหวะ bob ผูกกับระยะที่เดินจริง ไม่ใช่เวลา
/// </summary>
public class HeadBob : CinemachineExtension
{
    [Header("แหล่งข้อมูล")]
    [Tooltip("เว้นว่างได้ จะหาจาก parent ให้เอง")]
    [SerializeField] private CharacterController _character;

    [Tooltip("เว้นว่างได้ จะหาจาก GameObject นี้ให้เอง — ใช้ปิด bob ตอน cutscene/pause และลดตอนเล็ง")]
    [SerializeField] private PlayerCameraController _cameraController;

    [Header("จังหวะ")]
    [Tooltip("เดินกี่เมตรถึงครบ 1 รอบ (ซ้าย+ขวา = 2 ก้าว)\nน้อย = ถี่ / มาก = ช้า\n" +
             "รอบต่อวินาที = ความเร็ว ÷ ค่านี้ (ก้าวต่อวินาที = x2)\n" +
             "เป้าหมาย ~1–1.3 รอบ/วิ (2–2.5 ก้าว/วิ) เช่น ความเร็ว 5 → ตั้ง 4")]
    [SerializeField] private float _strideLength = 4f;

    [Tooltip("ความเร็วที่ถือว่าเต็มสปีด — ตั้งให้ตรงกับ Current Speed ของ PlayerLocomotion")]
    [SerializeField] private float _referenceSpeed = 5f;

    [Tooltip("ต่ำกว่านี้ถือว่ายืนอยู่")]
    [SerializeField] private float _minSpeed = 0.3f;

    [Header("ความแรง")]
    [Tooltip("ขึ้น-ลง (เมตร)")]
    [SerializeField] private float _bounce = 0.02f;

    [Tooltip("ส่ายซ้าย-ขวา (เมตร)")]
    [SerializeField] private float _sway = 0.012f;

    [Tooltip("เอียงหัวตามก้าว (องศา)")]
    [SerializeField] private float _roll = 0.35f;

    [Tooltip("ก้ม-เงยตามก้าว (องศา)")]
    [SerializeField] private float _pitch = 0.15f;

    [Header("ความสมูท")]
    [Tooltip("เวลา (วินาที) ที่ใช้กรองความเร็ว — สูง = นุ่มขึ้นแต่ตอบสนองช้าลง")]
    [SerializeField] private float _speedSmoothTime = 0.15f;

    [Tooltip("ลอยจากพื้นไม่เกินกี่วินาทีถือว่ายังเดินอยู่ — กัน isGrounded กระพริบ")]
    [SerializeField] private float _groundedGrace = 0.15f;

    [Tooltip("ความเร็วที่ bob ค่อย ๆ เข้า/ออก")]
    [SerializeField] private float _blendSpeed = 8f;

    [Header("ตัวคูณ")]
    [Tooltip("ตอนเล็งปืน/ยันต์ ภาพควรนิ่งขึ้น")]
    [SerializeField, Range(0f, 1f)] private float _aimMultiplier = 0.25f;

    [Tooltip("ตัวคูณรวม — ต่อกับ Settings ได้ทีหลัง")]
    [SerializeField, Range(0f, 1f)] private float _globalMultiplier = 1f;

    private float _speed;
    private float _speedVelocity;
    private float _airTime;
    private float _phase;
    private float _weight;
    private float _nextSearchTime;
    private bool _warned;

    private Vector3 _positionOffset;
    private Quaternion _rotationOffset = Quaternion.identity;

    public void SetGlobalMultiplier(float value)
    {
        _globalMultiplier = Mathf.Clamp01(value);
    }

    protected override void Awake()
    {
        base.Awake();
        ResolveReferences();
    }

    // หาเองทั้งหมด ไม่ต้องลากใส่ Inspector
    // ลำดับ: ตัวแม่ → ลูกทั้งหมดของ root (เผื่ออยู่บนตัวพี่น้องของกล้อง) → ผ่าน PlayerLocomotion
    private bool ResolveReferences()
    {
        if (_character == null)
        {
            _character = GetComponentInParent<CharacterController>();

            if (_character == null)
                _character = transform.root.GetComponentInChildren<CharacterController>(true);

            if (_character == null)
            {
                PlayerLocomotion locomotion = FindFirstObjectByType<PlayerLocomotion>();
                if (locomotion != null) _character = locomotion.GetComponent<CharacterController>();
            }
        }

        if (_cameraController == null) _cameraController = GetComponent<PlayerCameraController>();
        if (_cameraController == null) _cameraController = GetComponentInParent<PlayerCameraController>();
        if (_cameraController == null) _cameraController = FindFirstObjectByType<PlayerCameraController>();

        return _character != null;
    }

    // คำนวณใน Update — callback ของ Cinemachine อาจถูกเรียกหลายครั้งต่อเฟรม เลยให้มันแค่ "ใช้ค่า"
    private void Update()
    {
        if (_character == null)
        {
            // ผู้เล่นอาจถูกสร้างทีหลัง — ลองหาใหม่ทุกครึ่งวินาที
            if (Time.unscaledTime < _nextSearchTime) return;
            _nextSearchTime = Time.unscaledTime + 0.5f;

            if (!ResolveReferences())
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[HeadBob] หา CharacterController ไม่เจอ — ลองลากใส่ช่อง Character เอง", this);
                }
                return;
            }
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 horizontalVelocity = _character.velocity;
        horizontalVelocity.y = 0f;

        // กรองความเร็วให้นิ่ง ไม่งั้นแอมพลิจูดสั่นตามความเร็วที่แกว่งทุกเฟรม
        _speed = Mathf.SmoothDamp(_speed, horizontalVelocity.magnitude, ref _speedVelocity, _speedSmoothTime);

        if (_character.isGrounded) _airTime = 0f;
        else _airTime += dt;

        bool grounded = _airTime < _groundedGrace;
        bool blocked = _cameraController != null
                       && (_cameraController.IsPaused || _cameraController.IsCutsceneMode);

        bool walking = grounded && !blocked && _speed >= _minSpeed;

        float target = walking ? Mathf.Clamp01(_speed / Mathf.Max(0.01f, _referenceSpeed)) : 0f;

        if (_cameraController != null && (_cameraController.IsGunAiming || _cameraController.IsYantraAiming))
            target *= _aimMultiplier;

        _weight = Mathf.Lerp(_weight, target, 1f - Mathf.Exp(-_blendSpeed * dt));

        // phase เดินตามระยะทาง หยุดเดินก็หยุดนับ ไม่รีเซ็ต (weight ไต่จาก 0 เลยไม่เห็นรอยต่อ)
        if (walking)
        {
            _phase += _speed * dt / Mathf.Max(0.1f, _strideLength) * Mathf.PI * 2f;
            if (_phase > Mathf.PI * 2f) _phase -= Mathf.PI * 2f;
        }

        float k = _weight * _globalMultiplier;
        float side = Mathf.Sin(_phase);        // 1 ครั้งต่อรอบ → ส่าย/เอียงซ้ายขวา
        float step = Mathf.Sin(_phase * 2f);   // 2 ครั้งต่อรอบ → ขึ้นลงทุกก้าว

        _positionOffset = new Vector3(side * _sway, step * _bounce, 0f) * k;
        _rotationOffset = Quaternion.Euler(step * _pitch * k, 0f, -side * _roll * k);
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize) return;

        // offset อยู่ในแกนของกล้อง (ขึ้น/ขวาของจอ) แปลงเป็นโลกก่อนบวก
        state.PositionCorrection += state.GetCorrectedOrientation() * _positionOffset;
        state.OrientationCorrection = state.OrientationCorrection * _rotationOffset;
    }
}