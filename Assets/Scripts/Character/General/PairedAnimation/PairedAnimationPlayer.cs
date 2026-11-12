using SDFcl.GamePlay.Interactable;
using UnityEngine;

// ใส่ให้ทุกตัวที่เล่นท่าคู่ได้ (ผู้เล่น / ผี) บนตัวเดียวกับ PairedAnimationActor
// Component นี้ไม่รู้ว่าตัวเองเป็นฝ่ายทำหรือฝ่ายโดน — ใครเรียก Play พร้อม Partner คนนั้นคือฝ่ายทำ แล้วจะสั่ง Partner ให้เล่นตามเอง
// ตัวละครอยู่ใน State เปล่า ๆ ระหว่างเล่นท่า ใช้ PairedAnimationPlayerCondition อ่านว่ากำลังเล่น / จบแล้ว
// ไม่ล็อกการเดิน — State ของทีม (LockMovement) จัดการเอง
[RequireComponent(typeof(PairedAnimationActor))]
public class PairedAnimationPlayer : MonoBehaviour
{
    private PairedAnimationActor _actor;
    private CenterRayInteract _interactRay;
    private PlayerInteract _playerInteract;
    private PlayerPairedCamera _camera;   // มีเฉพาะผู้เล่น

    private PairedRequest _request;
    private PairedAnimationPlayer _partnerPlayer;   // คู่ที่สั่งให้หันเข้าหารอไว้แล้ว
    private bool _isPlaying;
    private bool _isPreparing;
    private bool _waitingForLeader;
    private bool _hasFinished;
    private bool _cancelNextFrame;
    private bool _interactRayDisabled;
    private bool _interactLost;

    public bool IsPlaying => _isPlaying;

    // true ตั้งแต่ท่าจบจนกว่าจะเริ่มท่าใหม่ ให้ State Machine อ่านได้แม้ actor ถูก Stop ไปแล้ว
    public bool HasFinished => _hasFinished;

    // ท่า Loop เล่นอยู่จริง (ผ่านช่วง Snap / หันกล้องแล้ว)
    public bool IsLooping => _isPlaying && !_isPreparing && _actor.IsLooping;

    public PairedAnimationActor Actor => _actor;

    // ปล่อยท่า Loop แล้วเล่นท่าจบต่อ (ว่าง = จบทันที)
    public void EndLoop(PairedAnimationId finishAnimation)
    {
        if (_isPlaying)
            _actor.EndLoop(finishAnimation);
    }

