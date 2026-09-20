using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// เสียงของผีกะ — ผีที่ "เดิน" บนพื้น
/// สิ่งที่มีเพิ่มจากผีทั่วไปคือระบบฝีเท้าที่เปลี่ยนเสียงตามพื้นผิว
///
/// เสียงพูด เสียงคราง gacha อยู่ในคลาสแม่ GhostSoundBase
/// </summary>

[MovedFrom(true, null, null, "GhostSoundController")]
public class KaSoundController : GhostSoundBase
{
    [Header("Footstep Cooldown")]
    [SerializeField] private float minStepInterval = 0.2f;
    private float lastStepTime = -999f;

    [Header("Footstep Culling & Budget")]
    [Tooltip("ไกลเกินระยะนี้ (เมตร) จะไม่คำนวณเสียงเท้าเลย ประหยัดทั้งแรงและหู")]
    [SerializeField] private float maxFootstepHearDistance = 14f;

    [Tooltip("ปิดถ้าอยากให้ผีตัวนี้ส่งเสียงเท้าได้เสมอ เช่น บอสตัวสำคัญ")]
    [SerializeField] private bool useSharedStepBudget = true;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float raycastDistance = 1.5f;

    [Header("Surface Sounds")]
    [SerializeField] private PlayerSoundController.SurfaceSound[] surfaceSounds;
    [SerializeField] private PlayerSoundController.TerrainSound[] terrainSounds;
    [SerializeField] private SoundID defaultFootstep;

    /// <summary>ใส่ Animation Event ตรงเฟรมที่เท้าแตะพื้น</summary>
    public override void PlayFootstep()
    {
        if (Time.time - lastStepTime < minStepInterval) return;

        // ด่าน 1: ไกลเกินได้ยิน ตัดทิ้งก่อนยิงเรดาร์
        // สำคัญเพราะการตรวจพื้นผิวต้องอ่าน Terrain alphamap ซึ่งจอง array ใหม่ทุกครั้ง
        float maxSqr = maxFootstepHearDistance * maxFootstepHearDistance;
        if (AudioListenerCache.SqrDistanceToListener(transform.position) > maxSqr) return;

        // ด่าน 2: ขอโควต้าจากงบกลางที่ผีทุกตัวแชร์กัน
        // ผีมาเยอะ = เสียงเท้าซ้อนกันจนรก อันนี้จำกัดไว้ไม่ให้เกินที่หูรับไหว
        if (useSharedStepBudget && !CreatureFootstepBudget.TryConsume()) return;

        lastStepTime = Time.time;

        Vector3 rayOrigin = transform.position + (Vector3.up * 0.5f);
        SurfaceResolver.SurfaceHit surface = SurfaceResolver.Probe(rayOrigin, raycastDistance, groundLayer);

        SoundID toPlay = ResolveFootstepSound(surface);
        PlaySound(toPlay, surface.point);
    }

    private SoundID ResolveFootstepSound(SurfaceResolver.SurfaceHit surface)
    {
        if (!surface.hasHit) return defaultFootstep;

        // พื้นแบบ Terrain
        if (surface.terrainLayer != null && terrainSounds != null)
        {
            foreach (var tSound in terrainSounds)
            {
                if (tSound.terrainLayers == null) continue;

                foreach (var layer in tSound.terrainLayers)
                {
                    if (layer == surface.terrainLayer)
                        return tSound.walkSound != null ? tSound.walkSound : defaultFootstep;
                }
            }
            return defaultFootstep;
        }

        // พื้นแบบ 3D Model — ดูจาก Tag
        if (surfaceSounds != null)
        {
            foreach (var s in surfaceSounds)
            {
                if (s.surfaceTag == surface.tag)
                    return s.walkSound != null ? s.walkSound : defaultFootstep;
            }
        }

        return defaultFootstep;
    }
}