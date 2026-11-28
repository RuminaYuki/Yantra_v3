#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HD = UnityEngine.Rendering.HighDefinition;

/// <summary>
/// หนาม + กระสุนตานี เปลี่ยนจาก greybox เป็นโมเดลจริง — กดครั้งเดียว (กดซ้ำได้ สร้างใหม่ทับของเดิม)
///   1. Thorn(Tanee): ลบกรวย greybox ใต้ Mesh → ใส่หนาม thai_thorn1 เป็นกอ 7 ต้น + GroundBurst (ฝุ่น + เศษดิน) ที่โคน
///   2. DeathBlossomProjectileBullet: โมเดล Projectile_Thani ใหญ่ขึ้น + เรืองแสงสีทอง (สีเดียวกับตาตานี)
///      + หางแสง + แสงเรืองรอบตัว — มองเห็นชัดแม้ฉากมืด
/// ไม่แตะ FBX / material ของคนทำโมเดล — material เรืองแสงสร้างใหม่ใน .../Tanee/Data/Fx
/// </summary>
public static class TaniThornProjectileSetup
{
    private const string DataFolder = "Assets/Prefab/Character/Enemy/Boss/Tanee/Data";
    private const string FxFolder = DataFolder + "/Fx";
    private const string ThornPrefabPath = DataFolder + "/Thorn(Tanee).prefab";
    private const string BulletPrefabPath = DataFolder + "/DeathBlossomProjectileBullet.prefab";
    private const string ThornModelPath = "Assets/Model/Character/Enemy/Tanee/thorn/thai_thorn1.fbx";
    private const string BulletModelPath = "Assets/Model/Character/Enemy/Tanee/Projectile_Thani/Projectile_Thani.fbx";
    private const string BurstName = "GroundBurst";

    // สีทองเดียวกับตาตานี (TaniMatt_Face)
    private static readonly Color Gold = new Color(1f, 0.72f, 0.2f);

    // กอหนาม: ตำแหน่งโคน (มุมรอบกลาง องศา, ระยะจากกลาง ม.), สูง (ม.), เอียงออกนอก (องศา)
    // กล่องโดนดาเมจของ Thorn(Tanee) = กว้าง 1 x สูง 1.37 ม. — กอนี้อยู่ในกล่องพอดี
    private static readonly (float angle, float radius, float height, float tilt)[] SpikeLayout =
    {
        (0f, 0f, 1.45f, 4f),
        (20f, 0.28f, 1.05f, 18f),
        (110f, 0.3f, 0.9f, 22f),
        (200f, 0.26f, 1.15f, 16f),
        (290f, 0.3f, 0.8f, 24f),
        (65f, 0.45f, 0.55f, 32f),
        (245f, 0.45f, 0.6f, 30f),
    };

    [MenuItem("Tools/Yantra/Setup (ใช้ครั้งเดียว)/Setup Tani Thorn + Projectile (หนาม + กระสุนโมเดลจริง)")]
    private static void Run()
    {
        var report = new StringBuilder();

        GameObject thornModel = AssetDatabase.LoadAssetAtPath<GameObject>(ThornModelPath);
        GameObject bulletModel = AssetDatabase.LoadAssetAtPath<GameObject>(BulletModelPath);
        if (thornModel == null) report.AppendLine($"✗ ไม่เจอโมเดลหนาม {ThornModelPath}");
        if (bulletModel == null) report.AppendLine($"✗ ไม่เจอโมเดลกระสุน {BulletModelPath}");

        Texture2D glowTex = SetupFxTexture($"{FxFolder}/Tani_SoftGlow.png");
        Texture2D trailTex = SetupFxTexture($"{FxFolder}/Tani_TrailGlow.png");
        if (glowTex == null || trailTex == null) report.AppendLine("✗ ไม่เจอรูปแสง Tani_SoftGlow / Tani_TrailGlow ใน Data/Fx");

        GameObject burst = FindGroundBurst(report);

        if (thornModel != null) SetupThorn(thornModel, burst, report);
        if (bulletModel != null) SetupBullet(bulletModel, glowTex, trailTex, report);

        AssetDatabase.SaveAssets();
        Debug.Log($"[TaniThornProjectile]\n{report}");
        EditorUtility.DisplayDialog("Tani Thorn + Projectile", report.ToString(), "OK");
    }

    // ---------- หนาม ----------

