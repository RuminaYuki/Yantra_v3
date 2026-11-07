using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace SDFcl.Tool.TreeGenerator
{
    public enum PipelineKind { BuiltIn, URP, HDRP }

    /// <summary>สร้าง/อ่าน material ให้ทำงานได้ทั้ง Built-in, URP และ HDRP</summary>
    public static class TreeMaterialUtil
    {
        public static readonly Color DefaultLeafColor = new Color(0.30f, 0.52f, 0.24f);

        static readonly Dictionary<string, Material> PreviewCache = new Dictionary<string, Material>();
        static Texture2D leafTex;
        static Texture2D barkTex;

        public static PipelineKind Pipeline
        {
            get
            {
                RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
                if (rp == null) return PipelineKind.BuiltIn;
                string n = rp.GetType().Name;
                if (n.Contains("HDRenderPipeline")) return PipelineKind.HDRP;
                if (n.Contains("Universal")) return PipelineKind.URP;
                return PipelineKind.BuiltIn;
            }
        }

        public static Shader LitShader()
        {
            Shader s = null;
            switch (Pipeline)
            {
                case PipelineKind.HDRP: s = Shader.Find("HDRP/Lit"); break;
                case PipelineKind.URP: s = Shader.Find("Universal Render Pipeline/Lit"); break;
            }
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Diffuse");
            return s;
        }

        // ------------------------------------------------------------------ สร้าง material

        public static Material CreateMaterial(Color color, Texture tex, bool cutout, bool doubleSided)
        {
            var m = new Material(LitShader());
            SetMainColor(m, color);
            if (tex != null) SetMainTexture(m, tex);
            SetFloat(m, "_Smoothness", 0.12f);
            SetFloat(m, "_Glossiness", 0.12f);
            SetFloat(m, "_Metallic", 0f);

            switch (Pipeline)
            {
                case PipelineKind.HDRP:
                    if (cutout)
                    {
                        SetFloat(m, "_AlphaCutoffEnable", 1f);
                        SetFloat(m, "_AlphaCutoff", 0.5f);
                        m.EnableKeyword("_ALPHATEST_ON");
                    }
                    if (doubleSided)
                    {
                        SetFloat(m, "_DoubleSidedEnable", 1f);
                        SetFloat(m, "_DoubleSidedNormalMode", 1f);
                        SetFloat(m, "_CullMode", 0f);
                        SetFloat(m, "_CullModeForward", 0f);
                        m.EnableKeyword("_DOUBLESIDED_ON");
                    }
                    HDRPValidate(m);
                    break;

                case PipelineKind.URP:
                    if (cutout)
                    {
                        SetFloat(m, "_AlphaClip", 1f);
                        SetFloat(m, "_Cutoff", 0.5f);
                        m.EnableKeyword("_ALPHATEST_ON");
                        m.SetOverrideTag("RenderType", "TransparentCutout");
                        m.renderQueue = (int)RenderQueue.AlphaTest;
                    }
                    if (doubleSided) SetFloat(m, "_Cull", 0f);
                    break;

                default:
                    if (cutout)
                    {
                        SetFloat(m, "_Mode", 1f);
                        SetFloat(m, "_Cutoff", 0.5f);
                        m.EnableKeyword("_ALPHATEST_ON");
                        m.SetOverrideTag("RenderType", "TransparentCutout");
                        m.renderQueue = (int)RenderQueue.AlphaTest;
                    }
                    break;
            }
            return m;
        }

        static MethodInfo hdrpValidator;
        static bool hdrpValidatorSearched;

        /// <summary>
        /// ให้ HDRP ตั้ง keyword/pass/queue ของ material ที่สร้างด้วยโค้ดใหม่ ไม่งั้นจะ render เป็นสีขาวและไม่ตัดขอบใบ
        /// หาเมธอดจากทุก assembly ที่โหลดอยู่ (ชื่อ assembly ของ HDRP ต่างกันไปตามเวอร์ชัน)
        /// </summary>
        public static void HDRPValidate(Material m)
        {
            if (Pipeline != PipelineKind.HDRP || m == null) return;
            if (!hdrpValidatorSearched)
            {
                hdrpValidatorSearched = true;
                hdrpValidator = FindStatic("UnityEditor.Rendering.HighDefinition.HDShaderUtils", "ResetMaterialKeywords")
                                ?? FindStatic("UnityEngine.Rendering.HighDefinition.HDMaterial", "ValidateMaterial");
                if (hdrpValidator == null)
                    Debug.LogWarning("[Tree Generator] หาเมธอดตั้งค่า HDRP material ไม่เจอ ใบไม้อาจแสดงผลไม่ถูกต้อง");
            }

            if (hdrpValidator == null) return;
            try
            {
                hdrpValidator.Invoke(null, new object[] { m });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tree Generator] ตั้งค่า HDRP material ไม่สำเร็จ: " + e.Message);
            }
        }

        static MethodInfo FindStatic(string typeName, string method)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(typeName);
                if (t == null) continue;
                MethodInfo mi = t.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Material) }, null);
                if (mi != null) return mi;
            }
            return null;
        }

        static void SetFloat(Material m, string prop, float v)
        {
            if (m.HasProperty(prop)) m.SetFloat(prop, v);
        }

        // ------------------------------------------------------------------ อ่าน/เขียนสีและ texture หลัก

        static readonly string[] TexProps = { "_BaseColorMap", "_BaseMap", "_MainTex" };
        static readonly string[] ColorProps = { "_BaseColor", "_Color" };

        public static Texture GetMainTexture(Material m)
        {
            if (m == null) return null;
            foreach (string p in TexProps)
                if (m.HasProperty(p) && m.GetTexture(p) != null) return m.GetTexture(p);
            return null;
        }

        public static void SetMainTexture(Material m, Texture t)
        {
            foreach (string p in TexProps)
                if (m.HasProperty(p)) m.SetTexture(p, t);
        }

        public static Color GetMainColor(Material m)
        {
            if (m == null) return Color.white;
            foreach (string p in ColorProps)
                if (m.HasProperty(p)) return m.GetColor(p);
            return Color.white;
        }

        public static void SetMainColor(Material m, Color c)
        {
            foreach (string p in ColorProps)
                if (m.HasProperty(p)) m.SetColor(p, c);
        }

        // ------------------------------------------------------------------ Preview materials

        public static Material[] GetPreviewMaterials(TreePreset p, TreeBuildResult r)
        {
            var mats = new Material[1 + r.leafTypeCount];
            WeightedMaterial bark = r.barkIndex >= 0 && r.barkIndex < p.barkMaterials.Count ? p.barkMaterials[r.barkIndex] : null;
            mats[0] = bark != null && bark.material != null ? bark.material : DefaultBark(bark != null ? bark.color : p.barkColor);
            for (int k = 0; k < r.leafTypeCount; k++)
            {
                WeightedMaterial e = k < p.leafMaterials.Count ? p.leafMaterials[k] : null;
                mats[1 + k] = e != null && e.material != null
                    ? e.material
                    : DefaultLeaf(e != null ? e.color : DefaultLeafColor, p.leafMode);
            }
            return mats;
        }

        public static Material DefaultBark(Color c)
        {
            string key = "bark" + ColorUtility.ToHtmlStringRGBA(c) + Pipeline;
            Material m;
            if (PreviewCache.TryGetValue(key, out m) && m != null) return m;
            m = CreateMaterial(c, ProceduralBarkTexture, false, false);
            m.hideFlags = HideFlags.HideAndDontSave;
            PreviewCache[key] = m;
            return m;
        }

        public static Material DefaultLeaf(Color c, LeafMode mode)
        {
            string key = "leaf" + mode + ColorUtility.ToHtmlStringRGBA(c) + Pipeline;
            Material m;
            if (PreviewCache.TryGetValue(key, out m) && m != null) return m;
            bool card = mode == LeafMode.Card;
            m = CreateMaterial(c, card ? ProceduralLeafTexture : null, card, card);
            m.hideFlags = HideFlags.HideAndDontSave;
            PreviewCache[key] = m;
            return m;
        }

        /// <summary>shader ที่ใบชนิดนี้จะใช้จริง (ไม่ใส่ material = shader Lit เริ่มต้น)</summary>
        public static Shader EffectiveLeafShader(TreePreset p, int k)
        {
            WeightedMaterial e = k < p.leafMaterials.Count ? p.leafMaterials[k] : null;
            return e != null && e.material != null ? e.material.shader : LitShader();
        }

        /// <summary>ใบที่ใช้ shader ต่างจากใบแรก (รวม Atlas ไม่ได้ → เพิ่ม draw call)</summary>
        public static List<int> LeafShaderMismatch(TreePreset p)
        {
            var list = new List<int>();
            int n = Mathf.Max(1, p.leafMaterials.Count);
            Shader first = EffectiveLeafShader(p, 0);
            for (int k = 1; k < n; k++)
                if (EffectiveLeafShader(p, k) != first) list.Add(k);
            return list;
        }

        /// <summary>material ต้นแบบของใบ (copy ใหม่ เอาไปเปลี่ยน texture เป็น atlas ได้)</summary>
        public static Material LeafTemplate(TreePreset p, int k)
        {
            WeightedMaterial e = k < p.leafMaterials.Count ? p.leafMaterials[k] : null;
            if (e != null && e.material != null) return new Material(e.material);
            bool card = p.leafMode == LeafMode.Card;
            return CreateMaterial(Color.white, null, card, card);
        }

        /// <summary>รูปหนึ่งช่องใน atlas (สีของ material ถูกคูณลงในรูปแล้ว)</summary>
        public static Texture2D MakeLeafTile(TreePreset p, int k, int size)
        {
            WeightedMaterial e = k < p.leafMaterials.Count ? p.leafMaterials[k] : null;
            Material mat = e != null ? e.material : null;
            Texture src;
            Color tint;
            if (mat != null)
            {
                src = GetMainTexture(mat);
                tint = GetMainColor(mat);
                if (src == null && p.leafMode == LeafMode.Card) src = ProceduralLeafTexture;
            }
            else
            {
                src = p.leafMode == LeafMode.Card ? ProceduralLeafTexture : null;
                tint = e != null ? e.color : DefaultLeafColor;
            }

            var tile = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] px;
            if (src == null)
            {
                px = new Color[size * size];
                Color c = tint;
                c.a = 1f;
                for (int i = 0; i < px.Length; i++) px[i] = c;
            }
            else
            {
                RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                RenderTexture prev = RenderTexture.active;
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                tile.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tile.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                px = tile.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    Color c = px[i];
                    px[i] = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a * tint.a);
                }
            }
            tile.SetPixels(px);
            tile.Apply();
            return tile;
        }

        // ------------------------------------------------------------------ texture ที่สร้างเอง

        /// <summary>รูปช่อใบสีขาว พื้นโปร่งใส (เอาไปคูณสี)</summary>
        public static Texture2D ProceduralLeafTexture
        {
            get
            {
                if (leafTex != null) return leafTex;
                const int S = 256;
                leafTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "TreeGen_Leaf", wrapMode = TextureWrapMode.Clamp };
                leafTex.hideFlags = HideFlags.HideAndDontSave;
                var px = new Color[S * S];
                // ใบ: ศูนย์กลาง, มุม, ขนาด (ในหน่วย 0..1)
                float[,] leaves =
                {
                    { 0.50f, 0.62f, 0f, 0.13f, 0.30f }, { 0.30f, 0.50f, 35f, 0.11f, 0.26f },
                    { 0.70f, 0.50f, -35f, 0.11f, 0.26f }, { 0.22f, 0.28f, 60f, 0.10f, 0.22f },
                    { 0.78f, 0.28f, -60f, 0.10f, 0.22f }, { 0.40f, 0.82f, 15f, 0.09f, 0.18f },
                    { 0.62f, 0.83f, -15f, 0.09f, 0.18f }
                };
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                        Color c = new Color(1f, 1f, 1f, 0f);
                        // ก้าน
                        if (Mathf.Abs(u - 0.5f) < 0.008f && v < 0.6f) c = new Color(0.75f, 0.75f, 0.7f, 1f);
                        for (int i = 0; i < leaves.GetLength(0); i++)
                        {
                            float a = leaves[i, 2] * Mathf.Deg2Rad;
                            float dx = u - leaves[i, 0], dy = v - leaves[i, 1];
                            float lx = dx * Mathf.Cos(a) - dy * Mathf.Sin(a);
                            float ly = dx * Mathf.Sin(a) + dy * Mathf.Cos(a);
                            float w = leaves[i, 3], h = leaves[i, 4];
                            float yy = ly / h;
                            if (Mathf.Abs(yy) >= 1f) continue;
                            float width = w * Mathf.Pow(1f - yy * yy, 0.75f);
                            if (Mathf.Abs(lx) < width)
                            {
                                float vein = Mathf.Abs(lx) < w * 0.06f ? 0.82f : 1f;
                                float shade = Mathf.Lerp(0.82f, 1f, 1f - Mathf.Abs(lx) / Mathf.Max(0.0001f, width)) * vein;
                                c = new Color(shade, shade, shade, 1f);
                            }
                        }
                        px[y * S + x] = c;
                    }
                }
                leafTex.SetPixels(px);
                leafTex.Apply(true);
                return leafTex;
            }
        }

        /// <summary>ลายเปลือกไม้สีเทาอ่อน (เอาไปคูณสี) ต่อกันได้ทุกด้าน</summary>
        public static Texture2D ProceduralBarkTexture
        {
            get
            {
                if (barkTex != null) return barkTex;
                const int W = 128, H = 256;
                barkTex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "TreeGen_Bark", wrapMode = TextureWrapMode.Repeat };
                barkTex.hideFlags = HideFlags.HideAndDontSave;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        float u = x / (float)W, v = y / (float)H;
                        float a = u * Mathf.PI * 2f;
                        // ใช้วงกลมใน noise ให้ขอบซ้าย-ขวาต่อกันพอดี และ crossfade แนวตั้งให้บน-ล่างต่อกันพอดี
                        float n1 = Mathf.Lerp(BarkNoise(a, v, 3f, 2f, 10f), BarkNoise(a, v - 1f, 3f, 2f, 10f), v);
                        float n2 = Mathf.Lerp(BarkNoise(a, v, 9f, 9f, 30f), BarkNoise(a, v - 1f, 9f, 9f, 30f), v);
                        float val = 0.62f + 0.3f * n1 + 0.18f * (n2 - 0.5f);
                        val = Mathf.Clamp01(val);
                        px[y * W + x] = new Color(val, val, val, 1f);
                    }
                }
                barkTex.SetPixels(px);
                barkTex.Apply(true);
                return barkTex;
            }
        }

        static float BarkNoise(float angle, float v, float radius, float vScale, float offset)
        {
            return Mathf.PerlinNoise(Mathf.Cos(angle) * radius + offset, Mathf.Sin(angle) * radius + v * vScale + 50f);
        }
    }
}
