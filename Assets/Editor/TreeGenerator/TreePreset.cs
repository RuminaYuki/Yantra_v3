using System.Collections.Generic;
using UnityEngine;

namespace SDFcl.Tool.TreeGenerator
{
    /// <summary>
    /// ต้นไม้หนึ่งพันธุ์ใน Gallery: เก็บทั้งเส้นที่วาดเอง ค่า slider วัสดุ และค่าการสุ่ม
    /// </summary>
    [CreateAssetMenu(menuName = "SDFcl/Tree Generator/Tree Preset", fileName = "TreePreset")]
    public class TreePreset : ScriptableObject
    {
        // ---------- Gallery
        public string displayName = "ต้นไม้ใหม่";
        public string category = "ของฉัน";
        public Texture2D thumbnail;

        // ---------- ทั่วไป
        public TreeStyle style = TreeStyle.Realistic;
        public int seed = 12345;

        // ---------- ปรับทรงแบบง่าย
        [Range(0.3f, 3f)] public float heightScale = 1f;
        [Range(0.3f, 3f)] public float thickness = 1f;
        [Range(0f, 1f)] public float fullness = 0.6f;
        [Range(0f, 1f)] public float messiness = 0.3f;
        [Range(-1f, 1f)] public float droop;
        [Range(0f, 1f)] public float age = 0.5f;

        // ---------- Advanced
        public float trunkRadius = 0.3f;
        [Range(0.02f, 1f)] public float tipRatio = 0.15f;
        [Range(0.2f, 0.95f)] public float childRadiusRatio = 0.6f;
        [Range(0f, 1f)] public float trunkTwigStart = 0.5f;

        // ---------- ความละเอียด (จำนวน poly)
        [Range(0.2f, 2f)] public float radialDetail = 1f;
        [Range(3, 16)] public int maxSides = 12;
        [Range(0.2f, 2f)] public float lengthDetail = 1f;
        [Range(0f, 15f)] public float simplifyAngle = 4f;

        // ---------- โครง spline
        public List<TreeBranch> branches = new List<TreeBranch>();
        public List<TwigLevel> twigLevels = new List<TwigLevel>();
        /// <summary>กิ่งอัตโนมัติที่ถูกแปลงเป็น spline แล้ว (ไม่ต้องงอกซ้ำ)</summary>
        public List<int> suppressedTwigs = new List<int>();

        // ---------- วัสดุ
        public List<WeightedMaterial> barkMaterials = new List<WeightedMaterial>();
        public Color barkColor = new Color(0.42f, 0.32f, 0.24f);
        public List<WeightedMaterial> leafMaterials = new List<WeightedMaterial>();
        public LeafMode leafMode = LeafMode.Card;
        public LeafDistribution leafDistribution = LeafDistribution.Clustered;
        [Range(0f, 1f)] public float leafDensity = 0.7f;
        public float leafSize = 0.5f;
        /// <summary>สุ่มขนาดใบ ± (0.25 = ขนาด 75%–125%)</summary>
        [Range(0f, 1f)] public float leafSizeJitter = 0.25f;
        /// <summary>true = โคนใบ (ขอบล่างกลางของรูป) อยู่ตรงจุดบนกิ่งพอดี และใบชี้ออกจากกิ่ง</summary>
        public bool leafAttach = true;
        /// <summary>ตำแหน่งโคนใบในรูป วัดจากขอบล่าง (0 = ขอบล่าง)</summary>
        [Range(0f, 0.5f)] public float leafPivotV = 0.05f;
        [Range(0f, 1f)] public float hueVariation = 0.3f;
        [Range(0f, 1f)] public float leafStartT = 0.25f;
        [Range(0f, 1f)] public float leafEndT = 1f;

        // ---------- ใบแบบแผ่น (Card)
        [Range(1, 3)] public int leafPlanes = 2;
        [Range(10f, 90f)] public float leafPlaneAngle = 90f;
        [Range(0f, 45f)] public float leafPlaneJitter = 15f;
        [Range(0f, 180f)] public float leafYawJitter = 60f;
        [Range(0f, 90f)] public float leafPitchJitter = 25f;
        [Range(0f, 90f)] public float leafRollJitter = 20f;

        // ---------- Bake
        public JunctionQuality junction = JunctionQuality.Fast;
        public bool windData = true;
        public bool trunkCollider = true;
        public bool lodShadowsOff = true;
        public int atlasSize = 2048;
        [Range(0, 32)] public int atlasPadding = 8;
        public string exportFolder = "Assets/Trees";

        // ---------- การสุ่ม
        public VariationSettings variation = new VariationSettings();
        public Vector2 leanAngles;
        public int barkSeed;
        public int jitterSeed;
        [Range(0f, 1f)] public float twigJitter;

        // ---------- Variant
        public TreePreset sourcePreset;
        public int variantSeed;
    }
}
