using System.Collections;
using System.Collections.Generic;
using SDFcl.GamePlay.Interactable;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yantra.UI;

// คัตซีนเจอผีครั้งแรก แบบ RE1 ซอมบี้หันหน้า — กล้องตัดไปทีละมุม
// มุมทัก → (มุมแทรก) → มุมผีหัน: หัวเหลียวมาก่อน (บิดกระดูกคอ/หัวเอง) → ตัวหันตาม → ตัดกลับสายตาริน
// → ผีเดินเข้ามา รินพูดไปถอยไป → ผีตบ = QTE กันผี (BlockQTE)
//
// มุมกล้อง = Cinemachine Camera ที่วางไว้ในฉาก (สคริปต์ปิดไว้ตอนเริ่มเกม เปิดเฉพาะตอนถึงมุมนั้น)
// มุมไหนเว้นว่าง = ใช้สายตาริน (กล้องเกมเพลย์ หันไปจ้องผีเอง / มุมผีหันเว้นว่าง = ซูมเข้าหน้าผีแทน)
//
// ระหว่างคัตซีน ผู้เล่นเดิน / หันกล้อง / เปิดกระเป๋า / กด E ไม่ได้ / ซ่อน HUD + แขน (ตอนอยู่มุมอื่น) / แถบดำบนล่าง
// ผีถูกพักสมอง (State Machine Controller) ไว้ ยืนนิ่งหันหลังรอ — ปล่อยทันทีที่หันมาเสร็จ ผีจะเดินมาตีตามระบบของผีเอง
// พอผีตี QTE เริ่ม → คัตซีนคืนการบังคับ ให้ BlockQTE คุมต่อ
//
// สั่งผ่าน Trigger Zone ของทีม: Trigger Zone Events → On Enter → GhostEncounterCutscene.OnEnter (Dynamic GameObject)
[DefaultExecutionOrder(10000)]   // ยึดทิศผีหลังสคริปต์อื่นทุกตัว — กันผีกระตุกตอนหัน
public class GhostEncounterCutscene : MonoBehaviour
{
    [System.Serializable]
    public struct CameraShot
    {
        [Tooltip("Cinemachine Camera ที่วางมุมไว้ในฉาก")]
        public CinemachineCamera Camera;

        [Tooltip("ค้างมุมนี้กี่วินาที")]
        [Min(0f)] public float Seconds;

        [Tooltip("0 = ตัดฉับ / มากกว่า 0 = ค่อยๆ เลื่อนจากมุมก่อนหน้ากี่วินาที")]
        [Min(0f)] public float BlendIn;
    }

    [Header("ผี")]
    [Tooltip("ตัวผี — ตัวที่มี State Machine Controller")]
    [SerializeField] private Transform _ghost;

    [Tooltip("กระดูกหัวผี — ใช้บิดหัวตอนเหลียว + สายตารินจ้องที่นี่\nว่าง = หาเอง (โมเดล Humanoid / กระดูกชื่อ Head)")]
    [SerializeField] private Transform _ghostHead;

    [Tooltip("ใช้ตอนไม่ได้ใส่กระดูกหัว — สูงจากเท้าผีกี่เมตร")]
    [SerializeField] private Vector3 _faceOffset = new Vector3(0f, 1.6f, 0f);

    [Tooltip("พักสมองผีไว้ตั้งแต่เริ่มฉาก — ผียืนนิ่งหันหลังรอ ไม่เดินวน ไม่หันมาเห็นผู้เล่นก่อนคัตซีน")]
    [SerializeField] private bool _holdGhostFromStart = true;

    [Header("ผีหัน: หัวเหลียวก่อน → ค้างจ้อง → ตัวหันตาม")]
    [Tooltip("หัวผีค่อยๆ เหลียวมาทางผู้เล่นกี่วินาที (ตัวยังไม่ขยับ)")]
    [SerializeField, Min(0.1f)] private float _headTurnTime = 1.4f;

    [Tooltip("หัวเหลียวได้สุดกี่องศา — 90 = เห็นหน้าด้านข้าง / 120 = เหลียวข้ามไหล่")]
    [SerializeField, Range(30f, 150f)] private float _maxHeadAngle = 110f;

    [Tooltip("เหลียวแล้วค้างจ้องกี่วินาที ก่อนตัวหันตาม")]
    [SerializeField, Min(0f)] private float _headHoldTime = 0.7f;

    [Tooltip("ตัวผีค่อยๆ หันตามกี่วินาที — ช้า = หลอนกว่า")]
    [SerializeField, Min(0.1f)] private float _ghostTurnTime = 2.2f;

    [Tooltip("ตอนเหลียว อกบิดตามกี่ส่วน (ที่เหลือแบ่งให้คอกับหัว) — มีนิดหน่อยจะดูเป็นธรรมชาติ ไม่ใช่คอหมุนอย่างเดียว")]
    [SerializeField, Range(0f, 0.6f)] private float _chestShare = 0.25f;

    [Tooltip("ชื่อ Trigger ใน Animator ของผีตอนหันมา (ถ้ามีท่าหัน) — ว่าง = หมุนตัวเฉยๆ")]
    [SerializeField] private string _turnAnimTrigger = "";

    [Tooltip("เสียงตอนผีเริ่มหัน (เสียงตึง / เสียงสตริงหลอนๆ) — ว่างได้")]
    [SerializeField] private SoundID _turnSound;

    [Header("บทพูด")]
    [Tooltip("ก่อนผีหัน — เช่น Sub_Forest_01_Greet (รอพูดจบก่อนไปมุมต่อไป)")]
    [SerializeField] private SubtitleDataSO _greetLines;

    [Tooltip("ระหว่างผีเดินเข้ามา — เช่น Sub_Forest_02_Approach")]
    [SerializeField] private SubtitleDataSO _approachLines;

    [Header("มุมกล้อง (ว่าง = สายตาริน)")]
    [Tooltip("มุมตอนรินทัก — เช่น มุมกว้างข้างหลังริน เห็นผียืนหันหลังอยู่ไกลๆ")]
    [SerializeField] private CinemachineCamera _greetShot;

    [Tooltip("มุมแทรกหลังทักจบ ก่อนถึงมุมผีหัน — ตัดทีละมุมตามลำดับ (แบบ RE1 ตัดไปเห็นมือ / เลือด / หน้าริน)")]
    [SerializeField] private CameraShot[] _insertShots = new CameraShot[0];

    [Tooltip("มุมตอนผีหัน — เช่น ใกล้ๆ ข้างหลังหัวผี ผีจะเหลียวมาทางกล้อง\nว่าง = ซูมเข้าหน้าผีจากสายตาริน")]
    [SerializeField] private CinemachineCamera _turnShot;

    [Tooltip("ตัดเข้ามุมผีหัน: 0 = ตัดฉับ")]
    [SerializeField, Min(0f)] private float _turnShotBlend = 0f;

    [Tooltip("ตัดกลับมาสายตารินหลังผีหันเสร็จ: 0 = ตัดฉับ")]
    [SerializeField, Min(0f)] private float _returnBlend = 0f;

    [Header("สายตาริน")]
    [Tooltip("กล้องหันไปหาผีใช้เวลากี่วินาที")]
    [SerializeField, Min(0.05f)] private float _lookTurnTime = 0.6f;

    [Tooltip("ใช้ตอนไม่มีมุมผีหัน — ซูมเข้าเหลือภาพกี่ส่วน (0.45 ≈ ซูม 2 เท่า / 1 = ไม่ซูม)")]
    [SerializeField, Range(0.2f, 1f)] private float _zoom = 0.45f;
    [SerializeField, Min(0.05f)] private float _zoomInTime = 0.8f;
    [SerializeField, Min(0.05f)] private float _zoomOutTime = 0.5f;

    [Header("จังหวะ")]
    [Tooltip("เข้ามุมผีหันแล้ว เงียบรอกี่วินาทีก่อนผีเริ่มเหลียว")]
    [SerializeField, Min(0f)] private float _beforeTurn = 0.6f;

    [Tooltip("ผีหันมาเสร็จ ค้างหน้าไว้กี่วินาที ก่อนตัดกลับ")]
    [SerializeField, Min(0f)] private float _holdFace = 0.8f;

    [Header("ผีเดินเข้ามา — รินค่อยๆ ถอย")]
    [Tooltip("ความเร็วถอยหลัง (เมตร/วินาที) — ให้ช้ากว่าผีเดิน ผีจะได้ตามทัน")]
    [SerializeField, Min(0f)] private float _backOffSpeed = 0.8f;

