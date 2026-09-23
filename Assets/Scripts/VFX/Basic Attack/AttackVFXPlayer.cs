using UnityEngine;
using Effekseer;

public class AttackVFXPlayer : MonoBehaviour
{
    public enum ImpactPosition
    {
        /// <summary>จุดบนผิวเป้าที่ใกล้เราที่สุด — ถูกต้องที่สุด ใช้อันนี้</summary>
        ContactPointOnTarget,

        /// <summary>กลางตัวเป้า — ใช้ถ้าเอฟเฟกต์เป็นออร่าห่อตัว ไม่ใช่การกระเด็น</summary>
        TargetCenter,

        /// <summary>จุดเริ่มยิง (อยู่ที่ตัวผู้เล่น) — ของเดิม เก็บไว้เผื่อเทียบ</summary>
        AttackOriginTransform
    }

    public enum VFXRotationMode
    {
        /// <summary>ไม่หมุนเลย ใช้ท่าที่คนทำเอฟเฟกต์ออกแบบมา</summary>
        None,

        /// <summary>หันออกจากเป้ากลับมาหาเรา — เหมาะกับเอฟเฟกต์กระเด็น/สาด</summary>
        AwayFromTarget,

        /// <summary>หันตามทิศที่ต่อย</summary>
        AttackDirection,

        /// <summary>หันตามกล้อง</summary>
        FaceCamera
    }

    [Header("แหล่งเหตุการณ์")]
    [Tooltip("เว้นว่างได้ จะหาให้เองจาก GameObject นี้ ลูก และพ่อแม่")]
    [SerializeField] private AttackSphereCast _attack;

    [Header("เอฟเฟกต์")]
    [Tooltip("Effekseer Effect Asset\nขนาดปรับที่ช่อง Scale ของตัว asset เอง ไม่ใช่ที่นี่")]
    [SerializeField] private EffekseerEffectAsset _impactEffect;

    [Header("ตำแหน่ง")]
    [SerializeField] private ImpactPosition _positionMode = ImpactPosition.ContactPointOnTarget;

    [Tooltip("รัศมีที่ใช้หาเป้ารอบจุดเริ่มยิง\n" +
        "ควรตั้งใกล้เคียงกับ Attack Range ที่ lead ตั้งไว้ (ลองถามเขา)\n" +
        "กว้างไป = อาจไปเจอผีตัวอื่นที่ไม่ได้โดน / แคบไป = หาไม่เจอแล้วตกกลับไปที่ตัวเรา")]
    [SerializeField] private float _searchRadius = 2f;

    [Tooltip("เลเยอร์ของเป้าที่ตีได้ ปล่อย Everything ไว้ก่อนก็ได้\n" +
        "ตัวเราถูกกรองออกอัตโนมัติอยู่แล้ว")]
    [SerializeField] private LayerMask _targetMask = ~0;

    [Tooltip("ขยับเอฟเฟกต์จากจุดที่หาได้ (พิกัดโลก)\n" +
        "Y บวก = ยกขึ้น ใช้ตอนเอฟเฟกต์จมอยู่ในตัวผี")]
    [SerializeField] private Vector3 _worldOffset = Vector3.zero;

    [Header("ทิศทาง")]
    [SerializeField] private VFXRotationMode _rotationMode = VFXRotationMode.None;

    [Tooltip("หมุนเพิ่มจากโหมดข้างบน (องศา)\nปรับทีละ 90 ก่อน: Y=90 / Y=180 / X=90 / X=-90")]
    [SerializeField] private Vector3 _rotationOffset = Vector3.zero;

    [Tooltip("เว้นว่างได้ จะใช้ Camera.main")]
    [SerializeField] private Camera _cameraOverride;

    [Header("Options")]
    [Tooltip("เว้นอย่างน้อยกี่วินาทีระหว่างเอฟเฟกต์ กันซ้อนกันเป็นกอง")]
    [SerializeField] private float _minInterval = 0.05f;

    [Header("Debug")]
    [Tooltip("เปิดแล้วจะบอกว่าหาจุดปะทะเจอมั้ย และเจอที่ตัวไหน")]
    [SerializeField] private bool _logPlays = false;

    [Tooltip("วาดจุดที่เล่นเอฟเฟกต์ไว้ 2 วินาทีใน Scene view — ดูได้ว่าตำแหน่งถูกมั้ย")]
    [SerializeField] private bool _drawDebugPoint = false;

    private float _lastPlayTime = -999f;

