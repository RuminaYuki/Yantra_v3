using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Turns the player's camera to look at a target while this state runs (PlayerCameraController.StartForcedLook),
// and gives the mouse back when the state ends. The player's look can't be turned by rotating the body:
// PlayerCameraController keeps its own yaw / pitch and sets the camera from them every frame.
// Self Anchor = the player (or anything under it); the camera controller is found under its root.
[UseSelfAnchor]
[CreateAssetMenu(
    fileName = "ForceCameraLook_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Camera/Force Camera Look")]
public class ForceCameraLookActionSO : StateActionSO
{
    [Tooltip("Required. What the camera looks at.")]
    [SerializeField] private AnchorIDSO _targetAnchor;

    [Tooltip("Added to the target's position, e.g. (0, 1.6, 0) to look at a face instead of the feet.")]
    [SerializeField] private Vector3 _offset;

    [Tooltip("Seconds to turn to the target. Small = snaps.")]
    [SerializeField, Min(0.01f)] private float _turnTime = 0.3f;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new ForceCameraLookAction(_targetAnchor, _offset, _turnTime);
    }
}

public class ForceCameraLookAction : StateAction
{
    // Long enough to never run out by itself: the state's exit stops it.
    private const float Duration = 999f;

    private readonly AnchorIDSO _targetAnchor;
    private readonly Vector3 _offset;
    private readonly float _turnTime;

    private AnchorReference<Transform> _self;
    private AnchorReference<Transform> _target;
    private PlayerCameraController _camera;

    // Only stop the look this action started, never one from a QTE / cutscene.
    private bool _looking;

    // This action moved the locked camera's target yaw (see OnUpdate).
    private bool _droveLockedYaw;

    public ForceCameraLookAction(AnchorIDSO targetAnchor, Vector3 offset, float turnTime)
    {
        _targetAnchor = targetAnchor;
        _offset = offset;
        _turnTime = turnTime;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (_targetAnchor == null)
            Debug.LogError("ForceCameraLookAction has no target AnchorIDSO assigned.", stateMachine.Owner);

        _self = new AnchorReference<Transform>(stateMachine, this);
        _target = new AnchorReference<Transform>(stateMachine, _targetAnchor, this);
    }

    public override void OnStateEnter()
    {
        _looking = false;
        _droveLockedYaw = false;
        if (_targetAnchor == null || !_target.TryGet(out Transform target))
            return;

        PlayerCameraController camera = FindCamera();
        if (camera == null)
            return;

        // False while in Glory / cutscene or paused: then the camera isn't turned.
        // While the look is locked it's set but not used (see OnUpdate); it takes over once unlocked.
        _looking = camera.StartForcedLook(target, _offset, Duration, _turnTime);
    }

    // A locked look (e.g. PlayerPairedCamera during a grab) beats the forced look in
    // PlayerCameraController.LateUpdate: it turns the camera back to its own target yaw every frame.
    // So while locked, keep moving that target yaw onto the target instead. Left / right only:
    // a locked camera brings its pitch back to level by itself.
    public override void OnUpdate()
    {
        if (_targetAnchor == null || !_target.TryGet(out Transform target))
            return;

        // Found here too: the player anchor may only link after the state started.
        if (FindCamera() == null || !_camera.IsLookLocked)
            return;

        Vector3 direction = target.position + _offset - _camera.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        float yaw = Quaternion.LookRotation(direction, Vector3.up).eulerAngles.y;

        // snapDuration > 0: turns there over about _turnTime instead of snapping.
        _camera.SetLookTargetYaw(yaw, _turnTime);
        _droveLockedYaw = true;
    }

    public override void OnStateExit()
    {
        if (_looking && _camera != null && _camera.IsForcedLooking)
            _camera.StopForcedLook();

        // Still locked: whoever locked it (PlayerPairedCamera.Restore) resets the target yaw when it unlocks.
        // Already unlocked: clear the target yaw this action left behind.
        if (_droveLockedYaw && _camera != null && !_camera.IsLookLocked)
            _camera.ClearLookTargetYaw();

        _looking = false;
        _droveLockedYaw = false;
    }

    // Under the player's root (the camera rig is a child of Rin(Player)), else any in the scene, like BlockQTE.
    private PlayerCameraController FindCamera()
    {
        if (_camera != null)
            return _camera;

        if (_self.TryGet(out Transform self))
            _camera = self.root.GetComponentInChildren<PlayerCameraController>(true);

        if (_camera == null)
            _camera = Object.FindFirstObjectByType<PlayerCameraController>();

        return _camera;
    }
}
