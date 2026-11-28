using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// กำแพงหนามล้อมลานบอส — เริ่มสู้แล้วหนามพุ่งขึ้นเป็นวงรอบลาน ปิดทางหนี / บอสตายแล้วหนามหักลงหายไป
/// วงใหญ่พอให้ผู้เล่น + จุด Glory ทุกจุดอยู่ข้างใน (คิดตอนเริ่มสู้) ผู้เล่นไม่มีทางติดอยู่ข้างนอก
/// มีกำแพงล่องหน (Box Collider) ตามแนวหนามกันเดินทะลุ — BossIntro เป็นคนสั่ง Raise / Lower
/// </summary>
public class ArenaThornWall : MonoBehaviour
{
    [Header("หนาม")]
    [Tooltip("prefab หนาม เช่น Thorn(Tanee) — ใช้ท่าขึ้น/ท่าหักใน Animator ของมันเอง")]
    [SerializeField] private GameObject _thornPrefab;

    [Tooltip("ระยะห่างระหว่างหนามแต่ละต้น (เมตร)")]
    [SerializeField, Min(0.5f)] private float _spacing = 2.6f;

    [Tooltip("สุ่มขนาดหนาม (คูณกับขนาดใน prefab)")]
    [SerializeField] private Vector2 _thornScale = new Vector2(1.6f, 2.4f);

    [Tooltip("ดาเมจตอนผู้เล่นไปโดนหนาม (ต่อหนาม 1 ต้น)")]
    [SerializeField, Min(0f)] private float _thornDamage = 5f;

    [Tooltip("หนามขึ้นไล่รอบวงจากฝั่งผู้เล่น ใช้เวลารวมกี่วินาที")]
    [SerializeField, Min(0f)] private float _riseTime = 1.2f;

    [Tooltip("ชื่อ state ท่าหักใน Animator ของหนาม")]
    [SerializeField] private string _finishState = "Tanee_Thorn_Finish";

    [Tooltip("สั่งหักแล้วรอกี่วินาทีค่อยลบหนามทิ้ง")]
    [SerializeField, Min(0f)] private float _despawnDelay = 2.5f;

    [Tooltip("ฝุ่นดิน (ลูก GroundBurst ในหนาม) ทุกกี่ต้น — 1 = ทุกต้น / 2 = เว้นต้น\n" +
             "หนามรอบลานมีหลายสิบต้น ฝุ่นขึ้นพร้อมกันทุกต้นจะหนักเครื่อง")]
    [SerializeField, Min(1)] private int _burstEvery = 2;

    [Header("ขนาดวง")]
    [Tooltip("จุดกลางลาน — ว่าง = ตรงที่บอสยืนตอนเริ่มสู้")]
    [SerializeField] private Transform _center;

    [Tooltip("บอส — ใช้หาจุดกลางเมื่อไม่ได้ใส่ Center")]
    [SerializeField] private Transform _boss;

    [SerializeField, Min(3f)] private float _minRadius = 14f;
    [SerializeField, Min(3f)] private float _maxRadius = 32f;

    [Tooltip("ผู้เล่น / จุด Glory ต้องอยู่ห่างแนวหนามเข้ามาข้างในอย่างน้อยกี่เมตร")]
    [SerializeField, Min(0f)] private float _insideMargin = 5f;

    [Tooltip("จุด Glory ของบอส — ทุกจุดจะอยู่ในวง")]
    [SerializeField] private RandomGloryPoint _gloryPoints;

    [Header("กำแพงล่องหน")]
    [SerializeField, Min(0.5f)] private float _wallHeight = 6f;
    [SerializeField, Min(0.2f)] private float _wallThickness = 1.5f;

    [Header("พื้น")]
    [SerializeField] private LayerMask _groundLayers = ~0;

    [Tooltip("ฝังโคนหนามลงดินกี่เมตร (ต่อขนาด 1 เท่า) — พื้นเอียง / ไม่เรียบ โคนจะได้ไม่ลอย")]
    [SerializeField, Min(0f)] private float _sink = 0.35f;

    [Tooltip("เอียงหนามตามพื้นลาดแค่ไหน (0 = ตั้งตรง / 1 = ตั้งฉากกับพื้น)")]
    [SerializeField, Range(0f, 1f)] private float _followSlope = 0.6f;

    [Tooltip("พื้นตรงนั้นสูง / ต่ำกว่าลานเกินนี้ (เมตร) = ไม่วางหนาม (เช่น บนเนินสูง / ยอดไม้) — กำแพงล่องหนยังกันอยู่")]
    [SerializeField, Min(0.5f)] private float _maxHeightDiff = 4f;

