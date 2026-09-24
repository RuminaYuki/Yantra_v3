using UnityEngine;

/// <summary>
/// สั่นกล้อง มี 2 แบบ
///   AddTrauma — Perlin noise ใช้ตอนเราโดนตี
///   Kick      — แบบ lead: สุ่มจุดในรัศมีแล้วใช้ sine ดึงกลับกลาง ใช้ตอนเราตีโดน
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

    [Header("Kick (สั่นตอนตีโดน)")]
    [Tooltip("กี่วินาทีสุ่มจุดใหม่ 1 ครั้ง — ต่ำ = สั่นถี่ / สูง = กระตุกทีละจังหวะ")]
    [SerializeField] private float _kickStepTime = 0.035f;

    [Header("ออปชัน")]
    [Tooltip("ติ๊กถ้าจะให้สั่นต่อได้ตอน hitstop (timeScale ต่ำ)\nใช้กับ AddTrauma เท่านั้น — Kick สั่นระหว่าง hitstop เสมอ")]
    [SerializeField] private bool _useUnscaledTime = false;

    [Tooltip("ตัวคูณรวม — เอาไว้ต่อกับ setting ให้คนเล่นปิดได้ทีหลัง")]
    [SerializeField, Range(0f, 1f)] private float _globalMultiplier = 1f;

    private float _trauma;
    private float _seed;
    private Vector3 _basePosition;
    private Quaternion _baseRotation;

    private float _kickRadius;
    private float _kickTilt;
    private float _kickDuration;
    private float _kickElapsed;
    private float _kickStepElapsed;
    private Vector3 _kickTarget;
    private Vector3 _kickTargetTilt;

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

    /// <summary>
    /// กระตุกกล้องแบบ lead — เรียกตอนตีโดน
    /// radius = ระยะ (เมตร), duration = นานกี่วิ, tilt = เอียงกี่องศา (0 = ไม่เอียง)
    /// </summary>
    public void Kick(float radius, float duration, float tilt = 0f)
    {
        if (duration <= 0f) return;

        // ต่อยรัวๆ: เริ่มใหม่เฉพาะตอนแรงกว่าที่เหลืออยู่ ไม่เอามาบวกกันจนกล้องกระเด็น
        float remaining = _kickElapsed < _kickDuration
            ? _kickRadius * (1f - _kickElapsed / _kickDuration)
            : 0f;

        float newRadius = radius * _globalMultiplier;
        if (newRadius < remaining) return;

        _kickRadius = newRadius;
        _kickTilt = tilt * _globalMultiplier;
        _kickDuration = duration;
        _kickElapsed = 0f;

        NewKickStep();
    }

    /// <summary>หยุดสั่นทันที — ใช้ตอนเข้า cutscene หรือตาย</summary>
    public void StopImmediate()
    {
        _trauma = 0f;
        _kickElapsed = _kickDuration;
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
        UpdateTrauma(out Vector3 traumaPos, out Quaternion traumaRot);
        UpdateKick(out Vector3 kickPos, out Quaternion kickRot);

        // สองแบบสั่นพร้อมกันได้ เช่นตีโดนผีจังหวะเดียวกับโดนผีตี
        transform.localPosition = _basePosition + traumaPos + kickPos;
        transform.localRotation = _baseRotation * traumaRot * kickRot;
    }

    private void UpdateTrauma(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (_trauma <= 0f) return;

        float deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float time = _useUnscaledTime ? Time.unscaledTime : Time.time;

        // ยกกำลังทำให้การสั่นเบา ๆ จางหายเร็ว เหลือแต่จังหวะหนัก ๆ ที่เด่น
        float shake = Mathf.Pow(_trauma, _traumaExponent);
        float t = time * _frequency;

        float nx = Noise(_seed, t);
        float ny = Noise(_seed + 17f, t);
        float nz = Noise(_seed + 31f, t);

        position = new Vector3(nx, ny, 0f) * (_maxPositionOffset * shake);

        rotation = Quaternion.Euler(
            ny * _maxRotationOffset * shake,
            nx * _maxRotationOffset * shake,
            nz * _maxRotationOffset * shake);

        _trauma = Mathf.Max(0f, _trauma - _traumaDecay * deltaTime);
    }

    // ใช้เวลาจริงเสมอ กล้องจะได้กระตุกต่อระหว่าง hitstop — ภาพนิ่งแต่กล้องสั่น = ฟีลหนักสุด
    private void UpdateKick(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (_kickElapsed >= _kickDuration) return;

        float dt = Time.unscaledDeltaTime;
        _kickElapsed += dt;
        _kickStepElapsed += dt;

        float stepTime = Mathf.Max(0.01f, _kickStepTime);
        if (_kickStepElapsed >= stepTime) NewKickStep();

        // sine ดึงกลับกลาง: ต้นช่วงอยู่ที่จุดสุ่ม (sin 0° = 0) ปลายช่วงถึงกลางพอดี (sin 90° = 1)
        // ออกตัวเร็วแล้วค่อยๆ ช้าลงตอนใกล้กลาง
        float back = Mathf.Sin(Mathf.Clamp01(_kickStepElapsed / stepTime) * Mathf.PI * 0.5f);

        position = Vector3.Lerp(_kickTarget, Vector3.zero, back);
        rotation = Quaternion.Euler(Vector3.Lerp(_kickTargetTilt, Vector3.zero, back));
    }

    private void NewKickStep()
    {
        _kickStepElapsed = 0f;

        // ใกล้หมดเวลา รัศมียิ่งเล็ก = ค่อยๆ สงบ ไม่หยุดกึก
        float fade = 1f - Mathf.Clamp01(_kickElapsed / _kickDuration);

        // สุ่มแค่แกน X/Y (บนจอ) — แกน Z คือเดินหน้าถอยหลัง ใน FPS แทบมองไม่เห็นและดันแขนเข้าหน้ากล้อง
        Vector2 point = Random.insideUnitCircle * (_kickRadius * fade);
        _kickTarget = new Vector3(point.x, point.y, 0f);

        Vector2 tilt = Random.insideUnitCircle * (_kickTilt * fade);
        _kickTargetTilt = new Vector3(tilt.y, tilt.x, 0f);
    }

    /// <summary>Perlin ให้ค่า 0..1 — แปลงเป็น -1..1 และได้ความต่อเนื่องที่ Random ให้ไม่ได้</summary>
    private static float Noise(float seed, float time)
    {
        return Mathf.PerlinNoise(seed, time) * 2f - 1f;
    }
}