using SDFcl.GamePlay.Interactable;
using UnityEngine;
using UnityEngine.Events;

// ร่ายคาถา Seal/Unseal ผ่านการ interact กับวัตถุ
// ใช้คู่กับ Interactor ที่เป็น HoldInteract (เช่น LeverInteractor) และแปะ ExamineCameraFocus เพิ่มได้ถ้าอยากให้กล้องจ้องวัตถุ
public class SealSpellInteractAddition : MonoBehaviour
{
    public enum SealMode { OneShot, Toggle }

    [Header("Seal")]
    [SerializeField] SealMode mode = SealMode.OneShot;
    [SerializeField] bool startSealed = true;
    [SerializeField] string saveId;

    [Header("Templates")]
    [Tooltip("ใช้ตอนวัตถุยังไม่ถูกผนึก")]
    [SerializeField] GameObject sealTemplate;
    [Tooltip("ใช้ตอนวัตถุถูกผนึกอยู่")]
    [SerializeField] GameObject unsealTemplate;
    [Tooltip("จุดวาง template ต้องอยู่ในมุมที่กล้องเห็น")]
    [SerializeField] Transform drawAnchor;

    [Header("Control")]
    [Tooltip("กด interact ซ้ำเพื่อยกเลิกระหว่างวาดได้")]
    [SerializeField] bool allowCancel = true;
    [Tooltip("โดนตีระหว่างวาดแล้วยกเลิก (ข้าม allowCancel)")]
    [SerializeField] bool cancelOnHurt = true;
    [Tooltip("ล็อกการหมุนกล้องระหว่างวาด")]
    [SerializeField] bool lockLook = true;

    [Header("Events")]
    public UnityEvent OnSealed;
    public UnityEvent OnUnsealed;
    public UnityEvent OnSpellCanceled;

    public bool IsSealed { get; private set; }
    public string SaveId => saveId;

    Iinteractor _interactor;
    GameObject _player;
    SpellController _spellController;
    SplineToLineRenderer _template;
    ILocomotionLock _locomotionLock;
    PlayerCameraController _cameraController;
    Health _health;

    bool _drawing;
    bool _completed;
    bool _rejectPending;    // เริ่มร่ายไม่ได้ ต้องสั่งจบ interact ในเฟรมถัดไป

    // วาดครบแล้ว กำลัง fade รอ SpellFinished ช่วงนี้ถือว่าสำเร็จแล้ว ยกเลิกไม่ได้
    bool IsFinishing => _template != null && _template.GetProgress() >= 1f;

    private void Awake()
    {
        _interactor = GetComponent<Iinteractor>();
        IsSealed = startSealed;
    }

    private void OnEnable()
    {
        _interactor.OnInteract += HandleInteract;
        _interactor.OnEndInteract += HandleEndInteract;
        _interactor.AddCanInteractModifier(CanCancel);
        _interactor.AddCanCancelInteractModifier(CanCancel);
    }

    private void OnDisable()
    {
        // ถ้าถูกปิดตอนกำลังวาด ให้จบ interact เพื่อคืนค่าผู้เล่น
        if (_drawing && _player != null)
            _interactor.CancelInteraction(_player, true);
        else if (_drawing)
            EndDrawing();

        _rejectPending = false;

        _interactor.OnInteract -= HandleInteract;
        _interactor.OnEndInteract -= HandleEndInteract;
        _interactor.RemoveCanInteractModifier(CanCancel);
        _interactor.RemoveCanCancelInteractModifier(CanCancel);
    }

    private void Update()
    {
        if (!_rejectPending) return;

        _rejectPending = false;
        if (_player != null) _interactor.CancelInteraction(_player, true);
        _player = null;
    }

    // BaseInteractor มาถาม: กดได้ไหม / ยกเลิกได้ไหม
    private bool CanCancel()
    {
        return !_drawing || (allowCancel && !IsFinishing);
    }