    [Tooltip("ถอยได้ไกลสุดกี่เมตร — 0 = ไม่ถอย")]
    [SerializeField, Min(0f)] private float _backOffMaxDistance = 2.5f;

    [Tooltip("ผีเดินเข้ามาได้กี่เมตรแล้ว รินค่อยเริ่มพูด (Approach Lines) + ถอย — ตอนผียืนจ้องอยู่ รินยืนนิ่งเงียบ")]
    [SerializeField, Min(0f)] private float _reactWhenGhostMoved = 0.6f;

    [Tooltip("ปล่อยผีแล้ว ผีไม่ตีภายในกี่วินาที = คืนการบังคับให้ผู้เล่นเลย กันค้าง")]
    [SerializeField, Min(1f)] private float _releaseTimeout = 10f;

    [Header("QTE")]
    [Tooltip("QTE อยู่ในคัตซีน — แถบดำ / HUD ซ่อน / ห้ามเดิน ค้างไว้จนกว่าจะกันได้\n" +
             "โดนตีหรือผีตีวืด = สายตากลับจ้องผี รอผีถอยแล้วตีใหม่ ขึ้น QTE อีกรอบ")]
    [SerializeField] private bool _qteInCutscene = true;

    [Tooltip("โดนตีแล้วรินเซถอยหลังกี่เมตร (ทันทีที่โดน)")]
    [SerializeField, Min(0f)] private float _failStagger = 0.6f;

    [Tooltip("เซถอยใช้เวลากี่วินาที")]
    [SerializeField, Min(0.05f)] private float _failStaggerTime = 0.3f;

    [Tooltip("โดนตีแล้วผีถอยไปตั้งหลัก พอผีเดินกลับเข้ามา รินพูดอันนี้ + ถอยหนีอีกรอบ — ใส่หลายอันได้ วนตามรอบที่พลาด\n" +
             "ว่าง = ถอยเงียบๆ")]
    [SerializeField] private SubtitleDataSO[] _retryLines;

    [Tooltip("รอบที่พลาด ผีเดินกลับเข้ามา รินถอยหนีได้ไกลสุดกี่เมตร")]
    [SerializeField, Min(0f)] private float _retryBackOffDistance = 1.5f;

    [Header("หลังกันได้ — รินถอยตั้งหลัก + พูด แล้วค่อยขึ้นหน้าสอน")]
    [Tooltip("กันได้แล้วคัตซีนยังไม่จบ: รินเซถอยตั้งหลัก แล้วพูด Blocked Line (ใน BlockQTE) จนจบ\n" +
             "ผีเดินเข้ามาเมื่อไหร่ รินถอยหนีรักษาระยะ / ผียืนอยู่ รินก็ยืนจ้อง ไม่วิ่งหนีไปไกล\n" +
             "→ พูดจบ แถบดำออก หน้าสอนขึ้น → ปิดหน้าสอนแล้วผู้เล่นบังคับเอง")]
    [SerializeField] private bool _afterBlockScene = true;

    [Tooltip("รินรักษาระยะห่างจากผีกี่เมตร — ผีเข้ามาใกล้กว่านี้รินถอยตาม / ให้ห่างกว่าระยะตบของผี")]
    [SerializeField, Min(0.5f)] private float _afterBlockKeepDistance = 2.5f;

    [Tooltip("ถอยเร็วสุดกี่เมตร/วินาที")]
    [SerializeField, Min(0.1f)] private float _afterBlockMaxSpeed = 2.2f;

    [Tooltip("ถอยรวมได้ไกลสุดกี่เมตร — ถึงแล้วยืนพูดต่อ / ข้างหลังมีของขวางก็แค่หยุดถอย ไม่ค้าง")]
    [SerializeField, Min(0f)] private float _afterBlockMaxRetreat = 4f;

    [Tooltip("ผีเข้ามาใกล้กว่านี้ = รินถอยเต็มที่ (เร็วกว่า Max Speed ได้) ไม่ปล่อยให้ผีถึงตัว\n" +
             "ถ้ายังโดนผีเข้าท่าตบอีก (ติดกำแพง) = จบคัตซีนทันที หน้าสอนขึ้นหยุดเกมไว้ ปิดแล้วผู้เล่นกด F กันเองได้")]
    [SerializeField, Min(0.3f)] private float _afterBlockPanicDistance = 1.8f;

    [Tooltip("ช่วงนี้นานอย่างน้อยกี่วินาที — เผื่อบทพูดสั้น / ไม่ได้ใส่ Blocked Line")]
    [SerializeField, Min(0f)] private float _afterBlockMinTime = 2.5f;

    [Tooltip("ติ๊ก = หลังกันได้ ผียืนนิ่งจ้องริน รอรินพูด Blocked Line จบ → หน้าสอนขึ้น → ปิดหน้าสอนแล้วค่อยปล่อยผี\n" +
             "ไม่ติ๊ก = ผีทำตาม AI เอง (ง้างตีอีกรอบ = จบคัตซีนก่อนพูดจบ)")]
    [SerializeField] private bool _afterBlockHoldGhost = true;

    [Tooltip("กันได้แล้วรอกี่วินาทีค่อยตรึงผี — ต้องรอท่าตบจบก่อน ตรึงกลางท่าผีจะค้างในกล่องตบ\n" +
             "ผีเข้าท่าตบอีกรอบก่อนถึงเวลานี้ = จบคัตซีนแบบเดิม (ลดค่าลงถ้าเจอบ่อย)")]
    [SerializeField, Min(0f)] private float _afterBlockHoldDelay = 1f;

    [Tooltip("ตรึงผีแล้ว ให้ผีเดินถอยหลังออกห่างรินกี่เมตรก่อนหยุดยืนจ้อง (ใช้ท่าเดินถอยหลังใน Animator) — 0 = หยุดนิ่งทันที")]
    [SerializeField, Min(0f)] private float _afterBlockGhostBackDistance = 1.5f;

    [Tooltip("ถอยนานสุดกี่วินาที — ข้างหลังติดอะไรถอยไม่ครบระยะ ก็หยุดเองเมื่อครบเวลานี้")]
    [SerializeField, Min(0.1f)] private float _afterBlockGhostBackMaxTime = 2f;

    [Tooltip("ค่า Move Multiply ตอนผีถอย — 1 = ท่าเดิน / 2 = ท่าวิ่ง (ตามตำแหน่งใน blend tree)")]
    [SerializeField, Min(0.1f)] private float _afterBlockGhostBackMultiply = 1f;

    [Tooltip("ชื่อ state ท่าเดินใน Animator ของผี (blend tree VelocityZ) — ตอนถอยสั่งกลับมา state นี้")]
    [SerializeField] private string _afterBlockGhostMoveState = "BaseMovement";

    private LocomotionMoveMode _savedGhostMoveMode;

    [Header("ข้ามบทพูด")]
    [Tooltip("ระหว่างคัตซีน คลิกซ้าย / Space / Enter / ปุ่ม A จอย = ข้ามประโยคที่รินกำลังพูด (แบบเดียวกับ Taxi)")]
    [SerializeField] private bool _allowSkipLine = true;

    [Tooltip("ขึ้นบรรทัดใน Console บอกแต่ละขั้น (ผีเริ่มเดิน / ถอยไปกี่เมตร / ผล QTE แต่ละรอบ) — ปิดได้หลังจูนเสร็จ")]
    [SerializeField] private bool _logSteps = true;

    [Header("ภาพ")]
    [Tooltip("ซ่อน HUD (หลอดเลือด / Guard / SP / ปืน) ระหว่างคัตซีน — ซับยังขึ้นปกติ")]
    [SerializeField] private bool _hideHud = true;

    [Tooltip("ลูกของ HUD ที่ชื่อมีคำพวกนี้จะไม่ถูกซ่อน")]
    [SerializeField] private string[] _hudKeepVisible = { "Subtitle" };

    [Tooltip("แถบดำบนล่างแบบหนัง")]
    [SerializeField] private bool _letterbox = true;

    [Tooltip("แถบดำสูงกี่ส่วนของจอ (ต่อข้าง)")]
    [SerializeField, Range(0f, 0.25f)] private float _letterboxSize = 0.1f;

    [Header("Events")]
    [Tooltip("ตอนปล่อยผีให้เดินเข้ามา")]
    public UnityEvent OnGhostReleased;

    [Tooltip("ตอนคัตซีนจบ (กันได้ / หรือหมดเวลารอ)")]
    public UnityEvent OnFinished;

