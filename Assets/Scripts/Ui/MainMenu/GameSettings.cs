using System;
using UnityEngine;

public static class GameSettings
{
    // ---------- Event ----------
    public static event Action AudioChanged;
    public static event Action GraphicsChanged;
    public static event Action GameplayChanged;

    // ---------- Key สำหรับ PlayerPrefs ----------
    private const string K_Master = "set_vol_master";
    private const string K_BGM = "set_vol_bgm";
    private const string K_SFX = "set_vol_sfx";
    private const string K_Voice = "set_vol_voice";
    private const string K_UI = "set_vol_ui";

    private const string K_Quality = "set_gfx_quality";
    private const string K_Fullscreen = "set_gfx_fullscreen";
    private const string K_VSync = "set_gfx_vsync";
    private const string K_Brightness = "set_gfx_brightness";
    private const string K_MotionBlur = "set_gfx_motionblur";
    private const string K_ResW = "set_gfx_res_w";
    private const string K_ResH = "set_gfx_res_h";
    private const string K_ResHz = "set_gfx_res_hz";

    private const string K_Sens = "set_play_sensitivity";
    private const string K_InvertY = "set_play_inverty";
    private const string K_Subtitles = "set_play_subtitles";
    private const string K_HeadBob = "set_play_headbob";

    // ---------- ค่าเริ่มต้น ----------
    public const float DefaultVolume = 0.8f;
    public const float DefaultBrightness = 1.0f;
    public const float DefaultSensitivity = 1.0f;

    // ---------- Audio (0..1) ----------
    private static float _master, _bgm, _sfx, _voice, _ui;

    public static float MasterVolume
    {
        get => _master;
        set { _master = Mathf.Clamp01(value); AudioChanged?.Invoke(); }
    }
    public static float BGMVolume
    {
        get => _bgm;
        set { _bgm = Mathf.Clamp01(value); AudioChanged?.Invoke(); }
    }
    public static float SFXVolume
    {
        get => _sfx;
        set { _sfx = Mathf.Clamp01(value); AudioChanged?.Invoke(); }
    }
    public static float VoiceVolume
    {
        get => _voice;
        set { _voice = Mathf.Clamp01(value); AudioChanged?.Invoke(); }
    }
    /// <summary>เสียง hover/click ในเมนู — แยกออกมาเพราะดังบ่อยที่สุด</summary>
    public static float UIVolume
    {
        get => _ui;
        set { _ui = Mathf.Clamp01(value); AudioChanged?.Invoke(); }
    }

    // ---------- Graphics / Display ----------
    private static int _quality;
    private static bool _fullscreen, _vsync, _motionBlur;
    private static float _brightness;
    private static int _resW, _resH, _resHz;

