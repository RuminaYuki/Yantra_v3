using UnityEngine;
using UnityEngine.UI;

public class AudioDebugMenu : MonoBehaviour
{
    [Header("UI Sliders (ปรับค่าใน Inspector ให้ Min=0, Max=1)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider voiceSlider;

    private const string KeyMaster = "vol_master";
    private const string KeyBgm = "vol_bgm";
    private const string KeySfx = "vol_sfx";
    private const string KeyVoice = "vol_voice";

    private void Start()
    {
        // [FIX] โหลดค่าที่ผู้เล่นเคยตั้งไว้ ถ้าไม่เคยตั้งก็ใช้ 1 (ดังสุด)
        if (masterSlider) masterSlider.value = PlayerPrefs.GetFloat(KeyMaster, 1f);
        if (bgmSlider) bgmSlider.value = PlayerPrefs.GetFloat(KeyBgm, 1f);
        if (sfxSlider) sfxSlider.value = PlayerPrefs.GetFloat(KeySfx, 1f);
        if (voiceSlider) voiceSlider.value = PlayerPrefs.GetFloat(KeyVoice, 1f);

        if (SoundManager.Instance != null)
        {
            if (masterSlider) SoundManager.Instance.SetMasterVolume(masterSlider.value);
            if (bgmSlider) SoundManager.Instance.SetMusicVolume(bgmSlider.value);
            if (sfxSlider) SoundManager.Instance.SetSoundFXVolume(sfxSlider.value);
            if (voiceSlider) SoundManager.Instance.SetVoiceVolume(voiceSlider.value);
        }

        // 2. ผูก Event ว่าถ้าเลื่อน Slider ให้ส่งค่าไปหา SoundManager
        if (masterSlider)
            masterSlider.onValueChanged.AddListener(val => Apply(KeyMaster, val));

        if (bgmSlider)
            bgmSlider.onValueChanged.AddListener(val => Apply(KeyBgm, val));

        if (sfxSlider)
            sfxSlider.onValueChanged.AddListener(val => Apply(KeySfx, val));

        if (voiceSlider)
            voiceSlider.onValueChanged.AddListener(val => Apply(KeyVoice, val));
    }

    private void Apply(string key, float value)
    {
        if (SoundManager.Instance == null) return;

        switch (key)
        {
            case KeyMaster: SoundManager.Instance.SetMasterVolume(value); break;
            case KeyBgm: SoundManager.Instance.SetMusicVolume(value); break;
            case KeySfx: SoundManager.Instance.SetSoundFXVolume(value); break;
            case KeyVoice: SoundManager.Instance.SetVoiceVolume(value); break;
        }

        PlayerPrefs.SetFloat(key, value);
    }

    private void OnDestroy()
    {
        // ล้าง Event ทิ้งตอนปิดหน้า UI เพื่อป้องกันบั๊ก
        if (masterSlider) masterSlider.onValueChanged.RemoveAllListeners();
        if (bgmSlider) bgmSlider.onValueChanged.RemoveAllListeners();
        if (sfxSlider) sfxSlider.onValueChanged.RemoveAllListeners();
        if (voiceSlider) voiceSlider.onValueChanged.RemoveAllListeners();

        PlayerPrefs.Save();   // เขียนลงดิสก์จริง ไม่งั้นค่าหายตอนปิดเกมแรง
    }
}