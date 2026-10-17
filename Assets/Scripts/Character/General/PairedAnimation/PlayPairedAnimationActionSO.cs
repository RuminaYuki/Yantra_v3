using SDFcl.GamePlay.Interactable;
using Unity.Cinemachine;
using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "PlayPairedAnimation_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Paired Animation/Play Paired Animation")]
public class PlayPairedAnimationActionSO : StateActionSO
{
    [Tooltip("ล็อกการหันกล้องระหว่างเล่นท่า (มีผลเฉพาะผู้เล่น)")]
    [SerializeField] private bool _lockLook = true;

    [Tooltip("ใช้กล้อง Glory และให้กล้องเกมเพลย์ค่อย ๆ คืนมุมก้มเงยก่อนเข้าท่า")]
    [SerializeField] private bool _useGloryCamera = false;

    [Tooltip("เวลาสูงสุดที่รอกล้องมองตรงก่อนเริ่ม Glory — 0 = เริ่มทันที")]
    [SerializeField, Min(0f)] private float _cameraPreparationTimeout = 1f;

    [Tooltip("ให้กล้องแขน FPS ตามมุมกล้อง Glory แล้วคืนค่าเดิมหลังกล้องกลับเข้าเกมเพลย์")]
    [SerializeField] private bool _syncGloryViewModelCamera = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new PlayPairedAnimationAction(_lockLook, _useGloryCamera, _cameraPreparationTimeout, _syncGloryViewModelCamera);
    }
}

public class PlayPairedAnimationAction : StateAction
{
    private readonly bool _lockLook;
    private readonly bool _useGloryCamera;
    private readonly float _cameraPreparationTimeout;
    private readonly bool _syncGloryViewModelCamera;

    private AnchorReference<PairedAnimationActor> _actor;
    private BaseLocomotion _locomotion;
    private CenterRayInteract _interactRay;
    private PlayerCameraController _camera;

    private bool _hasStarted;
    private PairedRequest _request;
    private bool _locomotionLocked;
    private bool _interactRayDisabled;
    private bool _lookStateSaved;
    private bool _lookWasLocked;
    private bool _gloryCameraChanged;
    private bool _cutsceneWasActive;
    private CinemachineCamera _gloryCamera;
    private PrioritySettings _previousGloryPriority;
    private bool _isPreparing;
    private float _preparationElapsed;
    private PairedAnimationViewModelCameraSync _viewModelSync;

    // Keep the existing constructor so other paired actions remain compatible.
    public PlayPairedAnimationAction(bool lockLook, bool useGloryCamera)
        : this(lockLook, useGloryCamera, 1f)
    {
    }

    public PlayPairedAnimationAction(bool lockLook, bool useGloryCamera, float cameraPreparationTimeout)
        : this(lockLook, useGloryCamera, cameraPreparationTimeout, true)
    {
    }

    public PlayPairedAnimationAction(bool lockLook, bool useGloryCamera, float cameraPreparationTimeout, bool syncGloryViewModelCamera)
    {
        _lockLook = lockLook;
        _useGloryCamera = useGloryCamera;
        _cameraPreparationTimeout = Mathf.Max(0f, cameraPreparationTimeout);
        _syncGloryViewModelCamera = syncGloryViewModelCamera;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _actor = new AnchorReference<PairedAnimationActor>(stateMachine, this);
        stateMachine.Owner.TryGetComponent(out _locomotion);
        stateMachine.Owner.TryGetComponent(out _interactRay);

        if (stateMachine.Owner.TryGetComponent(out PlayerCameraInput cameraInput))
            _camera = cameraInput.cameraController;
    }

    public override void OnStateEnter()
    {
        _hasStarted = false;
        _request = default;
        _isPreparing = false;
        _preparationElapsed = 0f;

        if (!_actor.TryGet(out PairedAnimationActor actor))
            return;

        // A rejected request must not activate a camera or lock player controls.
        bool prepareFirst = _useGloryCamera && _cameraPreparationTimeout > 0f &&
            _camera != null && _camera.vcamGlory != null &&
            !_camera.IsCutsceneMode && !_camera.IsPaused;
        // Claim the request now, but delay both characters' clips until the camera is ready.
        _hasStarted = actor.Begin(prepareFirst);
        if (!_hasStarted)
            return;

        _request = actor.CurrentRequest;

        if (_locomotion != null)
        {
            _locomotion.LockLocomotion(this);
            _locomotionLocked = true;
        }

        if (_interactRay != null)
        {
            _interactRay.SetInteractEnabled(false);
            _interactRayDisabled = true;
        }

        PrepareCamera();

        _isPreparing = prepareFirst;
        if (!_isPreparing)
            StartPartnerAndCamera();
    }

