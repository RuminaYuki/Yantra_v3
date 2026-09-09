using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yantra.UI;

/// <summary>
/// ชั้นที่ 3 — แค่ต่อสาย UI เข้ากับ GameSettings
/// ไม่มี logic ว่าค่าถูกเอาไปทำอะไรต่อ นั่นเป็นหน้าที่ของ Applier
/// </summary>
public class SettingsScreen : UIScreen
{
    [Header("Audio Sliders")]
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Slider _voiceSlider;

    [Header("Audio Labels (ไม่ใส่ก็ได้)")]
    [SerializeField] private TMP_Text _masterLabel;
    [SerializeField] private TMP_Text _bgmLabel;
    [SerializeField] private TMP_Text _sfxLabel;
    [SerializeField] private TMP_Text _voiceLabel;

    // กันไม่ให้ตอนเราตั้งค่า slider เอง แล้วมันยิง event ย้อนกลับมาทับค่า
    private bool _isRefreshing;

    protected override void Awake()
    {
        base.Awake();

        HookSlider(_masterSlider, v => GameSettings.MasterVolume = v);
        HookSlider(_bgmSlider, v => GameSettings.BGMVolume = v);
        HookSlider(_sfxSlider, v => GameSettings.SFXVolume = v);
        HookSlider(_voiceSlider, v => GameSettings.VoiceVolume = v);
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

    private void HookSlider(Slider slider, System.Action<float> setter)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        slider.onValueChanged.AddListener(value =>
        {
            if (_isRefreshing) return;
            setter(value);
            UpdateLabels();
        });
    }

    private void RefreshFromSettings()
    {
        _isRefreshing = true;

        if (_masterSlider != null) _masterSlider.value = GameSettings.MasterVolume;
        if (_bgmSlider != null) _bgmSlider.value = GameSettings.BGMVolume;
        if (_sfxSlider != null) _sfxSlider.value = GameSettings.SFXVolume;
        if (_voiceSlider != null) _voiceSlider.value = GameSettings.VoiceVolume;

        _isRefreshing = false;
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        SetLabel(_masterLabel, GameSettings.MasterVolume);
        SetLabel(_bgmLabel, GameSettings.BGMVolume);
        SetLabel(_sfxLabel, GameSettings.SFXVolume);
        SetLabel(_voiceLabel, GameSettings.VoiceVolume);
    }

    private void SetLabel(TMP_Text label, float value)
    {
        if (label != null) label.text = Mathf.RoundToInt(value * 100f).ToString();
    }
}