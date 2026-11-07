using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// เอฟเฟกต์ยิงปืนคาบศิลา — ใส่แทน HitscanVFX บน prefab กระสุน (ตัวเดียวกับ HitscanShooter)
// แสงวาบ + ควันดินปืน + สะเก็ดไฟ + ควันบางๆ ตามแนวกระสุน + ฝุ่นตรงจุดที่โดน
// ทุกอย่างใช้วัสดุเดียว (shader ShotParticle) และสร้างแยกออกมาในโลก กระสุนหายไปเอฟเฟกต์ก็ยังอยู่ครบ
[RequireComponent(typeof(HitscanShooter))]
public class MusketShotFX : MonoBehaviour
{
    [Tooltip("วัสดุที่ใช้ shader 'Shader Graphs/ShotParticle'")]
    [SerializeField] private Material _material;

    [Header("กันควันบังหน้า")]
    [Tooltip("ควันจะเริ่มห่างจากกล้องอย่างน้อยเท่านี้ (เมตร)")]
    [SerializeField, Min(0f)] private float _smokeStartFromCamera = 1.4f;
    [Tooltip("ควันที่อยู่ใกล้กล้องกว่านี้จะมองไม่เห็นเลย")]
    [SerializeField, Min(0f)] private float _cameraFadeNear = 0.6f;
    [Tooltip("ควันที่อยู่ไกลกว่านี้จะเห็นเต็มๆ")]
    [SerializeField, Min(0f)] private float _cameraFadeFar = 2.2f;

    [Header("แสงวาบที่ปากกระบอก")]
    [SerializeField] private bool _flash = true;
    [SerializeField] private Color _flashColor = new Color(1f, 0.7f, 0.38f);
    [SerializeField, Min(0f)] private float _flashBrightness = 12f;
    [SerializeField, Min(0f)] private float _flashSize = 0.22f;
    [SerializeField, Min(0.01f)] private float _flashTime = 0.06f;
    [Tooltip("ไฟส่องรอบตัวตอนยิง (หน่วย Lumen) — 0 = ไม่มีไฟ")]
    [SerializeField, Min(0f)] private float _flashLight = 3000f;
    [SerializeField, Min(0f)] private float _flashLightRange = 8f;

    [Header("ควันดินปืน")]
    [SerializeField] private bool _muzzleSmoke = true;
    [SerializeField] private Color _smokeColor = new Color(0.62f, 0.6f, 0.56f);
    [SerializeField, Range(0f, 1f)] private float _smokeOpacity = 0.45f;
    [Tooltip("ฉากมืด ควันขาวจะดูเรืองแสง ลดตรงนี้ให้กลืนกับฉาก")]
    [SerializeField, Range(0f, 2f)] private float _smokeBrightness = 0.5f;
    [SerializeField, Range(0, 30)] private int _smokePuffs = 9;
    [SerializeField] private Vector2 _smokeSpeed = new Vector2(1.5f, 4.5f);
    [SerializeField] private Vector2 _smokeSize = new Vector2(0.35f, 0.7f);
    [Tooltip("ขยายตัวกี่เท่าก่อนหาย")]
    [SerializeField, Min(1f)] private float _smokeGrow = 2.6f;
    [SerializeField] private Vector2 _smokeLife = new Vector2(1.6f, 2.6f);

    [Header("สะเก็ดไฟ")]
    [SerializeField] private bool _sparks = true;
    [SerializeField] private Color _sparkColor = new Color(1f, 0.55f, 0.2f);
    [SerializeField, Min(0f)] private float _sparkBrightness = 6f;
    [SerializeField, Range(0, 60)] private int _sparkCount = 16;
    [SerializeField] private Vector2 _sparkSpeed = new Vector2(5f, 14f);
    [SerializeField] private Vector2 _sparkLife = new Vector2(0.12f, 0.4f);

