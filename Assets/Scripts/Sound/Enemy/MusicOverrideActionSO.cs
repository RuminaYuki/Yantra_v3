using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

/// <summary>
/// เปลี่ยนเพลงตอนผีเจอเรา — ใส่ใน State ของผี
///   Push         : เข้า State แล้วเปลี่ยนเป็นเพลงนี้ (ค้างไว้จนมี Pop)   เช่น EnterStare_State
///   Pop          : เข้า State แล้วคืนเพลงเดิม                          เช่น BackHome_State
///   WhileInState : เปลี่ยนตอนเข้า คืนตอนออก
///   PushIfNone   : เปลี่ยนเฉพาะตอนผีตัวนี้ยังไม่ได้ขอเพลงไว้ — ใส่ใน State ที่เข้าซ้ำบ่อย (เช่น Burrowing ของบอส)
///                  เพลงเฟสหลังที่ Push ทับไว้แล้วจะไม่ถูกเปลี่ยนกลับ
/// ฉากที่มี BattleMusicController = ทับเพลงรบ / ไม่รบ แล้วคืนให้ถูก
/// ฉากที่ไม่มี = สั่ง SoundManager ตรง ๆ แล้วคืนเป็น Scene BGM ของ LevelAudioManager
/// ผีหาย / ถูกปิด ระหว่างขอเพลงอยู่ = BattleMusicController ถอดให้เอง ไม่ค้าง
/// </summary>
[CreateAssetMenu(
    fileName = "MusicOverride_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Audio/Music Override")]
public class MusicOverrideActionSO : StateActionSO
{
    public enum Mode
    {
        Push,           // เปลี่ยนเพลง ค้างไว้
        Pop,            // คืนเพลงเดิม
        WhileInState,   // เปลี่ยนตอนเข้า คืนตอนออก
        PushIfNone      // เปลี่ยนเฉพาะตอนยังไม่ได้ขอเพลงไว้
    }

    [SerializeField] private Mode _mode = Mode.Push;

    [Tooltip("เพลง (SoundID ใน SoundLibrary ที่ตั้ง Mixer เป็น BGM) — โหมด Pop ไม่ต้องใส่")]
    [SerializeField] private SoundID _music;

    protected override StateAction OnCreateAction(StateMachine stateMachine) =>
        new MusicOverrideAction(_mode, _music);
}

public class MusicOverrideAction : StateAction
{
    private readonly MusicOverrideActionSO.Mode _mode;
    private readonly SoundID _music;
    private GameObject _owner;

    // ฉากไม่มี BattleMusicController — จำไว้ว่าเราเปลี่ยนเพลงไปแล้วหรือยัง
    private static GameObject _fallbackOwner;

    public MusicOverrideAction(MusicOverrideActionSO.Mode mode, SoundID music)
    {
        _mode = mode;
        _music = music;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _owner = stateMachine.Owner;
    }

    public override void OnStateEnter()
    {
        if (_mode == MusicOverrideActionSO.Mode.Pop) Pop();
        else if (_mode == MusicOverrideActionSO.Mode.PushIfNone) PushIfNone();
        else Push();
    }

    public override void OnUpdate() { }

    public override void OnStateExit()
    {
        if (_mode == MusicOverrideActionSO.Mode.WhileInState) Pop();
    }

    private void Push()
    {
        if (_music == null || _owner == null) return;

        BattleMusicController battle = BattleMusicController.Active;
        if (battle != null)
        {
            battle.PushOverride(_owner, _music);
            return;
        }

        if (SoundManager.Instance == null) return;
        SoundManager.Instance.PlayBGM(_music);
        _fallbackOwner = _owner;
    }

    private void PushIfNone()
    {
        BattleMusicController battle = BattleMusicController.Active;
        if (battle != null ? battle.HasOverride(_owner) : _fallbackOwner == _owner) return;
        Push();
    }

    private void Pop()
    {
        BattleMusicController battle = BattleMusicController.Active;
        if (battle != null)
        {
            battle.PopOverride(_owner);
            return;
        }

        if (_fallbackOwner != _owner || SoundManager.Instance == null) return;
        _fallbackOwner = null;

        SoundID sceneBgm = LevelAudioManager.Instance != null ? LevelAudioManager.Instance.SceneBGM : null;
        if (sceneBgm != null) SoundManager.Instance.PlayBGM(sceneBgm);
        else SoundManager.Instance.StopBGM();
    }
}
