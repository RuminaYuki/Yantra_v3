using UnityEngine;

public class ParrySuccessDetector : MonoBehaviour
{
    [Header("ฟังอะไร")]
    [Tooltip("ช่องเดียวกับ Void Hit Event Channel ใน BlockSystem\n" +
        "(PlayerOnHit_VoidEventChannel)")]
    [SerializeField] private VoidEventChannelSO _onPlayerHit;

    [Tooltip("ช่องเดียวกับ On Current Guard Point Change ใน BlockSystem\n" +
        "ใช้เพื่อ 'ตัดออก' ว่าไม่ใช่การบล็อกธรรมดา\n" +
        "(PlayerOnCurrentGuardPointChange_Fl)")]
    [SerializeField] private FloatEventChannelSO _onGuardPointChange;

    [Header("แหล่งข้อมูล (เว้นว่างได้ จะหาให้เอง)")]
    [Tooltip("ใช้ดูว่าเลือดลดมั้ย — ตัวหลักที่ใช้ตัดสิน")]
    [SerializeField] private Health _health;

    [Tooltip("ใช้เป็นตัวช่วยยืนยันเฉยๆ ไม่มีก็ยังทำงานได้")]
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
    private bool _guardChangedThisFrame;
    private float _retryTimer;

    private void OnEnable()
    {
        if (_onPlayerHit != null) _onPlayerHit.Raised += HandlePlayerHit;
        if (_onGuardPointChange != null) _onGuardPointChange.Raised += HandleGuardChange;

        _hitThisFrame = false;
        _guardChangedThisFrame = false;

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

    /// <summary>
    /// ถอดออกเสมอ — โปรเจกต์เราปิด Domain Reload ไว้
    /// event ใน ScriptableObject จึงไม่ถูกล้างตอนกด Stop
    /// </summary>
    private void OnDisable()
    {
        if (_onPlayerHit != null) _onPlayerHit.Raised -= HandlePlayerHit;
        if (_onGuardPointChange != null) _onGuardPointChange.Raised -= HandleGuardChange;
    }

    private void HandlePlayerHit() => _hitThisFrame = true;

    private void HandleGuardChange(float value) => _guardChangedThisFrame = true;

    /// <summary>
    /// ตัดสินใจใน LateUpdate เพราะตอนนั้นทุกคนในเฟรมนี้ทำงานเสร็จหมดแล้ว
    /// ทั้ง BlockSystem ทั้ง Health ร่องรอยจึงครบพอให้ตัดสิน
    /// </summary>
    private void LateUpdate()
    {
        ResolveSources();

        if (_hitThisFrame) Evaluate();

        _hitThisFrame = false;
        _guardChangedThisFrame = false;
        _healthPrevFrame = ReadHealth();
        _skillPointsPrevFrame = ReadSkillPoints();
    }

    private void Evaluate()
    {
        float healthLost = _healthPrevFrame - ReadHealth();
        float skillGained = ReadSkillPoints() - _skillPointsPrevFrame;

        bool tookDamage = healthLost > 0.001f;
        bool parried = !_guardChangedThisFrame && !tookDamage;

        if (_logDetection)
        {
            string verdict = _guardChangedThisFrame
                ? "บล็อกธรรมดา"
                : (tookDamage ? "โดนเต็มๆ" : "PARRY!");

            Debug.Log(
                $"[Parry] โดนตี — {verdict} " +
                $"(เกราะเปลี่ยน: {_guardChangedThisFrame}, เลือดลด: {healthLost:0.##}, SP +{skillGained:0.##})",
                this);
        }

        if (!parried) return;
        if (_onParrySuccess == null) return;

        _onParrySuccess.Raise();
    }

    private float ReadHealth() => _health != null ? _health.CurrentHP : 0f;

    private float ReadSkillPoints() => _skillPoints != null ? _skillPoints.CurrentSkillPoints : 0f;

    /// <summary>
    /// ลองหาใหม่เป็นระยะ เผื่อผู้เล่นถูก spawn ทีหลัง
    /// </summary>
    private void ResolveSources()
    {
        if (_health != null && _skillPoints != null) return;

        _retryTimer += Time.deltaTime;
        if (_retryTimer < 0.5f) return;
        _retryTimer = 0f;

        if (_health == null)
            _health = GetComponent<Health>() ?? GetComponentInParent<Health>(true);

        if (_skillPoints == null)
            _skillPoints = GetComponent<SkillPoints>() ?? GetComponentInParent<SkillPoints>(true);

        if (_health != null && _skillPoints != null) return;

        var playerObj = GameObject.FindGameObjectWithTag(_playerTag);
        if (playerObj == null) return;

        if (_health == null) _health = playerObj.GetComponent<Health>();
        if (_skillPoints == null) _skillPoints = playerObj.GetComponent<SkillPoints>();
    }
}