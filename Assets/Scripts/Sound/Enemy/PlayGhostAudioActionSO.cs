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

    [Tooltip("ใช้ช่องเสียงของผีแต่ละตัว (ตั้งใน GhostAudio > เสียงตามท่า)\n" +
             "table กลางที่ผีหลายตัวใช้ร่วมกัน (table ลูกน้อง) ให้ใช้ช่องนี้ — ผีแต่ละตัวเลือกไฟล์เอง\n" +
             "None = ใช้ช่อง Sound ข้างล่างตรง ๆ")]
    [SerializeField] private GhostSoundSlot _slot = GhostSoundSlot.None;

    [Tooltip("SoundID ที่อยู่ใน SoundLibrary — สุ่มไฟล์ / pitch ตั้งใน SoundData (ใช้เมื่อ Slot = None)")]
    [SerializeField] private SoundID _sound;

    [Tooltip("เข้า State แล้วรอกี่วินาทีค่อยเล่น — ให้ตรงจังหวะท่า")]
    [SerializeField, Min(0f)] private float _delay = 0f;

    [Tooltip("LoopInState: เวลาค่อย ๆ ดังขึ้น / เบาลง (วินาที)")]
    [SerializeField, Min(0f)] private float _fade = 0.5f;

    protected override StateAction OnCreateAction(StateMachine stateMachine) =>
        new PlayGhostAudioAction(_mode, _slot, _sound, _delay, _fade);
}

public class PlayGhostAudioAction : StateAction
{
    private readonly PlayGhostAudioActionSO.Mode _mode;
    private readonly GhostSoundSlot _slot;
    private readonly SoundID _sound;
    private readonly float _delay;
    private readonly float _fade;

    private GhostAudio _audio;
    private float _elapsed;
    private bool _played;

    // เสียงกับเวลารอของรอบนี้ (Slot หาจาก GhostAudio ของผีตัวนั้นตอนเข้า State)
    private SoundID _current;
    private float _currentDelay;

    public PlayGhostAudioAction(PlayGhostAudioActionSO.Mode mode, GhostSoundSlot slot, SoundID sound, float delay, float fade)
    {
        _mode = mode;
        _slot = slot;
        _sound = sound;
        _delay = delay;
        _fade = fade;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _audio = stateMachine.Owner.GetComponentInParent<GhostAudio>();
        if (_audio == null) _audio = stateMachine.Owner.GetComponentInChildren<GhostAudio>();

        // table กลาง (ใช้ Slot): ผีที่ยังไม่ทำเสียง ไม่มี GhostAudio ได้ — เงียบไปเฉย ๆ ไม่ต้องเตือน
        if (_audio == null && _slot == GhostSoundSlot.None)
            Debug.LogWarning($"[PlayGhostAudio] {stateMachine.Owner.name} ไม่มี GhostAudio — แปะบนตัวผีก่อน", stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        _elapsed = 0f;
        _played = false;

        _current = _sound;
        _currentDelay = _delay;
        if (_slot != GhostSoundSlot.None)
        {
            _current = null;
            if (_audio != null && _audio.TryGetSlot(_slot, out SoundID slotSound, out float slotDelay))
            {
                _current = slotSound;
                _currentDelay += slotDelay;
            }
        }

        if (_currentDelay <= 0f) Play();
    }

    public override void OnUpdate()
    {
        if (_played) return;

        // ใช้ UnityEngine.Time ตรง ๆ — ใน state machine มี Time ของตัวเอง (StateMachineClock)
        _elapsed += UnityEngine.Time.deltaTime;
        if (_elapsed >= _currentDelay) Play();
    }

    public override void OnStateExit()
    {
        if (_mode == PlayGhostAudioActionSO.Mode.LoopInState && _audio != null)
            _audio.StopLoop(_fade);
    }

    private void Play()
    {
        _played = true;
        if (_audio == null || _current == null) return;

        // ตายแล้วไม่ควรมีเสียงงึมงำ / หายใจค้างต่อ
        if (_slot == GhostSoundSlot.Death)
            _audio.StopIdleSounds();

        if (_mode == PlayGhostAudioActionSO.Mode.Once)
            _audio.PlayVoice(_current);
        else
            _audio.StartLoop(_current, _fade);
    }
}
