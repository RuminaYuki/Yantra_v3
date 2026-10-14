using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(PairedAnimationPlayer))]
public class PlayerPairedCamera : MonoBehaviour
{
    [Tooltip("ล็อกการหันกล้องและบังคับ Snap มุมมองเข้าหาทิศเริ่มต้นระหว่างเล่นท่า")]
    [SerializeField] private bool _lockLook = true;

    [Tooltip("ใช้กล้อง Glory และให้กล้องเกมเพลย์ค่อย ๆ คืนมุมก้มเงยก่อนเข้าท่า (เฉพาะท่า Glory)")]
    [SerializeField] private bool _useGloryCamera = true;

    [Tooltip("เวลาสูงสุดที่รอกล้องมองตรงก่อนเริ่มท่า — 0 = เริ่มทันที")]
    [SerializeField, Min(0f)] private float _cameraPreparationTimeout = 1f;

    [Tooltip("ให้กล้องแขน FPS ตามมุมกล้อง Glory แล้วคืนค่าเดิมหลังกล้องกลับเข้าเกมเพลย์")]
    [SerializeField] private bool _syncGloryViewModelCamera = true;

    private PairedAnimationActor _actor;
    private PlayerCameraController _camera;

    private PairedRequest _request;
    private float _preparationElapsed;
    private bool _lookStateSaved;
    private bool _lookWasLocked;
    private bool _gloryCameraChanged;
    private bool _cutsceneWasActive;
    private CinemachineCamera _gloryCamera;
    private PrioritySettings _previousGloryPriority;
    private PairedAnimationViewModelCameraSync _viewModelSync;

    private void Awake()
    {
        _actor = GetComponent<PairedAnimationActor>();

        PlayerCameraInput cameraInput = GetComponentInChildren<PlayerCameraInput>();
        if (cameraInput != null)
            _camera = cameraInput.cameraController;
    }

    // เรียกตอนเริ่มท่า — คืน true ถ้าต้องรอกล้องและตัวละคร Snap เข้าจุดเริ่มก่อนเริ่มคลิป
    public bool Prepare(PairedRequest request)
    {
        _request = request;
        _preparationElapsed = 0f;

        if (_camera == null)
            return false;

        bool isGlory = request.Type == PairedActionType.Glory;
        bool useGloryCam = isGlory && _useGloryCamera && _camera.vcamGlory != null;
        float snapDuration = request.Animation != null ? request.Animation.SnapDuration : 0f;

        // รอ Prepare ก่อนเริ่มเล่นแอนิเมชันเสมอถ้ามีตั้งเวลา SnapDuration ไว้ หรือใช้กล้อง Glory
        bool prepareFirst = !_camera.IsCutsceneMode && !_camera.IsPaused &&
            (snapDuration > 0f || (useGloryCam && _cameraPreparationTimeout > 0f));

        // ล็อกเมาส์และสั่ง Snap มุมกล้อง (Yaw) ให้หันไปตามทิศ StartRotation ของท่า (ทั้ง Climb และ Glory)
        if (_lockLook || useGloryCam)
        {
            _lookWasLocked = _camera.IsLookLocked;
            _lookStateSaved = true;
            _camera.IsLookLocked = true;

            // คำนวณมุม Yaw เป้าหมายจากทิศทางเริ่มต้นของท่า
            Vector3 flatForward = request.StartRotation * Vector3.forward;
            flatForward.y = 0f;
            float targetYaw = flatForward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(flatForward.normalized, Vector3.up).eulerAngles.y
                : transform.eulerAngles.y;

            _camera.SetLookTargetYaw(targetYaw, snapDuration);
        }

        // ยกเลิกการเล็งทุกชนิดเพื่อไม่ให้ไปกวนการหมุนกล้องกลับตรงกลาง
        _camera.IsGunAiming = false;
        _camera.IsYantraAiming = false;
        _camera.IsFreeLookingInBook = false;

        if (!useGloryCam)
            return prepareFirst;

        _gloryCamera = _camera.vcamGlory;
        _previousGloryPriority = _gloryCamera.Priority;
        _cutsceneWasActive = _camera.IsCutsceneMode;

        return prepareFirst;
    }

