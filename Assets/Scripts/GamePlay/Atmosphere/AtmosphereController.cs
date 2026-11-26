using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// คุมบรรยากาศของฉากด้วย AtmosphereProfileSO: แสงหลัก (Directional Light) + Fog Maximum Height ของ Global Volume
//
// วิธีใช้:
//   1) ใส่สคริปต์นี้ที่ Directional Light ของฉาก (หรือที่ไหนก็ได้แล้วลาก Light ใส่ช่อง)
//   2) Volume เว้นว่างได้ — หา Global Volume ที่มี Fog เอง
//   3) วาง AtmosphereZone ในฉาก หรือเรียก ApplyProfile จาก UnityEvent
//
// ค่าเดิมตอนเริ่มฉาก (ที่ตั้งใน Inspector) ถูกจำไว้ ResetToDefault จะกลับไปค่านั้น
// Fog แก้ที่สำเนาของ Volume Profile ตอนเล่น — ไฟล์ Volume Profile จริงไม่ถูกแก้
public class AtmosphereController : MonoBehaviour
{
    [Tooltip("Directional Light ที่จะคุม — เว้นว่าง = ใช้ Light บนตัวเอง หรือ Sun ของฉาก")]
    [SerializeField] private Light _light;

    [Tooltip("Global Volume ที่มี Fog — เว้นว่าง = หาเอง (Global ที่ Priority สูงสุดและมี Fog)")]
    [SerializeField] private Volume _volume;

    [Tooltip("เวลาเปลี่ยนตอน ResetToDefault (วินาที)")]
    [SerializeField, Min(0f)] private float _resetTime = 2f;

    [Header("Debug")]
    [SerializeField] private bool _log;

    public struct State
    {
        public float intensity;
        public Color filter;
        public bool useTemperature;
        public float temperature;
        public bool hasFog;            // false = ไม่แตะหมอก
        public float fogMaximumHeight;
    }

    private State _default;
    private Coroutine _transition;
    private Fog _fog;   // Fog ในสำเนา Volume Profile ของฉากนี้ (null = ไม่มีหมอกให้คุม)

    public AtmosphereProfileSO CurrentProfile { get; private set; }

    private void Awake()
    {
        if (_light == null) _light = GetComponent<Light>();
        if (_light == null) _light = RenderSettings.sun;
        if (_light == null)
        {
            Debug.LogWarning($"[Atmosphere] {name} หา Directional Light ไม่เจอ — ลาก Light ใส่ช่อง Light", this);
            enabled = false;
            return;
        }

        SetupFog();
        _default = Read();
    }

    private void SetupFog()
    {
        if (_volume == null) _volume = FindGlobalFogVolume();
        if (_volume == null)
        {
            Debug.LogWarning($"[Atmosphere] {name} หา Global Volume ที่มี Fog ไม่เจอ — จะคุมแค่แสง", this);
            return;
        }

        // .profile = สำเนาสำหรับตอนเล่น ไม่ไปแก้ไฟล์ Volume Profile จริง (ถ้าใช้ sharedProfile ค่าจะค้างในไฟล์หลังกด Stop)
        if (!_volume.profile.TryGet(out _fog))
        {
            Debug.LogWarning($"[Atmosphere] Volume {_volume.name} ไม่มี Fog override — จะคุมแค่แสง", _volume);
            return;
        }

        _fog.maximumHeight.overrideState = true;
    }

    private static Volume FindGlobalFogVolume()
    {
        Volume best = null;
        foreach (Volume v in FindObjectsByType<Volume>())
        {
            if (!v.isGlobal || v.sharedProfile == null || !v.sharedProfile.Has<Fog>()) continue;
            if (best == null || v.priority > best.priority) best = v;
        }
        return best;
    }

    // Trigger Zone Events → On Enter → ApplyProfile (ลาก Profile ใส่ช่อง)
    public void ApplyProfile(AtmosphereProfileSO profile) => ApplyProfile(profile, false);

