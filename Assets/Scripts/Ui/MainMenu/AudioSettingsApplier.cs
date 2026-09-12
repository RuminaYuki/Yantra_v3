using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class AudioSettingsApplier : MonoBehaviour
{
    [Header("Fallback (ใช้เมื่อไม่มี SoundManager ในฉาก)")]
    [SerializeField] private AudioMixer _mixer;

    [SerializeField] private string _masterParam = "MasterVolume";
    [SerializeField] private string _bgmParam = "BGMVolume";
    [SerializeField] private string _sfxParam = "SFXVolume";
    [SerializeField] private string _voiceParam = "VoiceVolume";
    [SerializeField] private string _uiParam = "UIVolume";

    [Header("Debug")]
    [SerializeField] private bool _logRoute = false;

    private SoundManager _soundManager;

    private void OnEnable()
    {
        GameSettings.AudioChanged += Apply;
        SceneManager.sceneLoaded += OnSceneLoaded;

        RefreshSoundManager();
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.AudioChanged -= Apply;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // SoundManager ไม่ข้าม scene (UseDontDestroyOnLoad = false)
        // ทุกครั้งที่เปลี่ยนฉากต้องหาตัวใหม่ แล้วยิงค่าให้มันอีกรอบ
        RefreshSoundManager();
        Apply();
    }

    private void RefreshSoundManager()
    {
        _soundManager = Object.FindFirstObjectByType<SoundManager>(FindObjectsInactive.Exclude);

        if (_logRoute)
            Debug.Log(_soundManager != null
                ? "[AudioSettings] ส่งผ่าน SoundManager"
                : "[AudioSettings] ไม่เจอ SoundManager — เขียน mixer ตรง", this);
    }

    private void Apply()
    {
        if (_soundManager != null)
        {
            _soundManager.SetMasterVolume(GameSettings.MasterVolume);
            _soundManager.SetMusicVolume(GameSettings.BGMVolume);
            _soundManager.SetSoundFXVolume(GameSettings.SFXVolume);
            _soundManager.SetVoiceVolume(GameSettings.VoiceVolume);
            _soundManager.SetUIVolume(GameSettings.UIVolume);
            return;
        }

        SetDirect(_masterParam, GameSettings.MasterVolume);
        SetDirect(_bgmParam, GameSettings.BGMVolume);
        SetDirect(_sfxParam, GameSettings.SFXVolume);
        SetDirect(_voiceParam, GameSettings.VoiceVolume);
        SetDirect(_uiParam, GameSettings.UIVolume);
    }

    private void SetDirect(string param, float linear)
    {
        if (_mixer == null || string.IsNullOrEmpty(param)) return;

        float db = linear <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp01(linear)) * 20f;
        _mixer.SetFloat(param, Mathf.Max(-80f, db));
    }
}