    private const int ShotPriority = 1000;
    private const int LetterboxSortingOrder = 500;

    // ---------- ผี ----------
    private MonoBehaviour _brain;
    private bool _brainDisabled;
    private NavMeshAgent _agent;
    private bool _agentHeld;
    private bool _savedAgentRotation;
    private BaseLocomotion _ghostLocomotion;
    private bool _ghostLocked;
    private bool _ghostHeld;
    private Quaternion _ghostRotation;
    private LookAtIKController _ghostLookIK;
    private Transform _headBone;
    private Transform _neckBone;
    private Transform _chestBone;
    private float _headYaw;          // หัวเหลียวจากลำตัวกี่องศา — บิดกระดูกทับท่าใน LateUpdate
    private bool _applyHeadYaw;
    private bool _warnedBackOff;

    // ---------- ผู้เล่น ----------
    private GameObject _player;
    private PlayerLocomotion _locomotion;
    private bool _movementLocked;
    private PlayerCameraController _camera;
    private bool _looking;
    private PlayerInventory _inventory;
    private bool _inventoryBlocked;
    private PlayerInteract _interact;
    private bool _interactBlocked;

    // ---------- กล้อง ----------
    private CinemachineBrain _cinemachineBrain;
    private CinemachineBlendDefinition _savedBlend;
    private bool _blendChanged;
    private CinemachineCamera _liveShot;
    private PrioritySettings _liveShotPriority;
    private bool _armsHidden;
    private CinemachineRecomposer _recomposer;
    private bool _zoomChanged;

    // ---------- ภาพ ----------
    private sealed class HiddenGroup
    {
        public CanvasGroup Group;
        public bool Added;
        public float Alpha;
    }

    private readonly List<HiddenGroup> _hiddenHud = new List<HiddenGroup>();
    private GameObject _letterboxRoot;
    private CanvasGroup _letterboxGroup;
    private RectTransform _barTop;
    private RectTransform _barBottom;
    private float _letterboxAmount;
    private float _letterboxTarget;

    private bool _running;
    private bool _qteStarted;
    private BlockQTE _activeQte;
    private bool _lateAttack;
    private readonly List<Collider> _mutedTutorialZones = new List<Collider>();

    // ===== เริ่ม =====

    private void Start()
    {
        // ปิดกล้องทุกมุมไว้ก่อน — กล้องที่ปิดอยู่จะไม่แย่งจอตอนเล่นปกติ
        MuteShot(_greetShot);
        MuteShot(_turnShot);
        if (_insertShots != null)
            foreach (CameraShot shot in _insertShots)
                MuteShot(shot.Camera);

        if (_holdGhostFromStart)
            HoldGhost();
    }

    /// <summary>ต่อกับ Trigger Zone Events → On Enter (Dynamic GameObject)</summary>
    public void OnEnter(GameObject who)
    {
        if (_running || who == null || !isActiveAndEnabled)
            return;

        if (_ghost == null)
        {
            Debug.LogWarning($"[GhostEncounter] {name} ยังไม่ได้ใส่ผีในช่อง Ghost", this);
            return;
        }

        StartCoroutine(Run(who.transform.root.gameObject));
    }

    private IEnumerator Run(GameObject player)
    {
        _running = true;
        _qteStarted = false;
        _activeQte = null;
        BlockQTE.AnyStarted += HandleQTEStarted;

        HoldGhost();
        HoldPlayer(player);
        HideHud();
        MuteTutorialZones();
        _letterboxTarget = 1f;

        // 1) มุมทัก (ว่าง = สายตารินหันไปจ้องผี) แล้วรินทักจนจบ
        if (_greetShot != null)
            CutTo(_greetShot, 0f);
        else
            yield return new WaitForSeconds(_lookTurnTime);

        yield return PlayAndWait(_greetLines);

        // 2) มุมแทรก ตัดทีละมุม
        if (_insertShots != null)
        {
            foreach (CameraShot shot in _insertShots)
            {
                if (shot.Camera == null)
                    continue;
                CutTo(shot.Camera, shot.BlendIn);
                yield return new WaitForSeconds(shot.Seconds);
            }
        }

        // 3) มุมผีหัน (ว่าง = กลับสายตารินแล้วซูมเข้าหน้า) → เงียบ → หัวเหลียว → ตัวหันตาม
        bool zoomInstead = _turnShot == null;
        if (zoomInstead)
        {
            CutTo(null, 0f);
            yield return Zoom(1f, _zoom, _zoomInTime);
        }
        else
        {
            CutTo(_turnShot, _turnShotBlend);
        }

        yield return new WaitForSeconds(_beforeTurn);

        PlayTurnSound();
        TriggerTurnAnimation();
        yield return TurnGhostToPlayer();

        // ปลุกผีทันทีที่หันเสร็จ — ช่วงที่ผียืนจ้อง (Alert) จะซ้อนกับตอนค้างหน้าพอดี ไม่ต้องรอเปล่า
        ReleaseGhost();
        OnGhostReleased?.Invoke();

        yield return new WaitForSeconds(_holdFace);

        // 4) ตัดกลับสายตาริน — รินพูดไปถอยไป จนผีตี (QTE เริ่ม)
        if (zoomInstead)
            yield return Zoom(_zoom, 1f, _zoomOutTime);
        else
            CutTo(null, _returnBlend);

        yield return BackOffUntilAttack(_approachLines, _backOffMaxDistance);

        // 5) ผีตี → QTE ในคัตซีน (แถบดำค้าง) จนกว่าจะกันได้
        if (_qteStarted && _qteInCutscene)
        {
            yield return StayUntilBlocked();

            // 6) กันได้ → รินถอยพูด → หน้าสอน → ปิดหน้าสอนแล้วค่อยปล่อย
            if (_afterBlockScene && _activeQte != null && _activeQte.LastResult == BlockQTE.Result.Blocked)
                yield return AfterBlock();
        }

        Finish();
    }

    // ผีตี → QTE เริ่ม — BlockQTE หันกล้องหาผีเองแล้ว (กล้องเป็นของ QTE ช่วงนี้)
    private void HandleQTEStarted(BlockQTE qte)
    {
        if (!_running)
            return;
        _qteStarted = true;
        _activeQte = qte;
        _looking = false;

        // หน้าสอนหลังกันได้ ให้คัตซีนสั่งเองตอนจบ — ขึ้นกลางคัตซีนเกมจะหยุด รินค้างกลางทางถอย
        if (_qteInCutscene && _afterBlockScene)
            qte.DeferTutorials = true;
    }

    // QTE อยู่ในคัตซีน — แถบดำ / HUD ซ่อน / ห้ามเดิน ค้างไว้ตลอด
    // โดนตี หรือผีตีวืด = สายตากลับจ้องผี รอผีถอยแล้วตีใหม่ ขึ้น QTE อีก จนกว่าจะกันได้
    private IEnumerator StayUntilBlocked()
    {
        int round = 1;
        while (true)
        {
            while (_activeQte != null && _activeQte.IsRunning)
                yield return null;

            BlockQTE.Result result = _activeQte != null ? _activeQte.LastResult : BlockQTE.Result.None;
            if (_logSteps)
            {
                string text = result == BlockQTE.Result.Blocked ? "กันได้ — จบคัตซีน"
                    : result == BlockQTE.Result.Failed ? "โดนตี — รอผีตีใหม่"
                    : "ผีตีวืด — รอผีตีใหม่";
                Debug.Log($"[GhostEncounter] QTE รอบ {round}: {text}", this);
            }

            if (result == BlockQTE.Result.Blocked || _activeQte == null)
                yield break;

            // BlockQTE ปล่อยกล้องไปแล้ว → สายตารินกลับจ้องผี
            _qteStarted = false;
            if (_camera != null)
                _looking = _camera.StartForcedLook(FaceTarget(), FaceOffset(), 999f, _lookTurnTime * 0.5f);

            // โดนตบ → เซถอยหลัง / ผีถอยไปตั้งหลักแล้วเดินกลับมา → รินพูด + ถอยหนี (แบบตอนแรก) → ผีตี QTE รอบใหม่
            if (result == BlockQTE.Result.Failed)
                yield return Stagger();

            SubtitleDataSO line = _retryLines != null && _retryLines.Length > 0
                ? _retryLines[(round - 1) % _retryLines.Length]
                : null;
            yield return BackOffUntilAttack(line, _retryBackOffDistance);

            if (!_qteStarted)
                yield break;
            round++;
        }
    }

