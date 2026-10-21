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

    // รายชื่อ Property มาตรฐานของ Texture และสี (ครอบคลุมทั้ง URP, Built-in และ Shader Graph)
    private static readonly string[] TextureProps = { "_BaseMap", "_MainTex", "_Albedo", "_AlbedoMap", "_Diffuse", "_Tex" };
    private static readonly string[] ColorProps = { "_BaseColor", "_Color", "_Tint", "_MainColor" };
    private static readonly string[] NormalProps = { "_BumpMap", "_NormalMap", "_Normal" };
    private static readonly string[] EmissionProps = { "_EmissionMap", "_EmissiveMap" };

    private readonly List<RendererState> _states = new List<RendererState>();
    private readonly List<GameObject> _attachedEffects = new List<GameObject>();
    private bool _played;
    private bool _finished;
    private int _amountId;
    private int _runVersion;

    public void PlayDissolve()
    {
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

                bool originalSupportsDissolve = SupportsAmount(original[i]);
                Material template = originalSupportsDissolve ? original[i] : _dissolveMaterial;
                Material runtimeMat = new Material(template) { name = template.name + " (Dissolve instance)" };

                // ถ้าสลับไปใช้ _dissolveMaterial ต้นแบบ ให้คัดลอก Texture และสีจาก Material เดิมมาใส่ด้วย
                if (!originalSupportsDissolve && template != null)
                {
                    CopySurfaceProperties(original[i], runtimeMat);
                }

                state.RuntimeMaterials[i] = runtimeMat;
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

    private static void CopySurfaceProperties(Material source, Material destination)
    {
        if (source == null || destination == null)
            return;

        // 1. คัดลอกรูป Texture หลัก (Albedo / BaseMap) พร้อมค่า Tiling & Offset
        Texture mainTex = source.mainTexture;
        Vector2 scale = Vector2.one;
        Vector2 offset = Vector2.zero;
        bool foundTexProp = false;

        foreach (string prop in TextureProps)
        {
            if (source.HasProperty(prop))
            {
                Texture tex = source.GetTexture(prop);
                if (tex != null)
                {
                    mainTex = tex;
                    scale = source.GetTextureScale(prop);
                    offset = source.GetTextureOffset(prop);
                    foundTexProp = true;
                    break;
                }
            }
        }

        if (!foundTexProp && mainTex != null)
        {
            scale = source.mainTextureScale;
            offset = source.mainTextureOffset;
        }

        if (mainTex != null)
        {
            destination.mainTexture = mainTex;
            destination.mainTextureScale = scale;
            destination.mainTextureOffset = offset;

            foreach (string prop in TextureProps)
            {
                if (destination.HasProperty(prop))
                {
                    destination.SetTexture(prop, mainTex);
                    destination.SetTextureScale(prop, scale);
                    destination.SetTextureOffset(prop, offset);
                }
            }
        }

        // 2. คัดลอกสีพื้นผิว (BaseColor / Color)
        foreach (string srcProp in ColorProps)
        {
            if (source.HasProperty(srcProp))
            {
                Color col = source.GetColor(srcProp);
                foreach (string dstProp in ColorProps)
                {
                    if (destination.HasProperty(dstProp))
                        destination.SetColor(dstProp, col);
                }
                break;
            }
        }

        // 3. คัดลอก Normal Map (ถ้ามี)
        foreach (string srcProp in NormalProps)
        {
            if (source.HasProperty(srcProp))
            {
                Texture normalTex = source.GetTexture(srcProp);
                if (normalTex != null)
                {
                    foreach (string dstProp in NormalProps)
                    {
                        if (destination.HasProperty(dstProp))
                            destination.SetTexture(dstProp, normalTex);
                    }
                    break;
                }
            }
        }

        // 4. คัดลอก Emission Map (ถ้ามี)
        foreach (string srcProp in EmissionProps)
        {
            if (source.HasProperty(srcProp))
            {
                Texture emissionTex = source.GetTexture(srcProp);
                if (emissionTex != null)
                {
                    foreach (string dstProp in EmissionProps)
                    {
                        if (destination.HasProperty(dstProp))
                            destination.SetTexture(dstProp, emissionTex);
                    }
                    break;
                }
            }
        }
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

    private void OnDisable() => ResetDissolve();
    private void OnDestroy() => ResetDissolve();
}