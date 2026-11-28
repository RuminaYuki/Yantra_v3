#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using HD = UnityEngine.Rendering.HighDefinition;

/// <summary>
/// เปิดตัวบอสตานี — กดครั้งเดียว (กดซ้ำได้ จะสร้างใหม่ทับของเดิม)
///   1. ตั้งค่ารูป UI (ชื่อบอส / หลอดเลือด) ใน Assets/Ui/Boss/Tani ให้เป็น Sprite
///   2. สร้าง Volume Profile สุริยุปราคา + material ดวงอาทิตย์คราส ใน .../Tanee/Data/Eclipse
///   3. ใส่ตัว "BossIntro" ในพรีแฟบ Tanee(Boss): Volume + ดวงอาทิตย์ + Canvas ชื่อบอส/หลอดเลือด
///   4. หนามล้อมลาน (ArenaThornWall ใช้ Thorn(Tanee)) + ใบกล้วยร่วง (Particle System)
/// ไม่แตะ table — BossIntro ดูเองว่า Animator ตานีเข้า Jump_ShowDown ครั้งแรก = เริ่มสู้
/// </summary>
public static class TaniBossIntroSetup
{
    private const string PrefabPath = "Assets/Prefab/Character/Enemy/Boss/Tanee/Tanee(Boss).prefab";
    private const string UiFolder = "Assets/Ui/Boss/Tani";
    private const string FxFolder = "Assets/Prefab/Character/Enemy/Boss/Tanee/Data/Eclipse";
    private const string RootName = "BossIntro";
    private const string ThornPrefabPath = "Assets/Prefab/Character/Enemy/Boss/Tanee/Data/Thorn(Tanee).prefab";

    private static readonly string[] SpriteNames =
    {
        "BossTitle_Tani", "BossSubtitle_Tani", "BossBar_Name_Tani",
        "BossBar_Back", "BossBar_Fill", "BossBar_Frame", "BossBar_Notch"
    };

    [MenuItem("Tools/Yantra/Setup (ใช้ครั้งเดียว)/Setup Tani Boss Intro (เปิดตัวบอส + สุริยุปราคา)")]
    private static void Run()
    {
        var report = new StringBuilder();

        foreach (string n in SpriteNames)
            if (!SetupSprite($"{UiFolder}/{n}.png")) report.AppendLine($"✗ ไม่เจอรูป {n}.png");

        string sunTexPath = $"{FxFolder}/Tani_EclipseSun.png";
        Texture2D sunTex = SetupSunTexture(sunTexPath);
        if (sunTex == null) report.AppendLine("✗ ไม่เจอรูป Tani_EclipseSun.png");

        VolumeProfile profile = GetOrCreateProfile($"{FxFolder}/Tani_Eclipse_Profile.asset");
        Material sunMat = GetOrCreateSunMaterial($"{FxFolder}/Tani_EclipseSun_Mat.mat", sunTex);

        string leafTexPath = $"{FxFolder}/Tani_FallingLeaves.png";
        Texture2D leafTex = SetupSunTexture(leafTexPath);
        if (leafTex == null) report.AppendLine("✗ ไม่เจอรูป Tani_FallingLeaves.png");
        Material leafMat = GetOrCreateLeafMaterial($"{FxFolder}/Tani_FallingLeaves_Mat.mat", leafTex);

        GameObject thornPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThornPrefabPath);
        if (thornPrefab == null) report.AppendLine("✗ ไม่เจอ Thorn(Tanee).prefab — ใส่ช่อง Thorn Prefab ใน Arena Wall เอง");
        AssetDatabase.SaveAssets();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform old = root.transform.Find(RootName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
                report.AppendLine("ลบ BossIntro ตัวเก่าแล้วสร้างใหม่");
            }