    private static void SetupThorn(GameObject model, GameObject burst, StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ThornPrefabPath);
        try
        {
            Transform mesh = root.transform.Find("Mesh");
            if (mesh == null)
            {
                report.AppendLine("✗ Thorn(Tanee) ไม่มีลูกชื่อ Mesh — ไม่ได้แก้หนาม");
                return;
            }

            // ท่าขึ้น/ลงขยับ "Mesh" ทั้งก้อน → ของใหม่ใส่ใต้ Mesh จะขึ้นลงตามเดิม
            int removed = 0;
            for (int i = mesh.childCount - 1; i >= 0; i--)
            {
                Transform child = mesh.GetChild(i);
                if (child.name.StartsWith("Cone") || child.name == "Spikes")
                {
                    Object.DestroyImmediate(child.gameObject);
                    removed++;
                }
            }

            var spikes = new GameObject("Spikes").transform;
            spikes.SetParent(mesh, false);
            var rng = new System.Random(7);
            int made = 0;
            for (int i = 0; i < SpikeLayout.Length; i++)
            {
                var s = SpikeLayout[i];
                float a = s.angle * Mathf.Deg2Rad;
                var pivot = new GameObject($"Spike {i + 1}").transform;
                pivot.SetParent(spikes, false);
                pivot.localPosition = new Vector3(Mathf.Cos(a) * s.radius, -0.12f, Mathf.Sin(a) * s.radius);
                // หัน +Z ออกนอกกอ แล้วเอียงไปทาง +Z / หมุนรอบตัวเองสุ่ม ให้แต่ละต้นโค้งคนละทิศ
                float yaw = s.radius > 0f ? Mathf.Atan2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Rad2Deg : 0f;
                float spin = (float)rng.NextDouble() * 360f;
                pivot.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(s.tilt, 0f, 0f) *
                                      Quaternion.Euler(0f, spin, 0f);
                if (FitModel(model, pivot, Vector3.up, s.height, 1.3f, true, report)) made++;
            }

            Transform oldBurst = root.transform.Find(BurstName);
            if (oldBurst != null) Object.DestroyImmediate(oldBurst.gameObject);
            if (burst != null)
            {
                GameObject fx = PrefabUtility.IsPartOfPrefabAsset(burst) && burst.transform.parent == null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(burst, root.transform)
                    : Object.Instantiate(burst, root.transform);
                fx.name = BurstName;
                fx.transform.localPosition = Vector3.zero;   // ที่โคนหนาม — มุมหมุน / ขนาดใช้ตามที่คนทำเอฟเฟคตั้งไว้
            }

            if (root.GetComponent<RestartEffectsOnEnable>() == null)
                root.AddComponent<RestartEffectsOnEnable>();

            PrefabUtility.SaveAsPrefabAsset(root, ThornPrefabPath);
            report.AppendLine($"✓ Thorn(Tanee): ลบกรวย greybox {removed} อัน ใส่หนามจริง {made} ต้น" +
                              (burst != null ? " + GroundBurst" : ""));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------- กระสุน ----------

    private static void SetupBullet(GameObject model, Texture2D glowTex, Texture2D trailTex, StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BulletPrefabPath);
        try
        {
            // ตัวบนสุดเคยถูกบีบแบน (0.21, 0.26, 1) ทำให้โมเดลบางจนมองไม่เห็น → คืนเป็น 1 แล้วขยายที่โมเดลแทน
            root.transform.localScale = Vector3.one;
            var box = root.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = Vector3.zero;
                box.size = new Vector3(0.35f, 0.35f, 1.1f);   // ใกล้ของเดิม (0.26 x 0.22 x 1.14 ม.) อ้วนขึ้นนิดตามโมเดล
            }

            foreach (string n in new[] { "Projectile_Thani", "Visual" })
            {
                Transform old = root.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var holder = new GameObject("Dart").transform;
            holder.SetParent(visual, false);
            // กระสุนหันหน้าไปทางที่บิน (+Z) — ปลายแหลมชี้ +Z, ยาว 1.1 ม., อ้วนขึ้น 1.6 เท่าให้เห็นจากไกล
            FitModel(model, holder, Vector3.forward, 1.1f, 1.6f, false, report);

            Material source = null;
            foreach (Renderer r in holder.GetComponentsInChildren<Renderer>(true))
                if (r.sharedMaterial != null) { source = r.sharedMaterial; break; }
            Material dartMat = GetOrCreateGlowLit($"{FxFolder}/Tani_Projectile_Glow.mat", source);
            foreach (Renderer r in holder.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = dartMat;
                r.sharedMaterials = mats;
            }

            Material glowMat = GetOrCreateAdditive($"{FxFolder}/Tani_Projectile_Halo.mat", glowTex, 0.35f, 1.4f);
            Material trailMat = GetOrCreateAdditive($"{FxFolder}/Tani_Projectile_Trail.mat", trailTex, 0.4f, 1.8f);

            BuildTrail(visual, trailMat);
            BuildHalo(visual, glowMat);

            if (root.GetComponent<RestartEffectsOnEnable>() == null)
                root.AddComponent<RestartEffectsOnEnable>();

            PrefabUtility.SaveAsPrefabAsset(root, BulletPrefabPath);
            report.AppendLine("✓ DeathBlossomProjectileBullet: โมเดลจริงยาว 1.1 ม. เรืองทอง + หางแสง + แสงรอบตัว");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildTrail(Transform parent, Material mat)
    {
        var go = new GameObject("Trail");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.3f);   // ออกจากท้ายลูก
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.22f;                 // ความยาวหาง (วินาที) — ยิ่งมากหางยิ่งยาว
        trail.minVertexDistance = 0.06f;
        trail.widthMultiplier = 0.4f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        trail.textureMode = LineTextureMode.Stretch;
        trail.alignment = LineAlignment.View;
        trail.numCapVertices = 2;
        trail.autodestruct = false;
        trail.emitting = true;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.lightProbeUsage = LightProbeUsage.Off;
        trail.reflectionProbeUsage = ReflectionProbeUsage.Off;
        trail.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
        trail.sharedMaterial = mat;
    }

    private static void BuildHalo(Transform parent, Material mat)
    {
        var go = new GameObject("Halo");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.15f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // แสงกลม ๆ เกิดถี่ อายุสั้น ซ้อนกัน 2-3 อัน = เรืองแสงกะพริบนิด ๆ ตลอดทาง
        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 0.14f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.35f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 8;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 20f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 1.1f)));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    // ---------- จัดโมเดลให้ตั้งตรง / ขนาดที่ต้องการ ไม่ว่าในไฟล์ FBX จะวางแกนหรือจุดหมุนไว้ยังไง ----------

    /// <summary>
    /// ใส่โมเดลใต้ parent แล้วหมุนให้ด้านยาวสุดชี้ไป axis (ปลายแหลมนำ) ยาว length ม.
    /// anchorBase = โคนอยู่ที่จุดหมุน (หนามงอกจากพื้น) / ไม่ติ๊ก = กลางโมเดลอยู่ที่จุดหมุน (กระสุน)
    /// </summary>
    private static bool FitModel(GameObject model, Transform parent, Vector3 axis, float length, float widthMul,
                                 bool anchorBase, StringBuilder report)
    {
        var align = new GameObject("Model").transform;
        align.SetParent(parent, false);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, align);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        List<Vector3> points = CollectPoints(inst, align, out bool fromBounds);
        if (points.Count == 0)
        {
            report.AppendLine($"✗ {model.name}: ไม่เจอ mesh ในโมเดล");
            return false;
        }
        if (fromBounds) report.AppendLine($"! {model.name}: อ่านจุด mesh ไม่ได้ ใช้กรอบแทน — ถ้าหนามกลับหัว บอกได้");

        var b = new Bounds(points[0], Vector3.zero);
        foreach (Vector3 p in points) b.Encapsulate(p);
        int ax = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : (b.size.y >= b.size.z ? 1 : 2);
        float lo = b.min[ax], hi = b.max[ax], span = Mathf.Max(hi - lo, 0.0001f);

        // ปลายแหลม = ปลายที่แคบกว่า (เทียบช่วง 20% ล่างกับ 20% บน)
        float wLo = SliceWidth(points, ax, lo, lo + span * 0.2f);
        float wHi = SliceWidth(points, ax, hi - span * 0.2f, hi);
        float sign = wHi <= wLo ? 1f : -1f;
        Vector3 tipDir = Vector3.zero;
        tipDir[ax] = sign;

        Vector3 anchor = b.center;
        if (anchorBase) anchor[ax] = sign > 0f ? lo : hi;

        float s = length / span;
        Vector3 scale = Vector3.one * (s * widthMul);
        scale[ax] = s;
        Quaternion rot = Quaternion.FromToRotation(tipDir, axis.normalized);
        align.localRotation = rot;
        align.localScale = scale;
        align.localPosition = -(rot * Vector3.Scale(scale, anchor));
        return true;
    }

    // จุดทุกจุดของ mesh ใน space ของ align — อ่านไม่ได้ใช้มุมกรอบ (renderer.bounds) แทน
    private static List<Vector3> CollectPoints(GameObject inst, Transform align, out bool fromBounds)
    {
        var points = new List<Vector3>();
        fromBounds = false;
        Matrix4x4 toAlign = align.worldToLocalMatrix;
        foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>(true))
        {
            var r = mf.GetComponent<Renderer>();
            if (mf.sharedMesh == null || r == null || !r.enabled) continue;
            Matrix4x4 m = toAlign * mf.transform.localToWorldMatrix;
            Vector3[] verts = mf.sharedMesh.vertices;
            if (verts == null || verts.Length == 0)
            {
                fromBounds = true;
                Bounds wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = wb.center + Vector3.Scale(wb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    points.Add(toAlign.MultiplyPoint3x4(c));
                }
                continue;
            }
            foreach (Vector3 v in verts) points.Add(m.MultiplyPoint3x4(v));
        }
        return points;
    }

    private static float SliceWidth(List<Vector3> points, int ax, float from, float to)
    {
        int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
        float min1 = float.MaxValue, max1 = float.MinValue, min2 = float.MaxValue, max2 = float.MinValue;
        bool any = false;
        foreach (Vector3 p in points)
        {
            if (p[ax] < from || p[ax] > to) continue;
            any = true;
            min1 = Mathf.Min(min1, p[a1]); max1 = Mathf.Max(max1, p[a1]);
            min2 = Mathf.Min(min2, p[a2]); max2 = Mathf.Max(max2, p[a2]);
        }
        return any ? (max1 - min1) + (max2 - min2) : float.MaxValue;
    }

    // ---------- หา GroundBurst ----------

    private static GameObject FindGroundBurst(StringBuilder report)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{BurstName} t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null && go.name == BurstName)
            {
                report.AppendLine($"✓ เจอเอฟเฟค {BurstName}: {path}");
                return go;
            }
        }

        // ไม่มี prefab ชื่อนี้ตรง ๆ → หาเป็นลูกใน prefab เอฟเฟคตัวอื่น (เช่น HitboxVFX_root)
        string[] all = AssetDatabase.FindAssets("t:Prefab");
        try
        {
            for (int i = 0; i < all.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(all[i]);
                string lower = path.ToLowerInvariant();
                if (!lower.Contains("vfx") && !lower.Contains("effect") && !lower.Contains("fx")) continue;
                if (EditorUtility.DisplayCancelableProgressBar("หา GroundBurst", path, (float)i / all.Length)) break;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != BurstName) continue;
                    report.AppendLine($"✓ เจอ {BurstName} ใน {path} (ก๊อปมาใส่)");
                    return t.gameObject;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        report.AppendLine($"✗ ไม่เจอ {BurstName} — ลาก GroundBurst ใส่ใต้ Thorn(Tanee) เอง ตำแหน่ง 0,0,0");
        return null;
    }

    // ---------- รูป / material ----------

    private static Texture2D SetupFxTexture(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return null;
        ti.textureType = TextureImporterType.Default;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.mipmapEnabled = true;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ตัวลูกดอก: ก๊อป material ของคนทำโมเดล แล้วเพิ่มเรืองแสงสีทองตามลายผิว (ไม่แก้ไฟล์ต้นฉบับ)
    private static Material GetOrCreateGlowLit(string path, Material source)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = source != null ? new Material(source) : new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (source != null)
        {
            mat.CopyPropertiesFromMaterial(source);
        }

        Texture baseMap = mat.HasProperty("_BaseColorMap") ? mat.GetTexture("_BaseColorMap") : null;
        mat.SetTexture("_EmissiveColorMap", baseMap);
        mat.SetFloat("_UseEmissiveIntensity", 0f);
        mat.SetColor("_EmissiveColorLDR", Gold);
        mat.SetColor("_EmissiveColor", Gold * 1.6f);   // เรืองแค่ไหน — มากขึ้น = สว่างขึ้น
        mat.SetFloat("_EmissiveExposureWeight", 0f);   // ไม่มืดตามฉาก
        HD.HDMaterial.ValidateMaterial(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // แสงบวก (additive) — ตรงที่ดำ = ไม่บวกอะไร
    private static Material GetOrCreateAdditive(string path, Texture2D tex, float unlit, float emissive)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("HDRP/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetFloat("_SurfaceType", 1f);
        mat.SetFloat("_BlendMode", 1f);   // Additive
        mat.SetFloat("_EnableFogOnTransparent", 0f);
        mat.SetFloat("_TransparentZWrite", 0f);
        mat.SetFloat("_DoubleSidedEnable", 1f);
        mat.SetFloat("_CullMode", 0f);
        mat.SetTexture("_UnlitColorMap", tex);
        mat.SetColor("_UnlitColor", Gold * unlit);
        mat.SetTexture("_EmissiveColorMap", tex);
        mat.SetColor("_EmissiveColor", Gold * emissive);
        mat.SetFloat("_EmissiveExposureWeight", 0f);
        HD.HDMaterial.ValidateMaterial(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
#endif
