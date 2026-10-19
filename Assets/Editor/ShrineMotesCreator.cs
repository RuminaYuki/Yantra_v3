using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ต้องอยู่ในโฟลเดอร์ Editor
public static class ShrineMotesCreator
{
    private const string ShaderName = "Shader Graphs/GlowParticle";
    private const string ObjectName = "ShrineMotes";

    [MenuItem("Tools/VFX/สร้างละอองเรืองแสง (เลือกศาลก่อน)")]
    private static void Create()
    {
        Transform target = Selection.activeTransform;
        if (target == null)
        {
            Debug.LogWarning("[ละออง] คลิกเลือกศาลใน Hierarchy ก่อน แล้วค่อยกดเมนูนี้");
            return;
        }

        Transform existing = target.Find(ObjectName);
        if (existing != null)
        {
            Selection.activeTransform = existing;
            Debug.LogWarning($"[ละออง] {target.name} มี {ObjectName} อยู่แล้ว — ลบอันเดิมก่อนถ้าจะสร้างใหม่", existing);
            return;
        }

        Material material = GetOrCreateMaterial();
        if (material == null)
            return;

        Bounds bounds = GetBounds(target);

        var go = new GameObject(ObjectName);
        Undo.RegisterCreatedObjectUndo(go, "สร้างละอองเรืองแสง");
        go.transform.SetParent(target, false);
        go.transform.position = bounds.center;
        go.transform.rotation = Quaternion.identity;

        var ps = go.AddComponent<ParticleSystem>();
        Setup(ps, Mathf.Clamp(bounds.extents.magnitude, 0.8f, 2.5f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Selection.activeGameObject = go;
        Debug.Log($"[ละออง] สร้าง {ObjectName} ใต้ {target.name} แล้ว", go);
    }

    private static void Setup(ParticleSystem ps, float radius)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.55f, 0.2f));
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 10f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        shape.radiusThickness = 1f;

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.2f;
        noise.damping = true;

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(fade);

        // กระพริบเบาๆ เหมือนหิ่งห้อย
        var twinkle = new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(0.5f, 0.7f),
            new Keyframe(0.75f, 1f), new Keyframe(1f, 0.5f));
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, twinkle);

        ps.Play(true);
    }

    private static Material GetOrCreateMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("[ละออง] ไม่เจอ shader GlowParticle — ลากไฟล์ GlowParticle.shadergraph เข้าโปรเจกต์ก่อน");
            return null;
        }

        string shaderPath = AssetDatabase.GetAssetPath(shader);
        string folder = string.IsNullOrEmpty(shaderPath) ? "Assets" : Path.GetDirectoryName(shaderPath).Replace('\\', '/');
        string materialPath = folder + "/GlowParticle_Mat.mat";

        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material != null)
            return material;

        material = new Material(shader);
        AssetDatabase.CreateAsset(material, materialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Bounds GetBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        bool found = false;
        var bounds = new Bounds(target.position + Vector3.up, Vector3.one);
        foreach (Renderer r in renderers)
        {
            if (r is ParticleSystemRenderer)
                continue;
            if (!found)
            {
                bounds = r.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return bounds;
    }
}
