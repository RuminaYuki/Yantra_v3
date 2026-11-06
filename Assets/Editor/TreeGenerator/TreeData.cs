using System;
using System.Collections.Generic;
using UnityEngine;

namespace SDFcl.Tool.TreeGenerator
{
    public enum TreeStyle { Realistic, Stylized }
    public enum LeafMode { Card, Blob }
    public enum LeafDistribution { Scatter, Clustered }
    public enum JunctionQuality { Fast, Beautiful }

    /// <summary>จุดหนึ่งบนเส้น spline ของกิ่ง</summary>
    [Serializable]
    public class TreeKnot
    {
        /// <summary>ลำต้น: ตำแหน่งในพิกัดต้นไม้ · กิ่งลูก: ระยะห่างจากจุดที่แตกออกจากกิ่งแม่</summary>
        public Vector3 position;
        /// <summary>ใช้เมื่อ overrideRadius = true เท่านั้น (ไม่งั้นระบบเรียวกิ่งให้อัตโนมัติ)</summary>
        public float radius = 0.2f;
        public bool overrideRadius;

        /// <summary>true = ใช้ขา tangent ที่ปรับเอง · false = ระบบคำนวณขาให้ (ชี้ไปทางจุดถัดไป)</summary>
        public bool customTangent;
        /// <summary>true = ขาเข้ากับขาออกแยกกัน (กิ่งหักมุมได้)</summary>
        public bool brokenTangent;
        /// <summary>ขาออก (ระยะจากจุดถึงปลายขา หน่วยเดียวกับ position)</summary>
        public Vector3 tangentOut;
        /// <summary>ขาเข้า (ใช้เมื่อ brokenTangent = true ไม่งั้นใช้ -tangentOut)</summary>
        public Vector3 tangentIn;

        public TreeKnot() { }

        public TreeKnot(Vector3 position, float radius = 0.2f, bool overrideRadius = false)
        {
            this.position = position;
            this.radius = radius;
            this.overrideRadius = overrideRadius;
        }

        public TreeKnot Clone()
        {
            return new TreeKnot(position, radius, overrideRadius)
            {
                customTangent = customTangent,
                brokenTangent = brokenTangent,
                tangentOut = tangentOut,
                tangentIn = tangentIn
            };
        }
    }

    /// <summary>กิ่งที่วาดเอง (ลำต้น = parent -1)</summary>
    [Serializable]
    public class TreeBranch
    {
        public int parent = -1;
        [Range(0f, 1f)] public float parentT;
        public List<TreeKnot> knots = new List<TreeKnot>();
        /// <summary>ล็อกไว้ = ไม่ถูกแผงสุ่มเปลี่ยน</summary>
        public bool locked;
        /// <summary>seed เดิมของกิ่งย่อยบนกิ่งที่ล็อก (ใส่ให้อัตโนมัติตอนสุ่ม)</summary>
        public int seedOverride;
    }

    /// <summary>กฎการงอกกิ่งย่อยหนึ่งชั้น</summary>
    [Serializable]
    public class TwigLevel
    {
        public bool enabled = true;
        public float perMeter = 3f;
        [Range(0f, 1f)] public float startT = 0.2f;
        [Range(0f, 1f)] public float endT = 1f;
        public float angle = 45f;
        public float lengthRatio = 0.45f;
        public float radiusRatio = 0.5f;
        public float gravity = 0.15f;
        public float curl = 0.1f;
        [Range(0f, 1f)] public float taperLength = 0.5f;

        public static TwigLevel Make(float perMeter, float startT, float endT, float angle, float lengthRatio,
            float radiusRatio, float gravity, float curl, float taperLength, bool enabled = true)
        {
            return new TwigLevel
            {
                enabled = enabled,
                perMeter = perMeter,
                startT = startT,
                endT = endT,
                angle = angle,
                lengthRatio = lengthRatio,
                radiusRatio = radiusRatio,
                gravity = gravity,
                curl = curl,
                taperLength = taperLength
            };
        }
    }

    /// <summary>Material พร้อมน้ำหนักการสุ่ม (ถ้าไม่ใส่ material จะใช้สี color กับวัสดุเริ่มต้น)</summary>
    [Serializable]
    public class WeightedMaterial
    {
        public Material material;
        public Color color = Color.white;
        [Range(0f, 100f)] public float weight = 50f;

        public WeightedMaterial() { }

        public WeightedMaterial(Color color, float weight)
        {
            this.color = color;
            this.weight = weight;
        }
    }

    /// <summary>ค่าของแผง "สุ่มจากต้นนี้"</summary>
    [Serializable]
    public class VariationSettings
    {
        public bool shapeOn = true;
        [Range(0f, 100f)] public float shape = 10f;
        public bool twigsOn = true;
        [Range(0f, 100f)] public float twigs = 100f;
        public bool heightOn = true;
        [Range(0f, 50f)] public float height = 15f;
        public bool leanOn = true;
        [Range(0f, 20f)] public float lean = 5f;
        public bool leafOn = true;
        [Range(0f, 100f)] public float leaf = 30f;
        public bool barkOn;
        [Range(0f, 100f)] public float bark;
        public bool keepSilhouette = true;
        public bool linkVariants = true;
        [Range(1, 30)] public int batchCount = 6;

        public VariationSettings Clone()
        {
            return (VariationSettings)MemberwiseClone();
        }
    }
}
