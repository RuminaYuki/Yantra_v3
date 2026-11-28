using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คุมเพลงของฉาก: ไม่รบ / รบ (ฟัง Event Channel)
/// + เพลงพิเศษที่ผีขอทับ (Override) เช่นเปรตเจอเรา — ผ่าน action "Music Override" ใน table ของผี
///   มี Override อยู่ = เล่นตัวล่าสุด ทับเพลงรบ / ไม่รบ / หมดแล้ว = กลับไปเพลงตามสถานะเดิม
/// </summary>
public class BattleMusicController : MonoBehaviour
{
    /// <summary>ตัวที่ทำงานอยู่ในฉากตอนนี้ — ให้ action ของผีหาเจอ</summary>
    public static BattleMusicController Active { get; private set; }

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

    // เพลงที่ผีขอทับ — ตัวท้ายสุด = ตัวที่เล่น / owner = ตัวผี (หายไป = ถอดเพลงให้เอง)
    private readonly List<(GameObject owner, SoundID music)> _overrides = new List<(GameObject, SoundID)>();
    private bool _inBattle;

    /// <summary>เพลงที่ควรเล่นตอนนี้ (Override → รบ → ไม่รบ)</summary>
    public SoundID CurrentMusic
    {
        get
        {
            if (_overrides.Count > 0) return _overrides[_overrides.Count - 1].music;
            return _inBattle ? _battleMusic : ResolvedIdleMusic;
        }
    }

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
        Active = this;
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
        if (Active == this) Active = null;
        _overrides.Clear();
        if (_onEnterBattle != null) _onEnterBattle.Raised -= HandleEnterBattle;
        if (_onEnterIdle != null) _onEnterIdle.Raised -= HandleEnterIdle;
    }

    private void Start()
    {
        if (!_playIdleOnStart) return;
        Refresh("Idle (เริ่มฉาก)");
    }

    // ผีที่ขอเพลงไว้ถูกลบ / ปิดไป = ถอดเพลงของมันออกให้เอง ไม่ให้เพลงค้าง
    private void Update()
    {
        if (_overrides.Count == 0) return;

        int removed = _overrides.RemoveAll(o => o.owner == null || !o.owner.activeInHierarchy);
        if (removed > 0) Refresh("Override (ผีหายไป)");
    }

    private void HandleEnterBattle()
    {
        _inBattle = true;
        Refresh("Battle");
    }

    private void HandleEnterIdle()
    {
        _inBattle = false;
        Refresh("Idle");
    }

    // ---------- Override (เรียกจาก action ของผี) ----------

    /// <summary>ขอเพลงทับ — owner ขอได้ทีละเพลง ขอใหม่ = แทนของเดิม</summary>
    public void PushOverride(GameObject owner, SoundID music)
    {
        if (owner == null || music == null) return;

        _overrides.RemoveAll(o => o.owner == owner);
        _overrides.Add((owner, music));
        Refresh($"Override ← {owner.name}");
    }

    /// <summary>owner นี้ขอเพลงค้างไว้อยู่ไหม</summary>
    public bool HasOverride(GameObject owner) => owner != null && _overrides.Exists(o => o.owner == owner);

    /// <summary>คืนเพลงที่ owner ขอไว้ — ไม่มีก็ไม่ทำอะไร</summary>
    public void PopOverride(GameObject owner)
    {
        if (_overrides.RemoveAll(o => o.owner == owner) > 0)
            Refresh($"Override คืน ← {(owner != null ? owner.name : "?")}");
    }

    private void Refresh(string label) => Switch(CurrentMusic, label);

    private void Switch(SoundID music, string label)
    {
        if (_logStateChange)
            Debug.Log($"[BattleMusic] → {label}  (เพลง: {(music != null ? music.name : "ยังไม่ได้ใส่")})", this);

        if (SoundManager.Instance == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[BattleMusic] ไม่เจอ SoundManager ในฉากนี้ — เพลงจะไม่ดัง", this);
#endif
            return;
        }

        // ไม่มีเพลงตอนไม่รบ = ปิดเพลงรบทิ้ง เหลือแค่เสียงบรรยากาศ
        if (music == null)
            SoundManager.Instance.StopBGM();
        else
            SoundManager.Instance.PlayBGM(music);
    }
}