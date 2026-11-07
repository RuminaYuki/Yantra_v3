using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SDFcl.Tool.TreeGenerator
{
    /// <summary>สุ่มต้นใหม่จากต้นแม่ ตามค่าในแผง "สุ่มจากต้นนี้"</summary>
    public static class TreeVariation
    {
        public static TreePreset CreateVariant(TreePreset src, int variantSeed, TreePreset linkTo)
        {
            var v = ScriptableObject.CreateInstance<TreePreset>();
            EditorUtility.CopySerialized(src, v);
            v.hideFlags = HideFlags.None;
            Apply(v, src.variation, variantSeed);
            v.sourcePreset = linkTo;
            v.variantSeed = variantSeed;
            v.thumbnail = null;
            return v;
        }

        public static void Apply(TreePreset v, VariationSettings s, int seed)
        {
            var r = new System.Random(seed);
            float sil = s.keepSilhouette ? 0.5f : 1f;

            // ดึงค่าสุ่มตามลำดับเดิมเสมอ ไม่ว่าหมวดไหนจะเปิดหรือปิด (เปิด/ปิดหมวดหนึ่งจะไม่ทำให้หมวดอื่นเปลี่ยน)
            int newSeed = r.Next(1, int.MaxValue);
            int jitterSeed = r.Next(1, int.MaxValue);
            float h1 = R(r), h2 = R(r), l1 = R(r), l2 = R(r);
            double barkRoll = r.NextDouble();
            int barkSeed = r.Next(1, int.MaxValue);
            var shapeRng = new System.Random(r.Next());
            var leafRng = new System.Random(r.Next());

            // กิ่งที่ล็อก: จำ seed เดิมไว้ ให้กิ่งย่อยบนกิ่งนั้นเหมือนเดิม
            foreach (TreeBranch b in v.branches)
                if (b.locked && b.seedOverride == 0) b.seedOverride = v.seed;

            if (s.shapeOn && s.shape > 0f)
            {
                float a = s.shape / 100f * sil;
                foreach (TreeBranch b in v.branches)
                {
                    float acc = 0f;
                    for (int k = 1; k < b.knots.Count; k++)
                    {
                        Vector3 rv = TreeBuilder.RandVec(shapeRng);
                        acc += (b.knots[k].position - b.knots[k - 1].position).magnitude;
                        if (b.locked) continue;
                        b.knots[k].position += rv * a * 0.18f * acc;
                    }
                }
            }

            if (s.twigsOn && s.twigs > 0f)
            {
                if (s.twigs >= 99.5f) v.seed = newSeed;
                else
                {
                    v.jitterSeed = jitterSeed;
                    v.twigJitter = s.twigs / 100f * sil;
                }
            }

            if (s.heightOn && s.height > 0f)
            {
                float h = s.height / 100f;
                v.heightScale = Mathf.Max(0.1f, v.heightScale * (1f + h1 * h));
                v.thickness = Mathf.Max(0.1f, v.thickness * (1f + h2 * h * 0.5f));
            }

            if (s.leanOn && s.lean > 0f) v.leanAngles = new Vector2(l1 * s.lean, l2 * s.lean);

            if (s.leafOn && s.leaf > 0f)
            {
                foreach (WeightedMaterial w in v.leafMaterials)
                    w.weight = Mathf.Max(0.5f, w.weight * (1f + R(leafRng) * s.leaf / 100f * 1.5f));
            }

            if (s.barkOn && barkRoll < s.bark / 100f) v.barkSeed = barkSeed;
        }

        static float R(System.Random r)
        {
            return (float)(r.NextDouble() * 2.0 - 1.0);
        }

        /// <summary>สร้าง Variant ที่ผูกกับต้นแม่ใหม่ทั้งหมด หลังต้นแม่ถูกแก้ · คืนรายการที่อัปเดต</summary>
        public static List<TreePreset> UpdateLinkedVariants(TreePreset parent)
        {
            var updated = new List<TreePreset>();
            if (parent == null) return updated;
            foreach (string guid in AssetDatabase.FindAssets("t:TreePreset"))
            {
                var v = AssetDatabase.LoadAssetAtPath<TreePreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (v == null || v == parent || v.sourcePreset != parent) continue;

                string name = v.name, display = v.displayName, cat = v.category;
                Texture2D thumb = v.thumbnail;
                int vs = v.variantSeed;
                VariationSettings settings = v.variation != null ? v.variation.Clone() : parent.variation.Clone();

                var tmp = ScriptableObject.CreateInstance<TreePreset>();
                EditorUtility.CopySerialized(parent, tmp);
                tmp.variation = settings;
                Apply(tmp, settings, vs);
                EditorUtility.CopySerialized(tmp, v);
                Object.DestroyImmediate(tmp);

                v.name = name;
                v.hideFlags = HideFlags.None;
                v.displayName = display;
                v.category = cat;
                v.thumbnail = thumb;
                v.sourcePreset = parent;
                v.variantSeed = vs;
                EditorUtility.SetDirty(v);
                updated.Add(v);
            }
            return updated;
        }
    }
}
