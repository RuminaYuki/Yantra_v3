using UnityEngine;

// ต่อยโดนผี → ค้างแวบเดียว + จอสั่น ให้หมัดรู้สึกหนัก
// ฟัง event เดียวกับ AttackSoundPlayer / AttackVFXPlayer แปะไว้ตัวเดียวกันได้เลย
public class AttackHitFeedback : MonoBehaviour
{
    [Header("แหล่งเหตุการณ์")]
    [Tooltip("เว้นว่างได้ จะหาให้เองจาก GameObject นี้ ลูก และพ่อแม่")]
    [SerializeField] private AttackSphereCast _attack;

    [Header("Hit Stop")]
    [Tooltip("ค้างกี่วินาที — 0.05 ≈ 3 เฟรม / 0 = ปิด")]
    [SerializeField] private float _hitStopDuration = 0.05f;

    [Header("Screen Shake")]
    [Tooltip("รัศมีที่กล้องกระตุกออกไป (เมตร) — FPS ใช้น้อยๆ ก็รู้สึกแล้ว")]
    [SerializeField] private float _shakeRadius = 0.06f;

    [Tooltip("สั่นนานกี่วินาที (นับรวมช่วงที่เกมค้าง)")]
    [SerializeField] private float _shakeDuration = 0.18f;

    [Tooltip("เอียงกล้องสูงสุดกี่องศา — ให้ความรู้สึกแรงกว่าการขยับ\n0 = ขยับอย่างเดียวตาม spec")]
    [SerializeField] private float _shakeTilt = 1.5f;

    [Header("Options")]
    [Tooltip("กันยิงซ้ำถี่เกิน")]
    [SerializeField] private float _minInterval = 0.05f;

    [SerializeField] private bool _logPlays = false;

    private float _lastPlayTime = -999f;

    private void Awake()
    {
        if (_attack == null) _attack = ResolveAttack();

        if (_attack == null)
        {
            Debug.LogError("[HitFeedback] หา AttackSphereCast ไม่เจอ — ลากใส่ช่อง Attack เอง", this);
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

    // ปิด Domain Reload ไว้ ต้องถอดเสมอ ไม่งั้น Play รอบสองจะค้าง 2 รอบซ้อน
    private void OnDisable()
    {
        if (_attack != null) _attack.OnHit -= HandleHit;
    }

    private void HandleHit()
    {
        // ใช้เวลาจริง เพราะตอนค้าง Time.time ไม่เดิน
        if (Time.unscaledTime - _lastPlayTime < _minInterval) return;
        _lastPlayTime = Time.unscaledTime;

        if (_logPlays)
        {
            Debug.Log(
                $"[HitFeedback] โดน! — ค้าง {_hitStopDuration}s | " +
                $"สั่น r={_shakeRadius} {_shakeDuration}s | " +
                $"Shaker: {(CameraShaker.Instance != null ? "✓" : "✗ ไม่มีในซีน")}", this);
        }

        HitStop.Freeze(_hitStopDuration);
        CameraShaker.Instance?.Kick(_shakeRadius, _shakeDuration, _shakeTilt);
    }
}