    private void Awake()
    {
        _actor = GetComponent<PairedAnimationActor>();
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
    public bool Play(PairedRequest request) => StartPaired(request, false);

    // waitForLeader = เป็นฝ่ายโดน: หันเข้าหาก่อนแล้วรอให้ฝ่ายทำสั่งเริ่มท่าพร้อมกัน
    private bool StartPaired(PairedRequest request, bool waitForLeader)
    {
        if (_isPlaying || _actor.IsBusy || !_actor.TryRequest(request))
        {
            RejectNextFrame(request);
            return false;
        }

        bool prepareFirst = waitForLeader || (_camera != null && _camera.Prepare(request));
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
        _waitingForLeader = waitForLeader;
        _hasFinished = false;
        _interactLost = false;
        _partnerPlayer = null;

        // ปุ่ม E รัว ๆ ของ QTE คือปุ่ม Interact — ปิด ray ไว้ กันกดไปโดนประตู / ของอื่นระหว่างท่า
        if (_interactRay != null)
        {
            _interactRay.SetInteractEnabled(false);
            _interactRayDisabled = true;
        }

        if (!_isPreparing)
            StartPartner();
        else if (!waitForLeader)
            PreparePartner();

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
        // ยกเว้น Glory / Grab: กดแล้วต้องเล่นจนจบ
        // (Glory ไม่งั้นผีค้างไม่ตาย / Grab ระหว่างโดนแขนลาก State ของแขนอาจปิด Interact ไป)
        if (!_interactLost && _request.Source != null && _playerInteract != null && !_playerInteract.GetIsInterctable)
        {
            if (_request.Type != PairedActionType.Glory && _request.Type != PairedActionType.Grab)
            {
                End(false);
                return;
            }

            _interactLost = true;
            Debug.LogWarning($"[PairedAnimationPlayer] Interact โดนปิดระหว่าง {_request.Type} — เล่นต่อจนจบ", this);
        }

        _actor.Tick();

        if (_actor.IsFinished)
        {
            End(!_interactLost);
            _hasFinished = true;
            return;
        }

        // ฝ่ายโดนไม่ต้องดูกล้อง รอฝ่ายทำสั่งเริ่มอย่างเดียว
        if (_isPreparing && !_waitingForLeader && _camera.IsReady() && PartnerTurned())
        {
            _actor.StartPlayback();
            _isPreparing = false;
            StartPartner();
        }
    }

    // กด E แล้วให้คู่เริ่มหันเข้าหาเลยระหว่างรอกล้อง ไม่ต้องวาร์ปหันทีเดียวตอนท่าเริ่ม
    // ความเร็วหันตั้งที่ Snap Duration ในท่าของฝ่ายโดน (เช่น VictimGlory)
    private void PreparePartner()
    {
        PairedAnimationActor partner = _request.Partner;
        if (partner == null || !partner.TryGetComponent(out PairedAnimationPlayer partnerPlayer))
            return;

        if (partnerPlayer.StartPaired(_request.CreatePartnerRequest(), true))
            _partnerPlayer = partnerPlayer;
    }

    private bool PartnerTurned() =>
        _partnerPlayer == null || !_partnerPlayer._waitingForLeader || _partnerPlayer._actor.IsSnapDone;

    // ท่าของตัวเองเริ่มจริงแล้ว — สั่งคู่ให้เข้าท่าตาม
    private void StartPartner()
    {
        if (_camera != null)
            _camera.Activate();

        if (_partnerPlayer != null && _partnerPlayer._waitingForLeader)
        {
            _partnerPlayer.StartFromLeader();
        }
        else
        {
            PairedAnimationActor partner = _request.Partner;
            if (partner != null)
            {
                // คู่ที่ยังใช้ระบบ State (HasPairedRequest) จะรับต่อเองจาก TryRequest
                if (partner.TryGetComponent(out PairedAnimationPlayer partnerPlayer))
                    partnerPlayer.Play(_request.CreatePartnerRequest());
                else
                    partner.TryRequest(_request.CreatePartnerRequest());
            }
        }
        _partnerPlayer = null;

        if (IsAlive(_request.Source) && _request.Source is IPairedInteractionSource source)
            source.NotifyStarted(_request.RootPlayer);
    }

    private void StartFromLeader()
    {
        if (!_isPlaying || !_waitingForLeader)
            return;

        _waitingForLeader = false;
        _isPreparing = false;
        _actor.StartPlayback();
    }

    // cancelSource = true เมื่อท่าจบเอง ให้ CancelInteraction พาผู้เล่นออกจาก PlayerInteract_State
    private void End(bool cancelSource)
    {
        if (!_isPlaying)
            return;

        // ยกเลิกก่อนท่าเริ่ม — คู่ที่หันรออยู่ต้องเลิกรอด้วย ไม่งั้นค้าง
        if (_partnerPlayer != null && _partnerPlayer._waitingForLeader)
            _partnerPlayer.End(false);
        _partnerPlayer = null;

        if (_actor != null)
            _actor.Stop();

        if (_interactRayDisabled && _interactRay != null)
            _interactRay.SetInteractEnabled(true);

        if (_camera != null)
            _camera.Restore();

        // ต้องปิด IsPlaying ก่อนยกเลิก — Addition ที่ห้ามยกเลิกระหว่างเล่นท่า (CanCancel modifier) จะได้ยอมให้ผ่าน
        _isPlaying = false;

        if (cancelSource)
            CancelSource();

        _isPreparing = false;
        _waitingForLeader = false;
        _request = default;
        _interactRayDisabled = false;
        _interactLost = false;
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