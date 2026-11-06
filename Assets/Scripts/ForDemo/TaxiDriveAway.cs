using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yantra.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// ใส่ที่ตัวรถแท็กซี่ — จอดำ + เสียงเปิด/ปิดประตู → ค่อยๆ สว่าง → คุยกัน → รถขับออก → หันกล้องไปทางที่ต้องเดิน
// → จอดำแป๊บนึงให้ตั้งหลัก → สว่างขึ้นพร้อม UI → ผู้เล่นค่อยบังคับได้
public class TaxiDriveAway : MonoBehaviour
{
    [Header("เส้นทาง")]
    [Tooltip("GameObject เปล่าที่วางเรียงตามถนน — รถเริ่มจากจุดที่จอดอยู่ แล้ววิ่งผ่านจุดพวกนี้ตามลำดับ\n" +
             "จุดสุดท้ายควรอยู่ในหมอกหรือหลังต้นไม้ ผู้เล่นจะได้ไม่เห็นตอนรถหายไป")]
    [SerializeField] private Transform[] _waypoints = new Transform[0];

    [Tooltip("ติ๊ก = เลี้ยวโค้งนุ่มๆ ผ่านทุกจุด / ไม่ติ๊ก = วิ่งเป็นเส้นตรงหักมุม")]
    [SerializeField] private bool _smoothPath = true;

    [Header("ข้ามฉาก")]
    [Tooltip("ติ๊ก = กดคลิกซ้าย / Space / Enter เพื่อเร่งฉาก\n" +
             "ตอนจอดำ = ข้ามไปสว่างเลย / ตอนคุย = ข้ามทีละประโยค")]
    [SerializeField] private bool _allowSkip = true;

    [Tooltip("ติ๊ก = ข้ามทั้งฉากตอนเทสใน Editor — รถหายไปเลย เดินได้ทันที\n" +
             "ตอน build เกมจริงช่องนี้ไม่มีผล ลืมเอาติ๊กออกก็ไม่พัง")]
    [SerializeField] private bool _skipInEditor;

    [Tooltip("ติ๊ก = โหลดมาจากเซฟ (Retry / Continue / เพิ่งเซฟ) ไม่ต้องเล่นฉากเปิด รถหายไปเลย\n" +
             "ฉากเปิดเล่นแค่ตอนเริ่มเกมใหม่ หรือตายตอนยังไม่เคยเซฟ")]
    [SerializeField] private bool _skipWhenFromSave = true;

    [Header("ฉากเปิด (จอดำ)")]
    [Tooltip("เสียงเปิดประตู — ถ้ามีไฟล์เดียวที่เปิดแล้วปิดในตัว ใส่ช่องนี้ช่องเดียว ช่องปิดประตูเว้นว่าง")]
    [SerializeField] private SoundID _doorOpenSound;

    [Tooltip("เสียงปิดประตู")]
    [SerializeField] private SoundID _doorCloseSound;

    [Tooltip("จอดำเงียบๆ กี่วินาทีก่อนเสียงเปิดประตู")]
    [SerializeField, Min(0f)] private float _blackBeforeDoor = 0.5f;

    [Tooltip("เปิดประตูแล้วกี่วินาทีถึงปิด (เวลาที่รินก้าวลงจากรถ)")]
    [SerializeField, Min(0f)] private float _doorOpenToClose = 1.2f;

    [Tooltip("ปิดประตูแล้วค้างจอดำอีกกี่วินาที ก่อนเริ่มสว่าง")]
    [SerializeField, Min(0f)] private float _blackAfterDoor = 0.6f;

