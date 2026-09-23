using UnityEngine;
using Effekseer;

public class PlayVFXOnEventChannel : MonoBehaviour
{
    /// <summary>
    /// ช่อง Float ยิงทุกครั้งที่ค่าเปลี่ยน แต่เรามักอยากได้แค่บางทิศทาง
    /// เช่นหลอดเกราะ — อยากได้เอฟเฟกต์ตอนโดนตี (ลด) ไม่ใช่ตอนฟื้น (เพิ่ม)
    /// </summary>
    public enum FloatCondition
    {
        /// <summary>ยิงทุกครั้งที่ค่าเปลี่ยน</summary>
        Always,

        /// <summary>เฉพาะตอนค่าลดลง — บล็อกโดน / เลือดลด</summary>
        WhenDecreased,

        /// <summary>เฉพาะตอนค่าเพิ่มขึ้น — ฟื้นเกราะ / ได้เลือดคืน</summary>
        WhenIncreased,

        /// <summary>เฉพาะตอนค่า ≤ Threshold — เตือนใกล้ตาย</summary>
        WhenAtOrBelowThreshold,

        /// <summary>เฉพาะตอนค่า ≥ Threshold — เต็มแล้วพร้อมใช้</summary>
        WhenAtOrAboveThreshold
    }

    public enum RotationMode
    {
        /// <summary>ไม่หมุน ใช้ท่าที่คนทำเอฟเฟกต์ออกแบบมา — ลองอันนี้ก่อนเสมอ</summary>
        None,

        /// <summary>หันตามจุดกำเนิด</summary>
        FollowOrigin,

        /// <summary>หันตามกล้อง — มักถูกสำหรับ FPP</summary>
        FaceCamera
    }

    [Header("ฟังช่องไหน (ใส่ช่องเดียวหรือทั้งคู่ก็ได้)")]
    [Tooltip("ช่องแบบไม่มีค่า เช่น PlayerOnGuardPointDepleted_VEC")]
    [SerializeField] private VoidEventChannelSO _voidChannel;

    [Tooltip("ช่องแบบส่งตัวเลขมาด้วย เช่น PlayerOnCurrentGuardPointChange_Fl")]
    [SerializeField] private FloatEventChannelSO _floatChannel;

    [Header("กรองค่า (ใช้เฉพาะช่อง Float)")]
    [Tooltip("บล็อกโดน → เลือก WhenDecreased เพราะเกราะลดตอนโดนตี\n" +
        "ครั้งแรกที่ยิงจะยังไม่มีค่าเก่าให้เทียบ ระบบจะข้ามให้เอง ไม่เด้งมั่ว")]
    [SerializeField] private FloatCondition _floatCondition = FloatCondition.Always;

    [Tooltip("ใช้เฉพาะโหมด AtOrBelow / AtOrAbove")]
    [SerializeField] private float _threshold = 0f;

    [Header("VFX แบบ Effekseer")]
    [Tooltip("ขนาดปรับที่ช่อง Scale ของตัว asset เอง ไม่ใช่ที่นี่")]
    [SerializeField] private EffekseerEffectAsset _effekseerEffect;

    [Header("VFX แบบ Prefab ของ Unity")]
    [Tooltip("Prefab ที่มี ParticleSystem หรือ VFX Graph อยู่ข้างใน\n" +
        "ใช้กับ VFX ที่คนในทีมทำในยูนิตี้เอง เช่น VFX โล่")]
    [SerializeField] private GameObject _vfxPrefab;

    [Tooltip("ทำลาย prefab ทิ้งหลังกี่วินาที\n" +
        "0 = ไม่ทำลายให้ (ใช้เมื่อ prefab จัดการลบตัวเองอยู่แล้ว เช่นติ๊ก Stop Action = Destroy)\n" +
        "ถ้าไม่แน่ใจใส่ 3 ไว้ก่อน ปล่อยไว้จะกองสะสมจนเกมหน่วง")]
    [SerializeField] private float _prefabLifetime = 3f;

    [Tooltip("ให้ prefab เป็นลูกของจุดกำเนิด = วิ่งตามตัวละคร\n" +
        "เปิดสำหรับเอฟเฟกต์ที่ห่อตัว (โล่) / ปิดสำหรับเอฟเฟกต์ปักที่ (กระเด็น)")]
    [SerializeField] private bool _parentPrefabToOrigin = false;

    [Header("ตำแหน่งและทิศทาง")]
    [Tooltip("เว้นว่าง = ใช้ตำแหน่งของ GameObject นี้")]
    [SerializeField] private Transform _origin;

    [Tooltip("ขยับจากจุดกำเนิด (พิกัดโลก) Y บวก = ขึ้นบนเสมอ")]
    [SerializeField] private Vector3 _worldOffset = Vector3.zero;

    [SerializeField] private RotationMode _rotationMode = RotationMode.None;

    [Tooltip("หมุนเพิ่ม (องศา) ปรับทีละ 90 ก่อน: Y=90 / Y=180 / X=90 / X=-90")]
    [SerializeField] private Vector3 _rotationOffset = Vector3.zero;

