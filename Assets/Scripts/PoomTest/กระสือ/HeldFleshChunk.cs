using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ก้อนเนื้อในมือกระสือ — ช่วงที่มือดึงออกจากคอจนถึงตอนขว้าง
///
/// 1. ท่าโจมตีเล่นถึง Show At → ก้อนเนื้อโผล่ในมือ (ที่ Hand) พร้อมเส้นยืดจากตอคอ
/// 2. ก้อนจริง (projectile) เกิดแล้วมายึดตอคอเดียวกัน → ก้อนในมือหายในเฟรมเดียวกัน ดูเป็นก้อนเดียวที่ถูกขว้างออกไป
/// 3. ออกจากท่ากลางทาง / เลย Hide At แล้วก้อนจริงยังไม่เกิด → หายเอง ไม่ค้างในมือ
///
/// เลือด (ไม่ใส่ก็ได้): ควัก = พุ่งจากตอคอตอนท่าถึง Pull Blood At / ขว้าง = สาดจากมือตอนก้อนจริงหลุดมือ
/// เลือดตอนก้อนตกพื้น + กองเลือด อยู่ที่ FleshImpactBlood บนตัวก้อนเนื้อ
///
/// ไม่ต้องแก้ไฟล์อนิเมชั่น / Animator / state machine — อ่านเองว่าตอนนี้คลิปไหนเล่นอยู่ถึงไหน
/// ก้อนในมือสร้างจาก prefab ก้อนเนื้อตัวเดียวกับที่ขว้าง (ปิดการบิน / การชนให้) หน้าตาและเส้นจะเหมือนกันเป๊ะ
///
/// แปะบนตัวที่มี Animator ของกระสือ (EnemeyController)
/// </summary>
[DisallowMultipleComponent]
public class HeldFleshChunk : MonoBehaviour
{
    [Header("ท่า")]
    [Tooltip("เว้นว่าง = หาเองจากตัวนี้ / ลูก")]
    [SerializeField] private Animator _animator;

    [Tooltip("ชื่อคลิปท่าโจมตี (ชื่อคลิป ไม่ใช่ชื่อ state)")]
    [SerializeField] private string _clipName = "Krasue_Attack";

    [SerializeField, Min(0)] private int _layer = 0;

    [Tooltip("ก้อนโผล่ในมือเมื่อท่าเล่นถึงตรงนี้ (0–1)\nKrasue_Attack: เฟรม 62 จาก 194 = 0.32 ตอนมือเริ่มดึงออกจากคอ")]
    [SerializeField, Range(0f, 1f)] private float _showAt = 0.32f;

    [Tooltip("ถ้าเลยตรงนี้แล้วก้อนจริงยังไม่ถูกขว้าง ให้ก้อนในมือหายเอง (กันค้าง)")]
    [SerializeField, Range(0f, 1f)] private float _hideAt = 0.8f;

    [Header("ก้อนเนื้อ")]
    [Tooltip("prefab ก้อนเนื้อตัวเดียวกับที่ขว้าง (KrasueProjectileBullet)")]
    [SerializeField] private GameObject _chunkPrefab;

    [Tooltip("จุดที่ถือ — AttackOrigin ที่มือ")]
    [SerializeField] private Transform _hand;

    [Tooltip("หันหางก้อนเข้าหาตอคอตลอด — ดูเป็นเนื้อยาว ๆ ที่ถูกดึงออกมาจากคอ เส้นงอกจากหางก้อนพอดี\n" +
             "ปิด = ก้อนหันตาม Hand + Rotation อย่างเดียว")]
    [SerializeField] private bool _tailToNeck = true;

    [Tooltip("เลื่อน / หมุนก้อนในมือ ถ้าดูไม่พอดีมือ")]
    [SerializeField] private Vector3 _offset;
    [SerializeField] private Vector3 _rotation;

    [Tooltip("ตอคอ (NeckStump) — รู้ว่าก้อนจริงออกไปแล้วตอนมีก้อนมายึดจุดนี้")]
    [SerializeField] private FleshStrandAnchor _neck;

    [Header("เลือด (ไม่ใส่ก็ได้)")]
    [Tooltip("เลือดพุ่งออกจากตอคอตอนควัก — เช่น Vfx_blood_Bust Variant")]
    [SerializeField] private GameObject _pullBlood;

    [Tooltip("ควักตอนท่าเล่นถึงตรงนี้ (0–1) — ค่าเริ่มเท่ากับตอนก้อนโผล่ในมือ")]
    [SerializeField, Range(0f, 1f)] private float _pullBloodAt = 0.32f;

