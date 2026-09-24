using UnityEngine;
using UnityEngine.Serialization;
using Effekseer;

public class PlayVFXOnSpawn : MonoBehaviour
{
    public enum RotationMode
    {
        /// <summary>หันตามของชิ้นนี้ — ไฟปากกระบอกควรใช้อันนี้ จะได้พุ่งไปทางที่ยิง</summary>
        FollowThisObject,

        /// <summary>ไม่หมุน ใช้ท่าที่คนทำเอฟเฟกต์ออกแบบมา</summary>
        None,

        /// <summary>หันตามกล้อง</summary>
        FaceCamera
    }

    [Header("VFX แบบ Effekseer")]
    [Tooltip("ขนาดปรับที่ช่อง Scale ของตัว asset เอง ไม่ใช่ที่นี่")]
    [SerializeField] private EffekseerEffectAsset _effekseerEffect;

    [Header("VFX แบบ Prefab ของ Unity")]
    [Tooltip("Prefab ที่มี ParticleSystem หรือ VFX Graph อยู่ข้างใน")]
    [SerializeField] private GameObject _vfxPrefab;

    [Header("Options (ใช้ได้ทั้ง Effekseer และ Prefab)")]
    [Tooltip("ให้ VFX วิ่งตามของชิ้นนี้\n\n" +
        "ไฟปากกระบอก: **ปิด** — แสงควรค้างอยู่ที่ปากกระบอก ไม่วิ่งตามกระสุนไป\n" +
        "เอฟเฟกต์ที่ห่อตัวกระสุน (หางไฟ): เปิด")]
    [SerializeField] private bool _followObject = false;

    [Tooltip("ลบทิ้งหลังกี่วินาที — ต้องยาวกว่าตัวเอฟเฟกต์\n" +
        "Prefab: 0 = ไม่ลบให้ / Effekseer ที่ติ๊กวิ่งตาม: 0 = 5 วินาที")]
    [FormerlySerializedAs("_prefabLifetime")]   // ชื่อเดิม — กันค่าที่ตั้งไว้หาย
    [SerializeField] private float _lifetime = 1f;

    [Tooltip("ใช้ตำแหน่งนี้แทนตำแหน่งตัวเอง — เว้นว่างได้")]
    [SerializeField] private Transform _customOrigin;

    [Tooltip("ขยับจากจุดกำเนิด (พิกัดท้องถิ่นของจุดนั้น)\nZ บวก = ดันไปข้างหน้า ออกจากปากกระบอก")]
    [SerializeField] private Vector3 _localOffset = Vector3.zero;

    [Header("ทิศทาง")]
    [SerializeField] private RotationMode _rotationMode = RotationMode.FollowThisObject;

    [Tooltip("หมุนเพิ่ม (องศา) ปรับทีละ 90 ก่อน: Y=90 / Y=180 / X=90 / X=-90")]
    [SerializeField] private Vector3 _rotationOffset = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool _logPlay = false;

    // OnEnable ไม่ใช่ Start เพราะเผื่อวันหน้าเปลี่ยนไปใช้พูลแทน Instantiate
    // ของจากพูลจะไม่เรียก Start ซ้ำ แต่เรียก OnEnable ทุกครั้งที่ถูกปลุก
    private void OnEnable()
    {
        if (_logPlay)
        {
            Debug.Log(
                $"[SpawnVFX] {name} → Effekseer: {(_effekseerEffect != null ? _effekseerEffect.name : "-")} | " +
                $"Prefab: {(_vfxPrefab != null ? _vfxPrefab.name : "-")}", this);
        }

        Transform origin = _customOrigin != null ? _customOrigin : transform;
        Vector3 position = origin.position + origin.rotation * _localOffset;
        Quaternion rotation = BuildRotation(origin);

        PlayEffekseer(origin, position, rotation);
        SpawnPrefab(origin, position, rotation);
    }

    private void PlayEffekseer(Transform origin, Vector3 position, Quaternion rotation)
    {
        if (_effekseerEffect == null) return;

        if (_followObject)
        {
            EffekseerAttach.Play(_effekseerEffect, origin, position, rotation, _lifetime);
            return;
        }

        var handle = EffekseerSystem.PlayEffect(_effekseerEffect, position);
        handle.SetRotation(rotation);
    }

    private void SpawnPrefab(Transform origin, Vector3 position, Quaternion rotation)
    {
        if (_vfxPrefab == null) return;

        GameObject spawned = _followObject
            ? Instantiate(_vfxPrefab, position, rotation, origin)
            : Instantiate(_vfxPrefab, position, rotation);

        if (_lifetime > 0f)
            Destroy(spawned, _lifetime);
    }

    private Quaternion BuildRotation(Transform origin)
    {
        Quaternion baseRotation;

        switch (_rotationMode)
        {
            case RotationMode.FollowThisObject:
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