using UnityEngine;

/// <summary>
/// สคริปต์สำหรับ Dummy ใน Cutscene รองรับการยิงเรดาร์เช็คพื้นผิว (Terrain/3D Surface) อัตโนมัติ
/// </summary>
public class DummyCutsceneSound : MonoBehaviour
{
    [Header("Auto-Detect Surface Settings")]
    [Tooltip("เลเยอร์ของพื้น (Ground)")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Tooltip("ระยะยิงเรดาร์ลงพื้น (ตั้งเผื่อไว้ 2 เมตร กรณี Animator ทำตัว Dummy ลอยจากพื้นนิดหน่อย)")]
    [SerializeField] private float raycastDistance = 2.0f;

    [Header("Surface Sounds Settings (For 3D Models)")]
    [Tooltip("ยืมโครงสร้างตะกร้าเสียงมาจาก Player เลย จะได้ตั้งค่าเหมือนกันเป๊ะ")]
    [SerializeField] private PlayerSoundController.SurfaceSound[] surfaceSounds;

    [Header("Terrain Sounds Settings (For Unity Terrain)")]
    [SerializeField] private PlayerSoundController.TerrainSound[] terrainSounds;

    [Header("Default Sounds (Fallback)")]
    [SerializeField] private SoundID defaultWalkID;
    [SerializeField] private SoundID defaultRunID;

    [Header("Custom Cutscene SFX")]
    [Tooltip("เสียงอื่นๆ ประจำคัตซีน เช่น ชักดาบ, ล้ม, โดนตี (ให้ Animator เรียกผ่าน PlaySoundByIndex)")]
    [SerializeField] private SoundID[] customSounds;

    // ==========================================
    // 👣 รับ Event จากท่าเดิน/วิ่ง 
    // ==========================================
    public void PlayFootstepSound()
    {
        PlayMovementSound(false);
    }

    public void PlaySneakSound()
    {
        PlayMovementSound(false);
    }

    public void PlayRunSound()
    {
        PlayMovementSound(true);
    }

    // ==========================================
    // 📡 ระบบเรดาร์แยกพื้นผิว (คล้าย Player แต่ใช้สำหรับจังหวะ Animation Event)
    // ==========================================
    private void PlayMovementSound(bool isRunning)
    {
        SoundID walkToPlay = defaultWalkID;
        SoundID runToPlay = defaultRunID;

        // ยิงจากช่วงเอว/ขาของ Dummy ลงไปที่พื้น
        Vector3 rayOrigin = transform.position + (Vector3.up * 0.5f);
        Vector3 soundSpawnPos = transform.position;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
        {
            soundSpawnPos = hit.point;
            Terrain hitTerrain = hit.collider.GetComponent<Terrain>();

            if (hitTerrain != null)
            {
                TerrainLayer dominantLayer = GetDominantTerrainLayer(hit.point, hitTerrain);
                if (dominantLayer != null)
                {
                    bool foundMatch = false;
                    foreach (var tSound in terrainSounds)
                    {
                        if (tSound.terrainLayers == null) continue;
                        foreach (var layer in tSound.terrainLayers)
                        {
                            if (layer == dominantLayer)
                            {
                                walkToPlay = tSound.walkSound;
                                runToPlay = tSound.runSound;
                                foundMatch = true;
                                break;
                            }
                        }
                        if (foundMatch) break;
                    }
                }
            }
            else
            {
                string hitTag = hit.collider.tag;
                foreach (var surface in surfaceSounds)
                {
                    if (surface.surfaceTag == hitTag)
                    {
                        walkToPlay = surface.walkSound;
                        runToPlay = surface.runSound;
                        break;
                    }
                }
            }
        }

        SoundID idToPlay = isRunning ? (runToPlay != null ? runToPlay : walkToPlay) : walkToPlay;

        if (idToPlay != null && SoundManager.Instance != null)
        {
            // ส่งเสียงไปเกิดที่จุดปลายเท้า (hit.point)
            SoundManager.Instance.PlaySFX(idToPlay, soundSpawnPos);
        }
    }

    private TerrainLayer GetDominantTerrainLayer(Vector3 worldPos, Terrain terrain)
    {
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        float mapX = ((worldPos.x - terrainPos.x) / terrainData.size.x) * terrainData.alphamapWidth;
        float mapZ = ((worldPos.z - terrainPos.z) / terrainData.size.z) * terrainData.alphamapHeight;

        int x = Mathf.FloorToInt(mapX);
        int z = Mathf.FloorToInt(mapZ);

        if (x < 0 || z < 0 || x >= terrainData.alphamapWidth || z >= terrainData.alphamapHeight)
            return null;

        float[,,] aMap = terrainData.GetAlphamaps(x, z, 1, 1);

        float maxMix = 0f;
        int maxIndex = 0;

        for (int n = 0; n < aMap.GetUpperBound(2) + 1; n++)
        {
            if (aMap[0, 0, n] > maxMix)
            {
                maxIndex = n;
                maxMix = aMap[0, 0, n];
            }
        }

        if (terrainData.terrainLayers != null && maxIndex < terrainData.terrainLayers.Length)
        {
            return terrainData.terrainLayers[maxIndex];
        }

        return null;
    }

    // ==========================================
    // รับ Event เสียงพิเศษในคัตซีน (เช่น ฟันดาบ, โดนตบ)
    // ==========================================
    public void PlaySoundByIndex(int index)
    {
        if (index >= 0 && index < customSounds.Length && customSounds[index] != null)
        {
            SoundManager.Instance.PlaySFX(customSounds[index], transform.position);
        }
    }

}