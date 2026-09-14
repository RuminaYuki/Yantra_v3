using UnityEngine;
using UnityEngine.SceneManagement;
using Yantra.UI;

/// <summary>
/// คุมว่า HUD ควรโชว์ตอนไหน — แปะบน HUDScreen
///
/// HUD ไม่ใช่ UIScreen เพราะมันไม่เข้า stack
/// (ถ้าเข้า stack เวลาเปิด Pause มันจะถูกนับเป็นหน้าที่ต้องปิด/เปิดด้วย ซึ่งผิด)
/// เลยต้องมีตัวคุมแยกที่ตัดสินจากสถานการณ์แทน
/// </summary>
public class HUDVisibility : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("ชื่อ scene ที่ไม่ควรมี HUD")]
    [SerializeField] private string[] _hiddenInScenes = { "MainMenuScene" };

    [Header("Hide During")]
    [Tooltip("ซ่อนตอนมีเมนูเปิดอยู่ เช่น Pause, Settings, Game Over")]
    [SerializeField] private bool _hideWhenMenuOpen = true;

    [Tooltip("ซ่อนตอนเล่น cutscene")]
    [SerializeField] private bool _hideDuringCutscene = true;

    [Header("Fade")]
    [SerializeField] private float _fadeSpeed = 6f;

    private CanvasGroup _canvasGroup;
    private PlayerCameraController _camera;
    private bool _sceneAllowsHUD;
    private float _retryTimer;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // HUD ไม่รับคลิกอยู่แล้ว ปิดไว้กันบังปุ่มเมนู
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EvaluateScene(SceneManager.GetActiveScene().name);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _camera = null;   // scene ใหม่ต้องหาใหม่
        EvaluateScene(scene.name);
    }

    private void EvaluateScene(string sceneName)
    {
        _sceneAllowsHUD = true;

        foreach (var hidden in _hiddenInScenes)
        {
            if (string.Equals(sceneName, hidden, System.StringComparison.OrdinalIgnoreCase))
            {
                _sceneAllowsHUD = false;
                return;
            }
        }
    }

    private void Update()
    {
        float targetAlpha = ShouldShow() ? 1f : 0f;

        _canvasGroup.alpha = Mathf.MoveTowards(
            _canvasGroup.alpha, targetAlpha, _fadeSpeed * Time.unscaledDeltaTime);
    }

    private bool ShouldShow()
    {
        if (!_sceneAllowsHUD) return false;

        if (_hideWhenMenuOpen)
        {
            var ui = UIManager.Instance;
            if (ui != null && ui.HasAnyOpen) return false;
        }

        if (_hideDuringCutscene)
        {
            if (_camera == null)
            {
                // ลองหาใหม่เป็นระยะ เผื่อ player ถูก spawn ทีหลัง
                _retryTimer += Time.unscaledDeltaTime;
                if (_retryTimer >= 0.5f)
                {
                    _retryTimer = 0f;
                    _camera = FindFirstObjectByType<PlayerCameraController>();
                }
            }

            if (_camera != null && _camera.IsCutsceneMode) return false;
        }

        return true;
    }
}
