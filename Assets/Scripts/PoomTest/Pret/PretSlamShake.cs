using UnityEngine;
using UnityEngine.Events;

// ใส่บนตัวเปรต (Test Pret) — จับจังหวะมือตบ: มือพุ่งลงเร็ว แล้วหยุด = ตบ 1 ที
// ตบแต่ละครั้ง จอตึ้ง! (ใกล้ = แรง / ไกล = เบา) + เรียก On Slam (ไว้ใส่เสียง / ฝุ่นทีหลัง)
// ตอนเปรตกำลังเดินไม่นับ กันแขนแกว่งตอนเดินโดนนับเป็นตบ
// มี 2 โหมด: จับเองจากความเร็วมือ (ค่าเริ่มต้น) / ติ๊ก Use Animation Event = รอ event "PretSlam" ผ่าน PretAnimEvents
public class PretSlamShake : MonoBehaviour
{
    private class Hand
    {
        public Transform Bone;
        public Vector3 LastPosition;
        public bool HasLast;
        public bool Falling;
        public float PeakSpeed;
    }

    [Header("โหมด")]
    [Tooltip("ติ๊ก = ไม่จับมือเอง รอ Animation Event \"PretSlam\" ในท่าตบ (ต้องมี PretAnimEvents ที่ตัวที่มี Animator)\n" +
             "ไม่ติ๊ก = จับเองจากความเร็วมือแบบเดิม")]
    [SerializeField] private bool _useAnimationEvent;

    [Header("มือ")]
    [Tooltip("ว่างไว้ = หาเองจาก Animator (กระดูก LeftHand)")]
    [SerializeField] private Transform _leftHand;

    [Tooltip("ว่างไว้ = หาเองจาก Animator (กระดูก RightHand)")]
    [SerializeField] private Transform _rightHand;

    [Tooltip("มือพุ่งลงเร็วกว่านี้ (เมตร/วิ) ถึงนับว่ากำลังตบ — ไม่ขึ้นใน Console เลยให้ลดลง")]
    [SerializeField, Min(0.1f)] private float _slamSpeed = 4f;

    [Tooltip("ความเร็วลงเหลือต่ำกว่านี้ (เมตร/วิ) = มือกระแทกแล้ว")]
    [SerializeField, Min(0f)] private float _stopSpeed = 0.8f;

    [Tooltip("ตัวเปรตเคลื่อนที่เร็วกว่านี้ (เมตร/วิ) = กำลังเดิน ไม่นับ")]
    [SerializeField, Min(0f)] private float _ignoreWhenMovingFaster = 0.5f;

    [Tooltip("ตบ 2 ครั้งต้องห่างกันอย่างน้อยกี่วินาที (สองมือตบพร้อมกัน = สั่นทีเดียว)")]
    [SerializeField, Min(0f)] private float _minSlamInterval = 0.3f;

    [Header("จอตึ้ง!")]
    [Tooltip("กระชากกล้องแรงสุดกี่เมตร (ตอนเปรตอยู่ใกล้)")]
    [SerializeField, Min(0f)] private float _kick = 0.12f;

    [SerializeField, Min(0f)] private float _kickTime = 0.4f;

    [Tooltip("กล้องเอียงกี่องศาตอนแรงสุด")]
    [SerializeField, Min(0f)] private float _kickTilt = 3f;

    [Tooltip("ใกล้กว่านี้ (เมตร) = แรงเต็ม")]
    [SerializeField, Min(0f)] private float _fullDistance = 10f;

    [Tooltip("ไกลกว่านี้ (เมตร) = ไม่สั่นเลย")]
    [SerializeField, Min(0.01f)] private float _maxDistance = 100f;

    [Header("ทุกครั้งที่ตบ")]
    [Tooltip("ใส่เสียงตบ / ฝุ่นตรงนี้ทีหลัง")]
    public UnityEvent OnSlam;

    [Header("Debug")]
    [SerializeField] private bool _logSlams;

    private readonly Hand _left = new Hand();
    private readonly Hand _right = new Hand();
    private Vector3 _lastBodyPosition;
    private bool _hasBodyPosition;
    private float _lastSlamTime = -999f;
    private Transform _listener;