    [Tooltip("จอดำค่อยๆ สว่างกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _fadeInDuration = 1.5f;

    [Header("บทพูด")]
    [Tooltip("ไฟล์บทพูด (Subtitle Data) — เว้นว่าง = ไม่คุย รถออกเลย")]
    [SerializeField] private SubtitleDataSO _dialogue;

    [Tooltip("จอสว่างเต็มแล้วรอกี่วินาทีก่อนเริ่มคุย")]
    [SerializeField, Min(0f)] private float _startDelay = 0.5f;

    [Tooltip("คุยจบแล้วรอกี่วินาทีก่อนรถออก")]
    [SerializeField, Min(0f)] private float _afterDialogueDelay = 0.8f;

    [Header("ล็อกผู้เล่นตอนคุย")]
    [Tooltip("ห้ามเดินจนกว่ารถจะออก")]
    [SerializeField] private bool _lockMovement = true;

    [Tooltip("ห้ามหันกล้องจนกว่ารถจะออก")]
    [SerializeField] private bool _lockLook = true;

    [Tooltip("ตอนล็อกกล้อง ให้กล้องหันไปหารถเอง แล้วมองตามตอนรถขับออกไป")]
    [SerializeField] private bool _lookAtTaxi = true;

    [Tooltip("สคริปต์อื่นที่อยากปิดไว้ตอนคุย เช่น สคริปต์โจมตี — ลากมาใส่ได้เลย จะเปิดคืนให้เองตอนปลดล็อก")]
    [SerializeField] private Behaviour[] _alsoDisable = new Behaviour[0];

    [Tooltip("รถออกตัวไปกี่วินาที ถึงเริ่มหันกล้องไปทางที่ต้องเดิน แล้วให้ผู้เล่นบังคับได้")]
    [SerializeField, Min(0f)] private float _unlockDelay = 1.5f;

    [Header("หลังจบฉาก")]
    [Tooltip("GameObject เปล่า วางไว้ทางที่ผู้เล่นต้องเดินไปต่อ (ทางเข้า / ประตู / ปากทาง)\n" +
             "จบฉากแล้วกล้องจะหันไปทางนี้ก่อน แล้วค่อยปล่อยให้บังคับ\n" +
             "เว้นว่าง = ปล่อยให้บังคับเลย กล้องค้างมองทางรถเหมือนเดิม")]
    [SerializeField] private Transform _faceAtEnd;

    [Tooltip("กล้องใช้เวลาหันไปทางที่ต้องเดินกี่วินาที — ค่ามาก = หันช้าลง นุ่มขึ้น")]
    [SerializeField, Min(0.1f)] private float _faceTurnDuration = 1.2f;

    [Header("ตั้งหลักก่อนเล่น")]
    [Tooltip("หันไปทางที่ต้องเดินเสร็จ → จอดำแป๊บนึง → สว่างขึ้นพร้อม UI แล้วค่อยให้บังคับ")]
    [SerializeField] private bool _regroupFade = true;

    [Tooltip("จอค่อยๆ มืดลงกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _regroupFadeOut = 0.6f;

    [Tooltip("ค้างจอดำกี่วินาที")]
    [SerializeField, Min(0f)] private float _regroupHold = 0.5f;

    [Tooltip("จอค่อยๆ สว่างกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _regroupFadeIn = 0.9f;

    [Header("ซ่อน UI ระหว่างฉาก")]
    [Tooltip("ซ่อนหลอดเลือด / Guard / SP / HUD ปืน ระหว่างฉากรถ — โผล่กลับมาตอนจอสว่างรอบสุดท้าย")]
    [SerializeField] private bool _hideHud = true;

    [Tooltip("ลูกของ HUD ที่ชื่อมีคำพวกนี้จะไม่ถูกซ่อน (ซับไตเติลต้องขึ้นตอนคุยกับคนขับ)")]
    [SerializeField] private string[] _hudKeepVisible = { "Subtitle" };

    [Tooltip("ห้ามเปิดกระเป๋าระหว่างฉาก ถ้าเปิดค้างอยู่จะปิดให้")]
    [SerializeField] private bool _blockInventory = true;

    [Header("การขับ")]
    [Tooltip("ความเร็วสูงสุด เมตร/วินาที (12 ≈ 43 กม./ชม.)")]
    [SerializeField, Min(0.1f)] private float _maxSpeed = 12f;

    [Tooltip("เร่งเร็วแค่ไหน — ค่าน้อย = ค่อยๆ ออกตัว")]
    [SerializeField, Min(0.1f)] private float _acceleration = 3f;

    [Tooltip("ความไวในการหักพวงมาลัย — ค่าน้อย = เลี้ยวนุ่มขึ้น")]
    [SerializeField, Min(0.1f)] private float _steering = 5f;

    [Tooltip("ถ้าโมเดลหันหัวไม่ตรงลูกศรสีฟ้า (แกน Z) ให้หมุนชดเชยตรงนี้ เช่น 90 หรือ 180")]
    [SerializeField] private float _yawOffset = 0f;

    [Tooltip("ติ๊ก = ซ่อนรถเมื่อถึงจุดสุดท้าย")]
    [SerializeField] private bool _hideAtEnd = true;

    [Header("ล้อ (ไม่ใส่ก็ได้)")]
    [SerializeField] private Transform[] _wheels = new Transform[0];
    [SerializeField, Min(0.05f)] private float _wheelRadius = 0.35f;
    [Tooltip("แกนที่ล้อหมุน (ของตัวล้อเอง) ปกติคือแกน X")]
    [SerializeField] private Vector3 _wheelAxis = Vector3.right;

    [Header("เสียงรถ (ผ่าน SoundManager / ไม่ใส่ก็ได้)")]
    [Tooltip("เสียงเครื่องติดรอ — เล่นวนตั้งแต่เข้าฉาก (ตอนจอยังดำก็ได้ยินแล้ว)")]
    [SerializeField] private SoundID _idleSound;

    [Tooltip("เสียงรถขับออกไป — เล่นตอนออกตัว แล้วเสียงติดเครื่องรอจะค่อยๆ เบาลงแทน\n" +
             "เว้นว่าง = ใช้เสียงติดเครื่องรอต่อไปจนรถหาย")]
    [SerializeField] private SoundID _driveAwaySound;

    [Tooltip("ติ๊ก = เสียงขับออกเป็นเสียงวน เล่นจนรถหาย\n" +
             "ไม่ติ๊ก = เล่นครั้งเดียวจบ (ไฟล์เสียงรถออกตัวแล้วไกลออกไปเอง)")]
    [SerializeField] private bool _driveAwayLoops;

    [Tooltip("เสียงติดเครื่องรอค่อยๆ เบาลงกี่วินาที ตอนเสียงขับออกเริ่ม")]
    [SerializeField, Min(0f)] private float _idleCrossfade = 0.6f;

    [Tooltip("เสียงรถค่อยๆ เงียบกี่วินาทีก่อนถึงจุดสุดท้าย")]
    [SerializeField, Min(0.1f)] private float _engineFadeTime = 2f;

    private const int SamplesPerSegment = 16;
    private const int BlackSortingOrder = 32000;
    private const float TaxiFollowSmooth = 0.25f;

    private readonly List<Vector3> _points = new List<Vector3>();
    private readonly List<float> _distances = new List<float>();
    private readonly List<Behaviour> _disabled = new List<Behaviour>();
    private float _length;

    private CanvasGroup _black;
    private float _blackAlpha = 1f;

    private SFXHandle _idle;
    private SFXHandle _drive;
    private bool _idleFading;

    private PlayerLocomotion _locomotion;
    private PlayerCameraController _camera;
    private bool _playerLocked;
    private bool _releasing;
    private bool _facingGoal;

    private bool _lookReady;
    private float _lookYaw;
    private float _lookYawVelocity;

    private sealed class HiddenGroup
    {
        public CanvasGroup Group;
        public bool Added;
        public float Alpha;
    }

    private readonly List<HiddenGroup> _hiddenHud = new List<HiddenGroup>();
    private bool _hudHidden;
    private bool _hudDone;

    private PlayerInventory _inventory;
    private InventoryUI _inventoryUI;
    private bool _inventoryBlocked;

    private enum IntroPhase { Black, Dialogue, Done }
    private IntroPhase _phase = IntroPhase.Black;
    private bool _hurry;

    private void Start()
    {
        if ((_skipInEditor && Application.isEditor) || (_skipWhenFromSave && IsLoadedFromSave()))
        {
            gameObject.SetActive(false);
            return;
        }

        BuildPath(transform.position, _points, _distances);
        _length = _distances.Count > 0 ? _distances[_distances.Count - 1] : 0f;

        if (TryGetComponent(out Rigidbody body))
            body.isKinematic = true;

        _black = CreateBlackScreen();

        _playerLocked = true;
        foreach (Behaviour behaviour in _alsoDisable)
        {
            if (behaviour == null || !behaviour.enabled)
                continue;
            behaviour.enabled = false;
            _disabled.Add(behaviour);
        }
        TryLockPlayer();

        _idle = PlayLoop(_idleSound);
        StartCoroutine(IntroRoutine());
    }

    private void OnDisable()
    {
        _idle.Stop();
        _drive.Stop();
        UnlockPlayer();
        DestroyBlackScreen();
    }

    private void Update()
    {
        // มีเมนูเปิด (Pause / หน้าโหลด) ให้เมนูโผล่ ไม่โดนจอดำทับ
        if (_black != null)
            _black.alpha = IsMenuOpen() ? 0f : _blackAlpha;

        if (_allowSkip && _phase != IntroPhase.Done && CanSkipNow() && SkipPressed())
        {
            if (_phase == IntroPhase.Black)
                _hurry = true;
            else if (SubtitleSystem.Instance != null)
                SubtitleSystem.Instance.SkipLine();
        }

        if (!_playerLocked)
            return;

        TryLockPlayer();

        if (_camera == null || _facingGoal)
            return;

        if (!_lookReady)
        {
            _lookYaw = _camera.CameraRotation.x;
            _lookReady = true;
        }

        if (_lookAtTaxi)
            FollowTaxi();
    }

    private IEnumerator IntroRoutine()
    {
        while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
            yield return null;

        yield return WaitOrHurry(_blackBeforeDoor);

        if (_doorOpenSound != null && !_hurry)
        {
            PlayOneShot(_doorOpenSound);
            yield return WaitOrHurry(_doorOpenToClose);
        }

        if (_doorCloseSound != null && !_hurry)
            PlayOneShot(_doorCloseSound);

        yield return WaitOrHurry(_blackAfterDoor);
        yield return FadeInRoutine();
        yield return WaitOrHurry(_startDelay);

        _phase = IntroPhase.Dialogue;
        if (_dialogue != null)
            yield return PlayDialogue();
        _phase = IntroPhase.Done;

        yield return new WaitForSeconds(_afterDialogueDelay);

        if (_length <= 0.01f)
        {
            Debug.LogWarning("[Taxi] ยังไม่ได้วางจุด Waypoints — รถจะไม่ขยับ", this);
            BeginRelease();
            yield break;
        }

        yield return DriveRoutine();
    }

    // กดข้ามตอนจอดำ = ตัดเวลารอที่เหลือทิ้งหมด
    private IEnumerator WaitOrHurry(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds && !_hurry)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // มีจุดเกิดจากเซฟ = ไม่ใช่การเริ่มเกมใหม่ (เริ่มใหม่ SaveManager จะล้างข้อมูลเป็นค่าว่าง)
    private static bool IsLoadedFromSave()
    {
        SaveManager save = SaveManager.Instance;
        return save != null && !string.IsNullOrEmpty(save.SpawnPointId);
    }

    private static bool CanSkipNow()
    {
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
            return false;
        return !IsMenuOpen();
    }

    private static bool SkipPressed()
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

    private IEnumerator FadeInRoutine()
    {
        float elapsed = 0f;
        while (elapsed < _fadeInDuration && !_hurry)
        {
            elapsed += Time.deltaTime;
            _blackAlpha = 1f - Mathf.SmoothStep(0f, 1f, elapsed / _fadeInDuration);
            yield return null;
        }

        _blackAlpha = 0f;
        DestroyBlackScreen();
    }

    private IEnumerator PlayDialogue()
    {
        SubtitleSystem subtitles = SubtitleSystem.Instance;
        if (subtitles == null)
        {
            Debug.LogWarning("[Taxi] ไม่เจอ SubtitleSystem — ข้ามบทพูด", this);
            yield break;
        }

        subtitles.PlaySubtitle(_dialogue);

        // กด Pause ระหว่างคุย ระบบซับหยุดรอให้เอง เลยรอแค่จนกว่าจะเล่นจบ
        while (subtitles != null && subtitles.IsPlaying)
            yield return null;
    }

    private IEnumerator DriveRoutine()
    {
        StartDriveSound();

        float speed = 0f;
        float travelled = 0f;
        float driveTime = 0f;
        int positionCursor = 0;
        int aheadCursor = 0;

        while (travelled < _length)
        {
            float dt = Time.deltaTime;

            driveTime += dt;
            if (driveTime >= _unlockDelay)
                BeginRelease();

            speed = Mathf.Min(_maxSpeed, speed + _acceleration * dt);
            travelled = Mathf.Min(_length, travelled + speed * dt);

            Vector3 position = Sample(travelled, ref positionCursor);
            Vector3 ahead = Sample(Mathf.Min(_length, travelled + Mathf.Max(1.5f, speed * 0.4f)), ref aheadCursor);
            transform.position = position;

            Vector3 direction = ahead - position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, _yawOffset, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-_steering * dt));
            }

            SpinWheels(speed * dt);

            float timeLeft = speed > 0.01f ? (_length - travelled) / speed : _engineFadeTime;
            float volume = Mathf.Clamp01(timeLeft / _engineFadeTime);
            if (volume < 1f)
                FadeEngine(volume);

            yield return null;
        }

        _idle.Stop();
        _drive.Stop();

        // รอกล้องหันเสร็จก่อนซ่อนรถ ไม่งั้นการหันจะโดนตัดกลางทาง
        BeginRelease();
        while (_playerLocked)
            yield return null;

        if (_hideAtEnd)
            gameObject.SetActive(false);
    }

    // ---------- กล้อง ----------

    private void FollowTaxi()
    {
        if (!TryGetYawTo(transform.position, out float target))
            return;

        // ตอนจอยังดำสนิท หันทันที พอสว่างขึ้นมาจะได้มองรถอยู่แล้ว
        if (_blackAlpha >= 1f)
        {
            _lookYaw = target;
            _lookYawVelocity = 0f;
        }
        else
        {
            _lookYaw = Mathf.SmoothDampAngle(_lookYaw, target, ref _lookYawVelocity, TaxiFollowSmooth);
        }

        _camera.SetLookTargetYaw(_lookYaw, 0f);
    }

    private void BeginRelease()
    {
        if (_releasing)
            return;
        _releasing = true;
        StartCoroutine(FaceGoalThenUnlock());
    }

    private IEnumerator FaceGoalThenUnlock()
    {
        if (_faceAtEnd != null && _lockLook && _camera != null && TryGetYawTo(_faceAtEnd.position, out float goalYaw))
        {
            _facingGoal = true;

            float duration = _faceTurnDuration;
            float startYaw = _camera.CameraRotation.x;
            float turn = Mathf.DeltaAngle(startYaw, goalYaw);

            // เริ่มหมุนต่อจากความเร็วที่มองตามรถอยู่ แล้วค่อยๆ ช้าลงจนหยุดตรงทางเดิน กล้องจะได้ไม่กระตุก
            float maxStartSpeed = Mathf.Abs(turn) * 2f / duration;
            float startSpeed = Mathf.Clamp(_lookYawVelocity, -maxStartSpeed, maxStartSpeed);

            float elapsed = 0f;
            while (elapsed < duration && _camera != null)
            {
                elapsed += Time.deltaTime;
                float s = Mathf.Clamp01(elapsed / duration);
                float s2 = s * s;
                float s3 = s2 * s;

                _lookYaw = startYaw + (s3 - 2f * s2 + s) * duration * startSpeed + (3f * s2 - 2f * s3) * turn;
                _camera.SetLookTargetYaw(_lookYaw, 0f);
                yield return null;
            }
        }

        if (_regroupFade)
        {
            _facingGoal = true;   // ระหว่างจอดำไม่ต้องมองตามรถแล้ว
            yield return RegroupRoutine();
        }

        _facingGoal = false;
        UnlockPlayer();
    }

    // จอดำแป๊บนึงให้ผู้เล่นตั้งหลัก แล้วสว่างขึ้นพร้อม UI — ระหว่างนี้ผู้เล่นยังโดนล็อกอยู่
    private IEnumerator RegroupRoutine()
    {
        DestroyBlackScreen();
        _blackAlpha = 0f;
        _black = CreateBlackScreen();
        _black.alpha = 0f;   // จอดำที่สร้างใหม่เริ่มทึบ ต้องตั้งเป็นใสก่อน ไม่งั้นจอกะพริบดำ 1 เฟรม

        yield return FadeBlack(0f, 1f, _regroupFadeOut);

        RestoreHud();

        float hold = 0f;
        while (hold < _regroupHold)
        {
            hold += Time.deltaTime;
            yield return null;
        }

        yield return FadeBlack(1f, 0f, _regroupFadeIn);
        DestroyBlackScreen();
    }

    private IEnumerator FadeBlack(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _blackAlpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        _blackAlpha = to;
    }

    private bool TryGetYawTo(Vector3 point, out float yaw)
    {
        yaw = 0f;
        Vector3 to = point - _camera.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f)
            return false;

        yaw = Quaternion.LookRotation(to).eulerAngles.y;
        return true;
    }

    // ---------- จอดำ ----------

    private CanvasGroup CreateBlackScreen()
    {
        var root = new GameObject("TaxiIntro_Black", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = BlackSortingOrder;

        var group = root.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = IsMenuOpen() ? 0f : 1f;

        var child = new GameObject("Black", typeof(RectTransform), typeof(Image));
        child.transform.SetParent(root.transform, false);

        var rect = (RectTransform)child.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = child.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        return group;
    }

    private void DestroyBlackScreen()
    {
        if (_black != null)
            Destroy(_black.gameObject);
        _black = null;
    }

    private static bool IsMenuOpen()
    {
        UIManager ui = UIManager.Instance;
        return ui != null && ui.HasAnyOpen;
    }

    // ---------- ผู้เล่น ----------

    // ลองหาทุกเฟรมจนเจอ และเลือกตัวที่อยู่ฉากเดียวกับรถก่อน
    // ตอนโหลดฉากใหม่ ผู้เล่นตัวเก่ายังค้างอยู่อีกไม่กี่เฟรม ถ้าไปล็อกตัวเก่า ตัวจริงจะหันกล้องได้
    private void TryLockPlayer()
    {
        TryHideHud();
        TryBlockInventory();

        if (_lockMovement && _locomotion == null)
        {
            _locomotion = FindInMyScene<PlayerLocomotion>();
            if (_locomotion != null)
                _locomotion.LockLocomotion(this);
        }

        if (!_lockLook)
            return;

        if (_camera == null)
            _camera = FindInMyScene<PlayerCameraController>();

        // ระบบอื่นเผลอปลดล็อกกล้องกลางทาง ล็อกกลับให้
        if (_camera != null && !_camera.IsLookLocked)
            _camera.IsLookLocked = true;
    }

    private void UnlockPlayer()
    {
        if (!_playerLocked)
            return;
        _playerLocked = false;

        if (_locomotion != null)
            _locomotion.UnlockLocomotion(this);

        foreach (Behaviour behaviour in _disabled)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }
        _disabled.Clear();

        if (_camera != null)
        {
            _camera.ClearLookTargetYaw();
            _camera.IsLookLocked = false;
        }

        RestoreHud();

        if (_inventoryBlocked && _inventory != null)
            _inventory.RemoveOpenBlocker(this);
        _inventoryBlocked = false;
    }

    // ---------- UI ----------

    // ซ่อนลูกของ HUD ทีละอัน ไม่ซ่อนทั้งก้อน — ซับไตเติลอยู่ใน HUD ด้วย ต้องยังขึ้นตอนคุย
    private void TryHideHud()
    {
        if (!_hideHud || _hudDone)
            return;

        if (!_hudHidden)
        {
            HUDVisibility hud = FindAnyObjectByType<HUDVisibility>(FindObjectsInactive.Include);
            if (hud == null)
                return;

            _hudHidden = true;
            foreach (Transform child in hud.transform)
            {
                if (KeepVisible(child.name))
                    continue;

                bool added = !child.TryGetComponent(out CanvasGroup group);
                if (added)
                    group = child.gameObject.AddComponent<CanvasGroup>();
                _hiddenHud.Add(new HiddenGroup { Group = group, Added = added, Alpha = group.alpha });
            }
        }

        foreach (HiddenGroup hidden in _hiddenHud)
        {
            if (hidden.Group != null)
                hidden.Group.alpha = 0f;
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
        _hudDone = true;
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

    private void TryBlockInventory()
    {
        if (!_blockInventory)
            return;

        if (!_inventoryBlocked)
        {
            _inventory = FindInMyScene<PlayerInventory>();
            if (_inventory != null)
            {
                _inventory.AddOpenBlocker(this);
                _inventoryBlocked = true;
            }
        }

        if (_inventoryUI == null)
            _inventoryUI = FindInMyScene<InventoryUI>();

        // ปิดกระเป๋าก่อนล็อกกล้อง — SetOpen(false) ปลดล็อกกล้องให้ เดี๋ยวโค้ดข้างล่างล็อกกลับเอง
        if (_inventoryUI != null && _inventoryUI.IsOpen)
            _inventoryUI.SetOpen(false);
    }

    private T FindInMyScene<T>() where T : Component
    {
        T fallback = null;
        foreach (T found in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (found.gameObject.scene == gameObject.scene)
                return found;
            if (fallback == null)
                fallback = found;
        }
        return fallback;
    }

    // ---------- เสียง ----------

    private SFXHandle PlayLoop(SoundID id)
    {
        if (id == null || SoundManager.Instance == null)
            return SFXHandle.None;
        return SoundManager.Instance.PlayLoopSFXForeverAttached(id, transform);
    }

    private void PlayOneShot(SoundID id)
    {
        if (id != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFXAttached(id, transform);
    }

    private void StartDriveSound()
    {
        if (_driveAwaySound == null || SoundManager.Instance == null)
            return;

        _drive = _driveAwayLoops
            ? SoundManager.Instance.PlayLoopSFXForeverAttached(_driveAwaySound, transform)
            : SoundManager.Instance.PlaySFXAttachedTracked(_driveAwaySound, transform, out _);

        _idle.FadeOutAndStop(_idleCrossfade);
        _idleFading = true;
    }

    private void FadeEngine(float volume)
    {
        _drive.SetVolumeMultiplier(volume);
        if (!_idleFading)
            _idle.SetVolumeMultiplier(volume);
    }

    // ---------- เส้นทาง ----------

    private void SpinWheels(float distance)
    {
        if (_wheels.Length == 0)
            return;
        float degrees = distance / _wheelRadius * Mathf.Rad2Deg;
        foreach (Transform wheel in _wheels)
            if (wheel != null)
                wheel.Rotate(_wheelAxis, degrees, Space.Self);
    }

    // ระยะที่ขอเพิ่มขึ้นเรื่อยๆ เลยเดินหน้า cursor ต่อจากเฟรมก่อนได้ ไม่ต้องค้นใหม่ทุกเฟรม
    private Vector3 Sample(float distance, ref int cursor)
    {
        if (cursor >= _distances.Count - 1 || distance < _distances[cursor])
            cursor = 0;
        while (cursor < _distances.Count - 2 && _distances[cursor + 1] < distance)
            cursor++;

        float segment = _distances[cursor + 1] - _distances[cursor];
        float t = segment > 0.0001f ? (distance - _distances[cursor]) / segment : 0f;
        return Vector3.Lerp(_points[cursor], _points[cursor + 1], t);
    }

    private void BuildPath(Vector3 start, List<Vector3> points, List<float> distances)
    {
        points.Clear();
        distances.Clear();

        var keys = new List<Vector3> { start };
        foreach (Transform waypoint in _waypoints)
            if (waypoint != null)
                keys.Add(waypoint.position);
        if (keys.Count < 2)
            return;

        points.Add(keys[0]);
        for (int i = 0; i < keys.Count - 1; i++)
        {
            if (!_smoothPath || keys.Count < 3)
            {
                points.Add(keys[i + 1]);
                continue;
            }

            Vector3 p0 = keys[Mathf.Max(0, i - 1)];
            Vector3 p1 = keys[i];
            Vector3 p2 = keys[i + 1];
            Vector3 p3 = keys[Mathf.Min(keys.Count - 1, i + 2)];
            for (int s = 1; s <= SamplesPerSegment; s++)
                points.Add(CatmullRom(p0, p1, p2, p3, s / (float)SamplesPerSegment));
        }

        float total = 0f;
        distances.Add(0f);
        for (int i = 1; i < points.Count; i++)
        {
            total += Vector3.Distance(points[i - 1], points[i]);
            distances.Add(total);
        }
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    private void OnDrawGizmos()
    {
        if (_faceAtEnd != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_faceAtEnd.position, 0.6f);
        }

        if (_waypoints == null || _waypoints.Length == 0)
            return;

        var points = new List<Vector3>();
        var distances = new List<float>();
        BuildPath(Application.isPlaying && _points.Count > 0 ? _points[0] : transform.position, points, distances);

        Gizmos.color = new Color(1f, 0.85f, 0.1f);
        for (int i = 1; i < points.Count; i++)
            Gizmos.DrawLine(points[i - 1], points[i]);
        foreach (Transform waypoint in _waypoints)
            if (waypoint != null)
                Gizmos.DrawWireSphere(waypoint.position, 0.4f);
    }
}