using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yantra.UI;

// วางใต้ UIRoot แยกจาก HUDScreen (HUD ซ่อนตอน cutscene แต่ซับต้องโชว์)
// ไม่ใช่ UIScreen — ถ้าเข้า stack ของ UIManager จะกด Pause ไม่ได้และ HUD จะซ่อนทุกครั้งที่ซับขึ้น
[RequireComponent(typeof(CanvasGroup))]
public class SubtitleUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI _subtitleText;

    [Header("Fade")]
    [Tooltip("เวลาเฟดต่อประโยค — ให้เท่ากับ Voice Delay To Match Fade ใน SubtitleSystem")]
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.25f;

    [Tooltip("เวลาซ่อน/โชว์ตอนเปิดปิดเมนู หรือเปิดปิดซับใน Settings")]
    [SerializeField, Min(0.01f)] private float _visibilityFadeDuration = 0.15f;

    [Header("Hide When")]
    [Tooltip("ซ่อนตอนมีเมนูเปิดอยู่ เช่น Pause, Settings, Game Over, Loading")]
    [SerializeField] private bool _hideWhenMenuOpen = true;

    private CanvasGroup _canvasGroup;

    // ความทึบของประโยค (เฟดตอนเปลี่ยนประโยค) คูณกับความทึบของชั้น (เมนู / Settings)
    private float _lineAlpha;
    private float _visibility;

    private bool _hasLine;
    private bool _swapPending;
    private string _pendingText;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        ClearImmediate();
    }

    private void OnEnable()
    {
        SubtitleSystem.OnSubtitleToggle += HandleToggle;
        SubtitleSystem.OnSubtitleUpdate += HandleUpdate;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SubtitleSystem.OnSubtitleToggle -= HandleToggle;
        SubtitleSystem.OnSubtitleUpdate -= HandleUpdate;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ClearImmediate();
    }

    private void HandleToggle(bool visible)
    {
        // ตอนเปิด ไม่ต้องทำอะไร รอข้อความแรกมาค่อยเฟดขึ้น
        if (visible) return;

        _hasLine = false;
        _swapPending = false;
        _pendingText = null;
    }

    private void HandleUpdate(string text)
    {
        _pendingText = text;
        _swapPending = true;
        _hasLine = true;
    }

    // ชั้นนี้อยู่ข้าม scene — เปลี่ยน scene กลางบทพูดต้องล้าง ไม่งั้นประโยคเก่าค้างไปโผล่ด่านใหม่
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            ClearImmediate();
    }

    // unscaled ทั้งหมด เพราะตอน Pause / Game Over timeScale = 0
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (_swapPending)
        {
            _lineAlpha = Mathf.MoveTowards(_lineAlpha, 0f, dt / _fadeDuration);

            // มืดสนิทก่อนค่อยเปลี่ยนข้อความ กันข้อความใหม่แวบขึ้นมาตอนยังเฟดออกไม่หมด
            if (_lineAlpha <= 0f)
            {
                SetText(_pendingText);
                _pendingText = null;
                _swapPending = false;
            }
        }
        else
        {
            _lineAlpha = Mathf.MoveTowards(_lineAlpha, _hasLine ? 1f : 0f, dt / _fadeDuration);

            if (!_hasLine && _lineAlpha <= 0f)
                SetText(string.Empty);
        }

        _visibility = Mathf.MoveTowards(_visibility, ShouldShow() ? 1f : 0f, dt / _visibilityFadeDuration);
        _canvasGroup.alpha = _lineAlpha * _visibility;
    }

    private bool ShouldShow()
    {
        // ปิดซับใน Settings แล้วซ่อนแค่ตัวหนังสือ เสียงพากย์ยังเล่นตามปกติ
        if (!GameSettings.Subtitles)
            return false;

        if (_hideWhenMenuOpen)
        {
            UIManager ui = UIManager.Instance;
            if (ui != null && ui.HasAnyOpen)
                return false;
        }

        return true;
    }

    private void SetText(string text)
    {
        if (_subtitleText != null && _subtitleText.text != text)
            _subtitleText.text = text;
    }

    private void ClearImmediate()
    {
        _hasLine = false;
        _swapPending = false;
        _pendingText = null;
        _lineAlpha = 0f;

        SetText(string.Empty);

        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
    }
}