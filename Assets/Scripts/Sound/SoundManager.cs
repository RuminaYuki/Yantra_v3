using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : Singleton<SoundManager>
{
    [SerializeField] private SoundTable soundTable;

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("ลำโพงเพลงตัวที่ 2 ใช้สลับตอน crossfade\n" +
        "เว้นว่างได้ จะสร้างให้เองตอนรัน แล้วตั้งค่าตามตัวแรกอัตโนมัติ")]
    [SerializeField] private AudioSource bgmSourceB;

    [Tooltip("ใช้เวลากี่วินาทีไล่เพลงเก่าออกพร้อมดันเพลงใหม่เข้า\n" +
        "สั้นไปจะรู้สึกเหมือนตัดดิบ ยาวไปจะรู้สึกว่าเพลงรบมาช้า — 1.5 ถึง 3 กำลังดี")]
    [Range(0.05f, 8f)][SerializeField] private float bgmCrossfadeTime = 2f;

    [Header("Voice System")]
    private AudioSource _voiceSource;

    [Header("Voice / Dialogue")]
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioMixerGroup voiceMixerGroup;

    [Header("Ducking")]
    [SerializeField] private bool duckBgmDuringVoice = true;
    [Range(0f, 1f)][SerializeField] private float duckedBgmMultiplier = 0.35f;
    [SerializeField] private float duckFadeTime = 0.25f;

    [Tooltip("หลังเสียงพูดจบ รอกี่วินาทีก่อนดันเพลงกลับขึ้น" +
        "\nกันเพลงกระเพื่อมขึ้นลงระหว่างประโยคในบทสนทนายาวๆ" +
        "\nควรตั้งให้มากกว่าช่องว่างระหว่างประโยคของระบบ Subtitle")]
    [SerializeField] private float duckReleaseDelay = 0.8f;

    [Header("Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Mixer Parameter Names")]
    [SerializeField] private string masterParam = "MasterVolume";
    [SerializeField] private string sfxParam = "SFXVolume";
    [SerializeField] private string bgmParam = "BGMVolume";
    [SerializeField] private string voiceParam = "VoiceVolume";
    [SerializeField] private string uiParam = "UIVolume";

    [Header("Mixer Base Levels (dB)")]
    [Tooltip("ใส่ค่า dB ที่คุณลากไว้ในหน้าต่าง Audio Mixer ของแต่ละกลุ่ม\n" +
        "Slider ของผู้เล่นจะ 'หรี่ลงจากค่านี้' แทนที่จะเขียนทับมัน")]
    [Range(-80f, 20f)][SerializeField] private float masterBaseDb = 0f;
    [Range(-80f, 20f)][SerializeField] private float sfxBaseDb = 0f;
    [Range(-80f, 20f)][SerializeField] private float bgmBaseDb = 0f;
    [Range(-80f, 20f)][SerializeField] private float voiceBaseDb = 0f;
    [Range(-80f, 20f)][SerializeField] private float uiBaseDb = 0f;

    [Header("Debug")]
    [Tooltip("เตือนใน Console เมื่อฉากนี้ไม่มี SoundPooler แล้วต้องสร้างให้เอง")]
    [SerializeField] private bool warnOnAutoCreatePooler = true;

    private SoundID currentBgmId;
    private Dictionary<SoundID, SoundData> soundsById;

    // ---------- สถานะ BGM ----------
    private AudioSource activeBgm;      // ตัวที่ถือเพลงปัจจุบัน
    private AudioSource standbyBgm;     // ตัวที่ว่าง รอรับเพลงถัดไป
    private float bgmBaseVolume = 1f;   // ความดังของเพลง ตามที่ตั้งไว้ใน SoundData
    private float duckMultiplier = 1f;  // 1 = ปกติ / 0.35 = หรี่อยู่ตอนมีบทพูด
    private Coroutine bgmFadeRoutine;

    private Coroutine duckRoutine;
    private bool duckHold;

    private SoundPooler cachedPooler;

    protected override bool UseDontDestroyOnLoad => false;

    public bool IsVoicePlaying => voiceSource != null && voiceSource.isPlaying;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        if (bgmSource == null)
            Debug.LogWarning("[SoundManager] BGM source is missing!");

        if (voiceSource == null)
            Debug.LogWarning("[SoundManager] Voice source is missing!");
        else
            voiceSource.spatialBlend = 0f;

        BuildSoundLookup();
    }

    private void BuildSoundLookup()
    {
        soundsById = new Dictionary<SoundID, SoundData>();

        if (soundTable == null || soundTable.soundLibraries == null) return;

        foreach (SoundLibrary library in soundTable.soundLibraries)
        {
            if (library == null || library.sounds == null) continue;

            foreach (SoundData sound in library.sounds)
            {
                if (sound == null || sound.id == null) continue;
                soundsById[sound.id] = sound;

#if UNITY_EDITOR
                // เตือนตั้งแต่ตอนเริ่มเกม ดีกว่าไปงงตอนกดแล้วไม่มีเสียง
                if (sound.GetClip() == null)
                    Debug.LogWarning($"[SoundManager] '{sound.id.name}' ไม่มีไฟล์เสียงในช่อง Clip", library);
                else if (sound.volume <= 0f && !sound.useRandomVolume)
                    Debug.LogWarning($"[SoundManager] '{sound.id.name}' ตั้ง Volume = 0 จะไม่ได้ยินเสียง", library);
                else if (sound.spatialBlend > 0f && sound.maxDistance <= 0f)
                    Debug.LogWarning($"[SoundManager] '{sound.id.name}' เป็นเสียง 3D แต่ Max Distance = 0 จะไม่ได้ยินเสียง", library);
#endif
            }
        }
    }

    private bool TryGetData(SoundID id, out SoundData data)
    {
        data = null;
        if (id == null || soundsById == null) return false;

        if (soundsById.TryGetValue(id, out data)) return true;

#if UNITY_EDITOR
        // เดิมจุดนี้ return false เงียบๆ ทำให้หาสาเหตุ 'ไม่มีเสียง' ยากมาก
        // สาเหตุที่พบบ่อยที่สุดคือลืมลาก SoundLibrary เข้า SoundTable
        Debug.LogWarning($"[SoundManager] ไม่พบ '{id.name}' ใน SoundTable\n" +
            "เช็ค: SoundData ที่มี Id นี้อยู่ในไลบรารีไหน และไลบรารีนั้นถูกลากเข้า SoundTable แล้วหรือยัง", id);
#endif
        return false;
    }

    // ==========================================
    // SFX (One-shot) — ไม่ต้องใช้ใบเสร็จ เพราะยิงแล้วจบ ไม่มีใครสั่งงานมันทีหลัง
    // ==========================================
    public float PlaySFX(SoundID id, Vector3 position)
    {
        SFXPlayer player = SpawnPlayer(id, position, out SoundData data);
        if (player == null) return 0f;
        return player.Play(data);
    }

    public float PlaySFXAttached(SoundID id, Transform target)
    {
        if (target == null) return 0f;

        SFXPlayer player = SpawnPlayer(id, target.position, out SoundData data);
        if (player == null) return 0f;

        player.FollowTarget(target);
        return player.Play(data);
    }

    // ==========================================
    // SFX (Loop) — คืน SFXHandle แทน SFXPlayer
    // ==========================================
    public SFXHandle PlayLoopSFX(SoundID id, Vector3 position, float duration)
    {
        SFXPlayer player = SpawnPlayer(id, position, out SoundData data);
        if (player == null) return SFXHandle.None;

        player.PlayLoop(data, duration);
        return new SFXHandle(player, player.Version);
    }

    public SFXHandle PlayLoopSFXForever(SoundID id, Vector3 position)
    {
        SFXPlayer player = SpawnPlayer(id, position, out SoundData data);
        if (player == null) return SFXHandle.None;

        player.PlayLoopForever(data);
        return new SFXHandle(player, player.Version);
    }

    // เสียงติดตามแบบยิงครั้งเดียว แต่คืน handle ไว้สั่งหยุดกลางคันได้
    // ใช้กับเสียงพูดที่ต้องตัดคิวกันได้ เช่นเสียงกรี๊ดต้องแทรกประโยคที่พูดค้างอยู่
    public SFXHandle PlaySFXAttachedTracked(SoundID id, Transform target, out float duration)
    {
        duration = 0f;
        if (target == null) return SFXHandle.None;

        SFXPlayer player = SpawnPlayer(id, target.position, out SoundData data);
        if (player == null) return SFXHandle.None;

        player.FollowTarget(target);
        duration = player.Play(data);
        return new SFXHandle(player, player.Version);
    }

    // เสียงลูปที่ 'วิ่งตาม' วัตถุ — จำเป็นสำหรับผีที่ลอยไปมา
    // ของเดิม PlayLoopSFXForever ปักเสียงไว้กับที่ พอผีลอยไป เสียงจะค้างอยู่จุดเดิม
    public SFXHandle PlayLoopSFXForeverAttached(SoundID id, Transform target)
    {
        if (target == null) return SFXHandle.None;

        SFXPlayer player = SpawnPlayer(id, target.position, out SoundData data);
        if (player == null) return SFXHandle.None;

        player.FollowTarget(target);
        player.PlayLoopForever(data);
        return new SFXHandle(player, player.Version);
    }

    // ==========================================
    // พูลลำโพง
    // ==========================================

    private SoundPooler GetPooler()
    {
        if (cachedPooler != null) return cachedPooler;

        if (SoundPooler.Instance != null)
        {
            cachedPooler = SoundPooler.Instance;
            return cachedPooler;
        }

        GameObject obj = new GameObject("SoundPooler (Auto)");
        cachedPooler = obj.AddComponent<SoundPooler>();

#if UNITY_EDITOR
        if (warnOnAutoCreatePooler)
            Debug.LogWarning("[SoundManager] ฉากนี้ไม่มี SoundPooler — สร้างให้อัตโนมัติแล้ว\n" +
                "เสียงดังปกติ แต่ปรับขนาดพูลให้เหมาะกับฉากไม่ได้ " +
                "ถ้าอยากปรับ ให้ลาก prefab SoundPooler เข้ามาวางในฉาก", this);
#endif

        return cachedPooler;
    }

    private SFXPlayer SpawnPlayer(SoundID id, Vector3 position, out SoundData data)
    {
        if (!TryGetData(id, out data)) return null;

        SoundPooler pooler = GetPooler();
        return pooler != null ? pooler.Get(position) : null;
    }

    public void PlayEventSFX(SoundID id) => PlaySFX(id, GetListenerPosition());

    private Vector3 GetListenerPosition()
    {
        // AudioListenerCache หาครั้งเดียวแล้วจำไว้ (และ deprecated ใน Unity 6 ด้วย)
        Transform listener = AudioListenerCache.Transform;
        if (listener != null) return listener.position;
        if (Camera.main != null) return Camera.main.transform.position;
        return Vector3.zero;
    }

    // ==========================================
    // Voice & Ducking
    // ==========================================
    public float PlayVoice(AudioClip clip, float volume = 1f)
    {
        if (clip == null || voiceSource == null) return 0f;

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.volume = Mathf.Clamp01(volume);
        voiceSource.pitch = 1f;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;

        if (voiceMixerGroup != null)
            voiceSource.outputAudioMixerGroup = voiceMixerGroup;

        voiceSource.Play();

        if (duckBgmDuringVoice)
            StartDuck(true);

        return clip.length;
    }

    public void StopVoice()
    {
        if (voiceSource != null) voiceSource.Stop();
        if (duckBgmDuringVoice) StartDuck(false);
    }

    public void SetVoiceDuckHold(bool hold)
    {
        duckHold = hold;
        if (hold && duckBgmDuringVoice) StartDuck(true);
    }

    private void StartDuck(bool ducked)
    {
        if (duckRoutine != null) StopCoroutine(duckRoutine);
        duckRoutine = StartCoroutine(DuckRoutine(ducked));
    }

    private IEnumerator DuckRoutine(bool ducked)
    {
        float target = ducked ? Mathf.Clamp01(duckedBgmMultiplier) : 1f;
        yield return FadeDuckTo(target);

        if (!ducked)
        {
            duckRoutine = null;
            yield break;
        }

        // รอให้เงียบครบ duckReleaseDelay ก่อน ถ้าประโยคใหม่มาก่อนก็รอต่อ
        while (true)
        {
            while (IsVoicePlaying || duckHold) yield return null;

            float t = 0f;
            bool voiceResumed = false;

            while (t < duckReleaseDelay)
            {
                if (IsVoicePlaying || duckHold)
                {
                    voiceResumed = true;
                    break;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!voiceResumed) break;
        }

        duckRoutine = StartCoroutine(DuckRoutine(false));
    }

    private IEnumerator FadeDuckTo(float targetMultiplier)
    {
        float start = duckMultiplier;
        float fadeTime = Mathf.Max(0.01f, duckFadeTime);
        float t = 0f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            duckMultiplier = Mathf.Lerp(start, targetMultiplier, t / fadeTime);
            ApplyBgmVolume();
            yield return null;
        }

        duckMultiplier = targetMultiplier;
        ApplyBgmVolume();
    }

    private float TargetBgmVolume() => bgmBaseVolume * duckMultiplier;

    private void ApplyBgmVolume()
    {
        // ระหว่าง crossfade ปล่อยให้ routine นั้นคุมความดังเอง
        // มันอ่าน TargetBgmVolume() ทุกเฟรมอยู่แล้ว การหรี่จึงมีผลด้วย
        if (bgmFadeRoutine != null) return;

        if (activeBgm != null) activeBgm.volume = TargetBgmVolume();
    }

    // ==========================================
    // BGM
    // ==========================================

    public void PlayBGM(SoundID id)
    {
        if (!EnsureBgmSources()) return;
        if (!TryGetData(id, out SoundData data)) return;

        AudioClip clipToPlay = data.GetClip();
        if (clipToPlay == null) return;

        if (currentBgmId == id && activeBgm != null && activeBgm.isPlaying) return;

        currentBgmId = id;
        bgmBaseVolume = data.volume;

        // เพลงแรกของฉาก ไม่มีอะไรให้ไล่ออก เปิดตรงๆ เลย
        bool nothingPlaying = !activeBgm.isPlaying || activeBgm.clip == null;

        if (nothingPlaying)
        {
            if (bgmFadeRoutine != null) { StopCoroutine(bgmFadeRoutine); bgmFadeRoutine = null; }

            SetupBgmSource(activeBgm, clipToPlay, data);
            activeBgm.volume = TargetBgmVolume();
            activeBgm.Play();
            return;
        }

        SetupBgmSource(standbyBgm, clipToPlay, data);
        standbyBgm.volume = 0f;
        standbyBgm.Play();

        if (bgmFadeRoutine != null) StopCoroutine(bgmFadeRoutine);
        bgmFadeRoutine = StartCoroutine(CrossfadeRoutine());
    }

    private void SetupBgmSource(AudioSource source, AudioClip clip, SoundData data)
    {
        source.clip = clip;
        source.loop = true;
        source.pitch = 1f;
        source.spatialBlend = 0f;

        if (data.mixerGroup != null)
            source.outputAudioMixerGroup = data.mixerGroup;
    }

    private IEnumerator CrossfadeRoutine()
    {
        AudioSource fadeIn = standbyBgm;
        AudioSource fadeOut = activeBgm;

        // สลับบทบาทตั้งแต่ต้น เผื่อมีคนสั่ง PlayBGM ซ้ำระหว่างที่ยัง fade ไม่จบ
        // จะได้เห็นค่าถูกต้องว่าตอนนี้ใครถือเพลงอยู่
        activeBgm = fadeIn;
        standbyBgm = fadeOut;

        float fadeTime = Mathf.Max(0.01f, bgmCrossfadeTime);
        float fadeOutStart = fadeOut != null ? fadeOut.volume : 0f;
        float t = 0f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;   // unscaled เพราะเพลงต้องไหลต่อตอนเกมหยุด
            float k = Mathf.Clamp01(t / fadeTime);

            fadeIn.volume = TargetBgmVolume() * k;
            if (fadeOut != null) fadeOut.volume = fadeOutStart * (1f - k);

            yield return null;
        }

        fadeIn.volume = TargetBgmVolume();

        if (fadeOut != null)
        {
            fadeOut.Stop();
            fadeOut.clip = null;
            fadeOut.volume = 0f;
        }

        bgmFadeRoutine = null;
    }

    public void StopBGM()
    {
        currentBgmId = null;

        if (bgmFadeRoutine != null) { StopCoroutine(bgmFadeRoutine); bgmFadeRoutine = null; }

        if (bgmSource != null) { bgmSource.Stop(); bgmSource.clip = null; bgmSource.volume = 0f; }
        if (bgmSourceB != null) { bgmSourceB.Stop(); bgmSourceB.clip = null; bgmSourceB.volume = 0f; }
    }

    /// <summary>เตรียมลำโพงเพลงให้ครบ 2 ตัว — ตัวที่ 2 สร้างเองได้ถ้าไม่ได้ใส่มา</summary>
    private bool EnsureBgmSources()
    {
        if (bgmSource == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[SoundManager] ไม่มี Bgm Source — เพลงจะไม่ดัง\n" +
                "สร้าง GameObject ใส่ Audio Source แล้วลากเข้าช่อง Bgm Source", this);
#endif
            return false;
        }

        if (bgmSourceB == null)
            bgmSourceB = CreateBgmSourceLike(bgmSource, "BGM Source B");

        if (activeBgm == null)
        {
            activeBgm = bgmSource;
            standbyBgm = bgmSourceB;
        }

        return true;
    }

    private AudioSource CreateBgmSourceLike(AudioSource template, string objectName)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(transform, false);

        AudioSource source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.priority = template.priority;
        source.outputAudioMixerGroup = template.outputAudioMixerGroup;

        return source;
    }

    // ==========================================
    // Mixer Snapshots (Adaptive Audio)
    // ==========================================
    public void TransitionToSnapshot(string snapshotName, float transitionTime)
    {
        if (audioMixer == null || string.IsNullOrEmpty(snapshotName)) return;

        AudioMixerSnapshot snapshot = audioMixer.FindSnapshot(snapshotName);

        if (snapshot == null)
        {
            Debug.LogWarning($"[SoundManager] ไม่พบ Snapshot ชื่อ '{snapshotName}'");
            return;
        }

        snapshot.TransitionTo(Mathf.Max(0f, transitionTime));
    }

    // ==========================================
    // Volume Control
    // ==========================================
    public void SetMasterVolume(float level01) => SetMixerVolume(masterParam, level01, masterBaseDb);
    public void SetSoundFXVolume(float level01) => SetMixerVolume(sfxParam, level01, sfxBaseDb);
    public void SetMusicVolume(float level01) => SetMixerVolume(bgmParam, level01, bgmBaseDb);
    public void SetVoiceVolume(float level01) => SetMixerVolume(voiceParam, level01, voiceBaseDb);

    public void SetUIVolume(float level01) => SetMixerVolume(uiParam, level01, uiBaseDb);

    private void SetMixerVolume(string param, float level01, float baseDb)
    {
        if (audioMixer == null || string.IsNullOrEmpty(param)) return;

        if (level01 <= 0.0001f)
        {
            audioMixer.SetFloat(param, -80f);   // -80 คือเงียบสนิทของ Unity
            return;
        }

        float userDb = Mathf.Log10(Mathf.Clamp01(level01)) * 20f;   // 1.0 = 0 dB, 0.5 = -6 dB
        audioMixer.SetFloat(param, Mathf.Max(-80f, baseDb + userDb));
    }
}