    // instant = true → เปลี่ยนทันทีไม่เฟด (เช่น หลังวาร์ป)
    public void ApplyProfile(AtmosphereProfileSO profile, bool instant)
    {
        if (profile == null || _light == null) return;

        float duration = instant ? 0f : profile.transitionTime;
        CurrentProfile = profile;
        StartTransition(new State
        {
            intensity = profile.intensity,
            filter = profile.filter,
            useTemperature = profile.useTemperature,
            temperature = profile.temperature,
            hasFog = profile.useFog,
            fogMaximumHeight = profile.fogMaximumHeight,
        }, duration);

        if (_log) Debug.Log($"[Atmosphere] → {profile.name} ({duration}s)", this);
    }

    // On Exit → ResetToDefault — กลับไปค่าที่ตั้งไว้ในฉาก
    public void ResetToDefault()
    {
        if (_light == null) return;

        CurrentProfile = null;
        StartTransition(_default, _resetTime);

        if (_log) Debug.Log("[Atmosphere] → ค่าเดิมของฉาก", this);
    }

    // สำหรับ UnityEvent<GameObject> (TriggerZoneEvents) ที่เลือกแบบ Dynamic
    public void ResetToDefault(GameObject _) => ResetToDefault();

    // ---------- ใช้กับ AtmosphereSaveable ----------

    // ค่าตอนนี้ (ถ้ากำลังเฟดอยู่ = ค่าระหว่างทาง)
    public bool TryGetCurrent(out State state)
    {
        state = default;
        if (_light == null) return false;
        state = Read();
        return true;
    }

    // ตั้งค่าทันที ไม่เฟด — ใช้ตอน Load
    public void SetInstant(State state)
    {
        if (_light == null) return;

        if (_transition != null)
        {
            StopCoroutine(_transition);
            _transition = null;
        }

        Write(state);
    }

    // ---------- ภายใน ----------

    private void StartTransition(State target, float duration)
    {
        if (_transition != null) StopCoroutine(_transition);

        if (duration <= 0f || !isActiveAndEnabled)
        {
            Write(target);
            return;
        }

        _transition = StartCoroutine(Transition(Read(), target, duration));
    }

    private IEnumerator Transition(State from, State to, float duration)
    {
        State final = to;

        // Temperature เปิด/ปิดไม่ตรงกัน: รวม Temperature เข้าไปในสีก่อนค่อย lerp ไม่งั้นสีกระโดดตอนสลับโหมด
        if (from.useTemperature != to.useTemperature)
        {
            from = Flatten(from);
            to = Flatten(to);
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float k = Mathf.SmoothStep(0f, 1f, t);

            Write(new State
            {
                intensity = Mathf.Lerp(from.intensity, to.intensity, k),
                filter = Color.Lerp(from.filter, to.filter, k),
                useTemperature = to.useTemperature,
                temperature = Mathf.Lerp(from.temperature, to.temperature, k),
                hasFog = to.hasFog,
                fogMaximumHeight = Mathf.Lerp(from.fogMaximumHeight, to.fogMaximumHeight, k),
            });
            yield return null;
        }

        Write(final);   // จบที่ค่าของ Profile เป๊ะ (รวมถึงสถานะเปิด/ปิด Temperature)
        _transition = null;
    }

    private State Read() => new State
    {
        intensity = _light.intensity,
        filter = _light.color,
        useTemperature = _light.useColorTemperature,
        temperature = _light.colorTemperature,
        hasFog = _fog != null,
        fogMaximumHeight = _fog != null ? _fog.maximumHeight.value : 0f,
    };

    private void Write(State s)
    {
        _light.intensity = s.intensity;
        _light.color = s.filter;
        _light.useColorTemperature = s.useTemperature;
        _light.colorTemperature = s.temperature;

        if (s.hasFog && _fog != null)
            _fog.maximumHeight.value = s.fogMaximumHeight;
    }

    // รวม Temperature เข้าไปในสี Filter แล้วปิด Temperature — ไว้ lerp ระหว่างโหมดที่ต่างกัน
    private static State Flatten(State s)
    {
        if (!s.useTemperature) return s;
        s.filter *= Mathf.CorrelatedColorTemperatureToRGB(s.temperature);
        s.useTemperature = false;
        return s;
    }
}
