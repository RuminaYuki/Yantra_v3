using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

// The owning system calls PlayDissolve/ResetDissolve explicitly. No Health or StateMachine dependency.
[DisallowMultipleComponent]
public class DissolveVFXPlayer : MonoBehaviour
{
    [Header("โมเดลที่ต้องการให้หาย")]
    [Tooltip("เว้นว่าง = หา SkinnedMeshRenderer ใต้ GameObject นี้")]
    [SerializeField] private Transform _visualRoot;
    [Tooltip("เว้นว่าง = หาให้เอง ใส่เองได้ถ้าต้องการเลือกเฉพาะชิ้นส่วนของโมเดล")]
    [SerializeField] private Renderer[] _renderers = new Renderer[0];
    [Tooltip("Material Dissolve ที่มีอยู่ ใช้เป็นต้นแบบเมื่อเรียก PlayDissolve")]
    [SerializeField] private Material _dissolveMaterial;
    [SerializeField] private string _amountProperty = "_DissolveAmount";
    [Tooltip("บอกใน Console ว่ายกรูปผิว/สีของโมเดลไปใส่ Material Dissolve ได้ไหม (ไว้หาสาเหตุตอนเป็นสีเทาหรือขาว)")]
    [SerializeField] private bool _logMaterialCopy = true;
    [Tooltip("บอกใน Console ทุกขั้นตอน เริ่มสลาย / สลายเสร็จ / ถูกรีเซ็ตกลับ พร้อมบอกว่าใครสั่ง")]
    [SerializeField] private bool _logSteps = true;

    [Header("เวลา")]
    [SerializeField, Min(0f)] private float _delay = 0.2f;
    [SerializeField, Min(0.01f)] private float _duration = 1.5f;
    [SerializeField] private float _visibleAmount = 0f;
    [SerializeField] private float _hiddenAmount = 1f;

    [System.Serializable]
    private class EffectSlot
    {
        [Tooltip("ลาก Prefab เอฟเฟกต์ใส่ได้เลย (Particle System / VFX Graph / Effekseer)\n" +
                 "หรือลาก object ในฉากที่ปิด active ไว้ ระบบจะก๊อปแล้วเปิดเล่นให้")]
        public GameObject Effect;

        [Tooltip("จุดที่เอฟเฟกต์เกิด — เว้นว่าง = Visual Root หรือตัวนี้")]
        public Transform Point;

        [Tooltip("ติ๊ก = เอฟเฟกต์ขยับตามจุดเกิด\nไม่ติ๊ก = เกิดแล้วอยู่กับที่ ไม่หายไปพร้อมโมเดล")]
        public bool FollowPoint = true;

        [Tooltip("ให้ Particle ตัวหลักพ่นออกมาจากผิวโมเดล — ใช้ได้กับ Particle System เท่านั้น")]
        public bool EmitFromMesh;

        [Tooltip("ลบเอฟเฟกต์ทิ้งหลังกี่วินาที — 0 = ไม่ลบเอง (ลบตอน Reset)")]
        [Min(0f)] public float Lifetime = 3f;
    }

    [Header("วนเล่นซ้ำ (ไว้โชว์ mockup)")]
    [Tooltip("ติ๊ก = เล่นเองตอนเริ่ม แล้ววน หาย → กลับมา → หายใหม่ ไปเรื่อยๆ\n" +
             "ไม่ต้องมีระบบอื่นเรียก PlayDissolve — ห้ามติ๊กกับตัวที่ใช้ในเกมจริง")]
    [SerializeField] private bool _loop;

    [Tooltip("ติ๊ก = ค่อยๆ ปรากฏกลับมา (dissolve ย้อนกลับ)\nไม่ติ๊ก = โผล่กลับมาทันที")]
    [SerializeField] private bool _loopFadeIn = true;

    [Tooltip("หายไปนานกี่วินาทีก่อนกลับมา")]
    [SerializeField, Min(0f)] private float _loopHiddenTime = 1f;

    [Tooltip("กลับมาแล้วโชว์อยู่กี่วินาทีก่อนหายรอบใหม่")]
    [SerializeField, Min(0f)] private float _loopVisibleTime = 1f;