    // กันได้แล้ว: ผีเดินไล่ รินถอยหลังไปเรื่อยๆ พร้อมพูด → พูดจบหยุดถอย
    // → ภาพกลับปกติ → หน้าสอนขึ้น (หยุดเกม) → ปิดหน้าสอนแล้ว Finish ปล่อยผู้เล่น + ผี
    private IEnumerator AfterBlock()
    {
        if (!_activeQte.HasBlockedLine)
            Debug.LogWarning($"[GhostEncounter] ช่อง Blocked Line ใน {_activeQte.name} ว่าง/Missing — รินจะถอยเงียบๆ {_afterBlockMinTime:0.0} วิ ไม่พูด", _activeQte);

        // BlockQTE ปล่อยกล้องไปแล้ว → สายตารินกลับจ้องผี
        if (_camera != null && !_looking)
            _looking = _camera.StartForcedLook(FaceTarget(), FaceOffset(), 999f, _lookTurnTime * 0.5f);

        if (_logSteps)
            Debug.Log("[GhostEncounter] กันได้ — รินถอยตั้งหลัก + พูด", this);

        _lateAttack = false;
        BlockQTE.AnyAttackAfterBlocked += HandleLateAttack;
        yield return BackOffWhileTalking();
        BlockQTE.AnyAttackAfterBlocked -= HandleLateAttack;

        // จบภาพคัตซีน แล้วค่อยขึ้นหน้าสอน
        RestoreCameras();
        RestoreHud();
        _letterboxTarget = 0f;

        if (_activeQte.ShowHeldTutorials())
        {
            yield return null;
            while (UIManager.Instance != null && UIManager.Instance.HasAnyOpen)
                yield return null;
        }

        if (_logSteps)
            Debug.Log("[GhostEncounter] จบคัตซีน — ผู้เล่นบังคับเอง ผีเริ่มสู้", this);
    }

    // ผีไม่ได้ถูกหยุด — ทำตาม AI ของมันเอง (ถ้าตั้ง Transition Stance → Pursue ตอน Blocked ไว้ ผีจะเดินใส่ทันที)
    // รินรักษาระยะ: ผีใกล้กว่า Keep Distance = ถอย (กันได้ใหม่ๆ ผีประชิดอยู่ = เซถอยออกมาตั้งหลักเอง)
    // ผียืนอยู่ = รินยืนจ้อง ไม่วิ่งหนีไปไกล → พูดจบ จบช่วงนี้
    private void HandleLateAttack(BlockQTE qte) => _lateAttack = true;

    private IEnumerator BackOffWhileTalking()
    {
        CharacterController controller = _player != null ? _player.GetComponentInChildren<CharacterController>() : null;
        const float accel = 5f;          // เร่ง / ชะลอ (เมตร/วินาที²)
        const float catchUp = 3f;        // ผีใกล้กว่า Keep Distance ไปทุก 1 ม. ถอยเร็วขึ้น 3 ม./วิ
        const float panicGrace = 1.5f;   // หลังกันได้ใหม่ๆ ผียังอยู่ประชิด (กำลังตีจบ) ยังไม่นับ
        float backed = 0f;
        float speed = 0f;
        float time = 0f;
        float closest = float.MaxValue;
        float startDistance = FlatDistanceToGhost();
        string endNote = "";
        bool ghostHeld = false;
        bool ghostStepping = false;
        float ghostBackTime = 0f;
        float savedMultiply = 1f;
        Vector3 ghostBackStart = Vector3.zero;

        while (time < 20f)
        {
            time += Time.deltaTime;
            float distance = FlatDistanceToGhost();
            if (time > panicGrace)
                closest = Mathf.Min(closest, distance);

            // ไม่หยุดสมองผีแล้ว (หยุดกลางท่าตบ ผีจะค้างในกล่องตบ) — ถ้าผีเข้าท่าตบอีกรอบ จบคัตซีนเลย
            // หน้าสอนขึ้นหยุดเกมไว้ก่อนมือถึง ปิดแล้วผู้เล่นกด F กันเองได้
            if (_lateAttack)
            {
                endNote = $" | ผีเข้าท่าตบอีกรอบตอน {time:0.0} วิ (ห่าง {distance:0.0} ม.) — จบคัตซีนก่อนบทพูดจบ";
                break;
            }

            // ท่าตบจบแล้ว → ตรึงผี (ปิดสมอง) → เดินถอยหลังออกห่างริน → หยุดยืนจ้อง จนรินพูดจบ + ปิดหน้าสอน (Finish ปล่อยให้)
            if (_afterBlockHoldGhost && !ghostHeld && time >= _afterBlockHoldDelay)
            {
                ghostHeld = true;
                HoldGhost(keepLooking: true);
                ghostStepping = StartGhostStepBack(out ghostBackStart, out savedMultiply);
                ghostBackTime = 0f;
                if (_logSteps)
                    Debug.Log($"[GhostEncounter] ตรึงผีรอรินพูดจบ (ห่าง {distance:0.0} ม.){(ghostStepping ? $" — ผีถอยหลัง {_afterBlockGhostBackDistance:0.0} ม. ก่อน" : "")}", this);
            }

            if (ghostStepping)
            {
                ghostBackTime += Time.deltaTime;
                Vector3 moved = _ghost.position - ghostBackStart;
                moved.y = 0f;
                if (moved.magnitude >= _afterBlockGhostBackDistance || ghostBackTime >= _afterBlockGhostBackMaxTime)
                {
                    ghostStepping = false;
                    StopGhostStepBack(savedMultiply);
                    if (_logSteps)
                        Debug.Log($"[GhostEncounter] ผีถอยไป {moved.magnitude:0.00} ม. ใช้ {ghostBackTime:0.0} วิ — หยุดยืนจ้อง", this);
                }
                else
                {
                    Vector3 away = _ghost.position - _player.transform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.0001f)
                    {
                        _ghostLocomotion.SetMovementDirection(away.normalized);
                        _ghostLocomotion.SetFacingDirection(-away);
                    }
                }
            }

            bool talking = SubtitleSystem.Instance != null && SubtitleSystem.Instance.IsPlaying;
            bool done = !talking && time >= _afterBlockMinTime;

            // ผีใกล้กว่า Keep Distance = ถอยตาม / ใกล้กว่า Panic Distance (หลังผีตบจบแล้ว) = ถอยเต็มที่ ไม่จำกัดความเร็ว
            float target = 0f;
            if (!done && distance < _afterBlockKeepDistance)
            {
                bool panic = time > panicGrace && distance < _afterBlockPanicDistance;
                float cap = panic ? _afterBlockMaxSpeed * 2f : _afterBlockMaxSpeed;
                target = Mathf.Min((_afterBlockKeepDistance - distance) * catchUp, cap);
            }
            speed = Mathf.MoveTowards(speed, target, accel * Time.deltaTime);

            float remaining = _afterBlockMaxRetreat - backed;
            if (speed > 0.0001f && remaining > 0.001f && _player != null && _ghost != null)
            {
                Vector3 away = _player.transform.position - GhostPosition();
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f)
                {
                    float step = Mathf.Min(speed * Time.deltaTime, remaining);
                    if (!MovePlayer(controller, away.normalized * step))
                        backed = _afterBlockMaxRetreat;   // ข้างหลังมีของขวาง หยุดถอย
                    else
                        backed += step;
                }
            }

            // ผียังถอยไม่เสร็จ — รอให้หยุดก่อนค่อยจบช่วงนี้ (ไม่งั้นหน้าสอนขึ้นตอนผีกำลังเดิน)
            if (done && speed <= 0.0001f && !ghostStepping)
                break;

