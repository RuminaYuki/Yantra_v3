using UnityEngine;

/// <summary>
/// กองเลือดบนพื้น: ค่อย ๆ แผ่ออก → ค้างไว้ → หดหายแล้วลบตัวเอง
/// FleshImpactBlood ใส่ให้เองตอนเกิดกองเลือด ไม่ต้องแปะเอง
///
/// แผ่ / หดเฉพาะแกน X กับ Z (ตามพื้น) — แกน Y (ความหนา / ความลึก decal) คงเดิม
/// ถ้ากองเลือดเป็น Decal Projector ให้ตั้ง Scale Mode = Inherit From Hierarchy ไม่งั้นจะไม่แผ่
/// </summary>
[DisallowMultipleComponent]
public class BloodPoolFade : MonoBehaviour
{
    private const float StartSize = 0.15f;   // ตอนเพิ่งตก — เล็กนิดเดียวแล้วค่อยแผ่

    private Vector3 _fullScale;
    private float _growTime;
    private float _stayTime;
    private float _shrinkTime;
    private float _age;
    private bool _running;

    public void Begin(float growTime, float stayTime, float shrinkTime)
    {
        _fullScale = transform.localScale;
        _growTime = Mathf.Max(0.01f, growTime);
        _stayTime = Mathf.Max(0f, stayTime);
        _shrinkTime = Mathf.Max(0.01f, shrinkTime);
        _age = 0f;
        _running = true;
        Apply(StartSize);
    }

    private void Update()
    {
        if (!_running) return;

        _age += Time.deltaTime;

        if (_age < _growTime)
        {
            // แผ่เร็วตอนแรกแล้วค่อยช้าลง เหมือนของเหลวไหลออก
            float t = _age / _growTime;
            Apply(Mathf.Lerp(StartSize, 1f, 1f - (1f - t) * (1f - t)));
        }
        else if (_age < _growTime + _stayTime)
        {
            Apply(1f);
        }
        else if (_age < _growTime + _stayTime + _shrinkTime)
        {
            float t = (_age - _growTime - _stayTime) / _shrinkTime;
            Apply(1f - t * t);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Apply(float k)
    {
        transform.localScale = new Vector3(_fullScale.x * k, _fullScale.y, _fullScale.z * k);
    }
}
