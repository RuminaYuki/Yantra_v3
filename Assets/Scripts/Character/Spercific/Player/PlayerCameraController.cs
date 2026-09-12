using UnityEngine;
using Unity.Cinemachine;

public class PlayerCameraController : MonoBehaviour
{
    [Header("Cinemachine")]
    public CinemachineCamera vcamGameplay;

    [Header("Targets")]
    [Tooltip("จุดตากล้อง — กล้องจะอยู่ตรงนี้ตลอด")]
    [SerializeField] private Transform _fppEyePosition;

    [Tooltip("จุดเล็งปืน ถ้าเว้นว่างจะใช้ตำแหน่งตาแล้วซูมด้วย FOV แทน")]
    [SerializeField] private Transform _otsPivot;

    [Tooltip("จุดที่กล้องหันไปหาตอนเล็งยันต์")]
    [SerializeField] private Transform _yantraPivot;

    [Header("FOV")]
    [SerializeField] private float _fppFOV = 60f;
    [SerializeField] private float _otsFOV = 50f;

    [Tooltip("FOV ตอนเล็งยันต์")]
    [SerializeField] private float _yantraFOV = 40f;

    [Header("Controls")]
    [Tooltip("ค่าพื้นฐาน — จะถูกคูณกับค่าใน Settings อีกที")]
    [SerializeField] private float _mouseSensitivity = 0.15f;

    [Tooltip("ติ๊กถ้า FeedLookInput รับค่าจากอนาล็อกจอย (ค่าต่อเนื่อง) แทนเมาส์ (ค่า delta)")]
    [SerializeField] private bool _inputIsAnalog = false;

    [SerializeField] private float _minPitch = -40f;
    [SerializeField] private float _maxPitch = 60f;

    [Header("Smoothing")]
    [Tooltip("ความเร็วขยับตำแหน่ง — FPS ควรสูง ๆ ไม่งั้นกล้องจะตามหัวไม่ทัน")]
    [SerializeField] private float _positionSpeed = 30f;

    [Tooltip("ความเร็วหมุน — ต่ำเกินจะรู้สึกหน่วง")]
    [SerializeField] private float _rotationSpeed = 25f;

    [Tooltip("ความเร็วเปลี่ยน FOV ตอนเล็ง")]
    [SerializeField] private float _fovSpeed = 12f;

    // --- State Variables ---
    private float _pitch = 0f;
    private float _yaw = 0f;
    private Vector2 _currentLookDelta;

    private bool _isGunAiming = false;
    private bool _isYantraAiming = false;
    private bool _isFreeLookingInBook = false;
    private bool _isCutsceneMode = false;
    private bool _isPaused = false;

    #region Public Properties API

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            _isPaused = value;
            if (_isPaused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public bool IsCutsceneMode
    {
        get => _isCutsceneMode;
        set
        {
            _isCutsceneMode = value;
            if (_isCutsceneMode)
            {
                IsGunAiming = false;
                IsYantraAiming = false;
                IsFreeLookingInBook = false;
            }
        }
    }

    public bool IsGunAiming
    {
        get => _isGunAiming;
        set { if (IsYantraAiming && value) return; _isGunAiming = value; }
    }

    public bool IsYantraAiming
    {
        get => _isYantraAiming;
        set { _isYantraAiming = value; if (_isYantraAiming) IsGunAiming = false; }
    }

    public bool IsFreeLookingInBook
    {
        get => _isFreeLookingInBook;
        set => _isFreeLookingInBook = value;
    }

    public Vector2 CameraRotation => new Vector2(_yaw, _pitch);
    public float MinPitch => _minPitch;
    public float MaxPitch => _maxPitch;

    #endregion

    private void Start()
    {
        if (Application.isPlaying)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (_fppEyePosition != null)
        {
            _pitch = 0f;
            _yaw = _fppEyePosition.eulerAngles.y;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }

    public void FeedLookInput(Vector2 lookDelta)
    {
        _currentLookDelta = lookDelta;
    }

    private void LateUpdate()
    {

        if (_isPaused || _isCutsceneMode || _fppEyePosition == null)
        {
            _currentLookDelta = Vector2.zero;
            return;
        }

        float dt = Time.deltaTime;

        UpdateRotation(dt);
        UpdatePosition(dt);
        UpdateFOV(dt);

        _currentLookDelta = Vector2.zero;
    }

    // ---------- Rotation ----------

    private void UpdateRotation(float dt)
    {
        Quaternion targetRotation;

        if (_isYantraAiming && _yantraPivot != null)
        {
            Vector3 direction = _yantraPivot.position - transform.position;

            if (direction.sqrMagnitude > 0.0001f)
            {
                targetRotation = Quaternion.LookRotation(direction);

                Vector3 euler = targetRotation.eulerAngles;
                _yaw = euler.y;
                _pitch = NormalizeAngle(euler.x);
            }
            else
            {
                targetRotation = transform.rotation;
            }
        }
        else
        {
            float sensitivity = _mouseSensitivity * GameSettings.MouseSensitivity;

            // จอยส่งค่าต่อเนื่อง ต้องคูณ deltaTime ไม่งั้นความเร็วหมุนผูกกับ FPS
            if (_inputIsAnalog) sensitivity *= dt * 60f;

            float pitchDelta = _currentLookDelta.y * sensitivity;
            if (GameSettings.InvertY) pitchDelta = -pitchDelta;

            _yaw += _currentLookDelta.x * sensitivity;
            _pitch = Mathf.Clamp(_pitch - pitchDelta, _minPitch, _maxPitch);

            targetRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRotation, Smooth(_rotationSpeed, dt));
    }

    // ---------- Position ----------

    private void UpdatePosition(float dt)
    {

        Vector3 desiredPosition = _fppEyePosition.position;

        if (_isGunAiming && !_isYantraAiming && _otsPivot != null)
            desiredPosition = _otsPivot.position;

        transform.position = Vector3.Lerp(
            transform.position, desiredPosition, Smooth(_positionSpeed, dt));
    }

    // ---------- FOV ----------

    private void UpdateFOV(float dt)
    {
        if (vcamGameplay == null) return;

        float targetFOV = _fppFOV;
        if (_isYantraAiming) targetFOV = _yantraFOV;
        else if (_isGunAiming) targetFOV = _otsFOV;

        vcamGameplay.Lens.FieldOfView = Mathf.Lerp(
            vcamGameplay.Lens.FieldOfView, targetFOV, Smooth(_fovSpeed, dt));
    }

    // ---------- Helper ----------

    private static float Smooth(float speed, float deltaTime)
    {
        return 1f - Mathf.Exp(-speed * deltaTime);
    }

    /// <summary>แปลง 0..360 เป็น -180..180</summary>
    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }
}