    [Header("Debug")]
    [SerializeField] private bool _log;

    private readonly List<GameObject> _thorns = new List<GameObject>();
    private GameObject _walls;
    private Vector3 _lastCenter;
    private float _lastRadius;
    private bool _raised;
    private int _spawnCount;

    /// <summary>หนามพุ่งขึ้นล้อมลาน</summary>
    public void Raise()
    {
        if (_raised) return;
        _raised = true;

        Vector3 player = PlayerPosition();
        Vector3 center = _center != null ? _center.position : (_boss != null ? _boss.position : transform.position);
        float radius = FitRadius(ref center, player);
        _lastCenter = center;
        _lastRadius = radius;
        _spawnCount = 0;
        if (_log) Debug.Log($"[ArenaThornWall] ล้อมลาน กลาง {center} รัศมี {radius:0.0} ม.", this);

        BuildWalls(center, radius);
        if (_thornPrefab != null) StartCoroutine(RaiseThorns(center, radius, player));
    }

    /// <summary>หนามหักลง กำแพงหาย</summary>
    public void Lower()
    {
        if (!_raised) return;
        _raised = false;
        StopAllCoroutines();

        if (_walls != null) Destroy(_walls);
        _walls = null;

        int finishHash = Animator.StringToHash(_finishState);
        foreach (GameObject thorn in _thorns)
        {
            if (thorn == null) continue;
            Animator anim = thorn.GetComponentInChildren<Animator>();
            if (anim != null && anim.isActiveAndEnabled && anim.HasState(0, finishHash))
                anim.CrossFadeInFixedTime(finishHash, 0.15f, 0, Random.Range(0f, 0.3f));
            DamageHitbox hitbox = thorn.GetComponentInChildren<DamageHitbox>();
            if (hitbox != null) hitbox.enabled = false;
            Destroy(thorn, _despawnDelay + Random.Range(0f, 0.4f));
        }
        _thorns.Clear();
    }

    private void OnDestroy()
    {
        if (_walls != null) Destroy(_walls);
        foreach (GameObject thorn in _thorns)
            if (thorn != null) Destroy(thorn);
        _thorns.Clear();
    }

    // วงใหญ่พอให้ผู้เล่น + จุด Glory อยู่ข้างใน / ผู้เล่นไกลเกิน Max = เลื่อนกลางวงไปหาผู้เล่น
    private float FitRadius(ref Vector3 center, Vector3 player)
    {
        float need = Flat(player - center) + _insideMargin;
        if (_gloryPoints != null && _gloryPoints.GloryPoint != null)
            foreach (Transform p in _gloryPoints.GloryPoint)
                if (p != null) need = Mathf.Max(need, Flat(p.position - center) + _insideMargin);

        float radius = Mathf.Clamp(need, _minRadius, _maxRadius);
        float playerDist = Flat(player - center);
        if (playerDist + _insideMargin > radius)
        {
            Vector3 toPlayer = player - center;
            toPlayer.y = 0f;
            center += toPlayer.normalized * (playerDist + _insideMargin - radius);
        }
        return radius;
    }

