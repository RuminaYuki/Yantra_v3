using UnityEngine;

/// <summary>
/// ตัวช่วยเกิด VFX เลือด — ใช้ร่วมกันระหว่าง HeldFleshChunk (ควัก / ขว้าง) กับ FleshImpactBlood (ลงพื้น)
/// เกิด → เปิด → สั่ง Particle ที่ไม่ได้ติ๊ก Play On Awake → ลบทิ้งตามเวลา (แบบเดียวกับ GloryBloodTrigger)
/// VFX Graph (Visual Effect) เล่นเองตอนเกิดผ่าน Initial Event "OnPlay" ไม่ต้องสั่ง
/// เลือดพุ่งตามแกน Z (ลูกศรสีฟ้า) ของ VFX — Vfx_blood ตั้ง BloodVelocity = (0.5, 0.5, 5) คือพุ่งตาม Z เป็นลำแคบ
/// </summary>
public static class BloodFx
{
    public static GameObject Spawn(GameObject prefab, Vector3 position, Vector3 direction, Vector3 extraRotation, float lifetime)
    {
        if (prefab == null) return null;

        Quaternion rotation = Aim(direction) * Quaternion.Euler(extraRotation);
        GameObject instance = Object.Instantiate(prefab, position, rotation);
        instance.SetActive(true);

        // Particle ที่ไม่ได้ติ๊ก Play On Awake จะไม่เล่นเอง — สั่งเฉพาะตัวบนสุด ตัวลูกเล่นตาม
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (IsTopParticle(ps, instance.transform) && !ps.isPlaying)
                ps.Play(true);
        }

        if (lifetime > 0f) Object.Destroy(instance, lifetime);
        return instance;
    }

    // ทิศตามแนวพื้น (ตัดส่วนที่ชี้ขึ้น / ลงออกตาม up) แล้วเงยขึ้น angle องศา
    // VFX เลือดพุ่งเป็นลำแคบ ๆ — เล็งขึ้นฟ้าตรง ๆ จะเห็นเป็นเสาแนวตั้ง เลยต้องเงยแค่นิดเดียว
    public static Vector3 Tilt(Vector3 direction, Vector3 up, float angle)
    {
        Vector3 flat = Vector3.ProjectOnPlane(direction, up);
        if (flat.sqrMagnitude < 1e-4f)
        {
            // ไม่มีทิศตามพื้นเลย (เช่นตกลงมาตรง ๆ) — สุ่มทิศรอบตัว
            float yaw = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            flat = Vector3.ProjectOnPlane(new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)), up);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.ProjectOnPlane(Vector3.forward, up);
        }

        float rad = angle * Mathf.Deg2Rad;
        return flat.normalized * Mathf.Cos(rad) + up.normalized * Mathf.Sin(rad);
    }

    // หันแกน Z ไปตามทิศ — ทิศชี้ฟ้าตรง ๆ ก็ไม่หมุนมั่ว
    public static Quaternion Aim(Vector3 direction)
    {
        if (direction.sqrMagnitude < 1e-6f) return Quaternion.identity;

        Vector3 dir = direction.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
        return Quaternion.LookRotation(dir, up);
    }

    private static bool IsTopParticle(ParticleSystem ps, Transform root)
    {
        if (ps.transform == root)
            return true;

        for (Transform t = ps.transform.parent; t != null; t = t.parent)
        {
            if (t.TryGetComponent<ParticleSystem>(out _))
                return false;
            if (t == root)
                break;
        }
        return true;
    }
}
