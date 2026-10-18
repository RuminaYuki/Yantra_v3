using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yantra.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// หน้าจอโหลด — ไม่รู้จักการโหลด scene เลย
/// มันแค่รับตัวเลข 0..1 มาแสดง ส่วน SceneLoader เป็นคนป้อนให้
///
/// ช่องไหนไม่ได้ใช้ก็เว้นว่างไว้ ไม่พัง
/// ใช้ spinner อย่างเดียวก็ได้ ไม่ต้องมี progress bar
/// </summary>
public class LoadingScreen : UIScreen
{
    [Header("Progress (ไม่ใส่ก็ได้)")]
    [SerializeField] private Slider _progressBar;
    [SerializeField] private TMP_Text _percentText;

    [Tooltip("ความเร็วที่แถบวิ่งตามค่าจริง — ยิ่งต่ำยิ่งลื่นแต่หน่วง")]
    [SerializeField] private float _smoothSpeed = 4f;

    [Header("Press Any Key")]
    [SerializeField] private GameObject _pressAnyKeyPrompt;

    [Tooltip("ซ่อน spinner ตอนขึ้นข้อความให้กดปุ่ม")]
    [SerializeField] private GameObject _spinner;

    [Header("Tips")]
    [SerializeField] private TMP_Text _tipText;

    [TextArea(2, 4)]
    [SerializeField] private string[] _tips;

    [Tooltip("เปลี่ยน tip เองทุกกี่วินาที — 0 = ไม่เปลี่ยนเอง")]
    [SerializeField, Min(0f)] private float _tipInterval = 5f;

    [Tooltip("เวลาจางออก และเวลาจางเข้า ตอนเปลี่ยน tip เอง (อย่างละเท่านี้)")]
    [SerializeField, Min(0f)] private float _tipFadeDuration = 0.4f;

    [Tooltip("คลิกซ้ายเพื่อข้ามไป tip ถัดไป — กดรัว ๆ ได้")]
    [SerializeField] private bool _clickToSkipTip = true;

    [Tooltip("เวลา fade ตอนคลิกข้าม — สั้นกว่าปกติให้รู้สึกว่ากดแล้วตอบสนองทันที")]
    [SerializeField, Min(0f)] private float _skipFadeDuration = 0.15f;

    private enum TipPhase { Showing, FadingOut, FadingIn }

    private float _target;
    private float _displayed;

    private TipPhase _tipPhase;
    private float _tipTimer;
    private float _tipFade;
    private int _lastTip = -1;

    protected override void OnOpening()
    {
        _target = 0f;
        _displayed = 0f;
        ApplyToUI(0f);

        if (_pressAnyKeyPrompt != null) _pressAnyKeyPrompt.SetActive(false);
        if (_spinner != null) _spinner.SetActive(true);

        StartTips();
    }

    /// <summary>เรียกโดย SceneLoader — ค่า 0..1</summary>
    public void SetProgress(float value)
    {
        _target = Mathf.Clamp01(value);
    }

    public void ShowPressAnyKey(bool show)
    {
        if (_pressAnyKeyPrompt != null) _pressAnyKeyPrompt.SetActive(show);

        // โหลดเสร็จแล้ว ไม่ต้องหมุนต่อ ไม่งั้นดูเหมือนยังทำงานอยู่
        if (_spinner != null) _spinner.SetActive(!show);
    }

    private void Update()
    {
        if (!IsOpen) return;

        UpdateTips();
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (_progressBar == null && _percentText == null) return;

        // ไล่ตามค่าจริงแบบนุ่ม ๆ ไม่ให้แถบกระโดดจาก 0 ไป 100 ทีเดียว
        _displayed = Mathf.MoveTowards(_displayed, _target, _smoothSpeed * Time.unscaledDeltaTime);
        ApplyToUI(_displayed);
    }

    private void ApplyToUI(float value)
    {
        if (_progressBar != null) _progressBar.value = value;
        if (_percentText != null) _percentText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    // ---------- Tips ----------

    private bool HasTips => _tipText != null && _tips != null && _tips.Length > 0;

    private void StartTips()
    {
        if (!HasTips) return;

        _tipText.alpha = 1f;
        _tipText.text = NextTip();
        _tipPhase = TipPhase.Showing;
        _tipTimer = 0f;
    }

    // ใช้ unscaled time — ตอนโหลดจาก Pause ค่า timeScale อาจยังเป็น 0
    private void UpdateTips()
    {
        if (!HasTips) return;

        float dt = Time.unscaledDeltaTime;

        // คลิกตอนไหนก็ได้ แม้กำลัง fade อยู่ — จางออกจากความทึบตอนนั้นเลย กดรัวแล้วไม่ค้าง
        if (CanSkip() && SkipPressed())
        {
            _tipFade = _skipFadeDuration;
            _tipPhase = TipPhase.FadingOut;
        }

        switch (_tipPhase)
        {
            case TipPhase.Showing:
                if (_tips.Length < 2 || _tipInterval <= 0f) return;

                _tipTimer += dt;
                if (_tipTimer >= _tipInterval)
                {
                    _tipFade = _tipFadeDuration;
                    _tipPhase = TipPhase.FadingOut;
                }
                break;

            case TipPhase.FadingOut:
                _tipText.alpha = StepAlpha(_tipText.alpha, 0f, dt);
                if (_tipText.alpha <= 0f)
                {
                    _tipText.text = NextTip();
                    _tipPhase = TipPhase.FadingIn;
                }
                break;

            case TipPhase.FadingIn:
                _tipText.alpha = StepAlpha(_tipText.alpha, 1f, dt);
                if (_tipText.alpha >= 1f)
                {
                    _tipPhase = TipPhase.Showing;
                    _tipTimer = 0f;
                }
                break;
        }
    }

    private float StepAlpha(float from, float to, float dt)
    {
        if (_tipFade <= 0f) return to;
        return Mathf.MoveTowards(from, to, dt / _tipFade);
    }

    // ตอนขึ้น "กดปุ่มเพื่อไปต่อ" คลิกต้องเป็นการไปต่อ ไม่ใช่ข้าม tip
    private bool CanSkip()
    {
        if (!_clickToSkipTip || _tips.Length < 2) return false;
        return _pressAnyKeyPrompt == null || !_pressAnyKeyPrompt.activeSelf;
    }

    private static bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    // สุ่มแต่ไม่ให้ซ้ำกับอันที่เพิ่งโชว์
    private string NextTip()
    {
        int count = _tips.Length;
        int index = Random.Range(0, count);

        if (count > 1 && index == _lastTip)
            index = (_lastTip + 1 + Random.Range(0, count - 1)) % count;

        _lastTip = index;
        return _tips[index];
    }
}