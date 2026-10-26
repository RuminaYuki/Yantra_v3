using System.Collections.Generic;
using UnityEngine;

namespace Yantra.TerrainTools
{
    // ชุดแปรงวางของ — เก็บเป็นไฟล์ แชร์ให้ทั้งทีมใช้ชุดเดียวกันได้
    [CreateAssetMenu(menuName = "Yantra/Terrain Tools/ชุดแปรงวางของ", fileName = "ชุดแปรง_ใหม่")]
    public class ScatterPreset : ScriptableObject
    {
        [System.Serializable]
        public class Item
        {
            public GameObject prefab;
            [Range(0.05f, 1f)] public float weight = 1f;
            public bool enabled = true;
        }

        public List<Item> items = new List<Item>();

        [Min(0.5f)] public float radius = 4f;
        [Min(0.1f)] public float spacing = 2f;
        [Range(1, 20)] public int perStamp = 3;
        public Vector2 scale = new Vector2(0.85f, 1.2f);
        public bool randomYaw = true;
        [Range(0f, 1f)] public float alignToGround = 0f;
        public float sink = 0.05f;
        public bool slopeSink = true;
        [Min(0f)] public float baseRadius = 0.4f;
        [Range(0f, 90f)] public float maxSlope = 35f;
        [Min(0f)] public float minDistance = 1.5f;
        public bool terrainOnly = true;
    }
}
