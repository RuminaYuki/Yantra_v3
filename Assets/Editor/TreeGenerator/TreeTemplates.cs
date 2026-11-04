using System.Collections.Generic;
using UnityEngine;

namespace SDFcl.Tool.TreeGenerator
{
    /// <summary>พันธุ์ตั้งต้นของ Gallery</summary>
    public static class TreeTemplates
    {
        public static readonly string[] Kinds = { "Oak", "Pine", "Willow", "Dead", "Stylized" };

        public static string DisplayName(string kind)
        {
            switch (kind)
            {
                case "Oak": return "ต้นโอ๊ก";
                case "Pine": return "ต้นสน";
                case "Willow": return "ต้นหลิว";
                case "Dead": return "ต้นไม้ตาย";
                case "Stylized": return "ต้นไม้ Stylized";
                default: return "ลำต้นเปล่า";
            }
        }

        public static void Apply(TreePreset p, string kind)
        {
            Reset(p);
            p.displayName = DisplayName(kind);
            p.category = kind == "Empty" ? "ของฉัน" : "พันธุ์ตั้งต้น";
            switch (kind)
            {
                case "Oak": Oak(p); break;
                case "Pine": Pine(p); break;
                case "Willow": Willow(p); break;
                case "Dead": Dead(p); break;
                case "Stylized": Stylized(p); break;
                default: Empty(p); break;
            }
        }

        static void Reset(TreePreset p)
        {
            p.style = TreeStyle.Realistic;
            p.seed = 12345;
            p.heightScale = 1f;
            p.thickness = 1f;
            p.fullness = 0.6f;
            p.messiness = 0.3f;
            p.droop = 0f;
            p.age = 0.5f;
            p.trunkRadius = 0.3f;
            p.tipRatio = 0.15f;
            p.childRadiusRatio = 0.6f;
            p.trunkTwigStart = 0.5f;
            p.branches = new List<TreeBranch>();
            p.twigLevels = new List<TwigLevel>();
            p.suppressedTwigs = new List<int>();
            p.barkMaterials = new List<WeightedMaterial>();
            p.barkColor = new Color(0.42f, 0.32f, 0.24f);
            p.leafMaterials = new List<WeightedMaterial>();
            p.leafMode = LeafMode.Card;
            p.leafDistribution = LeafDistribution.Clustered;
            p.leafDensity = 0.7f;
            p.leafSize = 0.5f;
            p.hueVariation = 0.3f;
            p.leafStartT = 0.25f;
            p.leafEndT = 1f;
            p.leafPlanes = 2;
            p.leafPlaneAngle = 90f;
            p.leafPlaneJitter = 15f;
            p.leafYawJitter = 60f;
            p.leafPitchJitter = 25f;
            p.leafRollJitter = 20f;
            p.leafSizeJitter = 0.25f;
            p.leafAttach = true;
            p.leafPivotV = 0.05f;
            p.radialDetail = 1f;
            p.maxSides = 12;
            p.lengthDetail = 1f;
            p.simplifyAngle = 4f;
            p.variation = new VariationSettings();
            p.leanAngles = Vector2.zero;
            p.barkSeed = 0;
            p.jitterSeed = 0;
            p.twigJitter = 0f;
            p.sourcePreset = null;
            p.variantSeed = 0;
            p.thumbnail = null;
        }

        // ------------------------------------------------------------------ helpers

        static TreeBranch Trunk(params Vector3[] pts)
        {
            var b = new TreeBranch { parent = -1 };
            foreach (Vector3 v in pts) b.knots.Add(new TreeKnot(v));
            return b;
        }

        /// <summary>กิ่งใหญ่ 4 จุด ออกจากกิ่งแม่ที่ t · upBend บวก = โค้งขึ้น · sag บวก = ห้อยลงที่ปลาย</summary>
        static TreeBranch Limb(int parent, float t, Vector3 dir, float len, float upBend, float sag = 0f)
        {
            var b = new TreeBranch { parent = parent, parentT = t };
            Vector3 d = dir.normalized;
            Vector3 k1 = d * len * 0.34f;
            Vector3 d2 = (d + Vector3.up * upBend - Vector3.up * sag).normalized;
            Vector3 k2 = k1 + d2 * len * 0.33f;
            Vector3 d3 = (d + Vector3.up * upBend * 1.4f - Vector3.up * sag * 2f).normalized;
            Vector3 k3 = k2 + d3 * len * 0.33f;
            b.knots.Add(new TreeKnot(Vector3.zero));
            b.knots.Add(new TreeKnot(k1));
            b.knots.Add(new TreeKnot(k2));
            b.knots.Add(new TreeKnot(k3));
            return b;
        }

