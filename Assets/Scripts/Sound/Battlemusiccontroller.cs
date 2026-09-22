using UnityEngine;

public class BattleMusicController : MonoBehaviour
{
    [Header("Event Channels")]
    [Tooltip("ช่องที่ Battle_State ยิงตอนเข้าสู่การต่อสู้\nปกติคือ GameStateBattle_VoidEC")]
    [SerializeField] private VoidEventChannelSO _onEnterBattle;

    [Tooltip("ช่องที่ Idle_State ยิงตอนออกจากการต่อสู้\nปกติคือ GameStateIdle_VoidEC")]
    [SerializeField] private VoidEventChannelSO _onEnterIdle;

    [Header("เพลง")]
    [Tooltip("เพลงตอนไม่รบ\n" +
        "เว้นว่างได้ — จะไปใช้ Scene BGM ของ LevelAudioManager ในฉากนี้แทน\n" +
        "ใส่ค่าเมื่ออยากให้เพลงตอนเดินต่างจากที่ตั้งไว้ใน LevelAudioManager")]
    [SerializeField] private SoundID _idleMusic;

    [SerializeField] private SoundID _battleMusic;

    [Header("Options")]
    [Tooltip("เปิดเพลงตอนไม่รบให้เองตอนเริ่มฉาก\n" +
        "ตาข่ายกันตก เผื่อ event แรกยิงตอนที่ SoundManager ยังไม่พร้อม\n" +
        "ถ้าเพลงถูกเปิดไปแล้ว บรรทัดนี้จะไม่ทำอะไร เพราะ PlayBGM กันเล่นซ้ำอยู่แล้ว")]
    [SerializeField] private bool _playIdleOnStart = true;

    [Header("Debug")]
    [Tooltip("ขึ้น log ทุกครั้งที่สลับสถานะ\n" +
        "เปิดไว้ตอนยังไม่มีไฟล์เพลง จะได้เช็คว่าสายไฟต่อถูกแล้วจริง")]
    [SerializeField] private bool _logStateChange = false;

    /// <summary>
    /// เพลงตอนไม่รบที่จะใช้จริง
    /// ใส่ในช่องเอง = ใช้ของตัวเอง / เว้นว่าง = ยืม Scene BGM ของฉากมาใช้
    /// </summary>
    private SoundID ResolvedIdleMusic
    {
        get
        {
            if (_idleMusic != null) return _idleMusic;

            if (LevelAudioManager.Instance != null)
                return LevelAudioManager.Instance.SceneBGM;

            return null;
        }
    }

    private void OnEnable()
    {
        if (_onEnterBattle != null) _onEnterBattle.Raised += HandleEnterBattle;
        if (_onEnterIdle != null) _onEnterIdle.Raised += HandleEnterIdle;

#if UNITY_EDITOR
        if (_onEnterBattle == null || _onEnterIdle == null)
            Debug.LogWarning("[BattleMusic] ยังใส่ Event Channel ไม่ครบ 2 ช่อง — เพลงจะไม่สลับ", this);
#endif
    }

    /// <summary>
    /// ต้องถอดออกให้ครบเสมอ ห้ามลืม
    ///
    /// โปรเจกต์เราปิด Domain Reload ไว้ (Enter Play Mode Settings)
    /// event ที่อยู่ใน ScriptableObject จึงไม่ถูกล้างตอนกด Stop
    /// ถ้าไม่ถอด กด Play รอบสองจะมีตัวสมัครค้างจากรอบแรก
    /// เข้ารบทีเดียวแต่สั่งเปลี่ยนเพลง 2 ครั้งซ้อน
    /// </summary>
    private void OnDisable()
    {
        if (_onEnterBattle != null) _onEnterBattle.Raised -= HandleEnterBattle;
        if (_onEnterIdle != null) _onEnterIdle.Raised -= HandleEnterIdle;
    }

    private void Start()
    {
        if (!_playIdleOnStart) return;
        Switch(ResolvedIdleMusic, "Idle (เริ่มฉาก)");
    }

    private void HandleEnterBattle() => Switch(_battleMusic, "Battle");
    private void HandleEnterIdle() => Switch(ResolvedIdleMusic, "Idle");

    private void Switch(SoundID music, string label)
    {
        if (_logStateChange)
            Debug.Log($"[BattleMusic] → {label}  (เพลง: {(music != null ? music.name : "ยังไม่ได้ใส่")})", this);

        if (music == null) return;

        if (SoundManager.Instance == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[BattleMusic] ไม่เจอ SoundManager ในฉากนี้ — เพลงจะไม่ดัง", this);
#endif
            return;
        }

        SoundManager.Instance.PlayBGM(music);
    }
}