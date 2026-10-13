using SDFcl.GamePlay.Interactable;
using UnityEngine;

// ใส่ให้ทุกตัวที่เล่นท่าคู่ได้ (ผู้เล่น / ผี) บนตัวเดียวกับ PairedAnimationActor
// Component นี้ไม่รู้ว่าตัวเองเป็นฝ่ายทำหรือฝ่ายโดน — ใครเรียก Play พร้อม Partner คนนั้นคือฝ่ายทำ แล้วจะสั่ง Partner ให้เล่นตามเอง
// ตัวละครอยู่ใน State เปล่า ๆ ระหว่างเล่นท่า ใช้ PairedAnimationPlayerCondition อ่านว่ากำลังเล่น / จบแล้ว
[RequireComponent(typeof(PairedAnimationActor))]
public class PairedAnimationPlayer : MonoBehaviour
{
    private PairedAnimationActor _actor;
    private BaseLocomotion _locomotion;
    private CenterRayInteract _interactRay;
    private PlayerInteract _playerInteract;
    private PlayerPairedCamera _camera;   // มีเฉพาะผู้เล่น

    private PairedRequest _request;
    private bool _isPlaying;
    private bool _isPreparing;
    private bool _hasFinished;
    private bool _cancelNextFrame;
    private bool _locomotionLocked;
    private bool _interactRayDisabled;

    public bool IsPlaying => _isPlaying;

    // true ตั้งแต่ท่าจบจนกว่าจะเริ่มท่าใหม่ ให้ State Machine อ่านได้แม้ actor ถูก Stop ไปแล้ว
    public bool HasFinished => _hasFinished;

    private void Awake()
    {
        _actor = GetComponent<PairedAnimationActor>();
        _locomotion = GetComponent<BaseLocomotion>();
        _interactRay = GetComponentInChildren<CenterRayInteract>();
        _playerInteract = GetComponentInChildren<PlayerInteract>();
        _camera = GetComponent<PlayerPairedCamera>();
    }

    private void OnDisable()
    {
        End(false);
        _cancelNextFrame = false;
        _hasFinished = false;
    }

    // คืน false ถ้าเริ่มท่าไม่ได้ — ถ้ามาจาก Interact จะยกเลิก Interact ให้เองในเฟรมถัดไป
    public bool Play(PairedRequest request)
    {
        if (_isPlaying || _actor.IsBusy || !_actor.TryRequest(request))
        {
            RejectNextFrame(request);
            return false;
        }

        bool prepareFirst = _camera != null && _camera.Prepare(request);
        if (!_actor.Begin(prepareFirst))
        {
            if (_camera != null)
                _camera.Restore();

            RejectNextFrame(request);
            return false;
        }

        _request = _actor.CurrentRequest;
        _isPlaying = true;
        _isPreparing = prepareFirst;
        _hasFinished = false;

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

        if (!_isPreparing)
            StartPartner();

        return true;
    }

    private void Update()
    {
        if (_cancelNextFrame)
        {
            _cancelNextFrame = false;
            CancelSource();
            _request = default;
            return;
        }

        if (!_isPlaying)
            return;

        // เริ่มจาก Interact แต่มีอย่างอื่นปิด Interact ไปแล้ว — เก็บกวาดโดยไม่ยกเลิกซ้ำ
        if (_request.Source != null && _playerInteract != null && !_playerInteract.GetIsInterctable)
        {
            End(false);
            return;
        }

        _actor.Tick();

        if (_actor.IsFinished)
        {
            End(true);
            _hasFinished = true;
            return;
        }

        if (_isPreparing && _camera.IsReady())
        {
            _actor.StartPlayback();
            _isPreparing = false;
            StartPartner();
        }
    }

    // ท่าของตัวเองเริ่มจริงแล้ว — สั่งคู่ให้เข้าท่าตาม
    private void StartPartner()
    {
        if (_camera != null)
            _camera.Activate();

        PairedAnimationActor partner = _request.Partner;
        if (partner != null)
        {
            // คู่ที่ยังใช้ระบบ State (HasPairedRequest) จะรับต่อเองจาก TryRequest
            if (partner.TryGetComponent(out PairedAnimationPlayer partnerPlayer))
                partnerPlayer.Play(_request.CreatePartnerRequest());
            else
                partner.TryRequest(_request.CreatePartnerRequest());
        }

        if (IsAlive(_request.Source) && _request.Source is IPairedInteractionSource source)
            source.NotifyStarted(_request.RootPlayer);
    }

    // cancelSource = true เมื่อท่าจบเอง ให้ CancelInteraction พาผู้เล่นออกจาก PlayerInteract_State
    private void End(bool cancelSource)
    {
        if (!_isPlaying)
            return;

        if (_actor != null)
            _actor.Stop();

        if (_locomotionLocked && _locomotion != null)
            _locomotion.UnlockLocomotion(this);

        if (_interactRayDisabled && _interactRay != null)
            _interactRay.SetInteractEnabled(true);

        if (_camera != null)
            _camera.Restore();

        if (cancelSource)
            CancelSource();

        _isPlaying = false;
        _isPreparing = false;
        _request = default;
        _locomotionLocked = false;
        _interactRayDisabled = false;
    }

    // ตอนนี้ยังอยู่ใน base.Interact ซึ่งจะ SetIsInterctable(true) ทีหลัง ถ้ายกเลิกตอนนี้จะโดนเขียนทับ
    private void RejectNextFrame(PairedRequest request)
    {
        if (_isPlaying || request.Source == null)
            return;

        _request = request;
        _cancelNextFrame = true;
    }

    private void CancelSource()
    {
        if (IsAlive(_request.Source) && _request.RootPlayer != null)
            _request.Source.CancelInteraction(_request.RootPlayer, true);
    }

    private static bool IsAlive(IbaseInteractor interactor) =>
        interactor is MonoBehaviour target && target != null;
}
