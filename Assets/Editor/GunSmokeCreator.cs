using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ต้องอยู่ในโฟลเดอร์ Editor
// เลือก Muzzle (จุดปลายกระบอกที่มี MuzzlePoint) แล้วกดเมนู จะได้ควันปืนครบชุด กดซ้ำ = อัปเดตเป็นค่าล่าสุด
public static class GunSmokeCreator
{
    [MenuItem("Tools/VFX/ควันปืน/สร้างหรืออัปเดตที่ปากกระบอก (เลือก Muzzle ก่อน)")]
    private static void Create()
    {
        Transform muzzle = Selection.activeTransform;
        if (muzzle == null)
        {
            EditorUtility.DisplayDialog("ควันปืน", "เลือก Muzzle (จุดปลายกระบอก) ใน Hierarchy ก่อน", "ตกลง");
            return;
        }

        Shader smokeShader = Shader.Find("Shader Graphs/SmokeParticle");
        Shader hazeShader = Shader.Find("Shader Graphs/HeatHaze");
        if (smokeShader == null || hazeShader == null)
        {
            EditorUtility.DisplayDialog("ควันปืน", "หา SmokeParticle หรือ HeatHaze shader ไม่เจอ\nวางไฟล์ .shadergraph ทั้งสองในโปรเจกต์ก่อน", "ตกลง");
            return;
        }

        // มีของเดิมอยู่: สร้างใหม่ทับ แต่เก็บจุดจานดินปืนไว้
        Transform keepPan = null;
        GunSmoke old = muzzle.GetComponent<GunSmoke>();
        if (old != null)
        {
            var oldSo = new SerializedObject(old);
            keepPan = oldSo.FindProperty("_panPoint").objectReferenceValue as Transform;
            var oldRoot = oldSo.FindProperty("_fxRoot").objectReferenceValue as Transform;
            if (oldRoot != null)
                Undo.DestroyObjectImmediate(oldRoot.gameObject);
            Undo.DestroyObjectImmediate(old);
        }

        string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(smokeShader)).Replace('\\', '/');

        Material puffMat = GetMaterial(dir, "Smoke_Puff_Mat", smokeShader);
        SetTexture(puffMat, "_SmokeTex", FindTexture("Smoke_Flipbook", TexKind.Color, TextureWrapMode.Clamp));
        // ไม่ใส่ normal: แสงแยกเงาทีละก้อนทำให้ควันดูเป็นก้อนๆ
        if (puffMat.HasProperty("_SmokeNormal"))
            puffMat.SetTexture("_SmokeNormal", null);
        SetFloat(puffMat, "_SelfLight", 120f);
        SetFloat(puffMat, "_Opacity", 1f);
        SetFloat(puffMat, "_Erosion", 0.9f);

        Material hazeMat = GetMaterial(dir, "HeatHaze_Mat", hazeShader);
        SetTexture(hazeMat, "_HazeTex", FindTexture("HeatHaze_Noise", TexKind.Data, TextureWrapMode.Repeat));
        SetFloat(hazeMat, "_Strength", 0.15f);

        Material shockMat = GetMaterial(dir, "Shockwave_Mat", hazeShader);
        SetTexture(shockMat, "_HazeTex", FindTexture("Shockwave_Ring", TexKind.Data, TextureWrapMode.Clamp));
        SetFloat(shockMat, "_Strength", 0.4f);
        if (shockMat.HasProperty("_Scroll")) shockMat.SetVector("_Scroll", Vector4.zero);
        if (shockMat.HasProperty("_Tiling")) shockMat.SetVector("_Tiling", new Vector4(1f, 1f, 0f, 0f));

        Shader glow = Shader.Find("Shader Graphs/GlowParticle");
        Material emberMat = glow != null ? GetMaterial(dir, "Smoke_Ember_Mat", glow) : null;
        SetFloat(emberMat, "_Intensity", 600f);
        AssetDatabase.SaveAssets();

