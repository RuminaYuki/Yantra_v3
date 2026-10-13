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

    [Header("เอฟเฟกต์เสริม (ไม่จำเป็นต้องใส่)")]
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
    private bool _played;
    private int _amountId;
    private int _runVersion;

    public void PlayDissolve()
    {
        if (_played || !isActiveAndEnabled || !PrepareMaterials())
            return;

        _played = true;
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

                Material template = SupportsAmount(original[i]) ? original[i] : _dissolveMaterial;
                state.RuntimeMaterials[i] = new Material(template) { name = template.name + " (Dissolve instance)" };
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
        _onDissolveFinished?.Invoke();
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
        _runVersion++;
        StopAllCoroutines();
        RestoreMaterials();
        foreach (RendererState state in _states)
            if (state.Renderer != null)
                state.Renderer.enabled = state.OriginalEnabled;
        _states.Clear();
        _played = false;
    }

    private void OnDisable() => ResetDissolve();
    private void OnDestroy() => ResetDissolve();
}
