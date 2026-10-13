using Unity.Cinemachine;
using UnityEngine;

// ใส่เฉพาะผู้เล่น บนตัวเดียวกับ PairedAnimationPlayer — ทำงานทุกครั้งที่ผู้เล่นเข้าท่าคู่ ไม่ว่าจะเป็นฝ่ายทำหรือฝ่ายโดน
// ล็อกกล้อง, รอกล้องมองตรงก่อนเริ่มท่า, สลับไปกล้อง Glory แล้วคืนค่าทั้งหมดตอนจบ
[RequireComponent(typeof(PairedAnimationPlayer))]
public class PlayerPairedCamera : MonoBehaviour
{
    [Tooltip("ล็อกการหันกล้องระหว่างเล่นท่า")]
    [SerializeField] private bool _lockLook = true;

    [Tooltip("ใช้กล้อง Glory และให้กล้องเกมเพลย์ค่อย ๆ คืนมุมก้มเงยก่อนเข้าท่า")]
    [SerializeField] private bool _useGloryCamera = true;

    [Tooltip("เวลาสูงสุดที่รอกล้องมองตรงก่อนเริ่ม Glory — 0 = เริ่มทันที")]
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

    // เรียกตอนเริ่มท่า — คืน true ถ้าต้องรอกล้องก่อนเริ่มคลิป
    public bool Prepare(PairedRequest request)
    {
        _request = request;
        _preparationElapsed = 0f;

        if (_camera == null)
            return false;

        bool prepareFirst = _useGloryCamera && _cameraPreparationTimeout > 0f &&
            _camera.vcamGlory != null && !_camera.IsCutsceneMode && !_camera.IsPaused;

        // Glory must block mouse input even if Lock Look is off.
        if (_lockLook || _useGloryCamera)
        {
            _lookWasLocked = _camera.IsLookLocked;
            _lookStateSaved = true;
            _camera.IsLookLocked = true;
        }

        if (!_useGloryCamera || _camera.vcamGlory == null)
            return prepareFirst;

        _gloryCamera = _camera.vcamGlory;
        _previousGloryPriority = _gloryCamera.Priority;
        _cutsceneWasActive = _camera.IsCutsceneMode;

        // Clear aiming now, so it cannot override RecenterPitch during preparation.
        _camera.IsGunAiming = false;
        _camera.IsYantraAiming = false;
        _camera.IsFreeLookingInBook = false;

        return prepareFirst;
    }

    // เรียกทุกเฟรมระหว่างรอ — true เมื่อกล้องมองตรงแล้ว หรือรอนานเกิน timeout
    public bool IsReady()
    {
        if (_camera == null)
            return true;

        if (_camera.IsPaused)
            return false;

        _preparationElapsed += Time.deltaTime;
        float minimumTime = _request.Animation != null ? _request.Animation.SnapDuration : 0f;
        bool pitchCentered =
            Mathf.Abs(_camera.CameraRotation.y) <= 0.05f &&
            Mathf.Abs(Mathf.DeltaAngle(0f, _camera.transform.eulerAngles.x)) <= 1f;

        // The timeout also handles Recenter Pitch On Lock being disabled in the Inspector.
        return (pitchCentered && _preparationElapsed >= minimumTime) ||
            _preparationElapsed >= _cameraPreparationTimeout;
    }

    // เรียกตอนคลิปเริ่มเล่นจริง
    public void Activate()
    {
        if (!_useGloryCamera || _camera == null || _gloryCamera == null)
            return;

        _gloryCameraChanged = true;
        _camera.SetGloryCameraActive(true);

        // Look Lock already rejects mouse input. Let the gameplay camera run its
        // existing RecenterPitch behind Glory, without clearing a prior cutscene.
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

        // Request this only after the Glory priority has been restored above.
        if (_viewModelSync != null)
            _viewModelSync.End();
        _viewModelSync = null;

        if (_lookStateSaved && _camera != null)
        {
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

        // Stay inside this player's camera hierarchy; never select another player's camera.
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
