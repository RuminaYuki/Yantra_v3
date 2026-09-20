using UnityEngine;

public class BattleMusicController : MonoBehaviour
{
    [Header("Event Channels")]
    [Tooltip("ช่องที่ Battle_State ยิงตอนเข้าสู่การต่อสู้\nปกติคือ GameStateBattle_VoidEC")]
    [SerializeField] private VoidEventChannelSO _onEnterBattle;

    [Tooltip("ช่องที่ Idle_State ยิงตอนออกจากการต่อสู้\nปกติคือ GameStateIdle_VoidEC")]
    [SerializeField] private VoidEventChannelSO _onEnterIdle;

    [Header("เพลง")]
    [SerializeField] private SoundID _idleMusic;
    [SerializeField] private SoundID _battleMusic;

    [Header("Options")]
    [Tooltip("เปิดเพลงปกติให้เองตอนเริ่มฉาก\n" +
        "ตาข่ายกันตก เผื่อ event แรกยิงตอนที่ SoundManager ยังไม่พร้อม\n" +
        "ถ้าเพลงถูกเปิดไปแล้ว บรรทัดนี้จะไม่ทำอะไร เพราะ PlayBGM กันเล่นซ้ำอยู่แล้ว")]
    [SerializeField] private bool _playIdleOnStart = true;

    [Header("Debug")]
    [Tooltip("ขึ้น log ทุกครั้งที่สลับสถานะ\n" +
        "เปิดไว้ตอนยังไม่มีไฟล์เพลง จะได้เช็คว่าสายไฟต่อถูกแล้วจริง")]
    [SerializeField] private bool _logStateChange = false;

    private void OnEnable()
    {
        if (_onEnterBattle != null) _onEnterBattle.Raised += HandleEnterBattle;
        if (_onEnterIdle != null) _onEnterIdle.Raised += HandleEnterIdle;

#if UNITY_EDITOR
        if (_onEnterBattle == null || _onEnterIdle == null)
            Debug.LogWarning("[BattleMusic] ยังใส่ Event Channel ไม่ครบ 2 ช่อง — เพลงจะไม่สลับ", this);
#endif
    }

    private void OnDisable()
    {
        if (_onEnterBattle != null) _onEnterBattle.Raised -= HandleEnterBattle;
        if (_onEnterIdle != null) _onEnterIdle.Raised -= HandleEnterIdle;
    }

    private void Start()
    {
        if (!_playIdleOnStart) return;
        Switch(_idleMusic, "Idle (เริ่มฉาก)");
    }

    private void HandleEnterBattle() => Switch(_battleMusic, "Battle");
    private void HandleEnterIdle() => Switch(_idleMusic, "Idle");

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