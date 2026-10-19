using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ใส่ที่ตัวผี (เช่น EnemeyController) — สร้างร่างซ้อนที่เรืองแสงตามขอบ ทับโมเดลเดิม
// ไม่แตะ material เดิมของโมเดล และจางหายเองตอน DissolveVFXPlayer สั่งสลาย
[DisallowMultipleComponent]
public class GhostRimGlow : MonoBehaviour
{
    [Tooltip("Material ที่สร้างจาก GhostRim.shadergraph")]
    [SerializeField] private Material _rimMaterial;

    [Tooltip("เว้นว่าง = หา Skinned Mesh Renderer ทุกตัวใต้ object นี้")]
    [SerializeField] private Transform _modelRoot;

    [Tooltip("ชื่อเดียวกับใน DissolveVFXPlayer")]
    [SerializeField] private string _amountProperty = "_DissolveAmount";

    private sealed class Pair
    {
        public SkinnedMeshRenderer Source;
        public SkinnedMeshRenderer Rim;
        public float LastAmount = -1f;
    }

    private readonly List<Pair> _pairs = new List<Pair>();
    private MaterialPropertyBlock _read;
    private MaterialPropertyBlock _write;
    private int _amountId;

    private void Awake()
    {
        if (_rimMaterial == null)
        {
            Debug.LogWarning("[GhostRim] ยังไม่ได้ใส่ Rim Material", this);
            enabled = false;
            return;
        }

        _amountId = Shader.PropertyToID(_amountProperty);
        _read = new MaterialPropertyBlock();
        _write = new MaterialPropertyBlock();

        Transform root = _modelRoot != null ? _modelRoot : transform;
        foreach (SkinnedMeshRenderer source in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (source.sharedMesh == null)
                continue;
            _pairs.Add(new Pair { Source = source, Rim = CreateRim(source) });
        }

        if (_pairs.Count == 0)
            Debug.LogWarning("[GhostRim] ไม่เจอ Skinned Mesh Renderer ใต้ตัวนี้", this);
    }

    private SkinnedMeshRenderer CreateRim(SkinnedMeshRenderer source)
    {
        var go = new GameObject(source.name + "_Rim");
        go.layer = source.gameObject.layer;
        go.transform.SetParent(source.transform, false);

        var rim = go.AddComponent<SkinnedMeshRenderer>();
        rim.sharedMesh = source.sharedMesh;
        rim.bones = source.bones;
        rim.rootBone = source.rootBone;
        rim.localBounds = source.localBounds;
        rim.quality = source.quality;
        rim.updateWhenOffscreen = source.updateWhenOffscreen;
        rim.shadowCastingMode = ShadowCastingMode.Off;
        rim.receiveShadows = false;

        var materials = new Material[source.sharedMesh.subMeshCount];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = _rimMaterial;
        rim.sharedMaterials = materials;
        return rim;
    }

    private void LateUpdate()
    {
        foreach (Pair pair in _pairs)
        {
            if (pair.Source == null || pair.Rim == null)
                continue;

            bool visible = pair.Source.enabled && pair.Source.gameObject.activeInHierarchy;
            if (pair.Rim.enabled != visible)
                pair.Rim.enabled = visible;
            if (!visible)
                continue;

            CopyBlendShapes(pair);

            float amount = ReadAmount(pair.Source);
            if (Mathf.Approximately(amount, pair.LastAmount))
                continue;
            pair.LastAmount = amount;
            _write.Clear();
            _write.SetFloat(_amountId, amount);
            pair.Rim.SetPropertyBlock(_write);
        }
    }

    private float ReadAmount(SkinnedMeshRenderer source)
    {
        source.GetPropertyBlock(_read, 0);
        if (_read.HasFloat(_amountId))
            return _read.GetFloat(_amountId);

        Material material = source.sharedMaterial;
        if (material != null && material.HasFloat(_amountId))
            return material.GetFloat(_amountId);
        return 0f;
    }

    private static void CopyBlendShapes(Pair pair)
    {
        int count = pair.Source.sharedMesh.blendShapeCount;
        for (int i = 0; i < count; i++)
            pair.Rim.SetBlendShapeWeight(i, pair.Source.GetBlendShapeWeight(i));
    }
}
