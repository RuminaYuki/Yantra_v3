using UnityEngine;

/// <summary>
/// เครื่องมือกลางสำหรับ "ยิงเรดาร์ลงพื้นแล้วดูว่าเหยียบอะไรอยู่"
///
/// ทำไมต้องแยกออกมา:
/// โค้ดชุดนี้เคยถูกก๊อปไว้ใน PlayerSoundController, GhostSoundController และ DummyCutsceneSound
/// พอเจอบั๊กทีต้องไล่แก้ 3 ที่ และมักแก้ไม่ครบ
/// ตอนนี้อยู่ที่เดียว ใครจะใช้ก็เรียกเอา
/// </summary>
public static class SurfaceResolver
{
    public struct SurfaceHit
    {
        public bool hasHit;

        /// <summary>จุดที่เท้าแตะพื้นจริง — ใช้เป็นตำแหน่งเกิดเสียง</summary>
        public Vector3 point;

        /// <summary>Tag ของ collider ที่โดน (สำหรับพื้นแบบ 3D Model)</summary>
        public string tag;

        /// <summary>Terrain Layer ที่เด่นที่สุดตรงจุดนั้น (null ถ้าไม่ใช่ Terrain)</summary>
        public TerrainLayer terrainLayer;
    }

    public static SurfaceHit Probe(Vector3 origin, float distance, LayerMask groundLayer)
    {
        SurfaceHit result = new SurfaceHit
        {
            hasHit = false,
            point = origin,
            tag = "Untagged",
            terrainLayer = null
        };

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, groundLayer))
            return result;

        result.hasHit = true;
        result.point = hit.point;
        result.tag = hit.collider.tag;

        Terrain terrain = hit.collider.GetComponent<Terrain>();
        if (terrain != null)
            result.terrainLayer = GetDominantTerrainLayer(hit.point, terrain);

        return result;
    }

    /// <summary>หาว่าจุดนี้บน Terrain ทาสีเลเยอร์ไหนเข้มที่สุด</summary>
    public static TerrainLayer GetDominantTerrainLayer(Vector3 worldPos, Terrain terrain)
    {
        if (terrain == null) return null;

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
            return terrainData.terrainLayers[maxIndex];

        return null;
    }
}