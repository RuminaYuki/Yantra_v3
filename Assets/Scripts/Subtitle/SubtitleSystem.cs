using System;
using System.Collections;
using UnityEngine;
using Yantra.UI;

public class SubtitleSystem : MonoBehaviour
{
    public static SubtitleSystem Instance { get; private set; }

    // หอกระจายข่าวให้ UI คอยฟัง (ไม่ต้องพึ่งพากันและกัน)
    public static event Action<bool> OnSubtitleToggle;    // สั่งเปิด/ปิด
    public static event Action<string> OnSubtitleUpdate;  // สั่งเปลี่ยนข้อความ

    [Header("⏱️ Timing")]
    [Tooltip("หน่วงเสียงพากย์ให้ตรงกับตอนที่ซับเฟดขึ้นมาพอดี — ใส่เท่ากับ Fade Duration ใน SubtitleUI")]
    [SerializeField] private float voiceDelayToMatchFade = 0.25f;

    [Tooltip("เวลาโชว์ขั้นต่ำต่อประโยค กันเคสลืมใส่ FallbackDuration แล้วซับกระพริบหาย")]
    [SerializeField] private float minLineDuration = 1.2f;

    [Tooltip("ถ้าไม่มีทั้งเสียงและ FallbackDuration จะคำนวณเวลาจากจำนวนตัวอักษร (ตัวอักษรต่อวินาที)")]
    [SerializeField] private float charactersPerSecond = 14f;

    [Tooltip("เว้นจังหวะหายใจระหว่างประโยค")]
    [SerializeField] private float gapBetweenLines = 0.15f;

    [Header("🎙️ Voice")]
    [Tooltip("ส่งเสียงพากย์ผ่าน SoundManager (ได้ mixer + ปรับ volume + หรี่เพลงอัตโนมัติ)")]
    [SerializeField] private bool routeVoiceThroughSoundManager = true;

    [Tooltip("AudioSource สำรอง ใช้เมื่อไม่มี SoundManager ในซีน (เช่นเทสซีนเดี่ยวๆ)")]
    [SerializeField] private AudioSource fallbackAudioSource;

    [Header("⛔ Stop When")]
    [Tooltip("PlayerOnDead_VoidEventChannel — ตัวเดียวกับช่อง On Dead ของ Health ผู้เล่น\nผู้เล่นตายแล้วบทพูดกับเสียงหยุดทันที")]
    [SerializeField] private VoidEventChannelSO onPlayerDead;

    private Coroutine _subtitleCoroutine;
    private bool _isHeld;