    [Header("เอฟเฟกต์ (ลากใส่ได้เลย ไม่ใส่ก็ได้)")]
    [Tooltip("เล่นตอนเริ่มหาย")]
    [SerializeField] private EffectSlot _startEffect = new EffectSlot();

    [Tooltip("เล่นตอนหายหมดแล้ว")]
    [SerializeField] private EffectSlot _finishEffect = new EffectSlot { FollowPoint = false };

    [Header("Event เพิ่มเติม (ไม่ใส่ก็ได้)")]
    [Tooltip("เรียกเมื่อเริ่มหาย ต่อ ParticleSystem.Play หรือ VisualEffect.Play ของโมเดลตัวนี้ได้")]
    [SerializeField] private UnityEvent _onDissolveStarted = new UnityEvent();
    [SerializeField] private UnityEvent _onDissolveFinished = new UnityEvent();

    private sealed class RendererState
    {
        public Renderer Renderer;
        public bool OriginalEnabled;
        public Material[] OriginalMaterials;
        public Material[] RuntimeMaterials;
        public MaterialPropertyBlock[] OriginalBlocks;
        public MaterialPropertyBlock[] WorkingBlocks;
        public bool MaterialsRestored;
        public bool MaterialsApplied;
    }

    private readonly List<RendererState> _states = new List<RendererState>();
    private readonly List<GameObject> _attachedEffects = new List<GameObject>();
    private bool _played;
    private bool _finished;
    private int _amountId;
    private int _runVersion;

    public void PlayDissolve()
    {
        LogStep($"PlayDissolve ถูกเรียก (เล่นไปแล้ว: {_played})");
        if (_played || !isActiveAndEnabled || !PrepareMaterials())
            return;

        _played = true;
        _finished = false;
        _runVersion++;
        StartCoroutine(DissolveRoutine(_runVersion));
    }

    private bool PrepareMaterials()
    {
        if (string.IsNullOrWhiteSpace(_amountProperty))
        {
            Debug.LogWarning("[Dissolve VFX] ตั้งชื่อ Amount Property ก่อน", this);
            return false;
        }
        _amountId = Shader.PropertyToID(_amountProperty);

        Renderer[] targets = _renderers;
        if (targets == null || targets.Length == 0)
        {
            Transform root = _visualRoot != null ? _visualRoot : transform;
            targets = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (targets.Length == 0)
                targets = root.GetComponentsInChildren<MeshRenderer>(true);
        }

        var valid = new List<Renderer>();
        var unique = new HashSet<Renderer>();
        // Validate every visible material before replacing anything.
        foreach (Renderer renderer in targets)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !unique.Add(renderer))
                continue;
            // ร่างเรืองแสงที่ GhostRimGlow สร้าง ให้ GhostRimGlow คุมการจางเอง
            if (renderer.gameObject.name.EndsWith("_Rim"))
                continue;

