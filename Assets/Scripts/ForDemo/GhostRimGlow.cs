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

    [Tooltip("ร่างเรืองแสงจางเร็วกว่าตัวผีกี่เท่าตอนสลาย\n" +
             "ช้าเกินจะเห็นร่างโปร่งๆ ค้างอยู่ระหว่างที่ตัวจริงกำลังไหม้หาย")]
    [SerializeField, Min(1f)] private float _fadeFaster = 5f;

    [Tooltip("บอกใน Console ตอนร่างเรืองแสงโผล่/ซ่อน")]
    [SerializeField] private bool _logSteps = true;

    private sealed class Pair
    {
        public SkinnedMeshRenderer Source;
        public SkinnedMeshRenderer Rim;
        public float LastAmount = -1f;
        public bool LoggedFade;
        public bool HiddenNotDrawn;
        public Material OriginalMaterial;
        public string LastState;
        public string LastReason = "";
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
            _pairs.Add(new Pair { Source = source, Rim = CreateRim(source), OriginalMaterial = source.sharedMaterial });
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

            // ตามตัวจริงทุกอย่าง: ถ้าระบบอื่นซ่อนตัวจริงด้วยวิธีไหนก็ตาม ร่างเรืองแสงต้องหายตาม
            SkinnedMeshRenderer src = pair.Source;
            if (pair.Rim.gameObject.layer != src.gameObject.layer)
                pair.Rim.gameObject.layer = src.gameObject.layer;
            pair.Rim.renderingLayerMask = src.renderingLayerMask;
            pair.Rim.forceRenderingOff = src.forceRenderingOff;

            if (_logSteps)
                LogStateChange(pair);

            Camera cam = Camera.main;
            string reason = "";
            if (!src.enabled) reason = "ตัวจริงถูกปิด";
            else if (!src.gameObject.activeInHierarchy) reason = "ตัวจริงถูกปิด GameObject";
            else if (src.forceRenderingOff) reason = "ตัวจริงถูกซ่อน (forceRenderingOff)";
            else if (src.shadowCastingMode == ShadowCastingMode.ShadowsOnly) reason = "ตัวจริงเหลือแค่เงา (Shadows Only)";
            else if (cam != null && (cam.cullingMask & (1 << src.gameObject.layer)) == 0) reason = "กล้องไม่เห็น layer ของตัวจริง";
            else if (src.sharedMaterial != pair.OriginalMaterial) reason = "ตัวจริงเปลี่ยน material (เช่นกำลังสลาย)";
            else if (pair.HiddenNotDrawn)
            {
                // ซ่อนไว้จนกว่าตัวจริงจะถูกวาดอีกครั้ง กันกะพริบเปิดปิดทุกเฟรม
                if (src.isVisible) pair.HiddenNotDrawn = false;
                else reason = "ตัวจริงไม่ถูกวาด";
            }
            else if (pair.Rim.enabled && pair.Rim.isVisible && !src.isVisible)
            {
                pair.HiddenNotDrawn = true;
                reason = "ตัวจริงไม่ถูกวาด แต่ร่างเรืองแสงยังถูกวาด";
            }
            bool visible = reason.Length == 0;

            if (pair.Rim.enabled != visible)
            {
                pair.Rim.enabled = visible;
                if (_logSteps)
                    Debug.Log($"[GhostRim] {Time.time:0.00}s {pair.Rim.name} {(visible ? "โผล่" : "ซ่อน เพราะ" + reason)}", this);
            }
            if (!visible)
                continue;

            CopyBlendShapes(pair);

            float raw = ReadAmount(pair.Source);
            if (_logSteps && !pair.LoggedFade && raw > 0.001f)
            {
                pair.LoggedFade = true;
                Debug.Log($"[GhostRim] {Time.time:0.00}s {pair.Rim.name} เริ่มจางตามตัวผี (ค่าสลาย {raw:0.00})", this);
            }
            float amount = Mathf.Clamp01(raw * _fadeFaster);
            if (_logSteps && pair.LastAmount >= 1f && amount < 1f)
                Debug.Log($"[GhostRim] {Time.time:0.00}s {pair.Rim.name} กลับมาสว่าง (ค่าสลายที่อ่านได้ {raw:0.00})", this);
            if (Mathf.Approximately(amount, pair.LastAmount))
                continue;
            pair.LastAmount = amount;
            _write.Clear();
            _write.SetFloat(_amountId, amount);
            pair.Rim.SetPropertyBlock(_write);
        }
    }

    // บอกทุกครั้งที่ตัวจริงเปลี่ยนสถานะ จะได้รู้ว่าระบบอื่นซ่อนผีด้วยวิธีไหน
    private void LogStateChange(Pair pair)
    {
        SkinnedMeshRenderer src = pair.Source;
        Material mat = src.sharedMaterial;
        string state = $"เปิด={src.enabled} active={src.gameObject.activeInHierarchy} เงา={src.shadowCastingMode} " +
                       $"layer={LayerMask.LayerToName(src.gameObject.layer)} material={(mat != null ? mat.name : "-")} " +
                       $"forceOff={src.forceRenderingOff} ถูกวาด={src.isVisible}";
        if (state == pair.LastState)
            return;
        if (pair.LastState != null)
            Debug.Log($"[GhostRim] {Time.time:0.00}s {src.name} เปลี่ยน → {state}", this);
        pair.LastState = state;
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