            Build(root, profile, sunMat, leafMat, thornPrefab, report);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        report.Insert(0, "✓ ใส่ BossIntro ใน Tanee(Boss) แล้ว\n✓ Volume สุริยุปราคา + ดวงอาทิตย์คราส\n✓ ชื่อบอส + หลอดเลือด\n\n");
        Debug.Log($"[TaniBossIntro]\n{report}");
        EditorUtility.DisplayDialog("Tani Boss Intro", report.ToString(), "OK");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    // ---------- ประกอบในพรีแฟบ ----------

    private static void Build(GameObject root, VolumeProfile profile, Material sunMat, Material leafMat,
                              GameObject thornPrefab, StringBuilder report)
    {
        var intro = new GameObject(RootName);
        intro.transform.SetParent(root.transform, false);
        var bossIntro = intro.AddComponent<BossIntro>();

        // Volume — ทั้งฉาก (global) น้ำหนัก 0 จนกว่าจะเริ่มสู้
        var volGo = new GameObject("Eclipse Volume");
        volGo.transform.SetParent(intro.transform, false);
        var volume = volGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 0f;
        volume.sharedProfile = profile;

        // ดวงอาทิตย์คราส
        var sunGo = new GameObject("Eclipse Sun");
        sunGo.transform.SetParent(intro.transform, false);
        sunGo.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var sunRenderer = sunGo.AddComponent<MeshRenderer>();
        sunRenderer.sharedMaterial = sunMat;
        sunRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sunRenderer.receiveShadows = false;
        sunRenderer.lightProbeUsage = LightProbeUsage.Off;
        sunRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        sunRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
        var sun = sunGo.AddComponent<EclipseSun>();

        // Canvas — อยู่ใต้ UI หลัก (Pause / HUD ทับได้)
        var canvasGo = new GameObject("Boss UI", typeof(RectTransform));
        canvasGo.transform.SetParent(intro.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -5;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var canvasRt = (RectTransform)canvasGo.transform;

        // ชื่อบอส — ใต้กึ่งกลางจอนิดหน่อย
        CanvasGroup title = Group("Title", canvasRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f));
        Picture("Name", (RectTransform)title.transform, "BossTitle_Tani", Vector2.zero);
        CanvasGroup subtitle = Group("Subtitle", canvasRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -265f));
        Picture("Text", (RectTransform)subtitle.transform, "BossSubtitle_Tani", Vector2.zero);

        // หลอดเลือด — บนกลางจอ
        CanvasGroup bar = Group("Health Bar", canvasRt, new Vector2(0.5f, 1f), new Vector2(0f, -58f));
        var barRt = (RectTransform)bar.transform;
        barRt.localScale = Vector3.one * 0.62f;   // ไม่ให้บังจอ — อยากได้ใหญ่ / เล็กปรับ Scale ตัวนี้
        Picture("Back", barRt, "BossBar_Back", Vector2.zero);
        Image trail = Picture("Trail", barRt, "BossBar_Fill", Vector2.zero);
        trail.color = new Color(1f, 0.85f, 0.7f, 0.75f);
        MakeFilled(trail);
        Image fill = Picture("Fill", barRt, "BossBar_Fill", Vector2.zero);
        MakeFilled(fill);
        Picture("Frame", barRt, "BossBar_Frame", Vector2.zero);
        Picture("Half Mark", barRt, "BossBar_Notch", Vector2.zero);   // ครึ่งหลอด = ตานีเข้าเฟส 2
        Image barName = Picture("Name", barRt, "BossBar_Name_Tani", Vector2.zero);
        if (barName != null && barName.sprite != null)
        {
            Vector2 size = barName.rectTransform.sizeDelta;
            barName.rectTransform.anchoredPosition = new Vector2(-500f + size.x * 0.5f - 6f, 11f + size.y * 0.5f - 16f);
        }

