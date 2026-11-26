using UnityEngine;

/// <summary>
/// ตัวรับ Animation Event ของเปรต — แปะบน GameObject ที่มี Animator ของเปรต
///
/// ใส่ event ในคลิปท่า (ช่อง Function พิมพ์ตามนี้เป๊ะ ๆ):
///   PretFootstep  — เฟรมที่เท้าแตะพื้น (ท่าเดิน / วิ่ง)   → จอสั่นเบา + เสียงเท้า
///   PretSlam      — เฟรมที่มือกระแทกพื้น (ท่า Attack)     → จอสั่นแรง + ฝุ่น + เสียงตบ
///
/// ใช้คู่กับติ๊ก "Use Animation Event" ใน PretFootstepShake / PretSlamShake / HandSlamDust
/// และเอาติ๊ก Auto Steps ใน GhostAudio ออก
/// ตัวไหนไม่มีก็ข้ามไป (เช่น PPret(Boss) ไม่มี PretFootstepShake = เสียงเท้าอย่างเดียว ไม่สั่นจอ)
/// </summary>
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class PretAnimEvents : MonoBehaviour
{
    [Header("ตัวที่สั่ง (ว่าง = หาในตัวนี้ / ตัวแม่ให้เอง)")]
    [SerializeField] private PretFootstepShake _footstep;
    [SerializeField] private PretSlamShake _slamShake;
    [SerializeField] private HandSlamDust _slamDust;

    [Tooltip("ใช้เล่นเสียงเท้าตรง ๆ เมื่อตัวนี้ไม่มี PretFootstepShake\n(ถ้ามี PretFootstepShake เสียงจะไปทาง On Step ของมันแทน)")]
    [SerializeField] private GhostAudio _audio;

    [Header("กันเบิ้ล")]
    [Tooltip("ท่าใน Blend Tree จะยิง event ซ้อนกันหลายคลิป — event ชนิดเดียวกันที่มาถี่กว่านี้ (วินาที) ไม่นับ")]
    [SerializeField, Min(0f)] private float _minGap = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool _logEvents;

    private float _lastStep = -999f;
    private float _lastSlam = -999f;

    private void Awake()
    {
        if (_footstep == null) _footstep = GetComponentInParent<PretFootstepShake>();
        if (_slamShake == null) _slamShake = GetComponentInParent<PretSlamShake>();
        if (_slamDust == null) _slamDust = GetComponentInParent<HandSlamDust>();
        if (_audio == null) _audio = GetComponentInParent<GhostAudio>();

        if (_footstep == null && _slamShake == null && _slamDust == null && _audio == null)
            Debug.LogWarning($"[PretAnimEvents] {name} หา PretFootstepShake / PretSlamShake / HandSlamDust ไม่เจอเลย — ลากใส่เอง", this);
    }

    // Animation Event: PretFootstep
    public void PretFootstep()
    {
        if (Time.time - _lastStep < _minGap) return;
        _lastStep = Time.time;

        if (_logEvents) Debug.Log($"[PretAnimEvents] PretFootstep", this);
        if (_footstep != null) _footstep.StepFromEvent();   // จอสั่น + On Step (เสียงเท้า)
        else if (_audio != null) _audio.Footstep();        // ไม่มีตัวสั่นจอ เล่นเสียงตรง ๆ
    }

    // Animation Event: PretSlam
    public void PretSlam()
    {
        if (Time.time - _lastSlam < _minGap) return;
        _lastSlam = Time.time;

        if (_logEvents) Debug.Log($"[PretAnimEvents] PretSlam", this);
        if (_slamShake != null) _slamShake.SlamFromEvent();
        if (_slamDust != null) _slamDust.SlamFromEvent();
    }
}