            yield return null;
        }

        // หลุดลูปกลางทาง (ผีตบอีกรอบ / ครบ 20 วิ) — หยุดเดินถอยให้เรียบร้อย
        if (ghostStepping)
            StopGhostStepBack(savedMultiply);

        if (_logSteps)
        {
            float end = FlatDistanceToGhost();
            Debug.Log($"[GhostEncounter] พูดจบ — รินถอยไป {backed:0.00} ม. ใช้ {time:0.0} วิ | ผีห่าง {startDistance:0.0} → {end:0.0} ม. " +
                      $"(ใกล้สุด {(closest < float.MaxValue ? closest.ToString("0.0") : "-")}){endNote}", this);
        }
    }

    // โดนตบ → เซถอยหลังแรงๆ แวบเดียว (ออกเร็ว ค่อยๆ หยุด)
    private IEnumerator Stagger()
    {
        if (_failStagger <= 0f || _player == null || _ghost == null)
            yield break;

        CharacterController controller = _player.GetComponentInChildren<CharacterController>();
        Vector3 away = _player.transform.position - GhostPosition();
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            yield break;
        away.Normalize();

        float time = 0f;
        float moved = 0f;
        while (time < _failStaggerTime)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / _failStaggerTime);
            float target = _failStagger * (1f - (1f - t) * (1f - t));
            if (!MovePlayer(controller, away * (target - moved)))
                yield break;
            moved = target;
            yield return null;
        }
    }

    // โซนหน้าสอนอื่นในฉาก (เช่น โซนต่อยเก่า) — รินถอยหลังเข้าไปโดน หน้าสอนจะเด้งกลางบทพูด (หน้าสอนหยุดเกม)
    // ปิดตัวชนของโซนไว้ระหว่างคัตซีน / เปิดคืนตอนจบ ถ้ายืนอยู่ในโซนพอดี โซนจะทำงานตอนนั้นแทน
    private void MuteTutorialZones()
    {
        foreach (TutorialTrigger trigger in FindObjectsByType<TutorialTrigger>())
        {
            if (trigger.GetComponent<TriggerZone>() == null)
                continue;
            foreach (Collider zone in trigger.GetComponents<Collider>())
            {
                if (!zone.enabled)
                    continue;
                zone.enabled = false;
                _mutedTutorialZones.Add(zone);
            }
        }

        if (_logSteps && _mutedTutorialZones.Count > 0)
            Debug.Log($"[GhostEncounter] ปิดโซนหน้าสอนอื่นไว้ระหว่างคัตซีน {_mutedTutorialZones.Count} อัน", this);
    }

    private void UnmuteTutorialZones()
    {
        foreach (Collider zone in _mutedTutorialZones)
        {
            if (zone != null)
                zone.enabled = true;
        }
        _mutedTutorialZones.Clear();
    }

    private void Finish()
    {
        UnmuteTutorialZones();
        if (_activeQte != null)
            _activeQte.DeferTutorials = false;
        ReleaseGhost();
        // _looking = สายตารินที่คัตซีนสั่งไว้ยังค้างอยู่ / QTE กำลังคุมกล้อง = false ไม่ไปแย่งกล้องกลาง QTE
        ReleasePlayer(true);
        RestoreCameras();
        RestoreHud();
        _letterboxTarget = 0f;

        BlockQTE.AnyStarted -= HandleQTEStarted;
        _running = false;
        OnFinished?.Invoke();
    }

    // ===== บทพูด =====

    private static void Play(SubtitleDataSO lines)
    {
        if (lines != null && SubtitleSystem.Instance != null)
            SubtitleSystem.Instance.PlaySubtitle(lines);
    }

    private static IEnumerator PlayAndWait(SubtitleDataSO lines)
    {
        SubtitleSystem subtitles = SubtitleSystem.Instance;
        if (lines == null || subtitles == null)
            yield break;

        subtitles.PlaySubtitle(lines);

        // Pause / เมนูเปิด ระบบซับหยุดรอเอง — รอแค่จนเล่นจบ (กันค้างไว้ 30 วิ)
        float waited = 0f;
        while (subtitles != null && subtitles.IsPlaying && waited < 30f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ===== มุมกล้อง =====

    private static void MuteShot(CinemachineCamera shot)
    {
        if (shot != null)
            shot.enabled = false;
    }

    // null = กลับสายตาริน (กล้องเกมเพลย์)
    private void CutTo(CinemachineCamera shot, float blendSeconds)
    {
        if (!EnsureCinemachineBrain())
        {
            if (shot != null)
                Debug.LogWarning($"[GhostEncounter] {name} หา CinemachineBrain ของกล้องหลักไม่เจอ — ตัดมุมไม่ได้", this);
            return;
        }

        if (!_blendChanged)
        {
            _savedBlend = _cinemachineBrain.DefaultBlend;
            _blendChanged = true;
        }

        _cinemachineBrain.DefaultBlend = blendSeconds > 0f
            ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendSeconds)
            : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

        ReleaseLiveShot();

        if (shot != null)
        {
            _liveShot = shot;
            _liveShotPriority = shot.Priority;
            shot.Priority = ShotPriority;
            shot.enabled = true;
        }

        // แขน FPS แปะทับจออยู่ — มุมอื่นต้องซ่อน ไม่งั้นแขนลอยกลางจอ
        SetArmsVisible(shot == null);
    }

    private void ReleaseLiveShot()
    {
        if (_liveShot == null)
            return;

        _liveShot.Priority = _liveShotPriority;
        _liveShot.enabled = false;
        _liveShot = null;
    }

    private void RestoreCameras()
    {
        ReleaseLiveShot();
        SetArmsVisible(true);
        RestoreZoom();

        if (_blendChanged && _cinemachineBrain != null)
            _cinemachineBrain.DefaultBlend = _savedBlend;
        _blendChanged = false;
    }

    private bool EnsureCinemachineBrain()
    {
        if (_cinemachineBrain != null)
            return true;

        Camera main = Camera.main;
        if (main != null && main.TryGetComponent(out CinemachineBrain brain))
        {
            _cinemachineBrain = brain;
            return true;
        }

        // หาจากกล้องของผู้เล่น — ตัวที่วาดลงจอจริง ไม่ใช่กล้องแขน (วาดลง RenderTexture)
        if (_camera != null)
        {
            foreach (CinemachineBrain candidate in _camera.GetComponentsInChildren<CinemachineBrain>(true))
            {
                Camera output = candidate.OutputCamera;
                if (candidate.isActiveAndEnabled && output != null && output.targetTexture == null)
                {
                    _cinemachineBrain = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private void SetArmsVisible(bool visible)
    {
        if (_armsHidden == !visible || _camera == null)
            return;
        _armsHidden = !visible;

        foreach (ViewModelRenderer arms in _camera.GetComponentsInChildren<ViewModelRenderer>(true))
            arms.SetOverlayVisible(visible);
    }

    // ===== ซูม (ใช้ตอนไม่มีมุมผีหัน) =====

    // ซูมผ่าน Recomposer ของกล้องเกมเพลย์ (ตัวเดียวกับที่ CameraRecoil ใช้) — ไม่ไปยุ่ง FOV ของ PlayerCameraController
    private IEnumerator Zoom(float from, float to, float duration)
    {
        if (!EnsureRecomposer() || Mathf.Approximately(from, to))
            yield break;

        _zoomChanged = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _recomposer.ZoomScale = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        _recomposer.ZoomScale = to;
    }

    private bool EnsureRecomposer()
    {
        if (_recomposer != null)
            return true;
        if (_camera == null || _camera.vcamGameplay == null)
            return false;

        _recomposer = _camera.vcamGameplay.GetComponent<CinemachineRecomposer>();
        if (_recomposer == null)
        {
            _recomposer = _camera.vcamGameplay.gameObject.AddComponent<CinemachineRecomposer>();
            _recomposer.ApplyAfter = CinemachineCore.Stage.Finalize;
            _recomposer.ZoomScale = 1f;
            _recomposer.FollowAttachment = 1f;
            _recomposer.LookAtAttachment = 1f;
        }
        return true;
    }

    private void RestoreZoom()
    {
        if (_zoomChanged && _recomposer != null)
            _recomposer.ZoomScale = 1f;
        _zoomChanged = false;
    }

    // ===== ผี =====

    // keepLooking = หัวผียังจ้องผู้เล่นอยู่ (ตอนหยุดรอหลังกันได้) / false = หัวตรงตามตัว (ตอนยืนหันหลัง)
    private void HoldGhost(bool keepLooking = false)
    {
        if (_ghostHeld || _ghost == null)
            return;
        _ghostHeld = true;
        _ghostRotation = _ghost.rotation;

        // หาด้วยชื่อ ไม่ต้องอ้างถึงไฟล์ระบบ State Machine ตรงๆ
        _brain = FindComponentByName(_ghost, "StateMachineController");
        if (_brain != null && _brain.enabled)
        {
            _brain.enabled = false;
            _brainDisabled = true;
        }
        else if (_brain == null)
        {
            Debug.LogWarning($"[GhostEncounter] {name} หา State Machine Controller ของผีไม่เจอ — ผีอาจเดินเองระหว่างคัตซีน", this);
        }

        _agent = _ghost.GetComponentInChildren<NavMeshAgent>(true);
        if (_agent != null && _agent.enabled)
        {
            _savedAgentRotation = _agent.updateRotation;
            _agent.updateRotation = false;
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
            _agentHeld = true;
        }

        _ghostLocomotion = _ghost.GetComponentInChildren<BaseLocomotion>(true);
        if (_ghostLocomotion != null)
        {
            _ghostLocomotion.LockLocomotion(this);
            _ghostLocked = true;
        }

        // หัวผีต้องหันตรงตามตัวตอนยืนหันหลัง — ไม่งั้นเหลียวมามองผู้เล่นก่อนถึงจังหวะ
        _ghostLookIK = _ghost.GetComponentInChildren<LookAtIKController>(true);
        if (_ghostLookIK != null)
        {
            if (keepLooking && _player != null)
            {
                _ghostLookIK.SetLookTarget(_camera != null ? _camera.transform : _player.transform);
                _ghostLookIK.SetIKEnabled(true);
            }
            else
            {
                _ghostLookIK.SetIKEnabled(false);
            }
        }
    }

    // ผีถูกตรึงอยู่ (สมองปิด) — ปลดล็อกการเดินชั่วคราว ให้ Root Motion พาเดินถอยหลังด้วยท่าจริง
    // คืน false = ไม่ต้องถอย (ระยะ 0 / หา Locomotion ไม่เจอ) ผียืนนิ่งเลย
    private bool StartGhostStepBack(out Vector3 start, out float savedMultiply)
    {
        start = _ghost != null ? _ghost.position : Vector3.zero;
        savedMultiply = 1f;

        if (_afterBlockGhostBackDistance <= 0f || _ghostLocomotion == null || _player == null || !_ghostHeld)
            return false;

        // สมองปิดแล้ว Animator ยังค้างท่า Attack (ไม่มีทางกลับเอง) — สั่งกลับท่าเดินเอง ไม่งั้นไม่มี Root Motion
        Animator animator = _ghostLocomotion.GetComponent<Animator>();
        int moveState = Animator.StringToHash(_afterBlockGhostMoveState);
        if (animator != null && animator.HasState(0, moveState))
        {
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != moveState)
                animator.CrossFadeInFixedTime(moveState, 0.25f, 0);
        }
        else
        {
            Debug.LogWarning($"[GhostEncounter] {name} ไม่เจอ state \"{_afterBlockGhostMoveState}\" ใน Animator ของผี — ผีอาจไม่ถอย", this);
        }

        // ตอนตีสมองอาจตั้งโหมดเดินเป็นแบบไม่ขยับไว้ — บังคับ Root Motion ระหว่างถอย แล้วคืนตอนหยุด
        _savedGhostMoveMode = _ghostLocomotion.MoveMode;
        _ghostLocomotion.MoveMode = LocomotionMoveMode.RootMotion;

        if (_logSteps)
            Debug.Log($"[GhostEncounter] เริ่มถอยผี — Move Mode เดิม {_savedGhostMoveMode}", this);

        savedMultiply = _ghostLocomotion.GetMoveMultiply();
        _ghostLocomotion.SetMoveMultiply(_afterBlockGhostBackMultiply);
        _ghostLocomotion.UnlockLocomotion(this);
        return true;
    }

    private void StopGhostStepBack(float savedMultiply)
    {
        if (_ghostLocomotion == null)
            return;

        _ghostLocomotion.ClearMovementDirection();
        _ghostLocomotion.SetMoveMultiply(savedMultiply);
        _ghostLocomotion.MoveMode = _savedGhostMoveMode;

        // ล็อกกลับ (รีเซ็ตท่าเดินเป็นยืน) — ReleaseGhost ตอน Finish จะปลดให้เอง
        if (_ghostHeld)
            _ghostLocomotion.LockLocomotion(this);
    }

    private void ReleaseGhost()
    {
        if (!_ghostHeld)
            return;
        _ghostHeld = false;

        if (_ghostLocked && _ghostLocomotion != null)
            _ghostLocomotion.UnlockLocomotion(this);
        _ghostLocked = false;

        if (_agentHeld && _agent != null && _agent.enabled)
        {
            _agent.updateRotation = _savedAgentRotation;
            if (_agent.isOnNavMesh)
            {
                _agent.nextPosition = _agent.transform.position;
                _agent.isStopped = false;
            }
        }
        _agentHeld = false;

        if (_brainDisabled && _brain != null)
            _brain.enabled = true;
        _brainDisabled = false;

        // เปิด IK ของผีคืน ให้จ้องหน้าผู้เล่นต่อ (กล้องผู้เล่นอยู่ระดับตา)
        _applyHeadYaw = false;
        if (_ghostLookIK != null && _player != null)
        {
            _ghostLookIK.SetLookTarget(_camera != null ? _camera.transform : _player.transform);
            _ghostLookIK.SetIKEnabled(true);
        }
    }

    // ===== หัวผีเหลียว (บิดกระดูกเอง ไม่พึ่ง IK — ได้ทุกโมเดล ไม่โดนค่า IK จำกัดองศา) =====

    private void FindHeadBones()
    {
        if (_headBone != null || _ghost == null)
            return;

        Animator animator = _ghost.GetComponentInChildren<Animator>();
        bool human = animator != null && animator.isHuman;

        _headBone = _ghostHead;
        if (_headBone == null && human)
            _headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        if (_headBone == null)
            _headBone = FindChildNamed(_ghost, "head");

        if (_headBone == null)
        {
            Debug.LogWarning($"[GhostEncounter] {name} หากระดูกหัวผีไม่เจอ — หัวจะไม่เหลียว ลากกระดูกหัวใส่ช่อง Ghost Head", this);
            return;
        }

        _neckBone = human ? animator.GetBoneTransform(HumanBodyBones.Neck) : null;
        if (_neckBone == null && _headBone.parent != _ghost)
            _neckBone = _headBone.parent;

        Transform chest = null;
        if (human)
        {
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null)
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        }
        if (chest == null && _neckBone != null && _neckBone.parent != _ghost)
            chest = _neckBone.parent;
        _chestBone = chest;
    }

    // บิดรอบแกนตั้งของโลก ไล่จากอก → คอ → หัว (ลูกหมุนตามพ่อ องศารวมเลยเท่ากับ _headYaw พอดี)
    private void ApplyHeadYaw()
    {
        float chestShare = _chestBone != null ? _chestShare : 0f;
        float neckShare = _neckBone != null ? (1f - chestShare) * 0.4f : 0f;
        float headShare = 1f - chestShare - neckShare;

        RotateBone(_chestBone, _headYaw * chestShare);
        RotateBone(_neckBone, _headYaw * neckShare);
        RotateBone(_headBone, _headYaw * headShare);
    }

    private static void RotateBone(Transform bone, float angle)
    {
        if (bone != null && Mathf.Abs(angle) > 0.001f)
            bone.rotation = Quaternion.AngleAxis(angle, Vector3.up) * bone.rotation;
    }

    private static Transform FindChildNamed(Transform root, string contains)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            string lower = child.name.ToLowerInvariant();
            if (lower.Contains(contains) && !lower.Contains("end") && !lower.Contains("top"))
                return child;
        }
        return null;
    }

    // คลิกข้ามประโยค — เฉพาะตอนคัตซีนนี้เล่นอยู่ / ไม่มีเมนูเปิด / ไม่ได้โหลดฉาก
    private void Update()
    {
        if (!_running || !_allowSkipLine)
            return;

        SubtitleSystem subtitles = SubtitleSystem.Instance;
        if (subtitles == null || !subtitles.IsPlaying || !CanSkipLineNow() || !SkipLinePressed())
            return;

        subtitles.SkipLine();
        if (_logSteps)
            Debug.Log("[GhostEncounter] ผู้เล่นกดข้ามประโยค", this);
    }

    private static bool CanSkipLineNow()
    {
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
            return false;
        if (PauseController.Instance != null && PauseController.Instance.IsPaused)
            return false;
        return UIManager.Instance == null || !UIManager.Instance.HasAnyOpen;
    }

    private static bool SkipLinePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        return (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
               (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) ||
               (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
#else
        return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
    }

    private void LateUpdate()
    {
        // ระหว่างพักสมอง ยึดทิศผีไว้ทุกเฟรม — ระบบเดินของผีจะหมุนตัวทับไม่ได้
        if (_ghostHeld && _ghost != null)
            _ghost.rotation = _ghostRotation;

        // หลังท่าแอนิเมชันเฟรมนี้ — บิดทับทุกเฟรม (แอนิเมชันรีเซ็ตกระดูกให้ทุกเฟรมอยู่แล้ว ไม่สะสม)
        if (_applyHeadYaw && _ghost != null)
            ApplyHeadYaw();

        // ระบบ HUD ของเกมโชว์ HUD เองตอนเข้าโหมดสู้ (ผีเห็นผู้เล่น) — ซ่อนซ้ำทุกเฟรมจนคัตซีนจบ
        foreach (HiddenGroup hidden in _hiddenHud)
        {
            if (hidden.Group != null)
                hidden.Group.alpha = 0f;
        }

        UpdateLetterbox();
    }

    // แบบ RE1: หัวค่อยๆ เหลียวมา (ตัวยังนิ่ง) → ค้างจ้อง → ตัวค่อยๆ หันตาม หัวค่อยๆ คืนตรงตามที่ตัวหันมาถึง
    private IEnumerator TurnGhostToPlayer()
    {
        if (_ghost == null || _player == null)
            yield break;

        Vector3 toPlayer = _player.transform.position - _ghost.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            yield break;
        toPlayer.Normalize();

        Vector3 forward = _ghostRotation * Vector3.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : toPlayer;

        // หัวกับตัวต้องหันไปทางเดียวกันเสมอ — มุมนี้ใช้ทั้งเหลียวหัวและหมุนตัว
        float angle = Vector3.SignedAngle(forward, toPlayer, Vector3.up);
        if (Mathf.Abs(angle) < 0.5f)
            angle = 0f;
        float headAngle = Mathf.Clamp(angle, -_maxHeadAngle, _maxHeadAngle);

        FindHeadBones();
        bool turnHead = _headBone != null;

        // 1) หัวค่อยๆ เหลียว → 2) ค้างจ้อง
        if (turnHead)
        {
            _applyHeadYaw = true;

            float elapsed = 0f;
            while (elapsed < _headTurnTime)
            {
                elapsed += Time.deltaTime;
                _headYaw = headAngle * Mathf.SmoothStep(0f, 1f, elapsed / _headTurnTime);
                yield return null;
            }
            _headYaw = headAngle;

            if (_headHoldTime > 0f)
                yield return new WaitForSeconds(_headHoldTime);
        }

        // 3) ตัวค่อยๆ หันตาม ไปทางเดียวกับที่หัวเหลียว (ไม่ใช้ทางลัด ไม่งั้นตัวหมุนสวนหัว หัววาร์ปไปอีกข้าง)
        //    หัวยังจ้องผู้เล่น — องศาหัว = มุมที่ตัวยังหันไม่ถึง แล้วค่อยๆ คืนตรงเอง
        Quaternion from = _ghostRotation;

        float turned = 0f;
        while (turned < _ghostTurnTime)
        {
            turned += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, turned / _ghostTurnTime);
            _ghostRotation = Quaternion.AngleAxis(angle * t, Vector3.up) * from;

            if (turnHead)
                _headYaw = Mathf.Clamp(angle * (1f - t), -_maxHeadAngle, _maxHeadAngle);
            yield return null;
        }

        _ghostRotation = Quaternion.AngleAxis(angle, Vector3.up) * from;
        _headYaw = 0f;
        _applyHeadYaw = false;
    }

    private void TriggerTurnAnimation()
    {
        if (string.IsNullOrWhiteSpace(_turnAnimTrigger) || _ghost == null)
            return;

        Animator animator = _ghost.GetComponentInChildren<Animator>();
        if (animator == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == _turnAnimTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(_turnAnimTrigger);
                return;
            }
        }

        Debug.LogWarning($"[GhostEncounter] Animator ของผีไม่มี Trigger ชื่อ '{_turnAnimTrigger}'", this);
    }

    private void PlayTurnSound()
    {
        if (_turnSound != null && SoundManager.Instance != null && _ghost != null)
            SoundManager.Instance.PlaySFXAttached(_turnSound, _ghost);
    }

    private float FlatDistanceToGhost()
    {
        if (_ghost == null || _player == null)
            return float.MaxValue;
        Vector3 d = _player.transform.position - GhostPosition();
        d.y = 0f;
        return d.magnitude;
    }

    // ตำแหน่งผีที่ "เดินจริง" — บางตัวตัวแม่อยู่นิ่ง ตัวลูก (โมเดล / NavMeshAgent) เป็นคนเดิน
    // ใช้กระดูกหัวก่อน (ขยับตามภาพที่เห็นแน่นอน) แล้วค่อยไล่ตัวเดิน / ตัวแม่
    // ตัวผีที่ขยับจริง: NavMeshAgent อยู่ตัวที่เดิน (ตัวบนสุดของผีไม่ขยับ)
    // ใช้ตัวก่อนหัว — หัวผีโยกเยอะตอนเซ / โน้มตัวตี ระยะเลยแกว่ง รินถอยเกินจริง
    private Vector3 GhostPosition()
    {
        if (_agent != null && _agent.transform != _ghost)
            return _agent.transform.position;
        if (_headBone != null)
            return _headBone.position;
        if (_ghostLocomotion != null)
            return _ghostLocomotion.transform.position;
        return _ghost.position;
    }

    private Transform FaceTarget() => _ghostHead != null ? _ghostHead : _ghost;
    private Vector3 FaceOffset() => _ghostHead != null ? Vector3.zero : _faceOffset;

    // ===== ผู้เล่น =====

    private void HoldPlayer(GameObject player)
    {
        _player = player;

        _locomotion = player.GetComponentInChildren<PlayerLocomotion>(true);
        if (_locomotion != null)
        {
            _locomotion.LockLocomotion(this);
            _movementLocked = true;
        }

        _inventory = player.GetComponentInChildren<PlayerInventory>(true);
        if (_inventory != null)
        {
            _inventory.AddOpenBlocker(this);
            _inventoryBlocked = true;
        }

        _interact = player.GetComponentInChildren<PlayerInteract>(true);
        if (_interact != null)
        {
            _interact.AddInteractBlocker(this);
            _interactBlocked = true;
        }

        _camera = player.GetComponentInChildren<PlayerCameraController>(true);
        if (_camera == null)
            _camera = FindFirstObjectByType<PlayerCameraController>();

        // สายตารินจ้องผีตลอด (แม้ตอนตัดไปมุมอื่น) — ตัดกลับมาเมื่อไหร่ก็มองผีอยู่แล้ว / เมาส์หันไม่ได้
        if (_camera != null)
            _looking = _camera.StartForcedLook(FaceTarget(), FaceOffset(), 999f, _lookTurnTime * 0.5f);
    }

    private void ReleasePlayer(bool stopLook)
    {
        if (_movementLocked && _locomotion != null)
            _locomotion.UnlockLocomotion(this);
        _movementLocked = false;

        if (_inventoryBlocked && _inventory != null)
            _inventory.RemoveOpenBlocker(this);
        _inventoryBlocked = false;

        if (_interactBlocked && _interact != null)
            _interact.RemoveInteractBlocker(this);
        _interactBlocked = false;

        if (_looking && stopLook && _camera != null)
            _camera.StopForcedLook();
        _looking = false;
    }

    // ผีเริ่มเดินเข้ามาเมื่อไหร่ รินพูด + ค่อยๆ ถอยหลัง (ออกตัวนุ่ม / ใกล้ถึงระยะสุดค่อยๆ หยุด) จนกว่าผีจะตี
    // ขยับ CharacterController ตรงๆ — ปุ่มเดินของผู้เล่นยังล็อกอยู่ ชนกำแพงก็หยุดเอง
    // ใช้ทั้งตอนแรก และรอบที่พลาด (ผีถอยไปก่อน ห่างออก แล้วค่อยเดินกลับมา — นับจากตอนที่ห่างสุด)
    private IEnumerator BackOffUntilAttack(SubtitleDataSO lines, float maxDistance)
    {
        CharacterController controller = _player != null ? _player.GetComponentInChildren<CharacterController>() : null;
        float startDistance = FlatDistanceToGhost();
        float farthest = startDistance;

        const float easeTime = 0.6f;
        bool reacting = false;
        float backed = 0f;
        float speed = 0f;
        float waited = 0f;

        while (!_qteStarted && waited < _releaseTimeout)
        {
            waited += Time.deltaTime;

            // ผีเริ่มก้าวเข้ามาแล้ว (ใกล้กว่าตอนห่างสุด) → รินพูด + เริ่มถอย
            float distance = FlatDistanceToGhost();
            farthest = Mathf.Max(farthest, distance);
            if (!reacting && distance < farthest - _reactWhenGhostMoved)
            {
                reacting = true;
                Play(lines);
                if (_logSteps)
                    Debug.Log($"[GhostEncounter] ผีเดินเข้ามาแล้ว (ห่าง {FlatDistanceToGhost():0.0} ม.) — รินพูด + เริ่มถอย | ตัวชน {(controller != null && controller.enabled ? "ใช้ได้" : "ไม่มี/ปิดอยู่")}", this);
            }

            float remaining = maxDistance - backed;
            if (reacting && remaining > 0.001f && _backOffSpeed > 0f && _player != null && _ghost != null)
            {
                float target = remaining < _backOffSpeed * easeTime
                    ? _backOffSpeed * remaining / (_backOffSpeed * easeTime)
                    : _backOffSpeed;
                speed = Mathf.MoveTowards(speed, target, _backOffSpeed / easeTime * Time.deltaTime);

                Vector3 away = _player.transform.position - GhostPosition();
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f)
                {
                    float step = Mathf.Min(speed * Time.deltaTime, remaining);
                    if (!MovePlayer(controller, away.normalized * step))
                        backed = maxDistance;   // ข้างหลังมีของขวาง หยุดถอย
                    else
                        backed += step;
                }
            }

            yield return null;
        }

        if (_logSteps)
            Debug.Log($"[GhostEncounter] จบช่วงผีเดินเข้ามา — {(reacting ? $"ถอยไป {backed:0.00} ม." : $"ผีไม่ได้เดินเข้ามา (ห่างเท่าเดิม {startDistance:0.0} ม.)")} | QTE เริ่ม = {_qteStarted}", this);

        if (!_qteStarted)
            Debug.LogWarning($"[GhostEncounter] {name} ผีไม่ตีภายใน {_releaseTimeout} วิ — คืนการบังคับให้ผู้เล่น", this);
    }

    // ขยับผ่าน CharacterController (ชนกำแพงหยุดเอง) — ถ้าระบบล็อกเดินปิด CharacterController ไว้ ขยับตัวตรงๆ แทน
    private bool MovePlayer(CharacterController controller, Vector3 delta)
    {
        if (controller != null && controller.enabled)
        {
            controller.Move(delta);
            return true;
        }

        if (!_warnedBackOff)
        {
            _warnedBackOff = true;
            Debug.Log($"[GhostEncounter] {name} CharacterController ของผู้เล่น{(controller == null ? "หาไม่เจอ" : "ถูกปิดอยู่")} — ถอยด้วยการขยับตัวตรงๆ แทน", this);
        }

        // เช็คข้างหลังระดับเอวก่อน ไม่ให้ถอยทะลุกำแพง
        Vector3 origin = _player.transform.position + Vector3.up * 0.9f;
        if (Physics.Raycast(origin, delta.normalized, delta.magnitude + 0.4f, ~0, QueryTriggerInteraction.Ignore))
            return false;

        _player.transform.position += delta;
        Physics.SyncTransforms();
        return true;
    }

    // ===== HUD =====

    // ซ่อนลูกของ HUD ทีละอัน ไม่ซ่อนทั้งก้อน — ซับอยู่ใน HUD ด้วย ต้องยังขึ้น (วิธีเดียวกับ TaxiDriveAway)
    private void HideHud()
    {
        if (!_hideHud || _hiddenHud.Count > 0)
            return;

        HUDVisibility hud = FindAnyObjectByType<HUDVisibility>(FindObjectsInactive.Include);
        if (hud == null)
            return;

        // ป้าย QTE ต้องยังขึ้นตอน QTE ในคัตซีน
        BlockQTEPromptUI prompt = FindAnyObjectByType<BlockQTEPromptUI>(FindObjectsInactive.Include);
        HideChildren(hud.transform, prompt != null ? prompt.Visual : null);
    }

    // ซ่อนลูกทีละอัน — อันที่มีป้าย QTE อยู่ข้างใน ไม่ซ่อนทั้งก้อน ลงไปซ่อนลูกข้างในแทน
    private void HideChildren(Transform parent, Transform keep)
    {
        foreach (Transform child in parent)
        {
            if (KeepVisible(child.name))
                continue;

            if (keep != null && keep.IsChildOf(child))
            {
                if (keep != child)
                    HideChildren(child, keep);
                continue;
            }

            bool added = !child.TryGetComponent(out CanvasGroup group);
            if (added)
                group = child.gameObject.AddComponent<CanvasGroup>();

            _hiddenHud.Add(new HiddenGroup { Group = group, Added = added, Alpha = group.alpha });
            group.alpha = 0f;
        }
    }

    private bool KeepVisible(string childName)
    {
        foreach (string keep in _hudKeepVisible)
        {
            if (!string.IsNullOrEmpty(keep) && childName.IndexOf(keep, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private void RestoreHud()
    {
        foreach (HiddenGroup hidden in _hiddenHud)
        {
            if (hidden.Group == null)
                continue;
            if (hidden.Added)
                Destroy(hidden.Group);
            else
                hidden.Group.alpha = hidden.Alpha;
        }
        _hiddenHud.Clear();
    }

    // ===== แถบดำบนล่าง =====

    private void UpdateLetterbox()
    {
        if (!_letterbox)
            return;

        _letterboxAmount = Mathf.MoveTowards(_letterboxAmount, _letterboxTarget, Time.unscaledDeltaTime / 0.35f);

        if (_letterboxAmount <= 0f)
        {
            DestroyLetterbox();
            return;
        }

        if (_letterboxRoot == null)
            CreateLetterbox();

        float size = _letterboxSize * Mathf.SmoothStep(0f, 1f, _letterboxAmount);
        _barTop.anchorMin = new Vector2(0f, 1f - size);
        _barBottom.anchorMax = new Vector2(1f, size);

        // เมนูเปิด (Pause / Tutorial) ไม่ทับเมนู
        UIManager ui = UIManager.Instance;
        _letterboxGroup.alpha = ui != null && ui.HasAnyOpen ? 0f : 1f;
    }

    private void CreateLetterbox()
    {
        _letterboxRoot = new GameObject("GhostEncounter_Letterbox", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(_letterboxRoot, gameObject.scene);

        Canvas canvas = _letterboxRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = LetterboxSortingOrder;

        _letterboxGroup = _letterboxRoot.AddComponent<CanvasGroup>();
        _letterboxGroup.interactable = false;
        _letterboxGroup.blocksRaycasts = false;

        _barTop = CreateBar("Top", new Vector2(0f, 1f), Vector2.one);
        _barBottom = CreateBar("Bottom", Vector2.zero, new Vector2(1f, 0f));
    }

    private RectTransform CreateBar(string barName, Vector2 anchorMin, Vector2 anchorMax)
    {
        var bar = new GameObject(barName, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(_letterboxRoot.transform, false);

        var rect = (RectTransform)bar.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = bar.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return rect;
    }

    private void DestroyLetterbox()
    {
        if (_letterboxRoot != null)
            Destroy(_letterboxRoot);
        _letterboxRoot = null;
        _letterboxGroup = null;
        _barTop = null;
        _barBottom = null;
    }

    // ===== เก็บกวาด =====

    // ถูกปิดกลางคัน (เปลี่ยนซีน / ตาย) — คืนทุกอย่าง ไม่งั้นผู้เล่นค้าง / กล้องค้างมุมอื่น / ผีค้างไม่ขยับ
    private void OnDisable()
    {
        BlockQTE.AnyStarted -= HandleQTEStarted;
        BlockQTE.AnyAttackAfterBlocked -= HandleLateAttack;
        UnmuteTutorialZones();
        if (_activeQte != null)
            _activeQte.DeferTutorials = false;
        ReleasePlayer(true);
        RestoreCameras();
        RestoreHud();

        _letterboxAmount = 0f;
        _letterboxTarget = 0f;
        DestroyLetterbox();

        // กำลังปิดซีน = ผีก็หายไปด้วย ไม่ต้องปลุก (ปลุกตอนนี้ระบบของผีอาจสร้างของใหม่ค้างไว้)
        if (_ghost != null && _ghost.gameObject.activeInHierarchy && gameObject.scene.isLoaded)
            ReleaseGhost();

        _applyHeadYaw = false;

        _running = false;
    }

    private static MonoBehaviour FindComponentByName(Transform root, string typeName)
    {
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null && behaviour.GetType().Name == typeName)
                return behaviour;
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_ghost == null)
            return;

        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.DrawLine(transform.position, _ghost.position + _faceOffset);
    }
#endif
}