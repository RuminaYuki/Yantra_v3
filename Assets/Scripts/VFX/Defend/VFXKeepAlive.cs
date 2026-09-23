using UnityEngine;
using UnityEngine.VFX;

public class VFXKeepAlive : MonoBehaviour
{
    [Tooltip("ถ้าไม่มีใครต่ออายุให้เกินกี่วินาที ให้เริ่มปิดตัวเอง\n" +
        "สั้นเกิน = กระพริบตอน state สะดุด / ยาวเกิน = ค้างหลังปล่อยปุ่ม")]
    [SerializeField] private float _graceSeconds = 0.15f;

    [Tooltip("หลังสั่งหยุดพ่นอนุภาคแล้ว รออีกกี่วินาทีถึงลบ GameObject ทิ้ง\n" +
        "ต้องยาวพอให้อนุภาคชุดสุดท้ายเล่นจนจบ ไม่งั้นจะโดนตัดกลางคันอยู่ดี\n" +
        "สั้นเกิน = ยังตัดอยู่ / ยาวเกิน = GameObject เปล่าๆ ค้างในซีน (ไม่เปลืองเท่าไหร่)")]
    [SerializeField] private float _tailSeconds = 1.5f;

    private float _lastPing;
    private bool _running;

    public void Configure(float graceSeconds, float tailSeconds)
    {
        _graceSeconds = Mathf.Max(0.02f, graceSeconds);
        _tailSeconds = Mathf.Max(0f, tailSeconds);
        _running = true;

        Ping();
    }

    /// <summary>ต่ออายุ — เรียกทุกเฟรมที่ยังต้องการให้อยู่ต่อ</summary>
    public void Ping() => _lastPing = Time.time;

    private void OnEnable() => Ping();

    private void Update()
    {
        if (!_running) return;
        if (Time.time - _lastPing <= _graceSeconds) return;

        Finish();
    }

    private void Finish()
    {
        _running = false;

        StopEmitters();

        // ลบ GameObject ทีหลัง รอให้อนุภาคชุดสุดท้ายเล่นจนจบก่อน
        Destroy(gameObject, _tailSeconds);

        // ลบ component นี้ทันที เพื่อให้ฝั่งที่เรียกใช้รู้ว่า "ตัวนี้เลิกใช้แล้ว"
        // ถ้าไม่ลบ พอผู้เล่นกางโล่ใหม่ระหว่างที่ตัวเก่ายังรอลบอยู่
        // มันจะเห็นว่ายังมีของอยู่แล้วไม่สร้างใหม่ ทำให้โล่ไม่ขึ้นทั้งที่กดอยู่
        Destroy(this);
    }

    /// <summary>
    /// สั่งหยุดพ่นอนุภาค รองรับทั้ง VFX Graph และ ParticleSystem
    /// เพราะในทีมมีคนทำ VFX ด้วยเครื่องมือต่างกัน
    /// </summary>
    private void StopEmitters()
    {
        foreach (VisualEffect vfx in GetComponentsInChildren<VisualEffect>(true))
        {
            if (vfx != null) vfx.Stop();
        }

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}