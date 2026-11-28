using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// เปิดตัวนี้ทีไร เอฟเฟคข้างในเล่นใหม่ตั้งแต่ต้น — ของที่โผล่ซ้ำ ๆ จาก pool / SplineItemCache
/// (หนามตานี + ฝุ่นดิน GroundBurst, กระสุนตานี + หางแสง)
/// ล้างหาง Trail ด้วย ไม่งั้นหยิบออกจาก pool แล้วหางลากเส้นมาจากที่เดิม
/// ไม่ต้องตั้งอะไร ใส่ไว้บนตัวบนสุดของ prefab พอ
/// </summary>
public class RestartEffectsOnEnable : MonoBehaviour
{
    private ParticleSystem[] _particles;
    private VisualEffect[] _vfx;
    private TrailRenderer[] _trails;

    private void Awake()
    {
        _particles = GetComponentsInChildren<ParticleSystem>(true);
        _vfx = GetComponentsInChildren<VisualEffect>(true);
        _trails = GetComponentsInChildren<TrailRenderer>(true);
    }

    private void OnEnable()
    {
        foreach (TrailRenderer trail in _trails)
            if (trail != null) trail.Clear();

        // ทีละตัว ไม่ลูก (false) — Play(true) จะสั่งลูกซ้ำกับที่สั่งตัวลูกเองอีกรอบ
        foreach (ParticleSystem ps in _particles)
        {
            if (ps == null || !ps.gameObject.activeInHierarchy) continue;
            ps.Clear(false);
            ps.Play(false);
        }

        // VFX Graph เล่นเองตอนเปิดอยู่แล้ว (Initial Event) — สั่งเฉพาะตัวที่ไม่ได้ตั้งให้เล่นเอง
        foreach (VisualEffect vfx in _vfx)
        {
            if (vfx == null || !vfx.isActiveAndEnabled) continue;
            if (string.IsNullOrEmpty(vfx.initialEventName)) vfx.Play();
        }
    }
}
