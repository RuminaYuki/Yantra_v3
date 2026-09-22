using UnityEngine;

public class AttackSoundPlayer : MonoBehaviour
{
    [Header("แหล่งเหตุการณ์")]
    [Tooltip("เว้นว่างได้ จะหาให้เองจาก GameObject นี้ ลูก และพ่อแม่")]
    [SerializeField] private AttackSphereCast _attack;

    [Header("เสียง")]
    [Tooltip("เสียงตอนหมัด/อาวุธกระทบเป้า\n" +
        "ควรเปิด Use Random Clips ใน SoundData แล้วใส่หลายไฟล์\n" +
        "เสียงกระแทกซ้ำเป๊ะๆ ทุกหมัดจะรู้สึกเป็นเครื่องจักรทันที")]
    [SerializeField] private SoundID _impactSound;

    [Header("Options")]
    [Tooltip("เล่นที่ตำแหน่งกำปั้น (Attack Origin) แทนตำแหน่งตัวละคร\n" +
        "ปิดถ้า Attack Origin ว่างหรืออยู่ผิดที่")]
    [SerializeField] private bool _playAtAttackOrigin = true;

    [Tooltip("เว้นอย่างน้อยกี่วินาทีระหว่างเสียงกระแทก\n" +
        "หมัดเดียวอาจโดนหลายเป้าพร้อมกัน ถ้าไม่กันจะได้ยินเสียงซ้อนกันเป็นพรืด")]
    [SerializeField] private float _minInterval = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool _logPlays = false;

    private float _lastPlayTime = -999f;

    private void Awake()
    {
        if (_attack == null) _attack = ResolveAttack();

        if (_attack == null)
        {
            Debug.LogError(
                "[AttackSound] หา AttackSphereCast ไม่เจอ — " +
                "ลากใส่ช่อง Attack เองหรือย้ายสคริปต์นี้ไปอยู่กับมัน", this);
            enabled = false;
        }
    }

    private AttackSphereCast ResolveAttack()
    {
        var found = GetComponent<AttackSphereCast>();
        if (found != null) return found;

        found = GetComponentInChildren<AttackSphereCast>(true);
        if (found != null) return found;

        return GetComponentInParent<AttackSphereCast>(true);
    }

    private void OnEnable()
    {
        if (_attack != null) _attack.OnHit += HandleHit;
        _lastPlayTime = -999f;
    }

    private void OnDisable()
    {
        if (_attack != null) _attack.OnHit -= HandleHit;
    }

    private void HandleHit()
    {
        if (Time.time - _lastPlayTime < _minInterval) return;
        _lastPlayTime = Time.time;

        Vector3 position = GetImpactPosition();

        if (_logPlays)
            Debug.Log($"[AttackSound] โดน! — {(_impactSound != null ? _impactSound.name : "ยังไม่ได้ใส่เสียง")}", this);

        if (_impactSound == null || SoundManager.Instance == null) return;

        SoundManager.Instance.PlaySFX(_impactSound, position);
    }

    private Vector3 GetImpactPosition()
    {
        if (!_playAtAttackOrigin) return transform.position;
        if (_attack == null || _attack.AttackOrigin == null) return transform.position;

        return _attack.AttackOrigin.position;
    }
}