using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yantra.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// วางบน UIRoot (ตัวเดียวกับ UIManager) เพื่อให้อยู่ข้าม scene
/// </summary>
[DefaultExecutionOrder(-90)]
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Scenes")]
    [Tooltip("ลาก SceneCatalog asset ใส่ — เก็บ scene ทั้งหมดของเกมไว้ที่เดียว\n" +
             "ระบบอื่นเรียกผ่าน SceneLoader.Instance.Catalog ได้")]
    [SerializeField] private SceneCatalog _catalog;

    [Header("Timing")]
    [Tooltip("เวลาขั้นต่ำที่หน้าโหลดต้องอยู่ — กันจอกระพริบตอนโหลดเร็วเกิน")]
    [SerializeField] private float _minLoadingTime = 1.5f;

    [Tooltip("หน่วงหลัง scene เข้ามาแล้วก่อนปิดหน้าโหลด ให้เกมตั้งตัวทัน")]
    [SerializeField] private float _postLoadHold = 0.4f;

    [Header("Fade ดำ")]
    [Tooltip("Image ดำเต็มจอ + CanvasGroup — ต้องเป็นลูกตัวสุดท้ายของ UIRoot จะได้ทับทุกอย่าง\n" +
             "เว้นว่าง = ไม่มี fade ตัดฉับแบบเดิม")]
    [SerializeField] private CanvasGroup _fader;

    [Tooltip("เวลาที่จอค่อย ๆ ดำลง")]
    [SerializeField, Min(0f)] private float _fadeToBlackDuration = 0.5f;

    [Tooltip("เวลาที่จอค่อย ๆ สว่างขึ้นจากดำ")]
    [SerializeField, Min(0f)] private float _fadeFromBlackDuration = 0.6f;

    [Tooltip("ตอนเปิดเกม (หรือกด Play ใน Editor) เริ่มจากจอดำแล้วค่อย ๆ สว่าง")]
    [SerializeField] private bool _fadeInOnGameStart = true;

    [Header("Options")]
    [Tooltip("โหลดเสร็จแล้วรอให้กดปุ่มก่อนเข้าเกม")]
    [SerializeField] private bool _waitForKeyPress = false;

    [Tooltip("คืน memory หลังโหลด — ช่วยเยอะกับ HDRP แต่กินเวลาเล็กน้อย")]
    [SerializeField] private bool _unloadUnusedAssets = true;

    [Tooltip("ข้าม UnloadUnusedAssets เมื่อโหลดออกจาก Main Menu\n" +
             "หน้าเมนูหลักมี asset น้อยมาก ถ้าไปสแกนคืน memory หลังเข้าด่านใหญ่จะเสียเวลาฟรีหลายวินาที")]
    [SerializeField] private bool _skipUnloadFromMainMenu = true;

    [Tooltip("ความเร็วโหลดเบื้องหลัง ระหว่างอยู่หน้าโหลด\n" +
             "Low = จอลื่นสุดแต่โหลดช้ามาก (2ms/เฟรม)\n" +
             "Normal = สมดุล เร็วขึ้น ~5 เท่าและหน้าโหลดยังลื่น\n" +
             "High = โหลดเร็วสุด แต่จอกระตุก/ค้างระหว่างโหลด")]
    [SerializeField] private ThreadPriority _loadingPriority = ThreadPriority.Normal;

    [Tooltip("เร่งความเร็วอัปโหลด Texture/Mesh ขึ้น GPU ชั่วคราวระหว่างหน้าโหลด แล้วคืนค่าเดิมเมื่อโหลดเสร็จ")]
    [SerializeField] private bool _boostAsyncUpload = true;

    [SerializeField, Range(2, 33)] private int _loadingUploadTimeSliceMs = 16;
    [SerializeField, Range(16, 256)] private int _loadingUploadBufferMB = 64;

    [Header("Debug")]
    [Tooltip("แสดงเวลาที่ใช้ในแต่ละขั้นตอนลง Console หลังโหลดเสร็จ")]
    [SerializeField] private bool _logLoadTimings = true;

    public bool IsLoading { get; private set; }

    /// <summary>
    /// ให้ฉากเปิดของซีนใหม่ (เช่น TaxiDriveAway ที่มีจอดำของตัวเองอยู่แล้ว) ตั้งเป็น true ตอน Start
    /// SceneLoader จะส่งไม้ต่อให้จอดำนั้นทันที แทนที่จะเสียเวลาเฟดดำทับดำซ้ำอีกรอบ
    /// </summary>
    public bool SkipNextFadeIn { get; set; }

    /// <summary>ให้ระบบอื่นอ่านชื่อ scene ได้โดยไม่ต้องถือ reference เอง</summary>
    public SceneCatalog Catalog => _catalog;

    /// <summary>ให้ระบบอื่นฟังได้ เช่น หยุด BGM เมนู, ปิด input</summary>
    public event Action<string> LoadStarted;
    public event Action<string> LoadCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (_catalog == null)
            Debug.LogError("[SceneLoader] ยังไม่ได้ใส่ SceneCatalog — โหลด scene ไม่ได้", this);

        if (_fader != null)
        {
            _fader.alpha = _fadeInOnGameStart ? 1f : 0f;
            _fader.interactable = false;
            _fader.blocksRaycasts = _fadeInOnGameStart;
        }
    }

    private void Start()
    {
        if (_fadeInOnGameStart && _fader != null)
            StartCoroutine(FadeTo(0f, _fadeFromBlackDuration));
    }

    /// <summary>โหลดหน้าเมนูหลัก — ไม่ต้องรู้ชื่อ scene</summary>
    public void LoadMainMenu()
    {
        if (_catalog == null) return;
        LoadScene(_catalog.MainMenu, ScreenId.MainMenu);
    }

    /// <summary>
    /// โหลด scene เกมตัวจริง — ปุ่ม New Game ใช้ตัวนี้
    /// ชี้ไป Gameplay เสมอ ไม่ว่าจะอยู่ใน Editor หรือ build
    /// </summary>
    public void LoadGameplay()
    {
        if (_catalog == null) return;
        LoadScene(_catalog.Gameplay);
    }

    /// <summary>
    /// โหลด scene ทดสอบ — ใช้กับปุ่ม debug เท่านั้น
    ///
    /// แยกเมธอดจาก LoadGameplay เพื่อไม่ต้องสลับ catalog ไปมา
    /// ปุ่ม New Game จะชี้ scene จริงเสมอ ไม่มีทาง commit ผิดไป
    /// </summary>
    public void LoadTestGameplay()
    {
        if (_catalog == null) return;

        if (!_catalog.HasTestScene)
        {
            Debug.LogWarning("[SceneLoader] ยังไม่ได้ใส่ scene ทดสอบใน SceneCatalog " +
                             "— จะโหลด scene จริงแทน", this);
        }

        LoadScene(_catalog.TestGameplay);
    }

    /// <summary>โหลด scene ปัจจุบันใหม่ — ใช้ตอนกด Retry</summary>
    public void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// โหลด scene จากชื่อเรียกที่ตั้งไว้ในช่อง "Scene อื่น ๆ" ของ SceneCatalog
    ///
    /// ใช้เมื่อเพิ่ม scene ใหม่โดยไม่อยากแก้โค้ด — แค่กด + ใน catalog แล้วเรียกชื่อนั้น
    /// เช่น SceneLoader.Instance.LoadSceneByKey("Level02");
    /// </summary>
    public void LoadSceneByKey(string key, ScreenId openAfterLoad = ScreenId.None)
    {
        if (_catalog == null)
        {
            Debug.LogError("[SceneLoader] ยังไม่ได้ใส่ SceneCatalog", this);
            return;
        }

        if (!_catalog.TryGetScene(key, out string sceneName))
        {
            Debug.LogError($"[SceneLoader] ไม่พบ scene ชื่อเรียก '{key}' ใน SceneCatalog " +
                           "— เช็คว่าพิมพ์ตรงกับที่ตั้งไว้ไหม", this);
            return;
        }

        LoadScene(sceneName, openAfterLoad);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// โหลด scene
    /// openAfterLoad: หน้าจอที่จะเปิดหลังโหลดเสร็จ
    ///
    /// จำเป็นตอนกลับมา MainMenuScene เพราะ UIManager ตัวที่ข้าม scene มา
    /// รัน Start() ไปตั้งแต่เปิดเกมแล้ว ค่า Open On Start เลยไม่ทำงานอีก
    /// ส่วน UIManager ตัวใหม่ใน scene ถูกสั่ง Destroy ทิ้งก่อนจะถึง Start()
    /// ถ้าไม่สั่งเปิดเอง stack จะว่างเปล่าแล้วได้จอดำ
    /// </summary>
    public void LoadScene(string sceneName, ScreenId openAfterLoad = ScreenId.None)
    {
        if (IsLoading)
        {
            Debug.LogWarning("[SceneLoader] กำลังโหลดอยู่ ข้ามคำสั่งนี้");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneLoader] ชื่อ scene ว่าง");
            return;
        }

        StartCoroutine(LoadRoutine(sceneName, openAfterLoad));
    }

    // ลำดับ: จอค่อย ๆ ดำ → สว่างเป็นหน้าโหลด → โหลด+Activate ซีนใหม่ใต้หน้าโหลด → ดำสั้น ๆ → สว่างเข้าซีนใหม่
    // งานหนักทั้งหมดทำระหว่างที่ยังโชว์หน้าโหลดอยู่ จอดำช่วงสลับเข้าเกมเลยสั้นเท่าเวลาเฟดจริง ๆ
    private IEnumerator LoadRoutine(string sceneName, ScreenId openAfterLoad)
    {
        IsLoading = true;
        SkipNextFadeIn = false;
        LoadStarted?.Invoke(sceneName);

        var ui = UIManager.Instance;
        string prevSceneName = SceneManager.GetActiveScene().name;
        bool fromMainMenu = _catalog != null && _catalog.IsMainMenu(prevSceneName);

        // หยุดฉากเดิมทั้งหมดระหว่างโหลด — ผู้เล่นต่อย/เดินไม่ได้ ศัตรูก็ไม่ตีตอนจอดำ
        // ทุกอย่างของหน้าโหลด (fade, tip, วิดีโอ) ใช้ unscaled time อยู่แล้ว ไม่ค้าง
        float prevTimeScale = Time.timeScale;
        bool prevSmPaused = StateMachineController.IsPaused;
        Time.timeScale = 0f;
        StateMachineController.IsPaused = true;

        // 1) เช็คก่อนว่ามี scene นี้ใน Build Settings — ไม่มีก็จบตรงนี้ ยังไม่ได้แตะจอ
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneLoader] โหลด '{sceneName}' ไม่ได้ — เพิ่มใน Build Settings หรือยัง");
            Time.timeScale = prevTimeScale;
            StateMachineController.IsPaused = prevSmPaused;
            IsLoading = false;
            yield break;
        }

        // 2) ดำลงทับเมนูที่เปิดอยู่
        yield return FadeTo(1f, _fadeToBlackDuration);

        // 3) ตอนจอดำ: เปิดหน้าโหลด ปิดเมนูทั้งหมด (มันข้าม scene มาด้วยเพราะอยู่ใต้ UIRoot)
        ui.Open(ScreenId.Loading, instant: true);
        ui.CloseAllExcept(ScreenId.Loading, instant: true);
        UIManager.TryGet<LoadingScreen>(ScreenId.Loading, out var screen);

        // 4) สว่างขึ้นเห็นหน้าโหลดให้เต็มก่อน แล้วค่อยเริ่มโหลด
        // ถ้าเริ่มโหลดก่อน ตอนโหลดหนัก ๆ เกมจะค้างกลางทางที่จอยังมืดอยู่ — คนเล่นเห็นจอดำค้างหลายวินาที
        // แล้วพอสว่าง เส้นโหลดก็เต็มไปแล้ว
        yield return FadeTo(0f, _fadeFromBlackDuration);

        float startTime = Time.unscaledTime;
        float tLoadEnd = startTime;
        float tActivateEnd = startTime;
        float tUnloadEnd = startTime;
        bool didUnload = false;

        ThreadPriority prevPriority = Application.backgroundLoadingPriority;
        int prevUploadTimeSlice = QualitySettings.asyncUploadTimeSlice;
        int prevUploadBufferSize = QualitySettings.asyncUploadBufferSize;

        try
        {
            // เร่งความเร็วโหลดเบื้องหลัง + อัปโหลด GPU ชั่วคราวระหว่างอยู่หน้าโหลด
            Application.backgroundLoadingPriority = _loadingPriority;
            if (_boostAsyncUpload)
            {
                QualitySettings.asyncUploadTimeSlice = Mathf.Max(prevUploadTimeSlice, _loadingUploadTimeSliceMs);
                QualitySettings.asyncUploadBufferSize = Mathf.Max(prevUploadBufferSize, _loadingUploadBufferMB);
            }

            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            // ช่วง 0..0.9 ของ Unity = อ่านข้อมูลฉากเบื้องหลัง → แสดงบนแถบ 0..85%
            // เหลือช่วง 85..100% ไว้ให้ตอน Activate ซีนจริง แถบจะได้ไม่ค้างที่ 100%
            while (op.progress < 0.9f)
            {
                screen?.SetProgress((op.progress / 0.9f) * 0.85f);
                yield return null;
            }
            screen?.SetProgress(0.85f);
            tLoadEnd = Time.unscaledTime;

            // Activate ซีนทันทีขณะที่ยังเห็นหน้าโหลดอยู่ (ไม่ใช่ไปทำตอนจอดำ)
            // งานหนักจริง (Awake/OnEnable, โหลด APV/Occlusion/Lightmap, คอมไพล์ shader) จะจบใต้หน้าโหลดนี้
            Application.backgroundLoadingPriority = ThreadPriority.High;
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            Application.backgroundLoadingPriority = prevPriority;
            tActivateEnd = Time.unscaledTime;
            screen?.SetProgress(0.95f);

            // คืน memory ระหว่างที่ยังเห็นหน้าโหลดอยู่ ไม่ไปค้างตอนจอดำ
            // ถ้ามาจาก Main Menu (ซีนเล็กมาก) ข้ามได้ เพื่อไม่ต้องสแกนฉากเกมทั้งด่านฟรี ๆ
            if (_unloadUnusedAssets && !(_skipUnloadFromMainMenu && fromMainMenu))
            {
                didUnload = true;
                yield return Resources.UnloadUnusedAssets();
                GC.Collect();
            }
            tUnloadEnd = Time.unscaledTime;

            // ปล่อยให้ Start() ของซีนใหม่ (เช่น TaxiDriveAway เตรียมวิดีโอ) และเฟรมแรกของ HDRP วอร์มอัปใต้หน้าโหลด
            // ยังคงเร่ง asyncUpload ไว้ตลอด 2 เฟรมนี้ เพื่อให้ Texture/Lightmap อัปขึ้น GPU เร็วขึ้นด้วย
            yield return null;
            yield return null;
        }
        finally
        {
            Application.backgroundLoadingPriority = prevPriority;
            if (_boostAsyncUpload)
            {
                QualitySettings.asyncUploadTimeSlice = prevUploadTimeSlice;
                QualitySettings.asyncUploadBufferSize = prevUploadBufferSize;
            }
        }

        screen?.SetProgress(1f);

        // 5) รอให้ครบเวลาขั้นต่ำ (นับตั้งแต่เห็นหน้าโหลด)
        while (Time.unscaledTime - startTime < _minLoadingTime)
            yield return null;

        // หน่วงสั้น ๆ ตอนแถบ 100% ให้แถบวิ่งจนสุดและเกมตั้งตัวทัน ก่อนเริ่มเฟดดำ
        if (_postLoadHold > 0f)
            yield return new WaitForSecondsRealtime(_postLoadHold);

        // 6) รอกดปุ่ม (ถ้าเปิดออปชันไว้)
        if (_waitForKeyPress)
        {
            screen?.ShowPressAnyKey(true);
            while (!AnyKeyPressed()) yield return null;
            screen?.ShowPressAnyKey(false);
        }

        float tFadeStart = Time.unscaledTime;

        // 7) ดำลงจากหน้าโหลด — งานหนักจบหมดแล้ว จอดำจะสั้นเท่าเวลาเฟดจริง ๆ
        yield return FadeTo(1f, _fadeToBlackDuration);

        // ปลดหยุดเกมหลังหน้าโหลดมืดสนิทและซีนใหม่พร้อมแล้ว
        // ถ้ามีหน้าจออื่นเปิดขึ้นมาแล้ว (เช่น tutorial ตอนเกิด) ปล่อยให้หน้านั้นเป็นคนปลดเอง
        if (ui.Current == ScreenId.Loading)
        {
            Time.timeScale = 1f;
            StateMachineController.IsPaused = false;
        }

        // 8) ยังมืดอยู่ — เปิดหน้าที่ต้องการ ปิดหน้าโหลด
        if (openAfterLoad != ScreenId.None)
            ui.Open(openAfterLoad, instant: true);

        ui.Close(ScreenId.Loading, instant: true);
        LoadCompleted?.Invoke(sceneName);

        // 9) สว่างเข้าซีนใหม่
        // ถ้าฉากเปิดของซีนใหม่ (เช่น TaxiDriveAway) คุมจอดำต่อเองอยู่แล้ว ส่งไม้ต่อทันที ไม่ต้องเฟดดำทับดำเสียเวลา
        if (SkipNextFadeIn)
        {
            SkipNextFadeIn = false;
            yield return null;   // รอ 1 เฟรมให้จอดำของฉากเปิดโผล่รับก่อนปิด fader กันจอกระพริบ
            if (_fader != null)
            {
                _fader.alpha = 0f;
                _fader.blocksRaycasts = false;
            }
        }
        else
        {
            yield return FadeTo(0f, _fadeFromBlackDuration);
        }

        if (_logLoadTimings)
        {
            float now = Time.unscaledTime;
            Debug.Log(
                $"[SceneLoader] โหลด '{sceneName}' เสร็จ (รวม {now - startTime:0.00} วิ) | " +
                $"อ่านฉาก 0→0.9: {tLoadEnd - startTime:0.00} วิ | " +
                $"Activate: {tActivateEnd - tLoadEnd:0.00} วิ | " +
                $"Unload: {(didUnload ? $"{tUnloadEnd - tActivateEnd:0.00} วิ" : "ข้าม")} | " +
                $"Warmup+Hold: {tFadeStart - tUnloadEnd:0.00} วิ | " +
                $"Fade เข้าฉาก: {now - tFadeStart:0.00} วิ", this);
        }

        IsLoading = false;
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        if (_fader == null) yield break;

        // บังคลิกตลอดตอนกำลัง fade กันกดปุ่มเมนูซ้ำ
        _fader.blocksRaycasts = true;

        float start = _fader.alpha;
        float t = 0f;

        while (t < duration)
        {
            // จำกัดต่อเฟรม — เฟรมที่กระตุกตอนสลับ scene จะได้ไม่ทำให้ fade กระโดดข้าม
            t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            _fader.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        _fader.alpha = target;
        _fader.blocksRaycasts = target > 0f;
    }

    private bool AnyKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        return false;
#else
        return Input.anyKeyDown;
#endif
    }
}