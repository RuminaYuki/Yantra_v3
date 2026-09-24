using UnityEngine;
using UnityEngine.VFX;
using Effekseer;

// ดูแลเอฟเฟกต์ 1 ชิ้นตั้งแต่เกิดจนลบ: ความเร็ว / หยุดตามเวลา / ค่อยๆ จาง / ลบเมื่อจบ
// ใช้ร่วมกันทั้ง Effekseer, Particle System และ VFX Graph ตั้งค่าเหมือนกันได้ทุกที่
public class VFXPlayback : MonoBehaviour
{
    // เอฟเฟกต์ที่เช็คไม่ได้ว่าจบหรือยัง (VFX Graph / Particle วนลูป) จะถูกสั่งจบที่เวลานี้ กันค้างในซีน
    private const float SafetyLifetime = 5f;

    // เฟรมแรกๆ อนุภาคอาจยังไม่ทันเกิด ถ้าเช็คเร็วไปจะนึกว่าจบแล้ว
    private const float StartGrace = 0.2f;

    private ParticleSystem[] _particles;
    private VisualEffect[] _graphs;
    private EffekseerEmitter[] _effekseer;

    private float _startTime;
    private float _stopAt;
    private float _destroyAt;
    private float _fadeOut;
    private bool _stopping;

    // Effekseer ต้องมี GameObject ให้เกาะ ถึงจะคุมเวลา ความเร็ว และวิ่งตามได้
    // parent = null → ค้างอยู่ที่เดิมในโลก
    public static GameObject PlayEffekseer(EffekseerEffectAsset asset, Transform parent,
        Vector3 position, Quaternion rotation, float duration, float speed, float fadeOut)
    {
        if (asset == null) return null;

        var carrier = new GameObject($"[VFX] {asset.name}");
        carrier.transform.SetPositionAndRotation(position, rotation);

        if (parent != null)
        {
            carrier.transform.SetParent(parent, true);

            // กระดูกบางตัว scale ไม่ใช่ 1 → Unity ชดเชย localScale ให้ แล้ว Emitter เอาไปคูณขนาดเอฟเฟกต์
            carrier.transform.localScale = Vector3.one;
        }

        var emitter = carrier.AddComponent<EffekseerEmitter>();
        emitter.effectAsset = asset;
        emitter.speed = SafeSpeed(speed);
        emitter.Play();

        Manage(carrier, duration, speed, fadeOut);
        return carrier;
    }

    // ใช้กับ Prefab ที่ Instantiate มาแล้ว
    public static void Manage(GameObject instance, float duration, float speed, float fadeOut)
    {
        if (instance == null) return;

        var playback = instance.GetComponent<VFXPlayback>();
        if (playback == null) playback = instance.AddComponent<VFXPlayback>();

        playback.Begin(duration, speed, fadeOut);
    }

    // โหมด Hold ใช้แค่ความเร็ว ส่วนอายุให้ VFXKeepAlive ดูแล
    public static void SetSpeed(GameObject instance, float speed)
    {
        if (instance == null) return;

        ApplySpeed(
            instance.GetComponentsInChildren<ParticleSystem>(true),
            instance.GetComponentsInChildren<VisualEffect>(true),
            instance.GetComponentsInChildren<EffekseerEmitter>(true),
            SafeSpeed(speed));
    }

    private void Begin(float duration, float speed, float fadeOut)
    {
        _particles = GetComponentsInChildren<ParticleSystem>(true);
        _graphs = GetComponentsInChildren<VisualEffect>(true);
        _effekseer = GetComponentsInChildren<EffekseerEmitter>(true);

        float safeSpeed = SafeSpeed(speed);
        ApplySpeed(_particles, _graphs, _effekseer, safeSpeed);

        _startTime = Time.time;
        _fadeOut = Mathf.Max(0f, fadeOut);
        _stopping = false;

        // 0 = เล่นจนจบเอง แต่ยังมีเพดานกันค้าง (เล่นช้าลง เพดานก็ยืดตาม)
        _stopAt = _startTime + (duration > 0f ? duration : SafetyLifetime / safeSpeed);
    }

    // ใช้ Time.time (เวลาเกม) — hit stop หรือ pause จะหยุดนับไปพร้อมตัวเอฟเฟกต์
    private void Update()
    {
        float now = Time.time;

        if (!_stopping)
        {
            if (now >= _stopAt) StopEmitting(now);
            else if (now - _startTime > StartGrace && !AnythingAlive()) Destroy(gameObject);
            return;
        }

        // กำลังจาง: หมดแล้วลบเลย ไม่ต้องรอครบเวลา / ครบเวลาแล้วลบแม้ยังเหลือ
        if (now >= _destroyAt || !AnythingAlive()) Destroy(gameObject);
    }

    // หยุดพ่นอนุภาคใหม่ ส่วนที่อยู่บนจอแล้วปล่อยให้จางไปตามอายุ — ไม่หายวับ
    private void StopEmitting(float now)
    {
        _stopping = true;
        _destroyAt = now + _fadeOut;

        foreach (var ps in _particles)
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        foreach (var vfx in _graphs)
            if (vfx != null) vfx.Stop();

        foreach (var e in _effekseer)
            if (e != null) e.StopRoot();
    }

    private bool AnythingAlive()
    {
        // VFX Graph บอกไม่ได้แน่ชัดว่าจบหรือยัง / prefab ที่ไม่มีตัวพ่นเลย (เช่น mesh ขยับเอง)
        // ถือว่ายังเล่นอยู่ ไปจบตามเวลาแทน
        if (_graphs.Length > 0) return true;
        if (_particles.Length == 0 && _effekseer.Length == 0) return true;

        foreach (var e in _effekseer)
            if (e != null && e.exists) return true;

        foreach (var ps in _particles)
            if (ps != null && ps.IsAlive(true)) return true;

        return false;
    }

    private static void ApplySpeed(ParticleSystem[] particles, VisualEffect[] graphs,
        EffekseerEmitter[] effekseer, float speed)
    {
        if (Mathf.Approximately(speed, 1f)) return;

        // คูณจากค่าที่คนทำเอฟเฟกต์ตั้งไว้ ไม่ทับ — บางตัวเขาตั้งความเร็วมาเองแล้ว
        foreach (var ps in particles)
        {
            if (ps == null) continue;
            var main = ps.main;
            main.simulationSpeed *= speed;
        }

        foreach (var vfx in graphs)
            if (vfx != null) vfx.playRate *= speed;

        foreach (var e in effekseer)
            if (e != null) e.speed = speed;
    }

    private static float SafeSpeed(float speed) => Mathf.Max(0.05f, speed);
}