using UnityEngine;
using UnityEngine.SceneManagement;

// ซ่อน UI นอกการต่อสู้ — วางบน Object เปล่าใต้ HUDScreen แล้วย้ายแถบที่อยากซ่อน (เลือด / Guard / Skill) มาเป็นลูก
// ฟังช่องเดียวกับ BattleMusicController: เข้ารบ = โชว์ / ออกจากรบ = รอแป๊บแล้วจางหาย
// ใช้ CanvasGroup ของตัวเอง ไม่ยุ่งกับ CanvasGroup ของแถบ (GuardBar ซ่อนตอนเต็มเองอยู่แล้ว)
[RequireComponent(typeof(CanvasGroup))]
public class CombatHUDGroup : MonoBehaviour
{
    [Header("Event Channels (ช่องเดียวกับ BattleMusicController)")]
    [Tooltip("ช่องที่ Battle_State ยิงตอนเข้าสู่การต่อสู้\nปกติคือ GameStateBattle_VoidEC")]
    [SerializeField] private VoidEventChannelSO _onEnterBattle;

    [Tooltip("ช่องที่ Idle_State ยิงตอนออกจากการต่อสู้\nปกติคือ GameStateIdle_VoidEC")]
    [SerializeField] private VoidEventChannelSO _onEnterIdle;

    [Header("โชว์ตอนไหนอีก (นอกการต่อสู้)")]
    [Tooltip("เลือดลด / เพิ่มแล้วโชว์ค้างกี่วินาที — 0 = ไม่โชว์")]
    [SerializeField, Min(0f)] private float _showAfterHealthChange = 3f;

    [Header("จาง")]
    [Tooltip("ออกจากการต่อสู้แล้วรอกี่วินาทีค่อยจางหาย")]
    [SerializeField, Min(0f)] private float _hideDelay = 2f;
    [SerializeField, Min(0.1f)] private float _fadeSpeed = 3f;

    [Header("Debug")]
    [Tooltip("ขึ้น log ตอนเข้า / ออกจากการต่อสู้ — ใช้เช็คว่าต่อช่องถูก")]
    [SerializeField] private bool _logStateChange = false;

    private CanvasGroup _group;
    private Health _health;
    private SkillPoints _skillPoints;
    private BlockSystem _blockSystem;
    private float _retryTimer;
    private bool _inBattle;
    private float _showUntil = -999f;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _group.alpha = 0f;
    }

    private void OnEnable()
    {
        if (_onEnterBattle != null) _onEnterBattle.Raised += HandleEnterBattle;
        if (_onEnterIdle != null) _onEnterIdle.Raised += HandleEnterIdle;
        SceneManager.sceneLoaded += HandleSceneLoaded;

#if UNITY_EDITOR
        if (_onEnterBattle == null || _onEnterIdle == null)
            Debug.LogWarning("[CombatHUD] ยังใส่ Event Channel ไม่ครบ 2 ช่อง — จะโชว์แค่ตอนเลือดเปลี่ยน", this);
#endif
    }

    // ปิด Domain Reload ไว้ — event ใน ScriptableObject ไม่ถูกล้างตอนกด Stop ต้องถอดเองทุกครั้ง
    private void OnDisable()
    {
        if (_onEnterBattle != null) _onEnterBattle.Raised -= HandleEnterBattle;
        if (_onEnterIdle != null) _onEnterIdle.Raised -= HandleEnterIdle;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SetHealth(null);
    }

    private void Update()
    {
        FindPlayerStats();

        float target = ShouldShow() ? 1f : 0f;
        _group.alpha = Mathf.MoveTowards(_group.alpha, target, _fadeSpeed * Time.unscaledDeltaTime);
    }

    private bool ShouldShow()
    {
        if (_inBattle || Time.time < _showUntil)
            return true;

        if (_health != null && _health.MaxHealth > 0f &&
            _health.CurrentHP < _health.MaxHealth)
            return true;

        if (_skillPoints != null && _skillPoints.CurrentSkillPoints > 0f)
            return true;

        if (_blockSystem != null && _blockSystem.MaxGuardPoints > 0f &&
            _blockSystem.CurrentGuardPoints < _blockSystem.MaxGuardPoints)
            return true;

        return false;
    }

    private void HandleEnterBattle()
    {
        _inBattle = true;
        if (_logStateChange)
            Debug.Log("[CombatHUD] เข้าการต่อสู้ → โชว์ UI", this);
    }

    private void HandleEnterIdle()
    {
        _inBattle = false;
        ShowFor(_hideDelay);
        if (_logStateChange)
            Debug.Log($"[CombatHUD] ออกจากการต่อสู้ → จางหายใน {_hideDelay} วิ", this);
    }

    private void HandleHealthChanged(float current) => ShowFor(_showAfterHealthChange);

    private void ShowFor(float seconds)
    {
        if (seconds > 0f)
            _showUntil = Mathf.Max(_showUntil, Time.time + seconds);
    }

    // UIRoot อยู่ข้ามซีน — ฉากใหม่เริ่มนอกการต่อสู้เสมอ และผู้เล่นเป็นตัวใหม่
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _inBattle = false;
        _showUntil = -999f;
        SetHealth(null);
        _skillPoints = null;
        _blockSystem = null;
    }

    private void FindPlayerStats()
    {
        if (_health != null && _skillPoints != null && _blockSystem != null)
            return;

        _retryTimer += Time.unscaledDeltaTime;
        if (_retryTimer < 0.5f)
            return;
        _retryTimer = 0f;

        if (_health == null)
            SetHealth(PlayerLookup.Find<Health>());
        if (_skillPoints == null)
            _skillPoints = PlayerLookup.Find<SkillPoints>();
        if (_blockSystem == null)
            _blockSystem = PlayerLookup.Find<BlockSystem>();
    }

    private void SetHealth(Health health)
    {
        if (_health != null)
            _health.OnHealthChanged -= HandleHealthChanged;

        _health = health;

        if (_health != null)
            _health.OnHealthChanged += HandleHealthChanged;
    }
}
