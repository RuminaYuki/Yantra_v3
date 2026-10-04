using SDFcl.GamePlay.Interactable;
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

    [Tooltip("สลับไปใช้กล้อง Glory (vcamGlory) ที่หมุนตามกระดูกหัว")]
    [SerializeField] private bool _useGloryCamera = false; // <-- ส่วนที่เพิ่มเข้ามา

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new PlayPairedAnimationAction(_lockLook, _useGloryCamera);
    }
}

// ทุกอย่างของท่าคู่วิ่งผ่าน Action นี้ — เข้า State: เริ่มท่า / ทุกเฟรม: เลื่อนตัว / ออก State: จบท่า
public class PlayPairedAnimationAction : StateAction
{
    private readonly bool _lockLook;
    private readonly bool _useGloryCamera; // <-- ส่วนที่เพิ่มเข้ามา

    private PairedAnimationActor _actor;
    private BaseLocomotion _locomotion;
    private CenterRayInteract _interactRay;
    private PlayerCameraController _camera;
    private bool _lookWasLocked;

    private bool _hasStarted;
    private PairedRequest _request;

    public PlayPairedAnimationAction(bool lockLook, bool useGloryCamera)
    {
        _lockLook = lockLook;
        _useGloryCamera = useGloryCamera;
    }

    public override void Awake(StateMachine stateMachine)
    {
        if (!stateMachine.TryGetRequired(out _actor, this))
            return;

        // ไม่บังคับ — ถ้าตัวไหนไม่มี Locomotion บน GameObject เดียวกัน ท่ายังเล่นได้ แค่ไม่ได้ล็อกการเดิน
        stateMachine.Owner.TryGetComponent(out _locomotion);

        // สองตัวนี้มีแค่ผู้เล่น ผีไม่มีก็ข้ามไป
        stateMachine.Owner.TryGetComponent(out _interactRay);

        if (stateMachine.Owner.TryGetComponent(out PlayerCameraInput cameraInput))
            _camera = cameraInput.cameraController;
    }

    public override void OnStateEnter()
    {
        if (_locomotion != null)
            _locomotion.LockLocomotion(this);

        // ปิดการเล็ง interact ระหว่างท่า กันกดซ้ำและซ่อน highlight
        if (_interactRay != null)
            _interactRay.SetInteractEnabled(false);

        if (_lockLook && _camera != null)
        {
            _lookWasLocked = _camera.IsLookLocked;
            _camera.IsLookLocked = true;
        }

        // <-- สั่งเปิดกล้อง Glory ตอนเริ่มท่า
        if (_useGloryCamera && _camera != null)
        {
            _camera.SetGloryCameraActive(true);
        }

        _hasStarted = _actor.Begin();
        if (!_hasStarted)
            return;

        _request = _actor.CurrentRequest;

        // State นี้เป็นคนสั่งคู่ของท่า (เช่น ผีตอน Glory) ให้เข้าท่าตาม
        if (_request.Partner != null)
            _request.Partner.TryRequest(_request.CreatePartnerRequest());

        // ยิง OnInteract ตอนท่าเริ่มจริง — ถ้ากดแล้ว State ไม่รับ (เช่น ถือปืนอยู่) จะไม่มีอะไรเกิดขึ้นเลย
        if (IsAlive(_request.Source) && _request.Source is IPairedInteractionSource source)
            source.NotifyStarted(_request.RootPlayer);
    }

    public override void OnUpdate()
    {
        _actor.Tick();
    }

    public override void OnStateExit()
    {
        _actor.Stop();

        if (_locomotion != null)
            _locomotion.UnlockLocomotion(this);

        if (_interactRay != null)
            _interactRay.SetInteractEnabled(true);

        // คืนค่าเดิม ไม่ใช่ปลดล็อกทิ้ง เผื่อระบบอื่นล็อกกล้องไว้ก่อนแล้ว
        if (_lockLook && _camera != null)
            _camera.IsLookLocked = _lookWasLocked;

        // <-- สั่งปิดกล้อง Glory คืนสู่สภาพเดินยิงปกติ ตอนจบท่า
        if (_useGloryCamera && _camera != null)
        {
            _camera.SetGloryCameraActive(false);
        }

        // ยิง OnEndInteract — ทั้งตอนจบท่าเองและตอนโดน State อื่นแทรก
        // force = true เพราะท่าจบจริงแล้ว ต้องปิด Interact เสมอ ไม่ให้ตัวกันยกเลิกของ BaseInteractor มาขวาง
        if (_hasStarted && IsAlive(_request.Source))
            _request.Source.CancelInteraction(_request.RootPlayer, true);

        _hasStarted = false;
        _request = default;
    }

    private static bool IsAlive(IbaseInteractor interactor) =>
        interactor is MonoBehaviour target && target != null;
}