    // เรียกทุกเฟรมระหว่างรอ — true เมื่อกล้องหันตรงทั้ง Pitch และ Yaw และครบเวลา SnapDuration แล้ว
    public bool IsReady()
    {
        if (_camera == null)
            return true;

        if (_camera.IsPaused)
            return false;

        _preparationElapsed += Time.deltaTime;
        float minimumTime = _request.Animation != null ? _request.Animation.SnapDuration : 0f;

        bool pitchCentered =
            Mathf.Abs(_camera.CameraRotation.y) <= 0.5f &&
            Mathf.Abs(Mathf.DeltaAngle(0f, _camera.transform.eulerAngles.x)) <= 1.5f;

        bool yawCentered = _camera.IsYawAligned(1.5f);

        float timeout = Mathf.Max(_cameraPreparationTimeout, minimumTime);

        return (pitchCentered && yawCentered && _preparationElapsed >= minimumTime) ||
            _preparationElapsed >= timeout;
    }

    // เรียกตอนคลิปเริ่มเล่นจริง
    public void Activate()
    {
        if (_camera == null)
            return;

        // ซิงค์มุมกล้องให้ตรงกับ StartRotation เป๊ะๆ ตอนเริ่มคลิป
        Vector3 flatForward = _request.StartRotation * Vector3.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude > 0.0001f)
        {
            float targetYaw = Quaternion.LookRotation(flatForward.normalized, Vector3.up).eulerAngles.y;
            _camera.SetLookTargetYaw(targetYaw, 0f);
        }

        if (_request.Type != PairedActionType.Glory || !_useGloryCamera || _gloryCamera == null)
            return;

        _gloryCameraChanged = true;
        _camera.SetGloryCameraActive(true);
        _camera.IsCutsceneMode = _cutsceneWasActive;
        StartViewModelCameraSync();
    }

    public void Restore()
    {
        if (_gloryCameraChanged)
        {
            if (_gloryCamera != null)
                _gloryCamera.Priority = _previousGloryPriority;

            if (_camera != null)
                _camera.IsCutsceneMode = _cutsceneWasActive;
        }

        if (_viewModelSync != null)
            _viewModelSync.End();
        _viewModelSync = null;

        if (_lookStateSaved && _camera != null)
        {
            // ก่อนปลดล็อก อัปเดตมุม Yaw ของกล้องให้ตรงกับทิศที่ตัวละครหันอยู่หลังจบ Root Motion
            _camera.SetLookTargetYaw(transform.eulerAngles.y, 0f);
            _camera.ClearLookTargetYaw();
            _camera.FeedLookInput(Vector2.zero);
            _camera.IsLookLocked = _lookWasLocked;
        }

        _request = default;
        _lookStateSaved = false;
        _gloryCameraChanged = false;
        _gloryCamera = null;
    }

    private void StartViewModelCameraSync()
    {
        if (!_syncGloryViewModelCamera || _request.Type != PairedActionType.Glory)
            return;

        Camera viewCamera = null;
        foreach (ViewModelRenderer renderer in _camera.GetComponentsInChildren<ViewModelRenderer>(true))
        {
            Camera candidate = renderer.Camera != null ? renderer.Camera : renderer.GetComponent<Camera>();
            if (!renderer.isActiveAndEnabled || candidate == null || !candidate.isActiveAndEnabled)
                continue;

            if (viewCamera != null)
            {
                Debug.LogWarning("[PairedAnimation] พบกล้องแขนหลายตัว — ข้ามการตามกล้อง Glory", _camera);
                return;
            }
            viewCamera = candidate;
        }

        if (viewCamera == null)
            return;

        CinemachineBrain brain = null;
        foreach (CinemachineBrain candidate in _camera.GetComponentsInChildren<CinemachineBrain>(true))
        {
            Camera output = candidate.OutputCamera;
            if (!candidate.isActiveAndEnabled || output == null || !output.isActiveAndEnabled ||
                output == viewCamera || output.targetTexture != null)
                continue;

            if (brain != null)
            {
                Debug.LogWarning("[PairedAnimation] พบกล้องหลักหลายตัว — ข้ามการตามกล้อง Glory", _camera);
                return;
            }
            brain = candidate;
        }

        if (brain == null)
            return;

        PairedAnimationViewModelCameraSync sync = viewCamera.GetComponent<PairedAnimationViewModelCameraSync>();
        if (sync == null)
            sync = viewCamera.gameObject.AddComponent<PairedAnimationViewModelCameraSync>();

        if (sync.Begin(_actor, brain, viewCamera))
            _viewModelSync = sync;
    }
}