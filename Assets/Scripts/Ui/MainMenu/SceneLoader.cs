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

    [Header("Timing")]
    [Tooltip("เวลาขั้นต่ำที่หน้าโหลดต้องอยู่ — กันจอกระพริบตอนโหลดเร็วเกิน")]
    [SerializeField] private float _minLoadingTime = 1.5f;

    [Tooltip("หน่วงหลัง scene เข้ามาแล้วก่อนปิดหน้าโหลด ให้เกมตั้งตัวทัน")]
    [SerializeField] private float _postLoadHold = 0.4f;

    [Header("Options")]
    [Tooltip("โหลดเสร็จแล้วรอให้กดปุ่มก่อนเข้าเกม")]
    [SerializeField] private bool _waitForKeyPress = false;

    [Tooltip("คืน memory หลังโหลด — ช่วยเยอะกับ HDRP แต่กินเวลาเล็กน้อย")]
    [SerializeField] private bool _unloadUnusedAssets = true;

    public bool IsLoading { get; private set; }

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
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void LoadScene(string sceneName)
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

        StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        IsLoading = true;
        LoadStarted?.Invoke(sceneName);

        var ui = UIManager.Instance;

        // 1) เปิดหน้าโหลดทับทุกอย่าง แล้วรอให้ fade เข้าจนทึบก่อน
        ui.Open(ScreenId.Loading);
        yield return new WaitForSecondsRealtime(0.25f);

        // 2) ปิดเมนูทั้งหมด (มันข้าม scene มาด้วยเพราะอยู่ใต้ UIRoot)
        ui.CloseAllExcept(ScreenId.Loading, instant: true);

        UIManager.TryGet<LoadingScreen>(ScreenId.Loading, out var screen);

        // 3) กันเคสโหลดตอนเกม pause อยู่ ไม่งั้น coroutine ที่ใช้ scaled time จะค้าง
        Time.timeScale = 1f;

        // 4) เริ่มโหลดจริง แต่ยังไม่ให้สลับ scene
        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[SceneLoader] โหลด '{sceneName}' ไม่ได้ — เพิ่มใน Build Settings หรือยัง");
            ui.Close(ScreenId.Loading);
            IsLoading = false;
            yield break;
        }

        op.allowSceneActivation = false;

        float startTime = Time.unscaledTime;

        // progress ของ Unity จะตันที่ 0.9 เมื่อ allowSceneActivation = false
        while (op.progress < 0.9f)
        {
            screen?.SetProgress(op.progress / 0.9f);
            yield return null;
        }

        screen?.SetProgress(1f);

        // 5) รอให้ครบเวลาขั้นต่ำ
        while (Time.unscaledTime - startTime < _minLoadingTime)
            yield return null;

        // 6) รอกดปุ่ม (ถ้าเปิดออปชันไว้)
        if (_waitForKeyPress)
        {
            screen?.ShowPressAnyKey(true);
            while (!AnyKeyPressed()) yield return null;
            screen?.ShowPressAnyKey(false);
        }

        // 7) สลับ scene
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // 8) เก็บกวาด memory ตอนหน้าโหลดยังบังอยู่
        if (_unloadUnusedAssets)
        {
            yield return Resources.UnloadUnusedAssets();
            GC.Collect();
        }

        yield return new WaitForSecondsRealtime(_postLoadHold);

        // 9) เปิดจอ
        ui.Close(ScreenId.Loading);

        IsLoading = false;
        LoadCompleted?.Invoke(sceneName);
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