    private void Awake()
    {
        if (_attack == null) _attack = ResolveAttack();

        if (_attack == null)
        {
            Debug.LogError(
                "[AttackVFX] หา AttackSphereCast ไม่เจอ — " +
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

    /// <summary>
    /// ถอดออกเสมอ — โปรเจกต์เราปิด Domain Reload ไว้
    /// ถ้าไม่ถอด กด Play รอบสองจะมีตัวสมัครค้างจากรอบแรก เอฟเฟกต์เด้ง 2 ชุดซ้อน
    /// และผีที่ถูกปั๊มกลับเข้าพูลแล้วเอาออกมาใหม่ก็จะสมัครซ้ำเรื่อยๆ
    /// </summary>
    private void OnDisable()
    {
        if (_attack != null) _attack.OnHit -= HandleHit;
    }

    private void HandleHit()
    {
        if (Time.time - _lastPlayTime < _minInterval) return;
        _lastPlayTime = Time.time;

        if (_impactEffect == null)
        {
            if (_logPlays) Debug.Log("[AttackVFX] โดน! — แต่ยังไม่ได้ใส่เอฟเฟกต์", this);
            return;
        }

        Transform origin = GetOriginTransform();
        Vector3 position = ResolvePosition(origin) + _worldOffset;
        Vector3 awayFromTarget = (origin.position - position).normalized;

        if (_drawDebugPoint)
        {
            Debug.DrawLine(position + Vector3.up * 0.3f, position - Vector3.up * 0.3f, Color.magenta, 2f);
            Debug.DrawLine(position + Vector3.right * 0.3f, position - Vector3.right * 0.3f, Color.magenta, 2f);
            Debug.DrawLine(position + Vector3.forward * 0.3f, position - Vector3.forward * 0.3f, Color.magenta, 2f);
        }

        var handle = EffekseerSystem.PlayEffect(_impactEffect, position);

        handle.SetRotation(BuildRotation(origin, awayFromTarget));
    }

    private Vector3 ResolvePosition(Transform origin)
    {
        if (_positionMode == ImpactPosition.AttackOriginTransform)
            return origin.position;

        Collider target = FindNearestTarget(origin.position);

        if (target == null)
        {
            if (_logPlays)
            {
                Debug.LogWarning(
                    $"[AttackVFX] หาเป้าในรัศมี {_searchRadius} ไม่เจอ — " +
                    "เล่นที่จุดเริ่มยิงแทน (ลองเพิ่ม Search Radius)", this);
            }
            return origin.position;
        }

        if (_logPlays)
            Debug.Log($"[AttackVFX] โดน! — {_impactEffect.name} ที่ {target.name}", this);

        if (_positionMode == ImpactPosition.TargetCenter)
            return target.bounds.center;

        // จุดบนผิวเป้าที่ใกล้จุดเริ่มยิงที่สุด = จุดที่หมัดปะทะ
        return target.ClosestPoint(origin.position);
    }

    /// <summary>
    /// OnHit บอกแค่ว่า "โดนแล้ว" ไม่บอกว่าโดนตัวไหน
    /// เลยต้องหาเองแบบเดียวกับที่ AttackSphereCast หา แล้วเอาตัวที่ใกล้ที่สุด
    /// ซึ่งเป็นตัวเดียวกับที่โดนจริง เพราะ TryToExecuteAttack หยุดที่เป้าแรกที่เจอ
    /// </summary>
    private Collider FindNearestTarget(Vector3 from)
    {
        Collider[] overlaps = Physics.OverlapSphere(
            from, _searchRadius, _targetMask, QueryTriggerInteraction.Ignore);

        Transform selfRoot = transform.root;
        Collider best = null;
        float bestDistance = float.MaxValue;

        foreach (Collider col in overlaps)
        {
            if (col == null) continue;

            // ข้ามตัวเราเองและทุกอย่างที่อยู่ใต้ตัวเรา
            if (col.transform.IsChildOf(selfRoot)) continue;

            // ต้องเป็นของที่ตีได้จริง ไม่งั้นจะไปเจอกำแพงหรือพื้น
            if (col.GetComponentInParent<IDamageable>() == null) continue;

            float distance = (col.ClosestPoint(from) - from).sqrMagnitude;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = col;
        }

        return best;
    }

    private Quaternion BuildRotation(Transform origin, Vector3 awayFromTarget)
    {
        Quaternion baseRotation;

        switch (_rotationMode)
        {
            case VFXRotationMode.AwayFromTarget:
                baseRotation = awayFromTarget.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(awayFromTarget)
                    : Quaternion.identity;
                break;

            case VFXRotationMode.AttackDirection:
                baseRotation = origin.rotation;
                break;

            case VFXRotationMode.FaceCamera:
                Camera cam = _cameraOverride != null ? _cameraOverride : Camera.main;
                baseRotation = cam != null ? cam.transform.rotation : Quaternion.identity;
                break;

            default:
                baseRotation = Quaternion.identity;
                break;
        }

        return baseRotation * Quaternion.Euler(_rotationOffset);
    }

    private Transform GetOriginTransform()
    {
        if (_attack != null && _attack.AttackOrigin != null)
            return _attack.AttackOrigin;

        return transform;
    }
}