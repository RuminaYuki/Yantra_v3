using UnityEngine;

public class ParrySuccessDetector : MonoBehaviour
{
    [Header("ฟังอะไร")]
    [Tooltip("ช่องเดียวกับ Void Hit Event Channel ใน BlockSystem\n" +
        "(PlayerOnHit_VoidEventChannel)")]
    [SerializeField] private VoidEventChannelSO _onPlayerHit;

    [Tooltip("ช่องเดียวกับ On Current Guard Point Change ใน BlockSystem\n" +
        "ใช้เฉพาะตอนหา BlockSystem ไม่เจอ\n" +
        "(PlayerOnCurrentGuardPointChange_Fl)")]
    [SerializeField] private FloatEventChannelSO _onGuardPointChange;

    [Header("แหล่งข้อมูล (เว้นว่างได้ จะหาให้เอง)")]
    [Tooltip("ตัวหลักที่ใช้ตัดสิน ดูว่าตอนโดนตีผู้เล่นกำลังกด parry อยู่จริงไหม")]
    [SerializeField] private BlockSystem _blockSystem;

    [Tooltip("ใช้ดูว่าเลือดลดมั้ย")]
    [SerializeField] private Health _health;

    [Tooltip("ใช้โชว์ใน log เฉยๆ")]
    [SerializeField] private SkillPoints _skillPoints;

    [SerializeField] private string _playerTag = "Player";

    [Header("ยิงช่องไหนออกไป")]
    [Tooltip("สร้าง VoidEventChannelSO ใหม่ขึ้นมาใส่ (PlayerOnParrySuccess_VEC)\n" +
        "แล้วให้ PlaySoundOnEventChannel / PlayVFXOnEventChannel มาฟังช่องนี้")]
    [SerializeField] private VoidEventChannelSO _onParrySuccess;

    [Header("Debug")]
    [Tooltip("บอกทุกครั้งที่โดนตี ว่าตัดสินว่าเป็นอะไรและเพราะอะไร")]
    [SerializeField] private bool _logDetection = false;

    private float _healthPrevFrame;
    private float _skillPointsPrevFrame;
    private bool _hitThisFrame;
    private bool _parryActiveAtHit;
    private bool _guardChangedThisFrame;
    private float _retryTimer;

    private void OnEnable()
    {
        if (_onPlayerHit != null) _onPlayerHit.Raised += HandlePlayerHit;
        if (_onGuardPointChange != null) _onGuardPointChange.Raised += HandleGuardChange;

        _hitThisFrame = false;
        _parryActiveAtHit = false;
        _guardChangedThisFrame = false;

        _retryTimer = 999f;
        ResolveSources();
        _healthPrevFrame = ReadHealth();
        _skillPointsPrevFrame = ReadSkillPoints();

#if UNITY_EDITOR
        if (_onPlayerHit == null)
            Debug.LogWarning("[Parry] ยังไม่ได้ใส่ช่อง On Player Hit — จะตรวจจับอะไรไม่ได้เลย", this);

        if (_onParrySuccess == null)
            Debug.LogWarning("[Parry] ยังไม่ได้ใส่ช่อง On Parry Success — ตรวจเจอแล้วก็ไม่มีใครรู้", this);
#endif
    }

    // โปรเจกต์ปิด Domain Reload ไว้ ต้องถอดเสมอ ไม่งั้นกด Play รอบสองจะยิงซ้อน
    private void OnDisable()
    {
        if (_onPlayerHit != null) _onPlayerHit.Raised -= HandlePlayerHit;
        if (_onGuardPointChange != null) _onGuardPointChange.Raised -= HandleGuardChange;
    }

    private void HandlePlayerHit()
    {
        _hitThisFrame = true;
        if (_blockSystem != null && _blockSystem.IsParryEnable)
            _parryActiveAtHit = true;
    }

    private void HandleGuardChange(float value) => _guardChangedThisFrame = true;

    // ตัดสินใน LateUpdate ตอนทุกคนในเฟรมนี้ทำงานเสร็จแล้ว
    private void LateUpdate()
    {
        ResolveSources();

        if (_hitThisFrame) Evaluate();

        _hitThisFrame = false;
        _parryActiveAtHit = false;
        _guardChangedThisFrame = false;
        _healthPrevFrame = ReadHealth();
        _skillPointsPrevFrame = ReadSkillPoints();
    }

    private void Evaluate()
    {
        float healthLost = _healthPrevFrame - ReadHealth();
        float skillGained = ReadSkillPoints() - _skillPointsPrevFrame;
        bool tookDamage = healthLost > 0.001f;

        bool parried;
        string verdict;

        if (_blockSystem != null)
        {
            // ถามตรงๆ ว่ากด parry อยู่ไหม ช่วง Glory ไม่ได้กด parry เลยไม่นับ
            parried = _parryActiveAtHit && !tookDamage;
            if (parried) verdict = "PARRY!";
            else if (tookDamage) verdict = "โดนเต็มๆ";
            else verdict = "ไม่โดนดาเมจ แต่ไม่ได้กด parry (เช่นบล็อก / Glory) — ไม่นับ";
        }
        else
        {
            // หา BlockSystem ไม่เจอ ใช้วิธีเดาแบบเดิม
            parried = !_guardChangedThisFrame && !tookDamage;
            verdict = _guardChangedThisFrame ? "บล็อกธรรมดา" : (tookDamage ? "โดนเต็มๆ" : "PARRY! (เดา ไม่เจอ BlockSystem)");
        }

        if (_logDetection)
        {
            Debug.Log(
                $"[Parry] โดนตี — {verdict} " +
                $"(กด parry: {_parryActiveAtHit}, เลือดลด: {healthLost:0.##}, SP +{skillGained:0.##})",
                this);
        }

        if (!parried) return;
        if (_onParrySuccess == null) return;

        _onParrySuccess.Raise();
    }

    private float ReadHealth() => _health != null ? _health.CurrentHP : 0f;

    private float ReadSkillPoints() => _skillPoints != null ? _skillPoints.CurrentSkillPoints : 0f;

    // ลองหาใหม่ทุกครึ่งวินาที เผื่อผู้เล่นถูก spawn ทีหลัง
    private void ResolveSources()
    {
        if (_blockSystem != null && _health != null && _skillPoints != null) return;

        _retryTimer += Time.deltaTime;
        if (_retryTimer < 0.5f) return;
        _retryTimer = 0f;

        if (_blockSystem == null) _blockSystem = GetComponentInParent<BlockSystem>(true);
        if (_health == null) _health = GetComponentInParent<Health>(true);
        if (_skillPoints == null) _skillPoints = GetComponentInParent<SkillPoints>(true);

        if (_blockSystem != null && _health != null && _skillPoints != null) return;

        var playerObj = GameObject.FindGameObjectWithTag(_playerTag);
        if (playerObj == null) return;

        if (_blockSystem == null) _blockSystem = playerObj.GetComponentInChildren<BlockSystem>(true);
        if (_health == null) _health = playerObj.GetComponentInChildren<Health>(true);
        if (_skillPoints == null) _skillPoints = playerObj.GetComponentInChildren<SkillPoints>(true);
    }
}