    [Header("Options")]
    [Tooltip("เว้นอย่างน้อยกี่วินาทีก่อนเล่นซ้ำ\n\n" +
        "⚠️ ช่องโล่แตกต้องใส่อย่างน้อย 2 วินาที\n" +
        "เพราะ BlockSystem ยิง Raise() ทุกเฟรมตอนเกราะหมดแล้วยังกดบล็อกค้าง\n" +
        "ถ้าไม่กัน เอฟเฟกต์จะเด้ง 60 ตัวต่อวินาทีจนเกมค้าง")]
    [SerializeField] private float _minInterval = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool _logPlays = false;

    private float _lastPlayTime = -999f;
    private float _lastValue;
    private bool _hasLastValue;

    private void OnEnable()
    {
        _lastPlayTime = -999f;
        _hasLastValue = false;

        if (_voidChannel != null) _voidChannel.Raised += HandleVoid;
        if (_floatChannel != null) _floatChannel.Raised += HandleFloat;

#if UNITY_EDITOR
        if (_voidChannel == null && _floatChannel == null)
            Debug.LogWarning("[EventVFX] ยังไม่ได้ลาก Event Channel มาใส่ — จะไม่เกิดอะไรเลย", this);

        if (_effekseerEffect == null && _vfxPrefab == null)
            Debug.LogWarning("[EventVFX] ยังไม่ได้ใส่ VFX ทั้งสองช่อง — จะไม่เกิดอะไรเลย", this);
#endif
    }

    /// <summary>
    /// ถอดออกเสมอ
    ///
    /// โปรเจกต์เราปิด Domain Reload ไว้ event ใน ScriptableObject จึงไม่ถูกล้างตอนกด Stop
    /// ถ้าไม่ถอด กด Play รอบสองจะมีตัวสมัครค้างจากรอบแรก ยิงทีเดียวเด้ง 2 ชุดซ้อน
    /// </summary>
    private void OnDisable()
    {
        if (_voidChannel != null) _voidChannel.Raised -= HandleVoid;
        if (_floatChannel != null) _floatChannel.Raised -= HandleFloat;
    }

    private void HandleVoid() => Fire("void");

    private void HandleFloat(float value)
    {
        bool passed = PassesCondition(value);

        _lastValue = value;
        _hasLastValue = true;

        if (passed) Fire(value.ToString("0.##"));
    }

    private bool PassesCondition(float value)
    {
        switch (_floatCondition)
        {
            case FloatCondition.WhenDecreased:
                // ครั้งแรกไม่มีค่าเก่าให้เทียบ ข้ามไปก่อนดีกว่าเดามั่ว
                return _hasLastValue && value < _lastValue;

            case FloatCondition.WhenIncreased:
                return _hasLastValue && value > _lastValue;

            case FloatCondition.WhenAtOrBelowThreshold:
                return value <= _threshold;

            case FloatCondition.WhenAtOrAboveThreshold:
                return value >= _threshold;

            default:
                return true;
        }
    }

    private void Fire(string debugValue)
    {
        if (_minInterval > 0f && Time.time - _lastPlayTime < _minInterval) return;
        _lastPlayTime = Time.time;

        Transform origin = _origin != null ? _origin : transform;
        Vector3 position = origin.position + _worldOffset;
        Quaternion rotation = BuildRotation(origin);

        if (_logPlays)
        {
            Debug.Log(
                $"[EventVFX] {debugValue} → " +
                $"Effekseer: {(_effekseerEffect != null ? _effekseerEffect.name : "-")} | " +
                $"Prefab: {(_vfxPrefab != null ? _vfxPrefab.name : "-")}", this);
        }

        PlayEffekseer(position, rotation);
        SpawnPrefab(origin, position, rotation);
    }

    private void PlayEffekseer(Vector3 position, Quaternion rotation)
    {
        if (_effekseerEffect == null) return;

        var handle = EffekseerSystem.PlayEffect(_effekseerEffect, position);
        handle.SetRotation(rotation);
    }

    private void SpawnPrefab(Transform origin, Vector3 position, Quaternion rotation)
    {
        if (_vfxPrefab == null) return;

        GameObject spawned = _parentPrefabToOrigin
            ? Instantiate(_vfxPrefab, position, rotation, origin)
            : Instantiate(_vfxPrefab, position, rotation);

        // ไม่ทำลายให้ = prefab ต้องลบตัวเอง ไม่งั้นมันจะกองสะสมทุกครั้งที่ event ยิง
        if (_prefabLifetime > 0f)
            Destroy(spawned, _prefabLifetime);
    }

    private Quaternion BuildRotation(Transform origin)
    {
        Quaternion baseRotation;

        switch (_rotationMode)
        {
            case RotationMode.FollowOrigin:
                baseRotation = origin.rotation;
                break;

            case RotationMode.FaceCamera:
                Camera cam = Camera.main;
                baseRotation = cam != null ? cam.transform.rotation : Quaternion.identity;
                break;

            default:
                baseRotation = Quaternion.identity;
                break;
        }

        return baseRotation * Quaternion.Euler(_rotationOffset);
    }
}