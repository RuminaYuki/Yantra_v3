using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : Singleton<SoundManager>
{
    [SerializeField] private SoundTable soundTable;

    [Header("SFX")]
    [SerializeField] private string audioPoolTag = "SFXsource";

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;

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

    [Header("Mixer Base Levels (dB)")]
    [Tooltip("ใส่ค่า dB ที่คุณลากไว้ในหน้าต่าง Audio Mixer ของแต่ละกลุ่ม\n" +
        "Slider ของผู้เล่นจะ 'หรี่ลงจากค่านี้' แทนที่จะเขียนทับมัน")]
    [Range(-80f, 20f)][SerializeField] private float masterBaseDb = 0f;
    [Range(-80f, 20f)][SerializeField] private float sfxBaseDb = 0f;
    [Range(-80f, 20f)][SerializeField] private float bgmBaseDb = 0f;
    [Range(-80f, 20f)][SerializeField] private float voiceBaseDb = 0f;

    private SoundID currentBgmId;
    private Dictionary<SoundID, SoundData> soundsById;

    private float bgmBaseVolume = 1f;
    private Coroutine duckRoutine;
    private bool duckHold;

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
        // [ADD] เดิมจุดนี้ return false เงียบๆ ทำให้หาสาเหตุ 'ไม่มีเสียง' ยากมาก
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
    // SFX (Loop) — [CHANGED] คืน SFXHandle แทน SFXPlayer
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

    // [ADD] เสียงติดตามแบบยิงครั้งเดียว แต่คืน handle ไว้สั่งหยุดกลางคันได้
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

    // [ADD] เสียงลูปที่ 'วิ่งตาม' วัตถุ — จำเป็นสำหรับผีที่ลอยไปมา
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

    private SFXPlayer SpawnPlayer(SoundID id, Vector3 position, out SoundData data)
    {
        if (!TryGetData(id, out data)) return null;

        if (ObjectPooler.Instance == null) return null;

        SFXPlayer result = null;
        GameObject audioObject = ObjectPooler.Instance.SpawnFromPool(
            audioPoolTag, position, Quaternion.identity,
            (obj) =>
            {
                if (obj.TryGetComponent(out SFXPlayer p))
                {
                    p.myPoolTag = audioPoolTag;
                    result = p;
                }
            }
        );

        return result;
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

    /// <summary>
    /// ล็อกการหรี่เพลงไว้ตลอดบทสนทนา
    /// ระบบ Subtitle เรียก true ตอนเริ่มชุด และ false ตอนจบ
    /// ทำให้เพลงหรี่ค้างตลอด ไม่กระเพื่อมขึ้นลงตามช่องว่างระหว่างประโยค
    /// </summary>
    public void SetVoiceDuckHold(bool hold)
    {
        duckHold = hold;
        if (hold && duckBgmDuringVoice) StartDuck(true);
    }

    private void StartDuck(bool ducked)
    {
        if (bgmSource == null) return;
        if (duckRoutine != null) StopCoroutine(duckRoutine);
        duckRoutine = StartCoroutine(DuckRoutine(ducked));
    }

    private IEnumerator DuckRoutine(bool ducked)
    {
        float target = ducked ? bgmBaseVolume * duckedBgmMultiplier : bgmBaseVolume;
        yield return FadeBgmTo(target);

        if (!ducked)
        {
            duckRoutine = null;
            yield break;
        }

        // ตอนนี้จะรอให้เงียบครบ duckReleaseDelay ก่อน ถ้าประโยคใหม่มาก่อนก็รอต่อ
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

    private IEnumerator FadeBgmTo(float target)
    {
        float start = bgmSource.volume;
        float t = 0f;

        while (t < duckFadeTime)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(start, target, t / duckFadeTime);
            yield return null;
        }

        bgmSource.volume = target;
    }

    // ==========================================
    // BGM
    // ==========================================
    public void PlayBGM(SoundID id)
    {
        if (bgmSource == null) return;
        if (!TryGetData(id, out SoundData data)) return;

        AudioClip clipToPlay = data.GetClip();
        if (clipToPlay == null) return;

        if (currentBgmId == id && bgmSource.isPlaying) return;

        currentBgmId = id;
        bgmBaseVolume = data.volume;

        bgmSource.clip = clipToPlay;
        bgmSource.volume = IsVoicePlaying && duckBgmDuringVoice ? bgmBaseVolume * duckedBgmMultiplier : bgmBaseVolume;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;

        if (data.mixerGroup != null)
            bgmSource.outputAudioMixerGroup = data.mixerGroup;

        bgmSource.Play();
    }

    public void StopBGM()
    {
        currentBgmId = null;
        if (bgmSource != null) bgmSource.Stop();
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