        // หา Animator / Health ของตานี
        Animator animator = null;
        foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
        {
            if (a.GetComponent<PairedAnimationActor>() != null) { animator = a; break; }
            if (animator == null) animator = a;
        }
        Health health = root.GetComponentInChildren<Health>(true);
        if (animator == null) report.AppendLine("✗ หา Animator ของตานีไม่เจอ — ใส่ช่อง Animator ใน BossIntro เอง");
        if (health == null) report.AppendLine("✗ หา Health ของตานีไม่เจอ — ใส่ช่อง Health ใน BossIntro เอง");

        var so = new SerializedObject(bossIntro);
        so.FindProperty("_animator").objectReferenceValue = animator;
        so.FindProperty("_health").objectReferenceValue = health;
        so.FindProperty("_title").objectReferenceValue = title;
        so.FindProperty("_subtitle").objectReferenceValue = subtitle;
        so.FindProperty("_bar").objectReferenceValue = bar;
        so.FindProperty("_barFill").objectReferenceValue = fill;
        so.FindProperty("_barTrail").objectReferenceValue = trail;
        so.FindProperty("_volume").objectReferenceValue = volume;
        so.FindProperty("_sun").objectReferenceValue = sun;

        // หนามล้อมลาน
        var wallGo = new GameObject("Arena Wall");
        wallGo.transform.SetParent(intro.transform, false);
        var wall = wallGo.AddComponent<ArenaThornWall>();
        var wallSo = new SerializedObject(wall);
        wallSo.FindProperty("_thornPrefab").objectReferenceValue = thornPrefab;
        wallSo.FindProperty("_boss").objectReferenceValue = animator != null ? animator.transform : root.transform;
        wallSo.FindProperty("_gloryPoints").objectReferenceValue = root.GetComponentInChildren<RandomGloryPoint>(true);
        wallSo.ApplyModifiedPropertiesWithoutUndo();
        so.FindProperty("_arenaWall").objectReferenceValue = wall;

