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

    [Header("Options")]
    [Tooltip("โหลดเสร็จแล้วรอให้กดปุ่มก่อนเข้าเกม")]
    [SerializeField] private bool _waitForKeyPress = false;

    [Tooltip("คืน memory หลังโหลด — ช่วยเยอะกับ HDRP แต่กินเวลาเล็กน้อย")]
    [SerializeField] private bool _unloadUnusedAssets = true;

    public bool IsLoading { get; private set; }

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
            _fader.alpha = 0f;
            _fader.interactable = false;
            _fader.blocksRaycasts = false;
        }
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

    // ลำดับ: จอค่อย ๆ ดำ → สว่างเป็นหน้าโหลด → โหลดเสร็จค่อย ๆ ดำ → สว่างเข้าซีนใหม่
    // ทุกอย่างที่เปิด/ปิดหน้าจอ ทำตอนจอดำสนิท คนเล่นเลยไม่เห็นอะไรกระพริบ
    private IEnumerator LoadRoutine(string sceneName, ScreenId openAfterLoad)
    {
        IsLoading = true;
        LoadStarted?.Invoke(sceneName);

        var ui = UIManager.Instance;

        // 1) ดำลงทับเมนูที่เปิดอยู่
        yield return FadeTo(1f, _fadeToBlackDuration);

        // 2) เริ่มโหลด — ถ้าโหลดไม่ได้ สว่างกลับมาที่เมนูเดิม
        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[SceneLoader] โหลด '{sceneName}' ไม่ได้ — เพิ่มใน Build Settings หรือยัง");
            yield return FadeTo(0f, _fadeFromBlackDuration);
            IsLoading = false;
            yield break;
        }

        op.allowSceneActivation = false;

        // 3) กันเคสโหลดตอนเกม pause อยู่
        Time.timeScale = 1f;

        // 4) ตอนจอดำ: เปิดหน้าโหลด ปิดเมนูทั้งหมด (มันข้าม scene มาด้วยเพราะอยู่ใต้ UIRoot)
        ui.Open(ScreenId.Loading, instant: true);
        ui.CloseAllExcept(ScreenId.Loading, instant: true);
        UIManager.TryGet<LoadingScreen>(ScreenId.Loading, out var screen);

        // 5) สว่างขึ้นเห็นหน้าโหลด
        yield return FadeTo(0f, _fadeFromBlackDuration);

        float startTime = Time.unscaledTime;

        // progress ของ Unity จะตันที่ 0.9 เมื่อ allowSceneActivation = false
        while (op.progress < 0.9f)
        {
            screen?.SetProgress(op.progress / 0.9f);
            yield return null;
        }

        screen?.SetProgress(1f);

        // 6) รอให้ครบเวลาขั้นต่ำ (นับตั้งแต่เห็นหน้าโหลด)
        while (Time.unscaledTime - startTime < _minLoadingTime)
            yield return null;

        // 7) รอกดปุ่ม (ถ้าเปิดออปชันไว้)
        if (_waitForKeyPress)
        {
            screen?.ShowPressAnyKey(true);
            while (!AnyKeyPressed()) yield return null;
            screen?.ShowPressAnyKey(false);
        }

        // 8) ดำลงอีกรอบ แล้วค่อยสลับ scene ตอนมืด
        yield return FadeTo(1f, _fadeToBlackDuration);

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        if (_unloadUnusedAssets)
        {
            yield return Resources.UnloadUnusedAssets();
            GC.Collect();
        }

        yield return new WaitForSecondsRealtime(_postLoadHold);

        // 9) ยังมืดอยู่ — เปิดหน้าที่ต้องการ ปิดหน้าโหลด
        if (openAfterLoad != ScreenId.None)
            ui.Open(openAfterLoad, instant: true);

        ui.Close(ScreenId.Loading, instant: true);
        LoadCompleted?.Invoke(sceneName);

        // 10) สว่างเข้าซีนใหม่
        yield return FadeTo(0f, _fadeFromBlackDuration);

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