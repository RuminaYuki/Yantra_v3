using UnityEngine;

/// <summary>
/// เลือดตอนก้อนเนื้อกระสือตกถึงพื้น / โดนอะไรเข้า
/// - โดนแล้ว: เลือดกระเด็น (Land Splash) ตรงจุดที่โดน
/// - มีพื้นอยู่ใต้จุดนั้น: กองเลือด (Land Pool) แผ่บนพื้น ค้างไว้สักพักแล้วหดหายเอง
///
/// แปะบนตัวแม่ของ projectile (KrasueProjectileBullet) — ไม่ต้องแก้ ProjectileBullet
/// รู้ว่าโดนแล้วจากตอนที่ก้อนถูกคืน pool / ถูกลบ หลังบินออกไปจริง
/// ก้อนในมือ (HeldFleshChunk) ปิด ProjectileBullet ไว้ เลยไม่มีเลือดออกตอนก้อนในมือหาย
/// </summary>
[DisallowMultipleComponent]
public class FleshImpactBlood : MonoBehaviour
{
    [Header("เลือดกระเด็นตอนโดน (ไม่ใส่ก็ได้)")]
    [Tooltip("เช่น Vfx_blood_Bust Variant")]
    [SerializeField] private GameObject _landSplash;

    [Tooltip("เลือดกี่สาย — แผ่ออกเป็นรูปพัดตามทางที่ก้อนบินมา ดูเป็นเลือดสาดกระจาย")]
    [SerializeField, Range(1, 6)] private int _splashCount = 3;

    [Tooltip("พัดกว้างกี่องศา (สายซ้ายสุดถึงขวาสุด)")]
    [SerializeField, Range(0f, 360f)] private float _splashSpread = 120f;

    [Tooltip("เงยจากพื้นกี่องศา — 0 = ไถไปตามพื้น / 90 = พุ่งขึ้นฟ้าเป็นเสาแนวตั้ง")]
    [SerializeField, Range(0f, 90f)] private float _splashUpAngle = 20f;

    [Tooltip("หมุนเพิ่มหลังเล็งแล้ว — ปกติไม่ต้องแตะ (VFX พุ่งตามแกน Z ลูกศรสีฟ้า)")]
    [SerializeField] private Vector3 _splashRotation;

    [Tooltip("ลบ VFX ทิ้งหลังกี่วินาที")]
    [SerializeField, Min(0.1f)] private float _splashLifetime = 3f;

    [Header("กองเลือดบนพื้น (ไม่ใส่ก็ได้)")]
    [Tooltip("prefab กองเลือด — วางราบ ด้านบนหันแกน Y (สีเขียว) ขึ้นฟ้า\n" +
             "ถ้าเป็น Decal Projector ให้ Scale Mode = Inherit From Hierarchy ไม่งั้นจะไม่แผ่")]
    [SerializeField] private GameObject _landPool;

    [Tooltip("หาพื้นใต้จุดที่โดนลงไปไกลสุดเท่านี้ (เมตร)\n" +
             "2.5 = โดนผู้เล่นกลางอากาศก็มีกองเลือดที่เท้าด้วย / ~0.5 = มีกองเลือดเฉพาะตอนตกพื้นจริง")]
    [SerializeField, Min(0.1f)] private float _groundCheckDistance = 2.5f;

    [Tooltip("layer ที่นับเป็นพื้น")]
    [SerializeField] private LayerMask _groundLayers = ~0;

    [Tooltip("สุ่มขนาดกองเลือด คูณกับขนาดใน prefab")]
    [SerializeField] private Vector2 _poolSize = new Vector2(0.8f, 1.2f);

    [Tooltip("เวลาแผ่ออก (วินาที)")]
    [SerializeField, Min(0.01f)] private float _poolGrowTime = 0.4f;

    [Tooltip("ค้างบนพื้นกี่วินาทีก่อนเริ่มหด")]
    [SerializeField, Min(0f)] private float _poolStayTime = 8f;