    [Header("ควันตามแนวกระสุน")]
    [SerializeField] private bool _trail = true;
    [SerializeField, Min(0f)] private float _trailPerMeter = 1.5f;
    [SerializeField, Range(0, 120)] private int _trailMax = 50;
    [SerializeField, Range(0f, 1f)] private float _trailOpacity = 0.22f;
    [SerializeField] private Vector2 _trailSize = new Vector2(0.06f, 0.14f);
    [SerializeField] private Vector2 _trailLife = new Vector2(0.35f, 0.7f);

    [Header("จุดที่กระสุนโดน (กำแพง / พื้น)")]
    [SerializeField] private bool _impact = true;
    [SerializeField] private Color _dustColor = new Color(0.46f, 0.41f, 0.34f);
    [SerializeField, Range(0f, 1f)] private float _dustOpacity = 0.55f;
    [SerializeField, Range(0, 30)] private int _dustPuffs = 7;
    [SerializeField, Range(0, 40)] private int _impactSparks = 8;
    [SerializeField, Range(0, 40)] private int _debris = 10;

    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int ShapeId = Shader.PropertyToID("_Shape");
    private static readonly int NearId = Shader.PropertyToID("_Near_Fade");
    private static readonly int FarId = Shader.PropertyToID("_Far_Fade");

    private HitscanShooter _shooter;
    private bool _hasImpact;
    private Vector3 _impactPoint;
    private Vector3 _impactNormal;

    private void Awake() => _shooter = GetComponent<HitscanShooter>();

    private void OnEnable()
    {
        _shooter.raycastHit += HandleHit;
        _shooter.ShootCompleted += HandleShot;
    }

    private void OnDisable()
    {
        _shooter.raycastHit -= HandleHit;
        _shooter.ShootCompleted -= HandleShot;
    }

    // ศัตรูมีเอฟเฟกต์โดนของตัวเองอยู่แล้ว ฝุ่นขึ้นเฉพาะตอนโดนกำแพง / พื้น
    private void HandleHit(RaycastHit hit)
    {
        if (hit.collider == null || hit.collider.GetComponentInParent<IDamageable>() != null) return;
        _hasImpact = true;
        _impactPoint = hit.point;
        _impactNormal = hit.normal;
    }

    private void HandleShot(Vector3 origin, Vector3 end)
    {
        if (_material == null)
        {
            Debug.LogWarning("[MusketShotFX] ยังไม่ได้ใส่ Material", this);
            return;
        }

        Vector3 path = end - origin;
        float length = path.magnitude;
        Vector3 dir = length > 0.001f ? path / length : transform.forward;

        Camera cam = Camera.main;
        float camDist = cam != null ? Vector3.Distance(cam.transform.position, origin) : 0f;
        float smokeStart = Mathf.Max(0.15f, _smokeStartFromCamera - camDist);

        var root = new GameObject("MusketShotFX");
        root.transform.position = origin;
        float longest = 0.2f;

        if (_flash) longest = Mathf.Max(longest, Flash(root.transform, origin, dir));
        if (_sparks && _sparkCount > 0) longest = Mathf.Max(longest, Sparks(root.transform, origin, dir));
        if (_muzzleSmoke && _smokePuffs > 0) longest = Mathf.Max(longest, MuzzleSmoke(root.transform, origin, dir, smokeStart));
        if (_trail && length > smokeStart) longest = Mathf.Max(longest, Trail(root.transform, origin, dir, smokeStart, length));

        if (_impact && _hasImpact)
        {
            Vector3 normal = _impactNormal.sqrMagnitude > 0.01f ? _impactNormal : -dir;
            longest = Mathf.Max(longest, Impact(root.transform, _impactPoint, normal));
        }
        _hasImpact = false;

        Destroy(root, longest + 0.3f);
    }

    // ---------- ส่วนประกอบ ----------