            Material[] materials = renderer.sharedMaterials;
            bool hasMaterial = false;
            foreach (Material material in materials)
            {
                if (material == null)
                    continue;
                hasMaterial = true;
                if (!SupportsAmount(material) && !SupportsAmount(_dissolveMaterial))
                {
                    Debug.LogWarning($"[Dissolve VFX] ลาก Material Dissolve ที่มี {_amountProperty} ใส่ก่อน — {renderer.name}", this);
                    return false;
                }
            }
            if (hasMaterial)
                valid.Add(renderer);
        }

        if (valid.Count == 0)
        {
            Debug.LogWarning("[Dissolve VFX] ไม่พบ Renderer ของโมเดลที่ต้องการให้หายที่กำลังแสดงอยู่", this);
            return false;
        }

        foreach (Renderer renderer in valid)
        {
            Material[] original = renderer.sharedMaterials;
            var state = new RendererState
            {
                Renderer = renderer,
                OriginalEnabled = renderer.enabled,
                OriginalMaterials = original,
                RuntimeMaterials = new Material[original.Length],
                OriginalBlocks = new MaterialPropertyBlock[original.Length],
                WorkingBlocks = new MaterialPropertyBlock[original.Length]
            };
            for (int i = 0; i < original.Length; i++)
            {
                if (original[i] == null)
                    continue;

                bool ownShader = SupportsAmount(original[i]);
                Material template = ownShader ? original[i] : _dissolveMaterial;
                state.RuntimeMaterials[i] = new Material(template) { name = template.name + " (Dissolve instance)" };
                // ใช้ Material Dissolve แทนของเดิม: ต้องยกรูปผิว/สี/normal ของโมเดลมาด้วย ไม่งั้นจะกลายเป็นสีเทา
                if (!ownShader)
                    CopyLook(original[i], state.RuntimeMaterials[i], renderer);
                state.OriginalBlocks[i] = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(state.OriginalBlocks[i], i);
                state.WorkingBlocks[i] = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(state.WorkingBlocks[i], i);
                if (state.WorkingBlocks[i].isEmpty)
                    renderer.GetPropertyBlock(state.WorkingBlocks[i]);
            }
            _states.Add(state);
        }
        return true;
    }

    // ---------- ยกหน้าตาเดิมของโมเดลไปใส่ Material Dissolve ----------

    private static readonly string[] BaseTexSources = { "_BaseColorMap", "_BaseMap", "_MainTex", "_Albedo", "_AlbedoMap", "_BaseTexture", "_BaseTex", "_Diffuse", "_DiffuseMap" };
    private static readonly string[] BaseColorSources = { "_BaseColor", "_Color", "_Tint", "_TintColor", "_AlbedoColor" };
    private static readonly string[] NormalSources = { "_NormalMap", "_BumpMap", "_Normal", "_NormalTex" };
    // ชื่อเดียวกับ HDRP/Lit ก๊อปตรงๆ ได้เลย (ความเงา ความเป็นโลหะ เงาตามซอก)
    private static readonly string[] SharedFloats = { "_Metallic", "_Smoothness", "_SmoothnessRemapMin", "_SmoothnessRemapMax", "_AORemapMin", "_AORemapMax" };

    private static readonly string[] BaseWords = { "basecolormap", "basemap", "maintex", "albedo", "basetex", "diffuse", "base", "main", "color" };
    private static readonly string[] ColorWords = { "basecolor", "color", "tint", "albedo" };
    private static readonly string[] NormalWords = { "normal", "bump" };
    private static readonly string[] EffectWords = { "noise", "dissolve", "mask", "normal", "bump", "edge", "emiss", "glow", "rim", "burn", "detail", "ramp", "gradient" };

    private bool _warnedNoBaseSlot;

    private void CopyLook(Material from, Material to, Renderer renderer)
    {
        string srcTex = FindSource(from, BaseTexSources, ShaderPropertyType.Texture, ShaderPropertyFlags.MainTexture);
        string dstTex = FindTarget(to.shader, ShaderPropertyType.Texture, ShaderPropertyFlags.MainTexture, BaseWords, EffectWords);
        if (srcTex != null && dstTex != null)
        {
            to.SetTexture(dstTex, from.GetTexture(srcTex));
            to.SetTextureScale(dstTex, from.GetTextureScale(srcTex));
            to.SetTextureOffset(dstTex, from.GetTextureOffset(srcTex));
        }
        else if (srcTex != null && !_warnedNoBaseSlot)
        {
            _warnedNoBaseSlot = true;
            Debug.LogWarning($"[Dissolve VFX] Material Dissolve ไม่มีช่องรูปผิว (Base Map) โมเดล {renderer.name} จะเป็นสีเทาตอนสลาย " +
                             "ต้องเพิ่ม Texture property ใน shader dissolve แล้วต่อเข้า Base Color", this);
        }

        string srcColor = FindSource(from, BaseColorSources, ShaderPropertyType.Color, ShaderPropertyFlags.MainColor);
        string dstColor = FindTarget(to.shader, ShaderPropertyType.Color, ShaderPropertyFlags.MainColor, ColorWords, EffectWords);
        if (srcColor != null && dstColor != null)
            to.SetColor(dstColor, from.GetColor(srcColor));

        string srcNormal = FindSource(from, NormalSources, ShaderPropertyType.Texture, ShaderPropertyFlags.None);
        string dstNormal = FindTarget(to.shader, ShaderPropertyType.Texture, ShaderPropertyFlags.None, NormalWords, null);
        if (srcNormal != null && dstNormal != null && from.GetTexture(srcNormal) != null)
            to.SetTexture(dstNormal, from.GetTexture(srcNormal));

        // Mask Map ของ HDRP: R = โลหะ, G = เงาตามซอก, A = ความเงา ไม่ยกไป ผิวจะเงาวาวเท่ากันทั้งตัว ดูซีดขาว
        Texture mask = from.HasTexture("_MaskMap") ? from.GetTexture("_MaskMap") : null;
        if (to.HasTexture("_MaskMap"))
        {
            to.SetTexture("_MaskMap", mask);
            if (to.HasFloat("_MaskOn"))
                to.SetFloat("_MaskOn", mask != null ? 1f : 0f);
        }
        foreach (string name in SharedFloats)
            if (from.HasFloat(name) && to.HasFloat(name))
                to.SetFloat(name, from.GetFloat(name));

        if (_logMaterialCopy)
        {
            Texture tex = srcTex != null ? from.GetTexture(srcTex) : null;
            Debug.Log($"[Dissolve VFX] {renderer.name} | material '{from.name}' shader '{from.shader.name}'\n" +
                      $"รูปผิว: {srcTex ?? "ไม่เจอ"} ({(tex != null ? tex.name : "ว่าง")}) → {dstTex ?? "ไม่มีช่อง"} | " +
                      $"สี: {srcColor ?? "ไม่เจอ"} → {dstColor ?? "ไม่มีช่อง"} | normal: {srcNormal ?? "-"} → {dstNormal ?? "-"} | " +
                      $"mask: {(mask != null ? mask.name : "-")} → {(to.HasTexture("_MaskMap") ? "_MaskMap" : "ไม่มีช่อง")}\n" +
                      $"ช่องรูปทั้งหมดของ material เดิม: {ListTextures(from)}", this);
        }
    }

    private static string ListTextures(Material material)
    {
        var names = new List<string>();
        Shader shader = material.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            if (shader.GetPropertyType(i) != ShaderPropertyType.Texture)
                continue;
            string n = shader.GetPropertyName(i);
            Texture t = material.GetTexture(n);
            names.Add(t != null ? $"{n}={t.name}" : n);
        }
        return names.Count > 0 ? string.Join(", ", names) : "(ไม่มี)";
    }

    // หาช่องของ material เดิม: ช่องที่ shader ติดป้าย Main ไว้ก่อน แล้วค่อยไล่ตามชื่อที่ใช้กันบ่อย
    private static string FindSource(Material material, string[] names, ShaderPropertyType type, ShaderPropertyFlags flag)
    {
        Shader shader = material.shader;
        if (flag != ShaderPropertyFlags.None)
        {
            for (int i = 0; i < shader.GetPropertyCount(); i++)
                if (shader.GetPropertyType(i) == type && (shader.GetPropertyFlags(i) & flag) != 0)
                    return shader.GetPropertyName(i);
        }
        foreach (string name in names)
        {
            int index = shader.FindPropertyIndex(name);
            if (index >= 0 && shader.GetPropertyType(index) == type)
                return name;
        }
        return null;
    }

    // หาช่องของ Material Dissolve ที่จะรับค่า: ป้าย Main ก่อน แล้วค่อยเดาจากชื่อ แต่ไม่เอาช่องของเอฟเฟกต์ (noise, edge...)
    private static string FindTarget(Shader shader, ShaderPropertyType type, ShaderPropertyFlags flag, string[] include, string[] exclude)
    {
        int count = shader.GetPropertyCount();
        if (flag != ShaderPropertyFlags.None)
        {
            for (int i = 0; i < count; i++)
                if (shader.GetPropertyType(i) == type && (shader.GetPropertyFlags(i) & flag) != 0)
                    return shader.GetPropertyName(i);
        }
        foreach (string word in include)
        {
            for (int i = 0; i < count; i++)
            {
                if (shader.GetPropertyType(i) != type)
                    continue;
                string name = shader.GetPropertyName(i).ToLowerInvariant();
                if (!name.Contains(word) || ContainsAny(name, exclude))
                    continue;
                return shader.GetPropertyName(i);
            }
        }
        return null;
    }

    private static bool ContainsAny(string name, string[] words)
    {
        if (words == null)
            return false;
        foreach (string word in words)
            if (name.Contains(word))
                return true;
        return false;
    }

    private bool SupportsAmount(Material material)
    {
        if (material == null || material.shader == null)
            return false;
        int index = material.shader.FindPropertyIndex(_amountProperty);
        if (index < 0)
            return false;
        ShaderPropertyType type = material.shader.GetPropertyType(index);
        return type == ShaderPropertyType.Float || type == ShaderPropertyType.Range;
    }

    private bool IsCurrentRun(int version) =>
        _played && _runVersion == version && isActiveAndEnabled;

    private IEnumerator DissolveRoutine(int version)
    {
        // Preserve the original appearance through the short delay.
        if (_delay > 0f)
            yield return new WaitForSeconds(_delay);

        if (!IsCurrentRun(version))
            yield break;
        foreach (RendererState state in _states)
        {
            if (state.Renderer == null)
                continue;
            state.Renderer.sharedMaterials = state.RuntimeMaterials;
            state.MaterialsApplied = true;
        }
        ApplyAmount(_visibleAmount);
        PlayEffect(_startEffect);
        LogStep("เริ่มจาง: สลับเป็น Material Dissolve แล้ว");
        _onDissolveStarted?.Invoke();
        if (!IsCurrentRun(version))
            yield break;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _duration);
        while (elapsed < duration)
        {
            if (!IsCurrentRun(version))
                yield break;
            elapsed += Time.deltaTime;
            ApplyAmount(Mathf.Lerp(_visibleAmount, _hiddenAmount, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        if (!IsCurrentRun(version))
            yield break;
        ApplyAmount(_hiddenAmount);

        foreach (RendererState state in _states)
            if (state.Renderer != null)
                state.Renderer.enabled = false;

        // Hidden renderers no longer need material instances. Keep visibility snapshots for pooling.
        RestoreMaterials();
        PlayEffect(_finishEffect);
        _finished = true;
        LogStep("สลายเสร็จ: ปิดโมเดลแล้ว");
        _onDissolveFinished?.Invoke();
    }

    private IEnumerator LoopRoutine()
    {
        // รอ 1 เฟรม ให้สคริปต์อื่นเปิด/ตั้งค่าโมเดลเสร็จก่อน
        yield return null;

        while (true)
        {
            PlayDissolve();
            if (!_played)
                yield break;

            while (!_finished)
                yield return null;

            if (_loopHiddenTime > 0f)
                yield return new WaitForSeconds(_loopHiddenTime);

            ResetState();
            if (_loopFadeIn)
                yield return AppearRoutine();

            if (_loopVisibleTime > 0f)
                yield return new WaitForSeconds(_loopVisibleTime);
        }
    }

    // ทุกอย่างเกิดในเฟรมเดียวก่อนจอวาด โมเดลเลยไม่โผล่วับก่อนเริ่มจาง
    private IEnumerator AppearRoutine()
    {
        if (!PrepareMaterials())
            yield break;

        foreach (RendererState state in _states)
        {
            if (state.Renderer == null)
                continue;
            state.Renderer.sharedMaterials = state.RuntimeMaterials;
            state.MaterialsApplied = true;
        }
        ApplyAmount(_hiddenAmount);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ApplyAmount(Mathf.Lerp(_hiddenAmount, _visibleAmount, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        ApplyAmount(_visibleAmount);
        ResetState();
    }

    private void PlayEffect(EffectSlot slot)
    {
        if (slot == null || slot.Effect == null)
            return;

        Transform point = slot.Point != null ? slot.Point : (_visualRoot != null ? _visualRoot : transform);
        GameObject instance = slot.FollowPoint
            ? Instantiate(slot.Effect, point.position, point.rotation, point)
            : Instantiate(slot.Effect, point.position, point.rotation);

        if (slot.EmitFromMesh)
            BindToMesh(instance);

        instance.SetActive(true);
        PlayParticles(instance);

        // เอฟเฟกต์ที่ไม่ติดตัวและมีเวลาหมดอายุ ปล่อยให้เล่นจนจบเอง แม้โมเดลจะถูกปิดไปแล้ว
        if (slot.FollowPoint || slot.Lifetime <= 0f)
            _attachedEffects.Add(instance);
        if (slot.Lifetime > 0f)
            Destroy(instance, slot.Lifetime);
    }

    // Particle ที่ไม่ได้ติ๊ก Play On Awake จะไม่เล่นเอง — สั่งเฉพาะตัวบนสุด ตัวลูกเล่นตาม
    private static void PlayParticles(GameObject instance)
    {
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (IsTopParticle(ps, instance.transform) && !ps.isPlaying)
                ps.Play(true);
        }
    }

    private static bool IsTopParticle(ParticleSystem ps, Transform root)
    {
        if (ps.transform == root)
            return true;

        for (Transform t = ps.transform.parent; t != null; t = t.parent)
        {
            if (t.TryGetComponent<ParticleSystem>(out _))
                return false;
            if (t == root)
                break;
        }
        return true;
    }

    // เลือกชิ้นที่ใหญ่สุด (ปกติคือตัว) ไม่ใช่ชิ้นแรก เผื่อชิ้นแรกเป็นอาวุธหรือของประดับ
    private void BindToMesh(GameObject instance)
    {
        Renderer target = null;
        float biggest = -1f;
        foreach (RendererState state in _states)
        {
            if (state.Renderer == null)
                continue;
            float size = state.Renderer.bounds.size.sqrMagnitude;
            if (size > biggest)
            {
                biggest = size;
                target = state.Renderer;
            }
        }
        if (target == null)
            return;

        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!IsTopParticle(ps, instance.transform))
                continue;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            if (target is SkinnedMeshRenderer skinned)
            {
                shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                shape.skinnedMeshRenderer = skinned;
            }
            else if (target is MeshRenderer mesh)
            {
                shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                shape.meshRenderer = mesh;
            }
        }
    }

    private void ClearEffects()
    {
        foreach (GameObject effect in _attachedEffects)
            if (effect != null)
                Destroy(effect);
        _attachedEffects.Clear();
    }

    private void ApplyAmount(float amount)
    {
        foreach (RendererState state in _states)
        {
            if (state.Renderer == null || state.MaterialsRestored)
                continue;
            for (int i = 0; i < state.RuntimeMaterials.Length; i++)
            {
                Material material = state.RuntimeMaterials[i];
                if (material == null)
                    continue;
                material.SetFloat(_amountId, amount);
                state.WorkingBlocks[i].SetFloat(_amountId, amount);
                state.Renderer.SetPropertyBlock(state.WorkingBlocks[i], i);
            }
        }
    }

    private void RestoreMaterials()
    {
        foreach (RendererState state in _states)
        {
            if (state.MaterialsRestored)
                continue;
            if (state.MaterialsApplied && state.Renderer != null)
            {
                state.Renderer.sharedMaterials = state.OriginalMaterials;
                for (int i = 0; i < state.OriginalBlocks.Length; i++)
                    if (state.OriginalBlocks[i] != null)
                        state.Renderer.SetPropertyBlock(state.OriginalBlocks[i].isEmpty ? null : state.OriginalBlocks[i], i);
            }
            foreach (Material material in state.RuntimeMaterials)
                if (material != null)
                    Destroy(material);
            state.MaterialsRestored = true;
        }
    }

    public void ResetDissolve()
    {
        if (_states.Count > 0)
            LogStep("ResetDissolve: โมเดลจะถูกเปิดกลับมา (ดูบรรทัดล่างๆ ว่าใครเรียก)");
        StopAllCoroutines();
        ResetState();
    }

    // ไม่หยุด coroutine — LoopRoutine เรียกตัวนี้ระหว่างวนได้ ส่วน DissolveRoutine เก่าจะหยุดเองเพราะ _runVersion เปลี่ยน
    private void ResetState()
    {
        _runVersion++;
        RestoreMaterials();
        ClearEffects();
        foreach (RendererState state in _states)
            if (state.Renderer != null)
                state.Renderer.enabled = state.OriginalEnabled;
        _states.Clear();
        _played = false;
        _finished = false;
    }

    private void OnEnable()
    {
        if (_loop)
            StartCoroutine(LoopRoutine());
    }

    private void OnDisable()
    {
        if (_states.Count > 0)
            LogStep("OnDisable: ตัวนี้ถูกปิด → จะรีเซ็ตโมเดลกลับ");
        ResetDissolve();
    }

    private void LogStep(string message)
    {
        if (_logSteps)
            Debug.Log($"[Dissolve VFX] {Time.time:0.00}s {name}: {message}", this);
    }
    private void OnDestroy() => ResetDissolve();
}