    /// <summary>ตอนนี้มีบทพูดเล่นอยู่มั้ย (เผื่อระบบอื่นอยากเช็คก่อนแทรก)</summary>
    public bool IsPlaying => _subtitleCoroutine != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (fallbackAudioSource == null)
            fallbackAudioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        if (onPlayerDead != null)
            onPlayerDead.Raised += StopSubtitle;
    }

    // ปิด Domain Reload ไว้ — ไม่ถอดออก Play รอบหน้า channel จะยังเรียกหาตัวเก่าอยู่
    private void OnDisable()
    {
        if (onPlayerDead != null)
            onPlayerDead.Raised -= StopSubtitle;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        OnSubtitleToggle?.Invoke(false);
    }

    // Update ยังวิ่งตอน timeScale = 0 — ใช้จับจังหวะ Pause / Resume แล้วหยุดเสียงให้ตรงกับเวลาของบท
    private void Update()
    {
        if (_subtitleCoroutine == null)
            return;

        bool hold = ShouldHold();
        if (hold == _isHeld)
            return;

        _isHeld = hold;

        if (hold) PauseVoice();
        else ResumeVoice();
    }

    public void PlaySubtitle(SubtitleDataSO subtitleData)
    {
        if (subtitleData == null || subtitleData.Lines == null || subtitleData.Lines.Length == 0)
            return;

        StopSubtitle(); // ตัดของเก่าให้เกลี้ยงก่อนเสมอ รวมถึงเสียงด้วย

        _subtitleCoroutine = StartCoroutine(PlaySubtitleRoutine(subtitleData));
    }

    /// <summary>หยุดบทพูดกลางคัน (ผู้เล่นกดข้าม, เข้าคัตซีน, ตาย ฯลฯ)</summary>
    public void StopSubtitle()
    {
        if (_subtitleCoroutine != null)
        {
            StopCoroutine(_subtitleCoroutine);
            _subtitleCoroutine = null;
        }

        _isHeld = false;
        StopVoice();

        OnSubtitleToggle?.Invoke(false);
    }

    private IEnumerator PlaySubtitleRoutine(SubtitleDataSO subtitleData)
    {
        OnSubtitleToggle?.Invoke(true);

        // ล็อกให้เพลงหรี่ค้างตลอดบทสนทนา ไม่งั้นเพลงจะเด้งขึ้นลงทุกช่องว่างระหว่างประโยค
        if (routeVoiceThroughSoundManager && SoundManager.Instance != null)
            SoundManager.Instance.SetVoiceDuckHold(true);

        foreach (var line in subtitleData.Lines)
        {
            OnSubtitleUpdate?.Invoke(BuildDisplayText(line));

            // รอให้ซับเฟดขึ้นมาก่อนค่อยปล่อยเสียง
            if (voiceDelayToMatchFade > 0f)
                yield return WaitWhileRunning(voiceDelayToMatchFade);

            float waitTime = PlayLineVoice(line);
            waitTime = Mathf.Max(waitTime, GetFallbackDuration(line));

            yield return WaitWhileRunning(waitTime);

            if (gapBetweenLines > 0f)
                yield return WaitWhileRunning(gapBetweenLines);
        }

        if (routeVoiceThroughSoundManager && SoundManager.Instance != null)
            SoundManager.Instance.SetVoiceDuckHold(false);

        OnSubtitleToggle?.Invoke(false);
        _subtitleCoroutine = null;
    }

    // นับเวลาจริงเฉพาะตอนเกมเดินอยู่
    // ไม่ใช้ scaled time เพราะ hitstop / slow-mo ตอน parry จะทำให้ซับช้าลงทั้งที่เสียงยังพูดต่อ
    private IEnumerator WaitWhileRunning(float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            if (!ShouldHold())
                elapsed += Time.unscaledDeltaTime;

            yield return null;
        }
    }

    // มีหน้าจอเปิดอยู่ (Pause, Settings, Loading) = ซับถูกซ่อน ต้องหยุดบทไว้ด้วย ไม่งั้นผู้เล่นพลาดประโยค
    private static bool ShouldHold()
    {
        PauseController pause = PauseController.Instance;
        if (pause != null && pause.IsPaused)
            return true;

        UIManager ui = UIManager.Instance;
        return ui != null && ui.HasAnyOpen;
    }

    private string BuildDisplayText(SubtitleLine line)
    {
        if (line.Speaker == null) return line.DefaultText;

        string hexColor = ColorUtility.ToHtmlStringRGB(line.Speaker.NameColor);
        return $"<color=#{hexColor}>[{line.Speaker.DefaultName}]</color> {line.DefaultText}";
    }

    private float PlayLineVoice(SubtitleLine line)
    {
        if (line.VoiceClip == null) return 0f;

        if (routeVoiceThroughSoundManager && SoundManager.Instance != null)
            return SoundManager.Instance.PlayVoice(line.VoiceClip);

        // โหมดสำรอง — ไม่ผ่าน mixer ผู้เล่นปรับ volume ไม่ได้ ใช้เฉพาะตอนเทส
        if (fallbackAudioSource != null)
        {
            fallbackAudioSource.Stop();
            fallbackAudioSource.clip = line.VoiceClip;
            fallbackAudioSource.Play();
        }

        return line.VoiceClip.length;
    }

    private void PauseVoice()
    {
        if (routeVoiceThroughSoundManager && SoundManager.Instance != null)
            SoundManager.Instance.PauseVoice();

        if (fallbackAudioSource != null)
            fallbackAudioSource.Pause();
    }

    private void ResumeVoice()
    {
        if (routeVoiceThroughSoundManager && SoundManager.Instance != null)
            SoundManager.Instance.ResumeVoice();

        if (fallbackAudioSource != null)
            fallbackAudioSource.UnPause();
    }

    private void StopVoice()
    {
        if (routeVoiceThroughSoundManager && SoundManager.Instance != null)
        {
            SoundManager.Instance.StopVoice();
            SoundManager.Instance.SetVoiceDuckHold(false);   // ปลดล็อก ไม่งั้นเพลงหรี่ค้างตลอดเกม
        }

        if (fallbackAudioSource != null)
            fallbackAudioSource.Stop();
    }

    // กันเคส FallbackDuration = 0 แล้วซับกระพริบหายในเฟรมเดียว
    private float GetFallbackDuration(SubtitleLine line)
    {
        if (line.FallbackDuration > 0.01f)
            return line.FallbackDuration;

        int length = string.IsNullOrEmpty(line.DefaultText) ? 0 : line.DefaultText.Length;
        float estimated = charactersPerSecond > 0f ? length / charactersPerSecond : 0f;

        return Mathf.Max(estimated, minLineDuration);
    }
}