    private IEnumerator RaiseThorns(Vector3 center, float radius, Vector3 player)
    {
        int count = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / _spacing));
        Vector3 toPlayer = player - center;
        float startAngle = Mathf.Atan2(toPlayer.z, toPlayer.x);
        float step = 2f * Mathf.PI / count;

        // ไล่ขึ้นจากฝั่งผู้เล่นทั้งซ้ายขวา ไปบรรจบกันด้านหลังบอส
        int half = count / 2 + 1;
        float delay = half > 1 ? _riseTime / (half - 1) : 0f;
        for (int i = 0; i < half; i++)
        {
            SpawnThorn(center, radius, startAngle + i * step);
            if (i > 0 && i * 2 < count) SpawnThorn(center, radius, startAngle - i * step);
            if (delay > 0f) yield return new WaitForSeconds(delay);
        }
    }

    private void SpawnThorn(Vector3 center, float radius, float angle)
    {
        float r = radius + Random.Range(-0.4f, 0.4f);
        Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
        pos.y = center.y;
        float scale = Random.Range(_thornScale.x, Mathf.Max(_thornScale.x, _thornScale.y));

        // ยิงหาพื้นหลายจุดรอบโคน แล้ววางที่จุดต่ำสุด — พื้นลาด / ยิงโดนกิ่งไม้จุดเดียว หนามจะไม่ลอย
        if (!FindGroundUnderBase(pos, 0.6f * scale, out Vector3 ground, out Vector3 normal)) return;
        if (Mathf.Abs(ground.y - center.y) > _maxHeightDiff) return;

        Vector3 up = Vector3.Slerp(Vector3.up, normal, _followSlope);
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, up) *
                         Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));
        GameObject thorn = Instantiate(_thornPrefab, ground - up * (_sink * scale), rot);
        thorn.transform.localScale = _thornPrefab.transform.localScale * scale;

        if (_burstEvery > 1 && _spawnCount++ % _burstEvery != 0)
        {
            Transform burst = thorn.transform.Find("GroundBurst");
            if (burst != null) burst.gameObject.SetActive(false);
        }

        DamageHitbox hitbox = thorn.GetComponentInChildren<DamageHitbox>();
        if (hitbox != null) hitbox.SetDamage(_thornDamage);
        _thorns.Add(thorn);
    }

    private void BuildWalls(Vector3 center, float radius)
    {
        _walls = new GameObject("Arena Thorn Wall (Colliders)");
        const float segmentLength = 4f;
        int count = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / segmentLength));
        float step = 2f * Mathf.PI / count;
        float length = 2f * radius * Mathf.Tan(step * 0.5f) + 0.6f;   // ต่อกันไม่มีช่อง

        for (int i = 0; i < count; i++)
        {
            float angle = (i + 0.5f) * step;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 pos = center + dir * (radius + _wallThickness * 0.5f);
            float groundY = FindGround(pos, out Vector3 ground) ? ground.y : center.y;

            var seg = new GameObject("Wall");
            seg.transform.SetParent(_walls.transform, false);
            seg.transform.SetPositionAndRotation(new Vector3(pos.x, groundY + _wallHeight * 0.5f - 1f, pos.z),
                                                 Quaternion.LookRotation(dir));
            var box = seg.AddComponent<BoxCollider>();
            box.size = new Vector3(length, _wallHeight, _wallThickness);
        }
    }

    // ยิงหาพื้น 5 จุด (กลาง + 4 ทิศรอบโคน) เอาจุดต่ำสุด / normal เฉลี่ยไว้เอียงหนาม
    private bool FindGroundUnderBase(Vector3 pos, float spread, out Vector3 ground, out Vector3 normal)
    {
        ground = pos;
        normal = Vector3.up;
        Vector3 sum = Vector3.zero;
        int hits = 0;
        float lowest = float.MaxValue;
        Vector3[] offsets = { Vector3.zero, Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        foreach (Vector3 o in offsets)
        {
            if (!FindGround(pos + o * spread, out Vector3 g, out Vector3 n)) continue;
            hits++;
            sum += n;
            if (g.y < lowest)
            {
                lowest = g.y;
                ground = new Vector3(pos.x, g.y, pos.z);
            }
        }
        if (hits == 0) return false;
        normal = sum.normalized;
        return true;
    }

    private bool FindGround(Vector3 pos, out Vector3 ground) => FindGround(pos, out ground, out _);

    // พื้นใต้จุดนี้ (ใกล้ระดับลาน) — ไม่นับตัวละคร (มี CharacterController) / ของที่ขยับได้ / กำแพงตัวเอง
    private bool FindGround(Vector3 pos, out Vector3 ground, out Vector3 normal)
    {
        ground = pos;
        normal = Vector3.up;
        float best = float.MaxValue;
        bool found = false;
        foreach (RaycastHit hit in Physics.RaycastAll(pos + Vector3.up * 15f, Vector3.down, 40f,
                                                      _groundLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.normal.y < 0.5f) continue;
            if (hit.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
            if (_walls != null && hit.collider.transform.IsChildOf(_walls.transform)) continue;

            // เอาพื้นที่สูงใกล้ระดับลานที่สุด — ไม่ใช่ยอดไม้ / หลังคาที่โดนก่อน
            float diff = Mathf.Abs(hit.point.y - pos.y);
            if (diff >= best) continue;
            best = diff;
            ground = hit.point;
            normal = hit.normal;
            found = true;
        }
        return found;
    }

    private static Vector3 PlayerPosition()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    private static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 c = _raised ? _lastCenter : (_center != null ? _center.position : (_boss != null ? _boss.position : transform.position));
        float r = _raised ? _lastRadius : _minRadius;
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.9f);
        const int seg = 64;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r,
                            c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r);
        }
        if (!_raised)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * _maxRadius,
                                c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * _maxRadius);
            }
        }
    }
}