    private void HandleInteract(GameObject rootplayer)
    {
        if (_drawing) return;

        _player = rootplayer;
        _spellController = rootplayer.GetComponentInChildren<SpellController>();

        GameObject prefab = IsSealed ? unsealTemplate : sealTemplate;

        if (_spellController == null || _spellController.GetHaveTemplat || prefab == null || drawAnchor == null)
        {
            Debug.LogWarning($"{nameof(SealSpellInteractAddition)}: cannot start spell on {name} " +
                             "(missing SpellController/template/drawAnchor, or another spell is active).");
            _spellController = null;
            // ยังอยู่ระหว่าง Interact() สั่งจบตอนนี้ Interactor จะตั้งสถานะทับทีหลัง
            _rejectPending = true;
            return;
        }

        GameObject instance = Instantiate(prefab, drawAnchor.position, drawAnchor.rotation, drawAnchor);
        _template = instance.GetComponent<SplineToLineRenderer>();

        // ต้องทำในเฟรมเดียวกัน ไม่งั้น LateUpdate ของ SpellController จะลบ template ทิ้ง
        _spellController.SetSplineToLineRenderer(_template);
        _spellController.SetActive(true);
        _spellController.AddProgress();
        _spellController.SpellFinished += HandleSpellFinished;

        _completed = false;
        _drawing = true;

        _locomotionLock = rootplayer.GetComponentInChildren<ILocomotionLock>();
        _locomotionLock?.LockLocomotion(this);

        if (lockLook)
        {
            _cameraController = rootplayer.GetComponentInChildren<PlayerCameraController>();
            if (_cameraController == null && Camera.main != null)
                _cameraController = Camera.main.GetComponent<PlayerCameraController>();
            if (_cameraController != null) _cameraController.IsLookLocked = true;
        }

        if (cancelOnHurt)
        {
            _health = rootplayer.GetComponentInChildren<Health>();
            if (_health != null) _health.OnHurt += HandleHurt;
        }
    }

    // ร่ายเสร็จปกติ
    private void HandleSpellFinished()
    {
        if (!_drawing) return;

        ApplyResult();
        _interactor.CancelInteraction(_player, true);
    }

    private void HandleHurt()
    {
        if (!_drawing || _player == null) return;
        _interactor.CancelInteraction(_player, true);
    }

    // ไม่ใช้ player จาก parameter เพราะ LeverInteractor.OnDisable ส่ง null มา
    private void HandleEndInteract(GameObject _)
    {
        if (!_drawing) return;

        if (!_completed)
        {
            if (IsFinishing)
            {
                // วาดครบแล้วแต่โดนสั่งจบตอน fade ถือว่าสำเร็จ
                ApplyResult();
            }
            else
            {
                // SetActive(false) แล้ว LateUpdate ของ SpellController จะลบ template ให้เอง
                if (_spellController != null) _spellController.SetActive(false);
                OnSpellCanceled?.Invoke();
            }
        }

        EndDrawing();
    }

    private void ApplyResult()
    {
        if (_completed) return;
        _completed = true;

        IsSealed = !IsSealed;
        if (IsSealed) OnSealed?.Invoke();
        else OnUnsealed?.Invoke();

        if (mode == SealMode.OneShot && _interactor is BaseInteractor baseInteractor)
            baseInteractor.SetCanInteract(false);
    }

    private void EndDrawing()
    {
        if (_spellController != null)
        {
            _spellController.SpellFinished -= HandleSpellFinished;
            // ถ้าสำเร็จ template กำลัง fade อยู่ SpellController จะไม่ลบทิ้ง
            _spellController.SetActive(false);
        }

        if (_health != null) _health.OnHurt -= HandleHurt;

        _locomotionLock?.UnlockLocomotion(this);

        if (_cameraController != null) _cameraController.IsLookLocked = false;

        _drawing = false;
        _player = null;
        _spellController = null;
        _template = null;
        _locomotionLock = null;
        _cameraController = null;
        _health = null;
    }
}
