using UnityEngine;
using Unity.Cinemachine;

public class PlayerCameraController : MonoBehaviour
{
    [Header("Cinemachine")]
    public CinemachineCamera vcamGameplay;

    [Header("Targets")]
    [SerializeField] private Transform _tppPivot;
    [SerializeField] private Transform _fppEyePosition;
    [SerializeField] private Transform _otsPivot;
    [SerializeField] private Transform _yantraPivot;

    [SerializeField] private int _yantraOffsetRotationY;

    [Header("TPP Settings")]
    [SerializeField] private Vector3 _tppOffset = new Vector3(0.6f, 0.2f, -2.5f);
    [SerializeField] private float _tppFOV = 60f;

    [Header("FPP / OTS Settings")]
    [SerializeField] private float _fppFOV = 40f;
    [SerializeField] private float _otsFOV = 50f;

    [Header("Controls")]
    [SerializeField] private float _mouseSensitivity = 0.15f;
    [SerializeField] private float _transitionSpeed = 12f;
    [SerializeField] private float _minPitch = -40f;
    [SerializeField] private float _maxPitch = 60f;

    [Header("Camera Collision")]
    [SerializeField] private LayerMask _collisionMask;
    [SerializeField] private float _cameraRadius = 0.2f;
    [SerializeField] private float _minDistance = 0.5f;

    // --- State Variables ---
    private float _pitch = 0f;
    private float _yaw = 0f;
    private Vector2 _currentLookDelta;

    private bool _isGunAiming = false;
    private bool _isYantraAiming = false;
    private bool _isFreeLookingInBook = false;
    private bool _isCutsceneMode = false;

    // 1. เพิ่มตัวแปรเช็คสถานะ Pause
    private bool _isPaused = false;

    #region Public Properties API

    // 2. เพิ่ม Property สำหรับสลับโหมด Pause พร้อมจัดการลูกศรเมาส์
    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            _isPaused = value;
            if (_isPaused)
            {
                // ถ้า Pause ให้โชว์เมาส์ และปลดล็อคให้ออกไปคลิกเมนูได้
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // ถ้าเลิก Pause ให้ซ่อนเมาส์ และล็อคไว้กลางจอเหมือนเดิม
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public bool IsCutsceneMode
    {
        get => _isCutsceneMode;
        set { _isCutsceneMode = value; if (_isCutsceneMode) { IsGunAiming = false; IsYantraAiming = false; IsFreeLookingInBook = false; } }
    }
    public bool IsGunAiming
    {
        get => _isGunAiming;
        set { if (IsYantraAiming && value == true) return; _isGunAiming = value; }
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

        if (_tppPivot != null)
        {
            _pitch = 10f;
            _yaw = _tppPivot.eulerAngles.y;
        }
    }

    public void FeedLookInput(Vector2 lookDelta)
    {
        _currentLookDelta = lookDelta;
    }

    private void LateUpdate()
    {
        if (_isCutsceneMode || _tppPivot == null || _fppEyePosition == null) return;

        Quaternion targetRotation;

        if (!IsYantraAiming)
        {
            _yaw += _currentLookDelta.x * _mouseSensitivity;
            _pitch -= _currentLookDelta.y * _mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
            targetRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
        else
        {
            targetRotation = Quaternion.LookRotation(_yantraPivot.position - transform.position);
        }

        Vector3 desiredPosition;
        float targetFOV;

        if (IsYantraAiming)
        {
            desiredPosition = _fppEyePosition.position;
            targetFOV = _fppFOV;
        }
        else if (IsGunAiming)
        {
            desiredPosition = _otsPivot != null ? _otsPivot.position : _tppPivot.position + (targetRotation * new Vector3(0.5f, 0.1f, -1f));
            targetFOV = _otsFOV;
        }
        else
        {
            Vector3 rotatedOffset = targetRotation * _tppOffset;
            desiredPosition = _tppPivot.position + rotatedOffset;
            targetFOV = _tppFOV;

            Vector3 direction = desiredPosition - _tppPivot.position;
            float desiredDistance = direction.magnitude;
            direction.Normalize();

            if (Physics.SphereCast(_tppPivot.position, _cameraRadius, direction, out RaycastHit hit, desiredDistance, _collisionMask))
            {
                float adjustedDistance = Mathf.Max(hit.distance, _minDistance);
                desiredPosition = _tppPivot.position + direction * adjustedDistance;
            }
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * _transitionSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _transitionSpeed);

        if (vcamGameplay != null)
        {
            vcamGameplay.Lens.FieldOfView = Mathf.Lerp(vcamGameplay.Lens.FieldOfView, targetFOV, Time.deltaTime * _transitionSpeed);
        }

        _currentLookDelta = Vector2.zero;
    }
}