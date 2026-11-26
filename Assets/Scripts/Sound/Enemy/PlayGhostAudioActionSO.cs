using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

/// <summary>
/// ใส่ใน State ของผี (Alert / Stare ...) — เข้า State แล้วเล่น SoundID ผ่าน GhostAudio บนตัวผี
/// ตั้งไฟล์ / ความดัง / ระยะ ใน SoundData ของ SoundLibrary ตามระบบเสียงปกติ
/// สร้าง asset หนึ่งอันต่อหนึ่งเสียง เช่น Pret_Alert_Audio, Pret_Stare_Audio
/// </summary>
[CreateAssetMenu(
    fileName = "PlayGhostAudio_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Audio/Play Ghost Audio")]
public class PlayGhostAudioActionSO : StateActionSO
{
    public enum Mode
    {
        Once,           // เข้า State แล้วเล่นครั้งเดียว (เห็นผู้เล่น / กรีดร้อง)
        LoopInState     // วนตลอดที่อยู่ใน State ออกแล้วค่อย ๆ เงียบ (จ้อง)
    }

    [SerializeField] private Mode _mode = Mode.Once;

    [Tooltip("SoundID ที่อยู่ใน SoundLibrary — สุ่มไฟล์ / pitch ตั้งใน SoundData")]
    [SerializeField] private SoundID _sound;

    [Tooltip("เข้า State แล้วรอกี่วินาทีค่อยเล่น — ให้ตรงจังหวะท่า")]
    [SerializeField, Min(0f)] private float _delay = 0f;

    [Tooltip("LoopInState: เวลาค่อย ๆ ดังขึ้น / เบาลง (วินาที)")]
    [SerializeField, Min(0f)] private float _fade = 0.5f;

    protected override StateAction OnCreateAction(StateMachine stateMachine) =>
        new PlayGhostAudioAction(_mode, _sound, _delay, _fade);
}

public class PlayGhostAudioAction : StateAction
{
    private readonly PlayGhostAudioActionSO.Mode _mode;
    private readonly SoundID _sound;
    private readonly float _delay;
    private readonly float _fade;

    private GhostAudio _audio;
    private float _elapsed;
    private bool _played;

    public PlayGhostAudioAction(PlayGhostAudioActionSO.Mode mode, SoundID sound, float delay, float fade)
    {
        _mode = mode;
        _sound = sound;
        _delay = delay;
        _fade = fade;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _audio = stateMachine.Owner.GetComponentInParent<GhostAudio>();
        if (_audio == null) _audio = stateMachine.Owner.GetComponentInChildren<GhostAudio>();

        if (_audio == null)
            Debug.LogWarning($"[PlayGhostAudio] {stateMachine.Owner.name} ไม่มี GhostAudio — แปะบนตัวผีก่อน", stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        _elapsed = 0f;
        _played = false;
        if (_delay <= 0f) Play();
    }

    public override void OnUpdate()
    {
        if (_played) return;

        // ใช้ UnityEngine.Time ตรง ๆ — ใน state machine มี Time ของตัวเอง (StateMachineClock)
        _elapsed += UnityEngine.Time.deltaTime;
        if (_elapsed >= _delay) Play();
    }

    public override void OnStateExit()
    {
        if (_mode == PlayGhostAudioActionSO.Mode.LoopInState && _audio != null)
            _audio.StopLoop(_fade);
    }

    private void Play()
    {
        _played = true;
        if (_audio == null || _sound == null) return;

        if (_mode == PlayGhostAudioActionSO.Mode.Once)
            _audio.PlayVoice(_sound);
        else
            _audio.StartLoop(_sound, _fade);
    }
}