    [Tooltip("เวลาหดหาย (วินาที)")]
    [SerializeField, Min(0.01f)] private float _poolShrinkTime = 1.5f;

    [Header("เสียงตอนโดน (ไม่ใส่ก็ได้)")]
    [Tooltip("เช่น Krasue_ChunkHit — ก้อนเนื้อกระแทก + เลือดกระจาย ดังพร้อมเลือดกระเด็น ทั้งตอนโดนผู้เล่นและตกพื้น")]
    [SerializeField] private SoundID _landSound;

    [Header("Debug")]
    [SerializeField] private bool _logSteps;

    private const float LiftAbovePoint = 0.5f;   // ยิงหาพื้นจากเหนือจุดโดนนิดหน่อย เผื่อก้อนจมลงพื้นไปแล้ว
    private const float TouchRadius = 1f;        // กล่องชนของก้อนกว้างเกือบเมตร — ในระยะนี้มีอะไรอยู่ = โดนจริง
    private const float PoolLift = 0.01f;        // ยกกองเลือดพ้นพื้นนิดเดียว กันกระพริบ

    private static readonly RaycastHit[] _rayHits = new RaycastHit[16];
    private static readonly Collider[] _overlaps = new Collider[16];
    private static bool _quitting;

    private ProjectileBullet _bullet;
    private Vector3 _startPos;
    private Vector3 _lastPos;
    private Vector3 _velocity;
    private bool _hasStart;
    private bool _flying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        _quitting = false;
        Application.quitting -= OnQuit;
        Application.quitting += OnQuit;
    }

    private static void OnQuit() => _quitting = true;   // ออก Play Mode / ปิดเกม — ไม่ต้องเกิดเลือด

    private void Awake()
    {
        _bullet = GetComponent<ProjectileBullet>();
        if (_bullet == null)
            Debug.LogWarning($"[FleshImpactBlood] {name} ไม่มี ProjectileBullet — แปะบนตัวแม่ของก้อนเนื้อที่ขว้าง", this);
    }

    // ทุกครั้งที่หยิบจาก pool — รอเฟรมแรกค่อยจำจุดเริ่ม (pool มักเปิดก่อนแล้วค่อยย้ายไปที่มือ)
    private void OnEnable()
    {
        _hasStart = false;
        _flying = false;
        _velocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (!IsRealProjectile()) return;

        Vector3 pos = transform.position;
        if (!_hasStart)
        {
            _startPos = pos;
            _lastPos = pos;
            _hasStart = true;
            return;
        }

        if (Time.deltaTime > 0f) _velocity = (pos - _lastPos) / Time.deltaTime;
        _lastPos = pos;

        if (!_flying && (pos - _startPos).sqrMagnitude > 0.1f * 0.1f)
            _flying = true;
    }

    // ProjectileBullet โดนแล้วคืน pool (SetActive false) หรือ Destroy — ทั้งคู่ผ่านตรงนี้
    private void OnDisable()
    {
        if (!_flying || _quitting || !IsRealProjectile()) return;
        _flying = false;

        // pool อาจย้ายแม่ก่อนปิด ตำแหน่งจะกระโดด — ใช้ตำแหน่งล่าสุดที่จำไว้แทน
        Vector3 point = transform.position;
        float maxJump = _velocity.magnitude * 0.1f + 1f;
        if ((point - _lastPos).sqrMagnitude > maxJump * maxJump) point = _lastPos;

        Land(point);
    }

    // ก้อนในมือปิด ProjectileBullet ไว้ — ไม่นับ
    private bool IsRealProjectile() => _bullet != null && _bullet.enabled;

    private void Land(Vector3 point)
    {
        bool hasGround = FindGround(point, out RaycastHit ground);

        // ไม่เจอพื้นและไม่ได้ชนอะไรใกล้ ๆ = หมดเวลากลางอากาศ ไม่ต้องมีเลือด
        if (!hasGround && !TouchedSomething(point))
        {
            if (_logSteps) Debug.Log("[FleshImpactBlood] หมดเวลากลางอากาศ — ไม่มีเลือด", this);
            return;
        }

        // ตกถึงพื้น → สาดต่อไปตามทางที่บินมา เลียดพื้น / โดนกลางอากาศ (ผู้เล่น ผนัง) → เด้งกลับทางที่บินมา
        bool onGround = hasGround && ground.distance - LiftAbovePoint < TouchRadius;
        Vector3 up = onGround ? ground.normal : Vector3.up;
        Vector3 travel = onGround ? _velocity : -_velocity;
        Splash(point, travel, up);

        if (_landSound != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(_landSound, point);

        if (hasGround) SpawnPool(ground);

        if (_logSteps)
            Debug.Log($"[FleshImpactBlood] โดนที่ {point} — {(onGround ? "ตกพื้น" : "โดนกลางอากาศ")}" +
                      $"{(hasGround ? " + กองเลือด" : " (ไม่เจอพื้นข้างล่าง)")}", this);
    }

    // แผ่เป็นพัดรอบทิศหลัก — แต่ละสายเงยจากพื้นนิดเดียว
    private void Splash(Vector3 point, Vector3 travel, Vector3 up)
    {
        if (_landSplash == null) return;

        Vector3 main = BloodFx.Tilt(travel, up, 0f);   // ทิศหลักตามพื้น (ยังไม่เงย)
        int count = Mathf.Max(1, _splashCount);
        for (int i = 0; i < count; i++)
        {
            float yaw = count == 1 ? 0f : Mathf.Lerp(-_splashSpread * 0.5f, _splashSpread * 0.5f, i / (count - 1f));
            yaw += Random.Range(-10f, 10f);   // ไม่ให้สายเรียงกันเป๊ะเกินไป
            Vector3 dir = Quaternion.AngleAxis(yaw, up) * main;
            dir = BloodFx.Tilt(dir, up, _splashUpAngle + Random.Range(-5f, 5f));
            BloodFx.Spawn(_landSplash, point, dir, _splashRotation, _splashLifetime);
        }
    }

    private void SpawnPool(RaycastHit ground)
    {
        if (_landPool == null) return;

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, ground.normal) *
                              Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject pool = Instantiate(_landPool, ground.point + ground.normal * PoolLift, rotation);
        pool.SetActive(true);

        float size = Random.Range(_poolSize.x, Mathf.Max(_poolSize.x, _poolSize.y));
        pool.transform.localScale = Vector3.Scale(_landPool.transform.localScale, new Vector3(size, 1f, size));

        if (!pool.TryGetComponent(out BloodPoolFade fade))
            fade = pool.AddComponent<BloodPoolFade>();
        fade.Begin(_poolGrowTime, _poolStayTime, _poolShrinkTime);
    }

    // พื้นใต้จุดที่โดน — ไม่นับตัวผู้เล่น / ผี (มี CharacterController), ของที่ขยับได้, ผนัง, ตัวเอง
    private bool FindGround(Vector3 point, out RaycastHit ground)
    {
        ground = default;
        Vector3 origin = point + Vector3.up * LiftAbovePoint;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, _rayHits, _groundCheckDistance + LiftAbovePoint,
                                            _groundLayers, QueryTriggerInteraction.Ignore);

        bool found = false;
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _rayHits[i];
            if (hit.distance >= best) continue;
            if (hit.normal.y < 0.6f) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;

            best = hit.distance;
            ground = hit;
            found = true;
        }
        return found;
    }

    private bool TouchedSomething(Vector3 point)
    {
        int count = Physics.OverlapSphereNonAlloc(point, TouchRadius, _overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!_overlaps[i].transform.IsChildOf(transform)) return true;
        }
        return false;
    }

    private void OnValidate()
    {
        if (_poolSize.y < _poolSize.x) _poolSize.y = _poolSize.x;
    }
}