        // ------------------------------------------------------------------ พันธุ์

        static void Oak(TreePreset p)
        {
            p.trunkRadius = 0.32f;
            p.tipRatio = 0.18f;
            p.trunkTwigStart = 0.6f;
            p.fullness = 0.65f;
            p.age = 0.55f;
            p.branches.Add(Trunk(new Vector3(0, 0, 0), new Vector3(0.08f, 1.6f, 0.02f), new Vector3(-0.06f, 3.1f, 0.08f),
                new Vector3(0.05f, 4.6f, -0.02f), new Vector3(0, 5.8f, 0)));
            p.branches.Add(Limb(0, 0.48f, new Vector3(1f, 0.7f, 0.25f), 3.4f, 0.15f));
            p.branches.Add(Limb(0, 0.55f, new Vector3(-1f, 0.8f, 0.35f), 3.2f, 0.15f));
            p.branches.Add(Limb(0, 0.62f, new Vector3(0.25f, 0.8f, -1f), 3.0f, 0.2f));
            p.branches.Add(Limb(0, 0.70f, new Vector3(-0.4f, 0.9f, -0.9f), 2.8f, 0.2f));
            p.branches.Add(Limb(0, 0.78f, new Vector3(0.8f, 1f, 0.8f), 2.4f, 0.25f));
            p.branches.Add(Limb(0, 0.86f, new Vector3(-0.7f, 1.1f, 0.1f), 2.0f, 0.3f));
            p.twigLevels.Add(TwigLevel.Make(2.5f, 0.2f, 1f, 48f, 0.42f, 0.5f, 0.05f, 0.1f, 0.5f));
            p.twigLevels.Add(TwigLevel.Make(4f, 0.15f, 1f, 42f, 0.5f, 0.5f, 0.15f, 0.1f, 0.4f));
            p.twigLevels.Add(TwigLevel.Make(4f, 0.2f, 1f, 40f, 0.55f, 0.5f, 0.2f, 0.05f, 0.3f));
            p.leafMaterials.Add(new WeightedMaterial(new Color(0.30f, 0.52f, 0.24f), 100f));
            p.leafSize = 0.55f;
        }

        static void Pine(TreePreset p)
        {
            p.trunkRadius = 0.26f;
            p.tipRatio = 0.04f;
            p.trunkTwigStart = 0.12f;
            p.messiness = 0.2f;
            p.fullness = 0.6f;
            p.barkColor = new Color(0.40f, 0.30f, 0.25f);
            p.branches.Add(Trunk(new Vector3(0, 0, 0), new Vector3(0, 3f, 0.03f), new Vector3(0.02f, 6f, -0.02f),
                new Vector3(0, 9f, 0), new Vector3(0, 11f, 0)));
            p.twigLevels.Add(TwigLevel.Make(6f, 0.12f, 0.97f, 100f, 0.3f, 0.35f, 0.35f, 0.15f, 1f));
            p.twigLevels.Add(TwigLevel.Make(4f, 0.1f, 1f, 55f, 0.45f, 0.5f, 0.2f, 0.05f, 0.5f));
            p.leafMaterials.Add(new WeightedMaterial(new Color(0.18f, 0.36f, 0.22f), 100f));
            p.leafDensity = 0.9f;
            p.leafSize = 0.45f;
        }