        var root = new GameObject("GunSmokeFX");
        root.layer = 0;   // วาดด้วยกล้องหลัก ไม่ใช่กล้องปืน
        root.transform.SetParent(muzzle, false);

        ParticleSystem puff = BuildPuff(root.transform, "Puff", puffMat);
        ParticleSystem shock = BuildShockwave(root.transform, shockMat);
        ParticleSystem pan = BuildPuff(root.transform, "PanPuff", puffMat);
        ParticleSystem linger = BuildLinger(root.transform, puffMat);
        ParticleSystem haze = BuildHaze(root.transform, hazeMat);
        ParticleSystem embers = emberMat != null ? BuildEmbers(root.transform, emberMat) : null;

        var smoke = Undo.AddComponent<GunSmoke>(muzzle.gameObject);
        var so = new SerializedObject(smoke);
        so.FindProperty("_fxRoot").objectReferenceValue = root.transform;
        so.FindProperty("_puff").objectReferenceValue = puff;
        so.FindProperty("_shockwave").objectReferenceValue = shock;
        so.FindProperty("_pan").objectReferenceValue = pan;
        so.FindProperty("_linger").objectReferenceValue = linger;
        so.FindProperty("_haze").objectReferenceValue = haze;
        so.FindProperty("_embers").objectReferenceValue = embers;
        so.FindProperty("_panPoint").objectReferenceValue = keepPan;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(root, "สร้างควันปืน");
        Selection.activeGameObject = muzzle.gameObject;
    }

    // ---------- ชิ้นต่างๆ ----------

    private static ParticleSystem NewSystem(Transform parent, string name, Material mat, bool loop, bool litSmoke)
    {
        var go = new GameObject(name);
        go.layer = 0;
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = loop;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startSpeed = 0f;
        main.maxParticles = 800;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.enabled = false;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 3f;
        r.alignment = ParticleSystemRenderSpace.View;
        // ควันที่มี normal ต้องส่ง normal กับ tangent ไปให้ shader ไม่งั้นแสงบนควันจะผิด
        if (litSmoke)
        {
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Tangent,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV
            });
        }
        return ps;
    }

    // ภาพควันเคลื่อนไหว 64 เฟรม เล่นตลอดอายุของแต่ละก้อน
    private static void Flipbook(ParticleSystem ps)
    {
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 8;
        sheet.numTilesY = 8;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 0.999f));
        sheet.cycleCount = 1;
    }

    private static ParticleSystem BuildPuff(Transform parent, string name, Material mat)
    {
        ParticleSystem ps = NewSystem(parent, name, mat, false, true);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        // ก้อนบางๆ ซ้อนกันเยอะๆ = ควันหนาแต่เนียน ไม่เห็นเป็นก้อน
        main.startColor = new Color(0.9f, 0.9f, 0.92f, 0.45f);
        main.gravityModifier = -0.02f;

        // อากาศต้าน: พุ่งออกแรงแล้วหยุดเร็ว แบบควันดินปืนจริง
        var lv = ps.limitVelocityOverLifetime;
        lv.enabled = true;
        lv.limit = 100f;
        lv.drag = 4f;
        lv.multiplyDragByParticleSize = false;
        lv.multiplyDragByParticleVelocity = false;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.12f, 1.2f), new Keyframe(1f, 3.5f)));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Fade(new[] { 0f, 0.03f, 0.7f, 1f }, new[] { 0f, 1f, 0.8f, 0f });

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.8f;
        noise.scrollSpeed = 0.3f;
        noise.damping = true;
        noise.octaveCount = 2;

        Flipbook(ps);
        return ps;
    }

    // ควันม้วนออกจากปากกระบอกกว้างช้าๆ หลังยิง
    private static ParticleSystem BuildLinger(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem(parent, "Linger", mat, true, true);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.9f, 0.9f, 0.92f, 0.35f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.012f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.12f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.1f;
        noise.frequency = 1.5f;
        noise.scrollSpeed = 0.4f;
        noise.damping = true;
        noise.octaveCount = 2;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 5f));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Fade(new[] { 0f, 0.1f, 0.7f, 1f }, new[] { 0f, 1f, 0.7f, 0f });

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        Flipbook(ps);
        return ps;
    }

    // คลื่นกระแทก: วงบิดภาพขยายออกเร็วๆ ตอนยิง
    private static ParticleSystem BuildShockwave(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem(parent, "Shockwave", mat, false, false);
        var main = ps.main;
        main.startLifetime = 0.22f;
        main.startSize = 0.6f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.1f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f)));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Fade(new[] { 0f, 0.1f, 1f }, new[] { 1f, 1f, 0f });
        return ps;
    }

    private static ParticleSystem BuildHaze(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem(parent, "HeatHaze", mat, true, false);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = -0.2f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Fade(new[] { 0f, 0.2f, 1f }, new[] { 0f, 1f, 0f });
        return ps;
    }

    private static ParticleSystem BuildEmbers(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem(parent, "Embers", mat, false, false);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.015f);
        main.startColor = new Color(1f, 0.72f, 0.3f, 1f);
        main.gravityModifier = 0.4f;

        var lv = ps.limitVelocityOverLifetime;
        lv.enabled = true;
        lv.limit = 100f;
        lv.drag = 1.5f;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Fade(new[] { 0f, 0.7f, 1f }, new[] { 1f, 0.8f, 0f });

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 1.2f;
        return ps;
    }

    private static ParticleSystem.MinMaxGradient Fade(float[] times, float[] alphas)
    {
        var g = new Gradient();
        var a = new GradientAlphaKey[times.Length];
        for (int i = 0; i < times.Length; i++)
            a[i] = new GradientAlphaKey(alphas[i], times[i]);
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, a);
        return new ParticleSystem.MinMaxGradient(g);
    }

    // ---------- material / texture ----------

    private enum TexKind { Color, Data, Normal }

    private static Material GetMaterial(string dir, string name, Shader shader)
    {
        string path = dir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            ResetHdrpKeywords(mat);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
            ResetHdrpKeywords(mat);
        }
        return mat;
    }

    private static void SetTexture(Material mat, string name, Texture tex)
    {
        if (mat == null || tex == null || !mat.HasProperty(name))
            return;
        mat.SetTexture(name, tex);
        EditorUtility.SetDirty(mat);
    }

    private static void SetFloat(Material mat, string name, float value)
    {
        if (mat == null || !mat.HasProperty(name))
            return;
        mat.SetFloat(name, value);
        EditorUtility.SetDirty(mat);
    }

    private static Texture2D FindTexture(string name, TexKind kind, TextureWrapMode wrap)
    {
        string path = null;
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) { path = p; break; }
        }
        if (path == null)
        {
            Debug.LogWarning("[GunSmoke] หารูป " + name + " ไม่เจอ");
            return null;
        }

        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            TextureImporterType type = kind == TexKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool srgb = kind == TexKind.Color;
            bool changed = importer.textureType != type || importer.wrapMode != wrap
                           || (kind != TexKind.Normal && importer.sRGBTexture != srgb)
                           || (kind == TexKind.Color && !importer.alphaIsTransparency);
            if (changed)
            {
                importer.textureType = type;
                if (kind != TexKind.Normal)
                    importer.sRGBTexture = srgb;
                importer.alphaIsTransparency = kind == TexKind.Color;
                importer.wrapMode = wrap;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void ResetHdrpKeywords(Material mat)
    {
        try
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils");
                MethodInfo m = t?.GetMethod("ResetMaterialKeywords", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Material) }, null);
                if (m == null) continue;
                m.Invoke(null, new object[] { mat });
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GunSmoke] ตั้งค่า material ไม่สำเร็จ ลองคลิก material ให้ Inspector เปิดหนึ่งครั้ง: " + e.Message);
        }
    }
}