    [Tooltip("เลือดสาดจากมือตอนขว้าง (ตอนก้อนจริงหลุดมือ) — เช่น Vfx_blood_Bust Variant")]
    [SerializeField] private GameObject _throwBlood;

    [Tooltip("เลือดพุ่งไปข้างหน้ากระสือ (ทางผู้เล่น) แล้วเงยขึ้นกี่องศา\n" +
             "0 = พุ่งตรงไปข้างหน้า / 90 = พุ่งขึ้นฟ้าเป็นเสาแนวตั้ง")]
    [SerializeField, Range(-30f, 90f)] private float _bloodUpAngle = 25f;

    [Tooltip("หมุนเพิ่มหลังเล็งแล้ว — ปกติไม่ต้องแตะ (VFX พุ่งตามแกน Z ลูกศรสีฟ้า)")]
    [SerializeField] private Vector3 _bloodRotation;

    [Tooltip("ลบ VFX ทิ้งหลังกี่วินาที")]
    [SerializeField, Min(0.1f)] private float _bloodLifetime = 3f;

    [Header("Debug")]
    [SerializeField] private bool _logSteps = false;

    private readonly List<AnimatorClipInfo> _clips = new();
    private GameObject _held;
    private bool _thrown;   // ก้อนจริงออกไปแล้วในท่ารอบนี้
    private bool _pullBled; // เลือดตอนควักออกไปแล้วในท่ารอบนี้

