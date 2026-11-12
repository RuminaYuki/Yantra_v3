using UnityEngine;
using UnityEngine.Events;

// ใส่บนตัวเปรต (Test Pret) — จับจังหวะเท้าทีละข้าง: เท้าลงมาเร็ว แล้วหยุด = เท้าแตะพื้น 1 ก้าว
// แต่ละก้าว จอตึ้ง! (ใกล้ = แรง / ไกล = เบา) + เรียก On Step (ไว้ใส่เสียงเท้าทีหลัง)
// ไม่ต้องใส่ Animation Event ในคลิป
public class PretFootstepShake : MonoBehaviour
{
    private class Foot
    {
        public Transform Bone;
        public Vector3 LastPosition;
        public bool HasLast;
        public bool Falling;
        public float PeakSpeed;
    }

    [Header("เท้า")]
    [Tooltip("ว่างไว้ = หาเองจาก Animator (กระดูก LeftFoot)")]
    [SerializeField] private Transform _leftFoot;

    [Tooltip("ว่างไว้ = หาเองจาก Animator (กระดูก RightFoot)")]
    [SerializeField] private Transform _rightFoot;

    [Tooltip("เท้าลงเร็วกว่านี้ (เมตร/วิ) ถึงนับว่ากำลังวางเท้า — ก้าวแล้วไม่ขึ้นใน Console ให้ลดลง")]
    [SerializeField, Min(0.05f)] private float _stepSpeed = 1.5f;

    [Tooltip("ความเร็วลงเหลือต่ำกว่านี้ (เมตร/วิ) = เท้าแตะพื้นแล้ว")]
    [SerializeField, Min(0f)] private float _stopSpeed = 0.3f;

    [Tooltip("2 ก้าวต้องห่างกันอย่างน้อยกี่วินาที กันนับซ้ำ")]
    [SerializeField, Min(0f)] private float _minStepInterval = 0.2f;

    [Header("จอตึ้ง!")]
    [Tooltip("กระชากกล้องแรงสุดกี่เมตร (ตอนเปรตอยู่ใกล้)")]
    [SerializeField, Min(0f)] private float _kick = 0.05f;

    [SerializeField, Min(0f)] private float _kickTime = 0.25f;

    [Tooltip("กล้องเอียงกี่องศาตอนแรงสุด")]
    [SerializeField, Min(0f)] private float _kickTilt = 1.5f;

    [Tooltip("ใกล้กว่านี้ (เมตร) = แรงเต็ม")]
    [SerializeField, Min(0f)] private float _fullDistance = 5f;

    [Tooltip("ไกลกว่านี้ (เมตร) = ไม่สั่นเลย")]
    [SerializeField, Min(0.01f)] private float _maxDistance = 30f;

    [Header("ทุกก้าว")]
    [Tooltip("ใส่เสียงเท้า / ฝุ่นตรงนี้ทีหลัง")]
    public UnityEvent OnStep;

    [Header("Debug")]
    [SerializeField] private bool _logSteps;

    private readonly Foot _left = new Foot();
    private readonly Foot _right = new Foot();
    private float _lastStepTime = -999f;
    private Transform _listener;

    private void Awake()
    {
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            if (_leftFoot == null) _leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (_rightFoot == null) _rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        if (_leftFoot == null) _leftFoot = FindBone("LeftFoot");
        if (_rightFoot == null) _rightFoot = FindBone("RightFoot");

        if (_leftFoot == null || _rightFoot == null)
            Debug.LogWarning("[PretFootstepShake] หากระดูกเท้าไม่เจอ — ลากกระดูกเท้าซ้าย/ขวาใส่เอง", this);

        _left.Bone = _leftFoot;
        _right.Bone = _rightFoot;
    }

    // หลังแอนิเมชันกับ Foot IK ขยับกระดูกเสร็จแล้ว
    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || Time.timeScale <= 0f)
            return;

        UpdateFoot(_left, dt);
        UpdateFoot(_right, dt);
    }

    private void UpdateFoot(Foot foot, float dt)
    {
        if (foot.Bone == null)
            return;

        Vector3 position = foot.Bone.position;
        if (!foot.HasLast)
        {
            foot.LastPosition = position;
            foot.HasLast = true;
            return;
        }

        float downSpeed = (foot.LastPosition.y - position.y) / dt;
        foot.LastPosition = position;

        if (downSpeed >= _stepSpeed)
        {
            foot.Falling = true;
            foot.PeakSpeed = Mathf.Max(foot.PeakSpeed, downSpeed);
            return;
        }

        if (!foot.Falling || downSpeed > _stopSpeed)
            return;

        float peak = foot.PeakSpeed;
        foot.Falling = false;
        foot.PeakSpeed = 0f;

        if (Time.time - _lastStepTime < _minStepInterval)
            return;

        _lastStepTime = Time.time;
        Step(position, peak);
    }

    private void Step(Vector3 footPosition, float peakSpeed)
    {
        float distance = GetDistance(footPosition);
        float strength = distance < 0f
            ? 1f
            : Mathf.Clamp01(Mathf.InverseLerp(_maxDistance, _fullDistance, distance));

        if (_logSteps)
            Debug.Log($"[PretFootstepShake] ก้าว — เท้าลงเร็วสุด {peakSpeed:0.0} ม./วิ — ห่าง {distance:0.0} ม. — แรง {strength:0.00}", this);

        if (strength > 0f && CameraShaker.Instance != null)
            CameraShaker.Instance.Kick(_kick * strength, _kickTime, _kickTilt * strength);

        OnStep?.Invoke();
    }

    // ระยะจากเท้าถึงกล้อง (หากล้องไม่เจอ = -1)
    private float GetDistance(Vector3 position)
    {
        if (_listener == null)
        {
            PlayerCameraController camera = FindFirstObjectByType<PlayerCameraController>();
            if (camera != null) _listener = camera.transform;
            else if (Camera.main != null) _listener = Camera.main.transform;
        }

        return _listener != null ? Vector3.Distance(position, _listener.position) : -1f;
    }

    private Transform FindBone(string boneName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name.EndsWith(boneName))
                return child;
        }
        return null;
    }
}