    private float Flash(Transform root, Vector3 origin, Vector3 dir)
    {
        ParticleSystem ps = CreateSystem(root, "Flash", true, 1f, 0f, 0f, 0f, false);
        float[] offsets = { 0f, 0.06f, 0.14f };
        float[] sizes = { 1f, 0.75f, 0.45f };
        for (int i = 0; i < offsets.Length; i++)
        {
            Emit(ps, origin + dir * offsets[i], Vector3.zero, _flashSize * sizes[i] * Random.Range(0.85f, 1.15f),
                 _flashTime * Random.Range(0.8f, 1.2f), _flashColor);
        }
        SetLook(ps, Color.white * _flashBrightness, 1f, 0f, 0f);

        if (_flashLight > 0f)
        {
            var go = new GameObject("FlashLight");
            go.transform.SetParent(root, false);
            go.transform.position = origin + dir * 0.3f;
            Light light = go.AddComponent<Light>();
            if (!go.TryGetComponent(out HDAdditionalLightData _)) go.AddComponent<HDAdditionalLightData>();
            light.type = LightType.Point;
            light.color = _flashColor;
            light.range = _flashLightRange;
            light.intensity = _flashLight;
            light.shadows = LightShadows.None;
            go.AddComponent<ShotFlashLight>().Begin(light, _flashLight, _flashTime * 1.5f);
        }
        return _flashTime * 1.5f;
    }

    private float Sparks(Transform root, Vector3 origin, Vector3 dir)
    {
        ParticleSystem ps = CreateSystem(root, "Sparks", true, 1f, 1f, 0.5f, 0f, true);
        for (int i = 0; i < _sparkCount; i++)
        {
            Vector3 v = (dir + Random.insideUnitSphere * 0.3f).normalized * Random.Range(_sparkSpeed.x, _sparkSpeed.y);
            Emit(ps, origin + dir * 0.05f, v, Random.Range(0.008f, 0.02f),
                 Random.Range(_sparkLife.x, _sparkLife.y), _sparkColor);
        }
        SetLook(ps, Color.white * _sparkBrightness, 1f, 0f, 0f);
        return _sparkLife.y;
    }

    private float MuzzleSmoke(Transform root, Vector3 origin, Vector3 dir, float start)
    {
        ParticleSystem ps = CreateSystem(root, "MuzzleSmoke", false, _smokeGrow, -0.03f, 2.2f, 0.35f, false);
        Vector3 center = origin + dir * start;
        for (int i = 0; i < _smokePuffs; i++)
        {
            Vector3 pos = center + dir * Random.Range(0f, 0.6f) + Random.insideUnitSphere * 0.12f;
            Vector3 v = dir * Random.Range(_smokeSpeed.x, _smokeSpeed.y) + Random.insideUnitSphere * 0.4f + Vector3.up * 0.15f;
            Emit(ps, pos, v, Random.Range(_smokeSize.x, _smokeSize.y), Random.Range(_smokeLife.x, _smokeLife.y), _smokeColor);
        }
        SetLook(ps, new Color(_smokeBrightness, _smokeBrightness, _smokeBrightness, _smokeOpacity), 0f, _cameraFadeNear, _cameraFadeFar);
        return _smokeLife.y;
    }

