using UnityEngine;

// ปิดศัตรูทั้งกลุ่มไว้ตอนเริ่มเกม แล้วเปิดเมื่อผู้เล่นเดินเข้าวงกลม (ครั้งเดียว ไม่ปิดกลับ)
// ใช้ลด FPS ตก: ศัตรูโซนที่ผู้เล่นยังไปไม่ถึงจะไม่รัน AI / Animator / Light / VFX
//
// วิธีใช้
// - วางบน object ใหม่ที่มี SphereCollider (Radius = ระยะที่อยากให้ศัตรูเริ่มทำงาน ควรครอบกล่อง EnemyGroupTracker ทั้งหมด)
// - ลากลูก "EnemyGroup" (ที่เก็บศัตรู) ใส่ Targets — อย่าใส่ Navmesh หรือตัว EnemyGroupTracker
// - ในฉากเปิดศัตรูไว้ได้ตามปกติ script ปิดให้เองตอนเริ่ม
//
// ทำไม DefaultExecutionOrder(-10000): ต้องปิดก่อน Awake ของศัตรูทุกตัว
// ไม่งั้นบางตัว Awake ไปแล้ว บางตัวยัง (สุ่มตามรอบ Play) สภาพศัตรูจะไม่เหมือนกัน
//
// ทำไมเช็คตำแหน่งเองแทน OnTriggerEnter: ตอนโหลด save ผู้เล่นถูกวางในวงเลย
// ถ้ายังไม่ขยับ OnTriggerEnter จะไม่ยิง — เช็คระยะทุกเฟรมจนกว่าจะเปิด (ถูกมาก วงละไม่กี่บรรทัด)
[DefaultExecutionOrder(-10000)]
[RequireComponent(typeof(SphereCollider))]
public class EnemyZoneActivator : MonoBehaviour
{
    [Tooltip("สิ่งที่ปิดไว้ตอนเริ่ม แล้วเปิดเมื่อผู้เล่นเข้าวง (เช่นลูก EnemyGroup, Fog)")]
    [SerializeField] GameObject[] targets;

    [Tooltip("Tag ของผู้เล่น")]
    [SerializeField] string playerTag = "Player";

    SphereCollider sphere;
    Transform player;
    bool activated;

    void Awake()
    {
        sphere = GetComponent<SphereCollider>();
        // ใช้วงเป็นแค่ขอบเขต ไม่ให้ไปชน / ขวางกระสุน / raycast
        sphere.isTrigger = true;
        sphere.enabled = false;

        SetTargets(false);
    }

    void Update()
    {
        if (player == null)
        {
            PlayerLocomotion locomotion = PlayerLookup.Find<PlayerLocomotion>(playerTag);
            if (locomotion == null) return;
            player = locomotion.transform;
        }

        if (IsInside(player.position))
            Activate();
    }

    // เรียกจากที่อื่นได้ เช่น cutscene / quest อยากให้ศัตรูโผล่ก่อนผู้เล่นเดินถึง
    public void Activate()
    {
        if (activated) return;
        activated = true;

        SetTargets(true);
        enabled = false;   // เปิดแล้วไม่ต้องเช็คอีก
    }

    // เช็คระยะจากจุดศูนย์กลางวง (ใช้ center / radius ของ SphereCollider + scale แบบเดียวกับที่ Unity คิด)
    bool IsInside(Vector3 worldPos)
    {
        Vector3 center = transform.TransformPoint(sphere.center);
        float radius = WorldRadius(sphere);
        return (worldPos - center).sqrMagnitude <= radius * radius;
    }

    // SphereCollider ใช้ scale แกนที่ใหญ่สุด
    float WorldRadius(SphereCollider s)
    {
        Vector3 scale = s.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return s.radius * maxScale;
    }

    void SetTargets(bool active)
    {
        if (targets == null) return;

        foreach (GameObject target in targets)
        {
            if (target != null)
                target.SetActive(active);
        }
    }

    void OnDrawGizmos()
    {
        SphereCollider s = sphere != null ? sphere : GetComponent<SphereCollider>();
        if (s == null) return;

        Gizmos.color = activated ? new Color(0.3f, 1f, 0.3f, 0.6f) : new Color(1f, 0.6f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.TransformPoint(s.center), WorldRadius(s));
    }
}