    private void StartPartnerAndCamera()
    {
        ActivateGloryCamera();

        if (_request.Partner != null)
            _request.Partner.TryRequest(_request.CreatePartnerRequest());

        if (IsAlive(_request.Source) && _request.Source is IPairedInteractionSource source)
            source.NotifyStarted(_request.RootPlayer);
    }

    private void PrepareCamera()
    {
        if (_camera == null)
            return;

        // Glory must block mouse input even if the action's Lock Look checkbox is off.
        if (_lockLook || _useGloryCamera)
        {
            _lookWasLocked = _camera.IsLookLocked;
            _lookStateSaved = true;
            _camera.IsLookLocked = true;
        }

        if (!_useGloryCamera || _camera.vcamGlory == null)
            return;

        _gloryCamera = _camera.vcamGlory;
        _previousGloryPriority = _gloryCamera.Priority;
        _cutsceneWasActive = _camera.IsCutsceneMode;

        // Clear aiming now, so it cannot override RecenterPitch during preparation.
        _camera.IsGunAiming = false;
        _camera.IsYantraAiming = false;
        _camera.IsFreeLookingInBook = false;
    }

    private void ActivateGloryCamera()
    {
        if (!_useGloryCamera || _camera == null || _gloryCamera == null)
            return;

        _gloryCameraChanged = true;

        // Use the existing API to select Glory and clear aiming.
        _camera.SetGloryCameraActive(true);

        // Look Lock already rejects mouse input. Let the gameplay camera run its
        // existing RecenterPitch behind Glory, without clearing a prior cutscene.
        _camera.IsCutsceneMode = _cutsceneWasActive;
        StartViewModelCameraSync();
    }

    private void StartViewModelCameraSync()
    {
        if (!_syncGloryViewModelCamera || _request.Type != PairedActionType.Glory ||
            !_actor.TryGet(out PairedAnimationActor actor))
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

        if (sync.Begin(actor, brain, viewCamera))
            _viewModelSync = sync;
    }

    public override void OnUpdate()
    {
        if (!_hasStarted || !_actor.TryGet(out PairedAnimationActor actor))
            return;

        if (_isPreparing && _camera != null && _camera.IsPaused)
            return;

        actor.Tick();
        if (!_isPreparing)
            return;

        _preparationElapsed += Time.deltaTime;
        float minimumTime = _request.Animation != null ? _request.Animation.SnapDuration : 0f;
        bool pitchCentered = _camera != null &&
            Mathf.Abs(_camera.CameraRotation.y) <= 0.05f &&
            Mathf.Abs(Mathf.DeltaAngle(0f, _camera.transform.eulerAngles.x)) <= 1f;

        // The timeout also handles Recenter Pitch On Lock being disabled in the Inspector.
        if ((pitchCentered && _preparationElapsed >= minimumTime) ||
            _preparationElapsed >= _cameraPreparationTimeout)
        {
            actor.StartPlayback();
            _isPreparing = false;
            StartPartnerAndCamera();
        }
    }

    public override void OnStateExit()
    {
        if (_actor.TryGet(out PairedAnimationActor actor))
            actor.Stop();

        if (_locomotionLocked && _locomotion != null)
            _locomotion.UnlockLocomotion(this);

        if (_interactRayDisabled && _interactRay != null)
            _interactRay.SetInteractEnabled(true);

        RestoreCamera();

        if (_hasStarted && IsAlive(_request.Source))
            _request.Source.CancelInteraction(_request.RootPlayer, true);

        _hasStarted = false;
        _request = default;
        _locomotionLocked = false;
        _interactRayDisabled = false;
        _isPreparing = false;
        _preparationElapsed = 0f;
    }

    private void RestoreCamera()
    {
        if (_gloryCameraChanged)
        {
            if (_gloryCamera != null)
                _gloryCamera.Priority = _previousGloryPriority;

            if (_camera != null)
                _camera.IsCutsceneMode = _cutsceneWasActive;
        }

        // The helper follows the return blend before restoring the original arm-camera pose.
        // Request this only after the Glory priority has been restored above.
        if (_viewModelSync != null)
            _viewModelSync.End();
        _viewModelSync = null;

        if (_lookStateSaved && _camera != null)
        {
            _camera.FeedLookInput(Vector2.zero);
            _camera.IsLookLocked = _lookWasLocked;
        }

        _lookStateSaved = false;
        _gloryCameraChanged = false;
        _gloryCamera = null;
    }

    private static bool IsAlive(IbaseInteractor interactor) =>
        interactor is MonoBehaviour target && target != null;
}
