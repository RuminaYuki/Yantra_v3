using UnityEngine;

/// <summary>
/// สั่นกล้องแบบ trauma-based
///
/// ⚠️ ต้องแปะบน GameObject เปล่าที่คั่นระหว่างตัวหมุนกล้องกับกล้องจริง:
///
///   Player
///    └── CameraHolder   ← สคริปต์เมาส์หมุนตัวนี้
///         └── ShakePivot ← แปะตัวนี้ (localPos/localRot = 0)
///              └── Main Camera
///
/// ถ้าแปะบนกล้องตรง ๆ มันจะสู้กับสคริปต์หมุนกล้อง แล้วกระตุก
/// </summary>
public class CameraShaker : MonoBehaviour
{
    public static CameraShaker Instance { get; private set; }

    [Header("ความแรง")]
    [Tooltip("ระยะขยับสูงสุด (เมตร) — FPP ใช้น้อย ๆ ก็พอ")]
    [SerializeField] private float _maxPositionOffset = 0.10f;

    [Tooltip("องศาเอียงสูงสุด — ตัวนี้ให้ความรู้สึกมากกว่าการขยับ")]
    [SerializeField] private float _maxRotationOffset = 4f;

    [Header("ลักษณะการสั่น")]
    [Tooltip("ความถี่ — สูง = สั่นถี่แบบโดนกระแทก, ต่ำ = โยกแบบแผ่นดินไหว")]
    [SerializeField] private float _frequency = 22f;

    [Tooltip("ความเร็วที่ trauma ลดลงต่อวินาที — 1.6 ≈ สั่นประมาณ 0.6 วิ")]
    [SerializeField] private float _traumaDecay = 1.6f;

    [Tooltip("ยิ่งสูง การสั่นเบา ๆ ยิ่งแทบไม่รู้สึก ทำให้เฉพาะการโดนหนักเด่นขึ้น")]
    [SerializeField, Range(1f, 3f)] private float _traumaExponent = 2f;

    [Header("ออปชัน")]
    [Tooltip("ติ๊กถ้าจะให้สั่นต่อได้ตอน hitstop (timeScale ต่ำ)")]
    [SerializeField] private bool _useUnscaledTime = false;

    [Tooltip("ตัวคูณรวม — เอาไว้ต่อกับ setting ให้คนเล่นปิดได้ทีหลัง")]
    [SerializeField, Range(0f, 1f)] private float _globalMultiplier = 1f;

    private float _trauma;
    private float _seed;
    private Vector3 _basePosition;
    private Quaternion _baseRotation;

    /// <summary>ค่าปัจจุบัน 0..1 เผื่อระบบอื่นอยากรู้</summary>
    public float Trauma => _trauma;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        _seed = Random.value * 100f;
        _basePosition = transform.localPosition;
        _baseRotation = transform.localRotation;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// เติมความสั่น — เรียกตอนโดนตี
    /// 0.2 = โดนเบา, 0.4 = โดนปกติ, 0.7 = โดนหนัก, 1.0 = ระเบิด
    /// </summary>
    public void AddTrauma(float amount)
    {
        _trauma = Mathf.Clamp01(_trauma + amount * _globalMultiplier);
    }

    /// <summary>หยุดสั่นทันที — ใช้ตอนเข้า cutscene หรือตาย</summary>
    public void StopImmediate()
    {
        _trauma = 0f;
        transform.localPosition = _basePosition;
        transform.localRotation = _baseRotation;
    }

    public void SetGlobalMultiplier(float value)
    {
        _globalMultiplier = Mathf.Clamp01(value);
    }

    // LateUpdate เพื่อให้ทำงานหลังสคริปต์หมุนกล้องเสร็จแล้ว
    private void LateUpdate()
    {
        float deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float time      = _useUnscaledTime ? Time.unscaledTime      : Time.time;

        if (_trauma <= 0f)
        {
            transform.localPosition = _basePosition;
            transform.localRotation = _baseRotation;
            return;
        }

        // ยกกำลังทำให้การสั่นเบา ๆ จางหายเร็ว เหลือแต่จังหวะหนัก ๆ ที่เด่น
        float shake = Mathf.Pow(_trauma, _traumaExponent);
        float t = time * _frequency;

        float nx = Noise(_seed,        t);
        float ny = Noise(_seed + 17f,  t);
        float nz = Noise(_seed + 31f,  t);

        transform.localPosition = _basePosition
            + new Vector3(nx, ny, 0f) * (_maxPositionOffset * shake);

        transform.localRotation = _baseRotation * Quaternion.Euler(
            ny * _maxRotationOffset * shake,
            nx * _maxRotationOffset * shake,
            nz * _maxRotationOffset * shake);

        _trauma = Mathf.Max(0f, _trauma - _traumaDecay * deltaTime);
    }

    /// <summary>Perlin ให้ค่า 0..1 — แปลงเป็น -1..1 และได้ความต่อเนื่องที่ Random ให้ไม่ได้</summary>
    private static float Noise(float seed, float time)
    {
        return Mathf.PerlinNoise(seed, time) * 2f - 1f;
    }
}
