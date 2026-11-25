using System.Collections;
using UnityEngine;

// คุมแสงหลักของฉาก (Directional Light) ด้วย GlobalLightProfileSO
//
// วิธีใช้:
//   1) ใส่สคริปต์นี้ที่ Directional Light ของฉาก (หรือที่ไหนก็ได้แล้วลาก Light ใส่ช่อง)
//   2) Trigger Zone Events → On Enter → GlobalLightController.ApplyProfile แล้วลาก Profile ใส่ช่อง
//      ออกจากโซนแล้วอยากให้กลับค่าเดิม: On Exit → GlobalLightController.ResetToDefault
//
// ค่าเดิมตอนเริ่มฉาก (ที่ตั้งใน Inspector ของ Light) ถูกจำไว้ ResetToDefault จะกลับไปค่านั้น
public class GlobalLightController : MonoBehaviour
{
    [Tooltip("Directional Light ที่จะคุม — เว้นว่าง = ใช้ Light บนตัวเอง หรือ Sun ของฉาก")]
    [SerializeField] private Light _light;

    [Tooltip("เวลาเปลี่ยนตอน ResetToDefault (วินาที)")]
    [SerializeField, Min(0f)] private float _resetTime = 2f;

    [Header("Debug")]
    [SerializeField] private bool _log;

    private struct State
    {
        public float intensity;
        public Color filter;
        public bool useTemperature;
        public float temperature;
    }

    private State _default;
    private Coroutine _transition;

    public GlobalLightProfileSO CurrentProfile { get; private set; }

    private void Awake()
    {
        if (_light == null) _light = GetComponent<Light>();
        if (_light == null) _light = RenderSettings.sun;
        if (_light == null)
        {
            Debug.LogWarning($"[GlobalLight] {name} หา Directional Light ไม่เจอ — ลาก Light ใส่ช่อง Light", this);
            enabled = false;
            return;
        }

        _default = Read();
    }

    // Trigger Zone Events → On Enter → ApplyProfile (ลาก Profile ใส่ช่อง)
    public void ApplyProfile(GlobalLightProfileSO profile) => ApplyProfile(profile, false);

    // instant = true → เปลี่ยนทันทีไม่เฟด (เช่น หลังวาร์ป)
    public void ApplyProfile(GlobalLightProfileSO profile, bool instant)
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
        }, duration);

        if (_log) Debug.Log($"[GlobalLight] → {profile.name} ({duration}s)", this);
    }

    // On Exit → ResetToDefault — กลับไปค่าที่ตั้งไว้ในฉาก
    public void ResetToDefault()
    {
        if (_light == null) return;

        CurrentProfile = null;
        StartTransition(_default, _resetTime);

        if (_log) Debug.Log("[GlobalLight] → ค่าเดิมของฉาก", this);
    }

    // สำหรับ UnityEvent<GameObject> (TriggerZoneEvents) ที่เลือกแบบ Dynamic
    public void ResetToDefault(GameObject _) => ResetToDefault();

    // ---------- ใช้กับ GlobalLightSaveable ----------

    // ค่าแสงตอนนี้ (ถ้ากำลังเฟดอยู่ = ค่าระหว่างทาง)
    public bool TryGetCurrent(out float intensity, out Color filter, out bool useTemperature, out float temperature)
    {
        intensity = 0f; filter = Color.white; useTemperature = false; temperature = 6500f;
        if (_light == null) return false;

        State s = Read();
        intensity = s.intensity;
        filter = s.filter;
        useTemperature = s.useTemperature;
        temperature = s.temperature;
        return true;
    }

    // ตั้งค่าทันที ไม่เฟด — ใช้ตอน Load
    public void SetInstant(float intensity, Color filter, bool useTemperature, float temperature)
    {
        if (_light == null) return;

        if (_transition != null)
        {
            StopCoroutine(_transition);
            _transition = null;
        }

        Write(new State
        {
            intensity = intensity,
            filter = filter,
            useTemperature = useTemperature,
            temperature = temperature,
        });
    }

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
    };

    private void Write(State s)
    {
        _light.intensity = s.intensity;
        _light.color = s.filter;
        _light.useColorTemperature = s.useTemperature;
        _light.colorTemperature = s.temperature;
    }

    // รวม Temperature เข้าไปในสี Filter แล้วปิด Temperature — ไว้ lerp ระหว่างโหมดที่ต่างกัน
    private static State Flatten(State s)
    {
        if (!s.useTemperature) return s;
        Color tint = Mathf.CorrelatedColorTemperatureToRGB(s.temperature);
        return new State
        {
            intensity = s.intensity,
            filter = s.filter * tint,
            useTemperature = false,
            temperature = s.temperature,
        };
    }
}