        static void Willow(TreePreset p)
        {
            p.trunkRadius = 0.34f;
            p.tipRatio = 0.2f;
            p.trunkTwigStart = 0.85f;
            p.droop = 0.6f;
            p.fullness = 0.7f;
            p.branches.Add(Trunk(new Vector3(0, 0, 0), new Vector3(0.1f, 1.5f, 0), new Vector3(0, 2.8f, 0.1f),
                new Vector3(0, 3.6f, 0)));
            p.branches.Add(Limb(0, 0.75f, new Vector3(1f, 1f, 0.2f), 2.6f, 0.5f, 0.4f));
            p.branches.Add(Limb(0, 0.8f, new Vector3(-0.9f, 1f, 0.5f), 2.5f, 0.5f, 0.4f));
            p.branches.Add(Limb(0, 0.85f, new Vector3(0.2f, 1f, -1f), 2.4f, 0.5f, 0.4f));
            p.branches.Add(Limb(0, 0.9f, new Vector3(-0.6f, 1.1f, -0.7f), 2.2f, 0.6f, 0.4f));
            p.branches.Add(Limb(0, 0.95f, new Vector3(0.7f, 1.2f, 0.8f), 2.0f, 0.6f, 0.4f));
            p.twigLevels.Add(TwigLevel.Make(3f, 0.3f, 1f, 40f, 0.9f, 0.4f, 1.4f, -0.1f, 0.2f));
            p.twigLevels.Add(TwigLevel.Make(2f, 0.2f, 1f, 30f, 0.4f, 0.5f, 1.0f, 0f, 0.2f, false));
            p.leafMaterials.Add(new WeightedMaterial(new Color(0.45f, 0.60f, 0.30f), 100f));
            p.leafDensity = 0.9f;
            p.leafSize = 0.35f;
        }

        static void Dead(TreePreset p)
        {
            Oak(p);
            p.leafMaterials.Clear();
            p.leafDensity = 0f;
            p.messiness = 0.8f;
            p.age = 0.9f;
            p.barkColor = new Color(0.45f, 0.42f, 0.38f);
            p.twigLevels.Clear();
            p.twigLevels.Add(TwigLevel.Make(2f, 0.2f, 1f, 50f, 0.4f, 0.5f, 0.1f, 0.15f, 0.5f));
            p.twigLevels.Add(TwigLevel.Make(2.5f, 0.2f, 1f, 45f, 0.45f, 0.5f, 0.2f, 0.1f, 0.4f));
        }

        static void Stylized(TreePreset p)
        {
            p.style = TreeStyle.Stylized;
            p.trunkRadius = 0.28f;
            p.tipRatio = 0.4f;
            p.trunkTwigStart = 0.8f;
            p.messiness = 0.15f;
            p.barkColor = new Color(0.55f, 0.38f, 0.25f);
            p.branches.Add(Trunk(new Vector3(0, 0, 0), new Vector3(0.1f, 1.2f, 0), new Vector3(0, 2.4f, 0)));
            p.branches.Add(Limb(0, 0.8f, new Vector3(1f, 1.2f, 0f), 1.2f, 0.3f));
            p.branches.Add(Limb(0, 0.8f, new Vector3(-0.6f, 1.2f, 0.8f), 1.1f, 0.3f));
            p.branches.Add(Limb(0, 0.8f, new Vector3(-0.5f, 1.2f, -0.9f), 1.1f, 0.3f));
            p.twigLevels.Add(TwigLevel.Make(3f, 0.3f, 1f, 40f, 0.5f, 0.5f, -0.1f, 0.1f, 0.3f));
            p.leafMode = LeafMode.Blob;
            p.leafDensity = 1f;
            p.leafSize = 0.9f;
            p.leafMaterials.Add(new WeightedMaterial(new Color(0.47f, 0.73f, 0.35f), 70f));
            p.leafMaterials.Add(new WeightedMaterial(new Color(0.38f, 0.65f, 0.30f), 30f));
        }

        static void Empty(TreePreset p)
        {
            p.branches.Add(Trunk(new Vector3(0, 0, 0), new Vector3(0, 1.5f, 0), new Vector3(0, 3f, 0), new Vector3(0, 4f, 0)));
            p.trunkTwigStart = 0.4f;
            p.twigLevels.Add(TwigLevel.Make(2.5f, 0.2f, 1f, 50f, 0.45f, 0.5f, 0.1f, 0.1f, 0.6f));
            p.twigLevels.Add(TwigLevel.Make(4f, 0.15f, 1f, 42f, 0.5f, 0.5f, 0.15f, 0.1f, 0.4f));
            p.leafMaterials.Add(new WeightedMaterial(TreeMaterialUtil.DefaultLeafColor, 100f));
        }
    }
}