        // ใบกล้วยร่วง
        so.FindProperty("_leaves").objectReferenceValue = BuildLeaves(intro.transform, leafMat);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ParticleSystem BuildLeaves(Transform parent, Material mat)
    {
        var go = new GameObject("Falling Leaves");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 12f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.03f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 1f, 34f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.9f;
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.25f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.9f);   // ลมพัดเฉียง ๆ
        velocity.y = new ParticleSystem.MinMaxCurve(-0.6f, -0.3f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
        rotation.y = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        rotation.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.06f, 1f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0f));
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = 2;
        sheet.numTilesY = 2;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    private static CanvasGroup Group(string name, RectTransform parent, Vector2 anchor, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(1200f, 300f);
        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    private static Image Picture(string name, RectTransform parent, string sprite, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/{sprite}.png");
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        if (img.sprite != null) img.SetNativeSize();
        return img;
    }

    private static void MakeFilled(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
    }

    // ---------- asset ----------

    private static bool SetupSprite(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return false;
        bool changed = ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single ||
                       ti.mipmapEnabled || !ti.alphaIsTransparency;
        if (!changed) return true;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
        return true;
    }

    private static Texture2D SetupSunTexture(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return null;
        ti.textureType = TextureImporterType.Default;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.mipmapEnabled = true;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static VolumeProfile GetOrCreateProfile(string path)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        // ค่าทั้งหมดคิดเทียบกับ MilkOptimzeOutdoor (volume หลักของฉาก) — ค่าไหนไม่ตั้ง จะใช้ของ Milk ต่อ
        // โทนสุริยุปราคา: มืดกว่า Milk นิดเดียว (-0.2 EV) อมส้มแดง เงาเข้ม ขอบจอแดงคล้ำ หมอกเดิมแต่เป็นสีส้มหม่น
        Add<HD.ColorAdjustments>(profile, c =>
        {
            c.postExposure.Override(0.05f);      // Milk 0.26 — มืดไป / สว่างไป ปรับตัวนี้ตัวเดียว
            c.contrast.Override(0f);              // Milk -9.5
            c.colorFilter.Override(new Color(1f, 0.86f, 0.8f));
            c.saturation.Override(-6f);
        });
        Add<HD.WhiteBalance>(profile, c => c.temperature.Override(10f));   // Milk -19.4 (ฟ้าเย็น) → อุ่นขึ้น
        Add<HD.SplitToning>(profile, c =>
        {
            c.shadows.Override(new Color(0.26f, 0.31f, 0.3f));   // เข้มพอ ๆ กับ Milk (0.28) อมเขียวหม่น
            c.highlights.Override(new Color(1f, 0.62f, 0.36f));
            c.balance.Override(-20f);
        });
        Add<HD.Vignette>(profile, c =>
        {
            c.intensity.Override(0.42f);          // Milk 0.37
            c.smoothness.Override(0.45f);
            c.color.Override(new Color(0.16f, 0.02f, 0f));
        });
        Add<HD.Bloom>(profile, c =>
        {
            c.intensity.Override(0.35f);
            c.scatter.Override(0.75f);
        });
        Add<HD.ChromaticAberration>(profile, c => c.intensity.Override(0.2f));
        Add<HD.Fog>(profile, c =>
        {
            // ไม่ทับความหนาหมอก (Mean Free Path) — ใช้หมอกหนาของ Milk ต่อ ไม่งั้นเข้าสู้แล้วฉากโล่งทันที
            c.meanFreePath.overrideState = false;
            c.color.Override(new Color(0.6f, 0.44f, 0.4f));      // Milk ใช้สีหมอกแบบ Constant (ฟ้าเทา) → ส้มหม่น
            c.tint.Override(new Color(1f, 0.65f, 0.58f));        // เผื่อฉากที่ใช้สีหมอกแบบ Sky
            c.albedo.Override(new Color(0.95f, 0.7f, 0.6f));
        });

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void Add<T>(VolumeProfile profile, System.Action<T> set) where T : VolumeComponent
    {
        if (!profile.TryGet(out T comp))
        {
            comp = profile.Add<T>(false);
            comp.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(comp, profile);
        }
        set(comp);
        EditorUtility.SetDirty(comp);
    }

    private static Material GetOrCreateLeafMaterial(string path, Texture2D tex)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        // ทึบ + ตัดขอบตามรูปใบ (alpha clip) สองหน้า — รับแสงในฉากจริง ฟ้ามืดใบก็มืดตาม
        mat.SetTexture("_BaseColorMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_AlphaCutoffEnable", 1f);
        mat.SetFloat("_AlphaCutoff", 0.5f);
        mat.SetFloat("_DoubleSidedEnable", 1f);
        mat.SetFloat("_Smoothness", 0.25f);
        mat.SetFloat("_Metallic", 0f);
        HD.HDMaterial.ValidateMaterial(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material GetOrCreateSunMaterial(string path, Texture2D tex)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        // โปร่งใสแบบบวกแสง: ตรงกลางดำ = ไม่บวกอะไร เห็นฟ้าข้างหลัง / แสงรอบดวงเรืองจนติด bloom
        mat.SetFloat("_SurfaceType", 1f);
        mat.SetFloat("_BlendMode", 1f);            // Additive
        mat.SetFloat("_EnableFogOnTransparent", 0f);
        mat.SetFloat("_TransparentZWrite", 0f);
        mat.SetFloat("_DoubleSidedEnable", 1f);
        mat.SetFloat("_CullMode", 0f);
        mat.SetTexture("_UnlitColorMap", tex);
        mat.SetColor("_UnlitColor", new Color(0.6f, 0.5f, 0.45f, 1f));
        mat.SetTexture("_EmissiveColorMap", tex);
        mat.SetColor("_EmissiveColor", new Color(1f, 0.72f, 0.5f) * 2.5f);
        mat.SetFloat("_EmissiveExposureWeight", 0f);
        HD.HDMaterial.ValidateMaterial(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
#endif
