using UnityEngine;

/// <summary>
/// ฟัง event จาก Health แล้วสั่ง feedback ตอนโดนตี
///
/// ไม่แตะไฟล์ Health เลย — subscribe ผ่าน VoidEventChannelSO ที่มีอยู่แล้ว
/// แปะบน GameObject เดียวกับ Health (Rin/Player)
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerHurtReaction : MonoBehaviour
{
    [Header("Event Channels")]
    [Tooltip("ลาก channel ตัวเดียวกับที่ใส่ในช่อง On Hurt ของ Health\nถ้าเว้นว่าง จะดึงจาก Health ให้เอง")]
    [SerializeField] private VoidEventChannelSO _onHurt;

    [Tooltip("ลาก channel ตัวเดียวกับ On Dead ของ Health — ใช้หยุดสั่นตอนตาย")]
    [SerializeField] private VoidEventChannelSO _onDead;

    [Header("Shake")]
    [Tooltip("ใช้สัดส่วนดาเมจคิดความแรง (โดนหนัก = สั่นแรง)")]
    [SerializeField] private bool _scaleByDamage = true;

    [Tooltip("ความแรงคงที่ ใช้เมื่อปิด Scale By Damage")]
    [Range(0f, 1f)][SerializeField] private float _fixedStrength = 0.5f;

    private Health _health;
    private float _lastHP;

    private void Awake()
    {
        _health = GetComponent<Health>();

        // ไม่ได้ลากใส่ก็ดึงจาก Health เอง จะได้ไม่มีทางใส่ผิดตัว
        if (_onHurt == null) _onHurt = _health.OnHurt;
        if (_onDead == null) _onDead = _health.OnDead;
    }

    private void OnEnable()
    {
        _lastHP = _health.CurrentHP;

        if (_onHurt != null) _onHurt.Raised += HandleHurt;
        if (_onDead != null) _onDead.Raised += HandleDead;

        if (_onHurt == null)
            Debug.LogWarning("[PlayerHurtReaction] ไม่มี OnHurt channel — จอจะไม่สั่นตอนโดนตี", this);
    }

    private void OnDisable()
    {
        // ต้องถอนเสมอ ไม่งั้น ScriptableObject จะถือ reference ไปยัง object ที่ตายแล้ว
        // แล้วพอ scene ใหม่โหลด event จะยิงหา object เก่าที่ไม่มีอยู่จริง
        if (_onHurt != null) _onHurt.Raised -= HandleHurt;
        if (_onDead != null) _onDead.Raised -= HandleDead;
    }

    private void HandleHurt()
    {
        float strength = _fixedStrength;

        if (_scaleByDamage && _health.MaxHealth > 0f)
        {
            // Health ไม่ได้ส่งค่าดาเมจมากับ event เลยคิดเอาจากส่วนต่าง HP
            float damage = Mathf.Max(0f, _lastHP - _health.CurrentHP);
            strength = Mathf.Clamp01(damage / _health.MaxHealth);
        }

        _lastHP = _health.CurrentHP;

        if (HurtFeedback.Instance != null)
            HurtFeedback.Instance.Play(strength);
        else
            CameraShaker.Instance?.AddTrauma(Mathf.Lerp(0.25f, 0.75f, strength));
    }

    [Header("Death")]
    [Tooltip("หน่วงก่อนหยุดสั่นตอนตาย ให้จังหวะโดนตีครั้งสุดท้ายเล่นจบก่อน")]
    [SerializeField] private float _stopShakeDelay = 0.8f;

    private void HandleDead()
    {
        StartCoroutine(StopShakeAfterDelay());
    }

    private System.Collections.IEnumerator StopShakeAfterDelay()
    {
        // ใช้ Realtime เพราะหน้า Game Over ตั้ง timeScale = 0
        yield return new WaitForSecondsRealtime(_stopShakeDelay);
        CameraShaker.Instance?.StopImmediate();
    }
}
