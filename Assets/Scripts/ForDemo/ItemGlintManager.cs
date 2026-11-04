using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ใส่ไว้ในฉาก 1 ตัว — ของที่วางบนพื้น (WorldItem) ทุกชิ้นจะมีแสงเหลื่อมวิ่งผ่านแบบ RE4
// ของที่ดรอปทีหลังก็ได้ด้วย ส่วนของที่ไม่ใช่ WorldItem (เช่น ปืนบนโต๊ะ) ใส่ GlintTarget ไว้ที่ตัวมัน
public class ItemGlintManager : MonoBehaviour
{
    [Tooltip("วัสดุที่ใช้ shader 'Shader Graphs/ItemGlint'")]
    [SerializeField] private Material _material;

    [Tooltip("เห็นแสงเหลื่อมเมื่อกล้องอยู่ใกล้กว่ากี่เมตร")]
    [SerializeField, Min(0f)] private float _showDistance = 15f;

    [Tooltip("หาของชิ้นใหม่ทุกกี่วินาที")]
    [SerializeField, Min(0.1f)] private float _scanInterval = 0.5f;

    private static readonly int CenterXId = Shader.PropertyToID("_Center_X");
    private static readonly int CenterYId = Shader.PropertyToID("_Center_Y");
    private static readonly int CenterZId = Shader.PropertyToID("_Center_Z");
    private static readonly int ExtentId = Shader.PropertyToID("_Extent");
    private static readonly int OffsetId = Shader.PropertyToID("_Offset");

    private sealed class Entry
    {
        public readonly List<Renderer> Overlays = new List<Renderer>();
        public MaterialPropertyBlock Block;
        public Transform Root;
        public Vector3 LocalCenter;
        public bool Visible;
    }

    private readonly Dictionary<Component, Entry> _entries = new Dictionary<Component, Entry>();
    private readonly List<Component> _gone = new List<Component>();
    private float _scanTimer;

    private void Update()
    {
        _scanTimer -= Time.unscaledDeltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = _scanInterval;
            Scan();
        }
        UpdateVisibility();
    }

    private void Scan()
    {
        if (_material == null) return;

        foreach (WorldItem item in FindObjectsByType<WorldItem>(FindObjectsSortMode.None))
            Add(item);

        foreach (GlintTarget target in FindObjectsByType<GlintTarget>(FindObjectsSortMode.None))
        {
            if (target.GetComponentInParent<WorldItem>() == null)
                Add(target);
        }

        _gone.Clear();
        foreach (KeyValuePair<Component, Entry> pair in _entries)
            if (pair.Key == null) _gone.Add(pair.Key);
        foreach (Component key in _gone)
            _entries.Remove(key);
    }

    private void Add(Component root)
    {
        if (!_entries.ContainsKey(root))
            _entries.Add(root, Build(root));
    }

    // ร่างซ้อนทับโมเดลเดิม ใช้ mesh เดียวกันแต่เป็นวัสดุแสงเหลื่อม (รองรับทั้งโมเดลธรรมดาและโมเดลที่มีกระดูก)
    private Entry Build(Component item)
    {
        var entry = new Entry { Root = item.transform, Block = new MaterialPropertyBlock() };
        var sources = new List<Renderer>();
        Bounds bounds = default;

        foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer.name.EndsWith("_Glint") || SourceMesh(renderer) == null) continue;
            if (sources.Count == 0) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            sources.Add(renderer);
        }

        // ทุกชิ้นส่วนใช้จุดกึ่งกลางเดียวกัน (พิกัดโลก) แสงจะวิ่งผ่านทั้งชิ้นเป็นแนวเดียว
        entry.LocalCenter = entry.Root.InverseTransformPoint(bounds.center);
        entry.Block.SetFloat(ExtentId, Mathf.Max(bounds.extents.magnitude, 0.01f));
        entry.Block.SetFloat(OffsetId, Random.value);
        foreach (Renderer source in sources)
        {
            Mesh mesh = SourceMesh(source);
            var go = new GameObject(source.name + "_Glint");
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform, false);

            Renderer overlay;
            if (source is SkinnedMeshRenderer skinned)
            {
                var copy = go.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = mesh;
                copy.bones = skinned.bones;
                copy.rootBone = skinned.rootBone;
                copy.localBounds = skinned.localBounds;
                copy.quality = skinned.quality;
                copy.updateWhenOffscreen = skinned.updateWhenOffscreen;
                overlay = copy;
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                overlay = go.AddComponent<MeshRenderer>();
            }

            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = _material;
            overlay.sharedMaterials = materials;
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            overlay.lightProbeUsage = LightProbeUsage.Off;
            overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;

            overlay.enabled = false;
            entry.Overlays.Add(overlay);
        }
        return entry;
    }

    private static Mesh SourceMesh(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
        if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter)) return filter.sharedMesh;
        return null;
    }

    private void UpdateVisibility()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        float maxSqr = _showDistance * _showDistance;

        foreach (KeyValuePair<Component, Entry> pair in _entries)
        {
            if (pair.Key == null) continue;

            Entry entry = pair.Value;
            bool visible = (pair.Key.transform.position - camPos).sqrMagnitude < maxSqr;

            if (visible)
            {
                // ของที่ตกพื้น / กลิ้งได้ จุดกึ่งกลางต้องตามไปด้วย
                Vector3 center = entry.Root.TransformPoint(entry.LocalCenter);
                entry.Block.SetFloat(CenterXId, center.x);
                entry.Block.SetFloat(CenterYId, center.y);
                entry.Block.SetFloat(CenterZId, center.z);
            }

            foreach (Renderer overlay in entry.Overlays)
            {
                if (overlay == null) continue;
                if (visible) overlay.SetPropertyBlock(entry.Block);
                if (visible != entry.Visible) overlay.enabled = visible;
            }
            entry.Visible = visible;
        }
    }
}