    private void Awake()
    {
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            if (_leftHand == null) _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (_rightHand == null) _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (_leftHand == null) _leftHand = FindBone("LeftHand");
        if (_rightHand == null) _rightHand = FindBone("RightHand");

        if (_leftHand == null || _rightHand == null)
            Debug.LogWarning("[PretSlamShake] หากระดูกมือไม่เจอ — ลากกระดูกมือซ้าย/ขวาใส่เอง", this);

        _left.Bone = _leftHand;
        _right.Bone = _rightHand;

        if (_useAnimationEvent && GetComponentInChildren<PretAnimEvents>(true) == null)
            Debug.LogWarning("[PretSlamShake] ติ๊ก Use Animation Event แต่ยังไม่มี PretAnimEvents — แปะที่ตัวที่มี Animator", this);
    }

    /// <summary>เรียกจาก PretAnimEvents ตอนท่าตบถึงเฟรมมือกระแทกพื้น — ใช้มือที่ต่ำที่สุด</summary>
    public void SlamFromEvent()
    {
        if (Time.time - _lastSlamTime < _minSlamInterval)
            return;

        _lastSlamTime = Time.time;
        Slam(LowestHandPosition(), 0f);
    }

    private Vector3 LowestHandPosition()
    {
        if (_leftHand != null && _rightHand != null)
            return _leftHand.position.y <= _rightHand.position.y ? _leftHand.position : _rightHand.position;
        if (_leftHand != null) return _leftHand.position;
        if (_rightHand != null) return _rightHand.position;
        return transform.position;
    }

    // หลังแอนิเมชันกับ Hand IK ขยับกระดูกเสร็จแล้ว
    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (_useAnimationEvent || dt <= 0f || Time.timeScale <= 0f)
            return;

        Vector3 body = transform.position;
        float bodySpeed = _hasBodyPosition ? (body - _lastBodyPosition).magnitude / dt : 0f;
        _lastBodyPosition = body;
        _hasBodyPosition = true;

        bool walking = bodySpeed > _ignoreWhenMovingFaster;
        UpdateHand(_left, dt, walking);
        UpdateHand(_right, dt, walking);
    }

    private void UpdateHand(Hand hand, float dt, bool walking)
    {
        if (hand.Bone == null)
            return;

        Vector3 position = hand.Bone.position;
        if (!hand.HasLast)
        {
            hand.LastPosition = position;
            hand.HasLast = true;
            return;
        }

        float downSpeed = (hand.LastPosition.y - position.y) / dt;
        hand.LastPosition = position;

        if (walking)
        {
            hand.Falling = false;
            hand.PeakSpeed = 0f;
            return;
        }

        if (downSpeed >= _slamSpeed)
        {
            hand.Falling = true;
            hand.PeakSpeed = Mathf.Max(hand.PeakSpeed, downSpeed);
            return;
        }

        if (!hand.Falling || downSpeed > _stopSpeed)
            return;

        float peak = hand.PeakSpeed;
        hand.Falling = false;
        hand.PeakSpeed = 0f;

        if (Time.time - _lastSlamTime < _minSlamInterval)
            return;

        _lastSlamTime = Time.time;
        Slam(position, peak);
    }

    private void Slam(Vector3 handPosition, float peakSpeed)
    {
        float distance = GetDistance(handPosition);
        float strength = distance < 0f
            ? 1f
            : Mathf.Clamp01(Mathf.InverseLerp(_maxDistance, _fullDistance, distance));

        if (_logSlams)
            Debug.Log(peakSpeed > 0f
                ? $"[PretSlamShake] ตบ! — มือลงเร็วสุด {peakSpeed:0.0} ม./วิ — ห่าง {distance:0.0} ม. — แรง {strength:0.00}"
                : $"[PretSlamShake] ตบ! (Animation Event) — ห่าง {distance:0.0} ม. — แรง {strength:0.00}", this);

        if (strength > 0f && CameraShaker.Instance != null)
            CameraShaker.Instance.Kick(_kick * strength, _kickTime, _kickTilt * strength);

        OnSlam?.Invoke();
    }

    // ระยะจากมือถึงกล้อง (หากล้องไม่เจอ = -1)
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