    public static int QualityLevel
    {
        get => _quality;
        set { _quality = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1); GraphicsChanged?.Invoke(); }
    }
    public static bool Fullscreen
    {
        get => _fullscreen;
        set { _fullscreen = value; GraphicsChanged?.Invoke(); }
    }
    public static bool VSync
    {
        get => _vsync;
        set { _vsync = value; GraphicsChanged?.Invoke(); }
    }
    /// <summary>0.5 = มืด, 1 = ปกติ, 2 = สว่าง</summary>
    public static float Brightness
    {
        get => _brightness;
        set { _brightness = Mathf.Clamp(value, 0.5f, 2f); GraphicsChanged?.Invoke(); }
    }
    public static bool MotionBlur
    {
        get => _motionBlur;
        set { _motionBlur = value; GraphicsChanged?.Invoke(); }
    }

    public static int ResolutionWidth => _resW;
    public static int ResolutionHeight => _resH;
    public static int ResolutionHz => _resHz;

    public static void SetResolution(int width, int height, int refreshHz)
    {
        _resW = width; _resH = height; _resHz = refreshHz;
        GraphicsChanged?.Invoke();
    }

    // ---------- Gameplay ----------
    private static float _sensitivity;
    private static bool _invertY, _subtitles, _headBob;

    public static float MouseSensitivity
    {
        get => _sensitivity;
        set { _sensitivity = Mathf.Clamp(value, 0.1f, 3f); GameplayChanged?.Invoke(); }
    }
    public static bool InvertY
    {
        get => _invertY;
        set { _invertY = value; GameplayChanged?.Invoke(); }
    }
    public static bool Subtitles
    {
        get => _subtitles;
        set { _subtitles = value; GameplayChanged?.Invoke(); }
    }
    public static bool HeadBob
    {
        get => _headBob;
        set { _headBob = value; GameplayChanged?.Invoke(); }
    }

    // ---------- Load / Save ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load()
    {
        _master = PlayerPrefs.GetFloat(K_Master, DefaultVolume);
        _bgm = PlayerPrefs.GetFloat(K_BGM, DefaultVolume);
        _sfx = PlayerPrefs.GetFloat(K_SFX, DefaultVolume);
        _voice = PlayerPrefs.GetFloat(K_Voice, DefaultVolume);
        _ui = PlayerPrefs.GetFloat(K_UI, DefaultVolume);

        _quality = PlayerPrefs.GetInt(K_Quality, QualitySettings.GetQualityLevel());
        _fullscreen = PlayerPrefs.GetInt(K_Fullscreen, 1) == 1;
        _vsync = PlayerPrefs.GetInt(K_VSync, 1) == 1;
        _brightness = PlayerPrefs.GetFloat(K_Brightness, DefaultBrightness);
        _motionBlur = PlayerPrefs.GetInt(K_MotionBlur, 1) == 1;

        _resW = PlayerPrefs.GetInt(K_ResW, Screen.currentResolution.width);
        _resH = PlayerPrefs.GetInt(K_ResH, Screen.currentResolution.height);
        _resHz = PlayerPrefs.GetInt(K_ResHz, 60);

        _sensitivity = PlayerPrefs.GetFloat(K_Sens, DefaultSensitivity);
        _invertY = PlayerPrefs.GetInt(K_InvertY, 0) == 1;
        _subtitles = PlayerPrefs.GetInt(K_Subtitles, 1) == 1;
        _headBob = PlayerPrefs.GetInt(K_HeadBob, 1) == 1;

        // ยิงทุก event เพื่อให้ applier เอาค่าไปใช้ทันทีตั้งแต่เปิดเกม
        AudioChanged?.Invoke();
        GraphicsChanged?.Invoke();
        GameplayChanged?.Invoke();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(K_Master, _master);
        PlayerPrefs.SetFloat(K_BGM, _bgm);
        PlayerPrefs.SetFloat(K_SFX, _sfx);
        PlayerPrefs.SetFloat(K_Voice, _voice);
        PlayerPrefs.SetFloat(K_UI, _ui);

        PlayerPrefs.SetInt(K_Quality, _quality);
        PlayerPrefs.SetInt(K_Fullscreen, _fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(K_VSync, _vsync ? 1 : 0);
        PlayerPrefs.SetFloat(K_Brightness, _brightness);
        PlayerPrefs.SetInt(K_MotionBlur, _motionBlur ? 1 : 0);

        PlayerPrefs.SetInt(K_ResW, _resW);
        PlayerPrefs.SetInt(K_ResH, _resH);
        PlayerPrefs.SetInt(K_ResHz, _resHz);

        PlayerPrefs.SetFloat(K_Sens, _sensitivity);
        PlayerPrefs.SetInt(K_InvertY, _invertY ? 1 : 0);
        PlayerPrefs.SetInt(K_Subtitles, _subtitles ? 1 : 0);
        PlayerPrefs.SetInt(K_HeadBob, _headBob ? 1 : 0);

        PlayerPrefs.Save();
    }

    public static void ResetToDefaults()
    {
        MasterVolume = DefaultVolume;
        BGMVolume = DefaultVolume;
        SFXVolume = DefaultVolume;
        VoiceVolume = DefaultVolume;
        UIVolume = DefaultVolume;

        QualityLevel = QualitySettings.names.Length - 1;
        Fullscreen = true;
        VSync = true;
        Brightness = DefaultBrightness;
        MotionBlur = true;

        MouseSensitivity = DefaultSensitivity;
        InvertY = false;
        Subtitles = true;
        HeadBob = true;

        Save();
    }
}