    private void Awake()
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator == null) Debug.LogWarning($"[HeldFleshChunk] {name} ไม่เจอ Animator", this);
        if (_neck == null) Debug.LogWarning($"[HeldFleshChunk] {name} ยังไม่ได้ใส่ Neck (NeckStump)", this);

        CreateHeldChunk();
    }

    private void OnEnable()
    {
        if (_neck != null) _neck.Claimed += OnNeckClaimed;
        _thrown = false;
        SetHeld(false);
    }

    private void OnDisable()
    {
        if (_neck != null) _neck.Claimed -= OnNeckClaimed;
        SetHeld(false);
    }

    private void OnDestroy()
    {
        if (_held != null) Destroy(_held);
    }

    // LateUpdate — Animator อัปเดตท่าของเฟรมนี้เสร็จแล้ว
    private void LateUpdate()
    {
        if (_held == null) return;

        if (!TryGetAttackTime(out float t))
        {
            _thrown = false;
            _pullBled = false;
            SetHeld(false);
            return;
        }

        // ยังไม่ถึงจังหวะดึง = ท่ารอบใหม่ (หรือยังไม่เริ่ม) — พร้อมโชว์อีกรอบ
        if (t < _showAt) _thrown = false;

        if (t < _pullBloodAt) _pullBled = false;
        else if (!_pullBled)
        {
            _pullBled = true;
            PullBlood();
        }

        SetHeld(!_thrown && t >= _showAt && t < _hideAt);
        if (_held.activeSelf) AimTailToNeck();
    }

    // หันหาง (-Z ของก้อน) เข้าตอคอ — แกนยาวของก้อนคือ Z (ก้อนที่บินก็หันหัวตามทิศบินแบบเดียวกัน)
    private void AimTailToNeck()
    {
        if (!_tailToNeck || _neck == null) return;

        Vector3 away = _hand.position - _neck.transform.position;
        if (away.sqrMagnitude < 0.05f * 0.05f) return;   // มือยังจมอยู่ในคอ ทิศยังไม่นิ่ง — ใช้ทิศเดิมไปก่อน

        Vector3 dir = away.normalized;
        // ด้านบนของก้อนหันไปทางหน้ากระสือ — ถ้าดึงตรงไปข้างหน้าพอดีค่อยใช้ฟ้าแทน
        Vector3 up = Mathf.Abs(Vector3.Dot(dir, transform.forward)) < 0.95f ? transform.forward : Vector3.up;
        _held.transform.rotation = Quaternion.LookRotation(dir, up) * Quaternion.Euler(_rotation);
    }

    // ---------- ก้อนในมือ ----------

    private void CreateHeldChunk()
    {
        if (_chunkPrefab == null || _hand == null)
        {
            Debug.LogWarning($"[HeldFleshChunk] {name} ยังไม่ได้ใส่ Chunk Prefab หรือ Hand", this);
            return;
        }

        _held = Instantiate(_chunkPrefab, _hand);
        _held.name = _chunkPrefab.name + " (Held)";

        // เป็นของโชว์ในมือ — ห้ามบิน ห้ามชน ห้ามหมดเวลาแล้วคืน pool
        foreach (var bullet in _held.GetComponentsInChildren<ProjectileBullet>(true))
            bullet.enabled = false;
        foreach (var col in _held.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
        foreach (var rb in _held.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
            // prefab เปิด Interpolate ไว้ให้ก้อนที่บินลื่น — แต่ก้อนที่เป็นลูกของกระดูกมือ
            // ถ้ายังเปิดอยู่ ฟิสิกส์จะเขียนตำแหน่งทับทุกเฟรม ก้อนจะไม่ติดมือ
            rb.interpolation = RigidbodyInterpolation.None;
        }

        // ขนาดในโลกให้เท่ากับก้อนที่ขว้าง ไม่ว่ากระดูกมือจะถูก scale เท่าไหร่
        Transform t = _held.transform;
        t.localPosition = _offset;
        t.localRotation = Quaternion.Euler(_rotation);
        Vector3 want = _chunkPrefab.transform.localScale;
        Vector3 parent = _hand.lossyScale;
        t.localScale = new Vector3(
            want.x / NonZero(parent.x),
            want.y / NonZero(parent.y),
            want.z / NonZero(parent.z));

        _held.SetActive(false);
    }

    private static float NonZero(float v) => Mathf.Abs(v) < 1e-5f ? 1f : v;

    private void SetHeld(bool show)
    {
        if (_held == null || _held.activeSelf == show) return;

        _held.SetActive(show);
        if (_logSteps) Debug.Log($"[HeldFleshChunk] {(show ? "โชว์ก้อนในมือ" : "ซ่อนก้อนในมือ")}", this);
    }

    private void OnNeckClaimed(FleshStrands by)
    {
        // เส้นของก้อนในมือเองก็มายึดคอ — ไม่นับ
        if (_held != null && by.transform.IsChildOf(_held.transform)) return;

        _thrown = true;
        SetHeld(false);
        ThrowBlood();
        if (_logSteps) Debug.Log("[HeldFleshChunk] ก้อนจริงออกไปแล้ว", this);
    }

    // ---------- เลือด ----------

    // ควัก: พุ่งจากตอคอไปข้างหน้ากระสือ เงยนิดหน่อย
    private void PullBlood()
    {
        if (_pullBlood == null) return;

        Vector3 from = _neck != null ? _neck.transform.position : transform.position;
        BloodFx.Spawn(_pullBlood, from, BloodDirection(), _bloodRotation, _bloodLifetime);
        if (_logSteps) Debug.Log("[HeldFleshChunk] เลือดตอนควัก", this);
    }

    // ขว้าง: สาดจากมือไปข้างหน้ากระสือ (ทางที่ขว้างไป)
    private void ThrowBlood()
    {
        if (_throwBlood == null || _hand == null) return;

        BloodFx.Spawn(_throwBlood, _hand.position, BloodDirection(), _bloodRotation, _bloodLifetime);
        if (_logSteps) Debug.Log("[HeldFleshChunk] เลือดตอนขว้าง", this);
    }

    private Vector3 BloodDirection() => BloodFx.Tilt(transform.forward, Vector3.up, _bloodUpAngle);

    // ---------- อ่านท่า ----------

    // เวลาของคลิปโจมตี 0–1 — เช็คทั้ง state ปัจจุบัน และ state ที่กำลังเปลี่ยนเข้าไป
    private bool TryGetAttackTime(out float time)
    {
        time = 0f;
        if (_animator == null || !_animator.isActiveAndEnabled || _layer >= _animator.layerCount)
            return false;

        _animator.GetCurrentAnimatorClipInfo(_layer, _clips);
        if (HasAttackClip())
        {
            time = ToTime(_animator.GetCurrentAnimatorStateInfo(_layer));
            return true;
        }

        if (_animator.IsInTransition(_layer))
        {
            _animator.GetNextAnimatorClipInfo(_layer, _clips);
            if (HasAttackClip())
            {
                time = ToTime(_animator.GetNextAnimatorStateInfo(_layer));
                return true;
            }
        }

        return false;
    }

    private bool HasAttackClip()
    {
        foreach (var info in _clips)
        {
            if (info.clip != null && info.clip.name == _clipName) return true;
        }
        return false;
    }

    private static float ToTime(AnimatorStateInfo state)
    {
        return state.loop ? Mathf.Repeat(state.normalizedTime, 1f) : Mathf.Clamp01(state.normalizedTime);
    }

    private void OnValidate()
    {
        if (_hideAt < _showAt) _hideAt = _showAt;
    }
}