    private float Trail(Transform root, Vector3 origin, Vector3 dir, float start, float length)
    {
        int count = Mathf.Min(_trailMax, Mathf.CeilToInt((length - start) * _trailPerMeter));
        if (count <= 0) return 0f;

        ParticleSystem ps = CreateSystem(root, "Trail", false, 1.8f, -0.02f, 1.5f, 0.15f, false);
        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Lerp(start, length, (i + Random.value) / count);
            Vector3 v = dir * Random.Range(0.1f, 0.5f) + Random.insideUnitSphere * 0.15f;
            Emit(ps, origin + dir * t, v, Random.Range(_trailSize.x, _trailSize.y), Random.Range(_trailLife.x, _trailLife.y), _smokeColor);
        }
        SetLook(ps, new Color(_smokeBrightness, _smokeBrightness, _smokeBrightness, _trailOpacity), 0f, _cameraFadeNear, _cameraFadeFar);
        return _trailLife.y;
    }

    private float Impact(Transform root, Vector3 point, Vector3 normal)
    {
        Vector3 at = point + normal * 0.04f;

        if (_dustPuffs > 0)
        {
            ParticleSystem dust = CreateSystem(root, "ImpactDust", false, 3f, 0.05f, 2.5f, 0.25f, false);
            for (int i = 0; i < _dustPuffs; i++)
            {
                Vector3 v = (normal + Random.insideUnitSphere * 0.6f).normalized * Random.Range(0.6f, 2.2f);
                Emit(dust, at + Random.insideUnitSphere * 0.05f, v, Random.Range(0.12f, 0.3f), Random.Range(0.8f, 1.5f), _dustColor);
            }
            SetLook(dust, new Color(_smokeBrightness, _smokeBrightness, _smokeBrightness, _dustOpacity), 0f, _cameraFadeNear, _cameraFadeFar);
        }

        if (_impactSparks > 0)
        {
            ParticleSystem sparks = CreateSystem(root, "ImpactSparks", true, 1f, 1f, 0.5f, 0f, true);
            for (int i = 0; i < _impactSparks; i++)
            {
                Vector3 v = (normal + Random.insideUnitSphere * 0.8f).normalized * Random.Range(2f, 6f);
                Emit(sparks, at, v, Random.Range(0.006f, 0.014f), Random.Range(0.08f, 0.25f), _sparkColor);
            }
            SetLook(sparks, Color.white * _sparkBrightness, 1f, 0f, 0f);
        }

        if (_debris > 0)
        {
            ParticleSystem debris = CreateSystem(root, "Debris", false, 1f, 1.5f, 0.3f, 0f, false);
            for (int i = 0; i < _debris; i++)
            {
                Vector3 v = (normal + Random.insideUnitSphere * 0.9f).normalized * Random.Range(1.5f, 4.5f);
                Emit(debris, at, v, Random.Range(0.02f, 0.05f), Random.Range(0.5f, 1f), _dustColor * 0.45f);
            }
            SetLook(debris, new Color(1f, 1f, 1f, 1f), 0f, 0f, 0f);
        }
        return 1.5f;
    }

    // ---------- เครื่องมือ ----------

    // grow = ขยายกี่เท่าก่อนหาย, gravity ติดลบ = ลอยขึ้น, drag = ช้าลงเร็วแค่ไหน, noise = ส่ายไปมา
    private ParticleSystem CreateSystem(Transform root, string name, bool glow, float grow, float gravity, float drag, float noise, bool stretch)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 5f;
        main.startSpeed = 0f;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 256;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            glow
                ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;

        if (grow > 1.001f)
        {
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 1f / grow, 0f, 2.5f), new Keyframe(1f, 1f, 0.2f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(grow, curve);
        }

        if (drag > 0f)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = true;
        }

        if (noise > 0f)
        {
            ParticleSystem.NoiseModule n = ps.noise;
            n.enabled = true;
            n.strength = noise;
            n.frequency = 0.6f;
            n.scrollSpeed = 0.3f;
            n.damping = true;
            n.quality = ParticleSystemNoiseQuality.Medium;
        }

        if (!glow)
        {
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        }

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _material;
        renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        if (stretch)
        {
            renderer.velocityScale = 0.02f;
            renderer.lengthScale = 1.5f;
        }
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 3f;

        ps.Play();
        return ps;
    }

    private static void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, float size, float life, Color color)
    {
        color.a = 1f;
        var p = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startSize = size,
            startLifetime = life,
            startColor = color,
            rotation = Random.Range(0f, 360f),
            applyShapeToPosition = false
        };
        ps.Emit(p, 1);
    }

    private static void SetLook(ParticleSystem ps, Color tint, float shape, float near, float far)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor(TintId, tint);
        block.SetFloat(ShapeId, shape);
        block.SetFloat(NearId, near);
        block.SetFloat(FarId, far);
        ps.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(block);
    }
}

// ไฟวาบตอนยิง ค่อยๆ ดับแล้วลบตัวเอง
public class ShotFlashLight : MonoBehaviour
{
    private Light _light;
    private float _start;
    private float _time;
    private float _elapsed;

    public void Begin(Light light, float intensity, float time)
    {
        _light = light;
        _start = intensity;
        _time = Mathf.Max(0.01f, time);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _time);
        if (_light != null) _light.intensity = _start * (1f - t) * (1f - t);
        if (t >= 1f) Destroy(gameObject);
    }
}
