using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yantra.UI;

public class SettingsScreen : UIScreen
{
    [Header("── Audio ──")]
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Slider _voiceSlider;
    [SerializeField] private Slider _uiSlider;

    [SerializeField] private TMP_Text _masterValue;
    [SerializeField] private TMP_Text _bgmValue;
    [SerializeField] private TMP_Text _sfxValue;
    [SerializeField] private TMP_Text _voiceValue;
    [SerializeField] private TMP_Text _uiValue;

    [Header("── Gameplay ──")]
    [SerializeField] private Slider _sensitivitySlider;
    [SerializeField] private TMP_Text _sensitivityValue;
    [SerializeField] private Toggle _invertYToggle;
    [SerializeField] private Toggle _subtitlesToggle;
    [SerializeField] private Toggle _headBobToggle;

    [Header("── Display ──")]
    [SerializeField] private Toggle _fullscreenToggle;
    [SerializeField] private Toggle _vsyncToggle;
    [SerializeField] private Slider _brightnessSlider;
    [SerializeField] private TMP_Text _brightnessValue;

    // กันไม่ให้ตอนเราตั้งค่า UI เอง แล้วมันยิง event ย้อนกลับมาทับค่า
    private bool _isRefreshing;

    protected override void Awake()
    {
        base.Awake();

        // Audio
        HookSlider(_masterSlider, v => GameSettings.MasterVolume = v);
        HookSlider(_bgmSlider, v => GameSettings.BGMVolume = v);
        HookSlider(_sfxSlider, v => GameSettings.SFXVolume = v);
        HookSlider(_voiceSlider, v => GameSettings.VoiceVolume = v);
        HookSlider(_uiSlider, v => GameSettings.UIVolume = v);

        // Gameplay
        HookSlider(_sensitivitySlider, v => GameSettings.MouseSensitivity = v, 0.1f, 3f);
        HookToggle(_invertYToggle, v => GameSettings.InvertY = v);
        HookToggle(_subtitlesToggle, v => GameSettings.Subtitles = v);
        HookToggle(_headBobToggle, v => GameSettings.HeadBob = v);

        // Display
        HookToggle(_fullscreenToggle, v => GameSettings.Fullscreen = v);
        HookToggle(_vsyncToggle, v => GameSettings.VSync = v);
        HookSlider(_brightnessSlider, v => GameSettings.Brightness = v, 0.5f, 2f);
    }

    protected override void OnOpening()
    {
        RefreshFromSettings();
    }

    protected override void OnClosing()
    {
        GameSettings.Save();
    }

    public void OnBackClicked()
    {
        UIManager.Instance.Back();
    }

    public void OnResetClicked()
    {
        GameSettings.ResetToDefaults();
        RefreshFromSettings();
    }

    // ---------- ภายใน ----------

    private void HookSlider(Slider slider, System.Action<float> setter, float min = 0f, float max = 1f)
    {
        if (slider == null) return;

        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;

        slider.onValueChanged.AddListener(value =>
        {
            if (_isRefreshing) return;
            setter(value);
            UpdateValueTexts();
        });
    }

    private void HookToggle(Toggle toggle, System.Action<bool> setter)
    {
        if (toggle == null) return;

        toggle.onValueChanged.AddListener(value =>
        {
            if (_isRefreshing) return;
            setter(value);
        });
    }

    private void RefreshFromSettings()
    {
        _isRefreshing = true;

        SetSlider(_masterSlider, GameSettings.MasterVolume);
        SetSlider(_bgmSlider, GameSettings.BGMVolume);
        SetSlider(_sfxSlider, GameSettings.SFXVolume);
        SetSlider(_voiceSlider, GameSettings.VoiceVolume);
        SetSlider(_uiSlider, GameSettings.UIVolume);

        SetSlider(_sensitivitySlider, GameSettings.MouseSensitivity);
        SetToggle(_invertYToggle, GameSettings.InvertY);
        SetToggle(_subtitlesToggle, GameSettings.Subtitles);
        SetToggle(_headBobToggle, GameSettings.HeadBob);

        SetToggle(_fullscreenToggle, GameSettings.Fullscreen);
        SetToggle(_vsyncToggle, GameSettings.VSync);
        SetSlider(_brightnessSlider, GameSettings.Brightness);

        _isRefreshing = false;
        UpdateValueTexts();
    }

    private void SetSlider(Slider s, float v) { if (s != null) s.value = v; }
    private void SetToggle(Toggle t, bool v) { if (t != null) t.isOn = v; }

    private void UpdateValueTexts()
    {
        SetPercent(_masterValue, GameSettings.MasterVolume);
        SetPercent(_bgmValue, GameSettings.BGMVolume);
        SetPercent(_sfxValue, GameSettings.SFXVolume);
        SetPercent(_voiceValue, GameSettings.VoiceVolume);
        SetPercent(_uiValue, GameSettings.UIVolume);

        if (_sensitivityValue != null)
            _sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.0");

        if (_brightnessValue != null)
            _brightnessValue.text = Mathf.RoundToInt(GameSettings.Brightness * 100f) + "%";
    }

    private void SetPercent(TMP_Text label, float value01)
    {
        if (label != null) label.text = Mathf.RoundToInt(value01 * 100f).ToString();
    }
}