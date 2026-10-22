using UnityEngine;
using Unity.Cinemachine;

// ดีดกล้องผ่าน Cinemachine Recomposer ของ vcam เกมเพลย์ ไม่ขยับ Transform ตัวไหนเลย
// เลยไม่ทับ CameraShaker และไม่ย้อนกลับไปกวน PlayerCameraController
public class CameraRecoil : MonoBehaviour
{
    [System.Serializable]
    public class Profile
    {
        [Tooltip("องศาที่กล้องเงยขึ้น")] public float Pitch = 4f;
        [Tooltip("ส่ายซ้าย-ขวาแบบสุ่ม (องศา)")] public float Yaw = 0.8f;
        [Tooltip("เอียงหัวแบบสุ่ม (องศา) ตัวนี้ช่วยให้รู้สึกแรงมาก")] public float Roll = 1.5f;
        [Tooltip("ยิ่งสูง ดีดขึ้นยิ่งไว")] public float Snap = 30f;
        [Tooltip("ยิ่งต่ำ กล้องไหลกลับยิ่งช้า = รู้สึกปืนหนัก")] public float Return = 6f;
        [Tooltip("สั่นถี่ซ้อนอีกชั้น (องศา)")] public float Shake = 0.3f;
        [Tooltip("สั่นนานกี่วินาที")] public float ShakeTime = 0.15f;
    }

    public static CameraRecoil Instance { get; private set; }

    [Tooltip("เว้นว่างได้ จะใช้ Vcam Gameplay ของ PlayerCameraController")]
    [SerializeField] private CinemachineCamera _vcam;

    [SerializeField] private Profile _normal = new Profile();
    [SerializeField]
    private Profile _ultimate = new Profile
    {
        Pitch = 8f,
        Yaw = 1.5f,
        Roll = 3f,
        Snap = 35f,
        Return = 4f,
        Shake = 0.8f,
        ShakeTime = 0.3f
    };

    [Tooltip("ตัวคูณตอนเล็งปืน — COD เล็งแล้วดีดน้อยลง")]
    [SerializeField, Range(0f, 1f)] private float _aimMultiplier = 0.7f;

    [Tooltip("ตัวคูณรวม เอาไว้ต่อกับ Settings ทีหลัง")]
    [SerializeField, Range(0f, 1f)] private float _globalMultiplier = 1f;

    [SerializeField] private float _shakeFrequency = 25f;
    [SerializeField] private float _maxPitch = 20f;

    private PlayerCameraController _controller;
    private CinemachineRecomposer _recomposer;
    private Vector3 _targetRot;
    private Vector3 _currentRot;
    private float _snap = 30f;
    private float _return = 6f;
    private float _shakeAmount;
    private float _shakeTime;
    private float _shakeLeft;
    private float _seed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CameraRecoil] มีมากกว่า 1 ตัวในฉาก ตัวนี้จะไม่ทำงาน", this);
            enabled = false;
            return;
        }
        Instance = this;
        _seed = Random.value * 100f;

        _controller = GetComponentInParent<PlayerCameraController>();
        if (_controller == null)
            _controller = FindFirstObjectByType<PlayerCameraController>();

        if (_vcam == null && _controller != null)
            _vcam = _controller.vcamGameplay;

        if (_vcam == null)
        {
            Debug.LogWarning("[CameraRecoil] หา Cinemachine Camera ของเกมเพลย์ไม่เจอ ลากใส่ช่อง Vcam เอง", this);
            enabled = false;
            return;
        }

        _recomposer = _vcam.GetComponent<CinemachineRecomposer>();
        if (_recomposer == null)
        {
            _recomposer = _vcam.gameObject.AddComponent<CinemachineRecomposer>();
            _recomposer.ApplyAfter = CinemachineCore.Stage.Finalize;
            _recomposer.ZoomScale = 1f;
            _recomposer.FollowAttachment = 1f;
            _recomposer.LookAtAttachment = 1f;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
        StopImmediate();
    }

    public void Fire(bool ultimate)
    {
        Fire(ultimate ? _ultimate : _normal);
    }

    public void Fire(Profile p)
    {
        if (p == null || !isActiveAndEnabled) return;
        if (_controller != null && (_controller.IsCutsceneMode || _controller.IsPaused)) return;

        float k = _globalMultiplier;
        if (_controller != null && _controller.IsGunAiming) k *= _aimMultiplier;

        _targetRot += new Vector3(
            -p.Pitch,
            Random.Range(-p.Yaw, p.Yaw),
            Random.Range(-p.Roll, p.Roll)) * k;
        _targetRot.x = Mathf.Max(_targetRot.x, -_maxPitch);

        _snap = p.Snap;
        _return = p.Return;

        float shake = p.Shake * k;
        float remaining = _shakeLeft > 0f ? _shakeAmount * (_shakeLeft / _shakeTime) : 0f;
        if (shake >= remaining && p.ShakeTime > 0f)
        {
            _shakeAmount = shake;
            _shakeTime = p.ShakeTime;
            _shakeLeft = p.ShakeTime;
        }
    }

    public void StopImmediate()
    {
        _targetRot = _currentRot = Vector3.zero;
        _shakeLeft = 0f;
        Apply(Vector3.zero);
    }

    // ใช้เวลาจริง กล้องยังดีดต่อตอน hitstop
    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        _targetRot = Vector3.Lerp(_targetRot, Vector3.zero, Smooth(_return, dt));
        _currentRot = Vector3.Lerp(_currentRot, _targetRot, Smooth(_snap, dt));

        Vector3 shake = Vector3.zero;
        if (_shakeLeft > 0f)
        {
            _shakeLeft = Mathf.Max(0f, _shakeLeft - dt);
            float fade = _shakeLeft / _shakeTime;
            float t = Time.unscaledTime * _shakeFrequency;
            shake = new Vector3(Noise(_seed, t), Noise(_seed + 17f, t), Noise(_seed + 31f, t))
                    * (_shakeAmount * fade * fade);
        }

        Apply(_currentRot + shake);
    }

    private void Apply(Vector3 rot)
    {
        if (_recomposer == null) return;
        _recomposer.Tilt = rot.x;
        _recomposer.Pan = rot.y;
        _recomposer.Dutch = rot.z;
    }

    [ContextMenu("ทดสอบ: ยิงปกติ")]
    private void TestNormal() => Fire(false);

    [ContextMenu("ทดสอบ: Ultimate")]
    private void TestUltimate() => Fire(true);

    private static float Smooth(float speed, float dt)
    {
        return 1f - Mathf.Exp(-speed * dt);
    }

    private static float Noise(float seed, float time)
    {
        return Mathf.PerlinNoise(seed, time) * 2f - 1f;
    }
}