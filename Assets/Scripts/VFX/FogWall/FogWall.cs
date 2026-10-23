using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// กลุ่มควันหนากั้นทาง เป็นหมอก HDRP จริง (Local Volumetric Fog) มีลายก้อนลอยช้าๆ เดินเข้าไปได้นิดหน่อย
// Close() = ควันม้วนเข้ามาแล้วข้นขึ้น / Open() = ควันถอยออกไปแล้วค่อยๆ สลาย
// ของที่สร้างตอนรันไม่ถูกเซฟลงฉาก ปรับค่าที่ช่องด้านล่างได้เลย
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class FogWall : MonoBehaviour
{
    [Header("ขนาด")]
    [SerializeField] private float _width = 6f;
    [SerializeField] private float _height = 6f;
    [Tooltip("ความหนาของหมอก (เมตร) ผู้เล่นเดินเข้าไปได้ครึ่งนึงก่อนชนกำแพง")]
    [SerializeField] private float _fogDepth = 8f;
    [Tooltip("จมลงใต้ดิน กันเห็นขอบล่างตรงพื้นที่ไม่เรียบ")]
    [SerializeField] private float _sinkIntoGround = 0.5f;

    [Header("ควัน")]
    [Tooltip("สีควัน มืด = ควันดำทึบดูน่ากลัว / สว่าง = หมอกขาว")]
    [SerializeField] private Color _smokeColor = new Color(0.16f, 0.18f, 0.22f);
    [Tooltip("มองทะลุได้กี่เมตร น้อย = หนา (ต่ำสุด 1)")]
    [SerializeField] private float _seeThrough = 1f;
    [Tooltip("ขนาดก้อนหมอก (เมตร)")]
    [SerializeField] private float _puffSize = 6f;
    [Tooltip("ความเร็วที่ก้อนหมอกลอย")]
    [SerializeField] private Vector3 _drift = new Vector3(0.02f, 0.01f, 0.015f);
    [Tooltip("ขอบหมอกนุ่มแค่ไหน (สัดส่วน 0-0.5) ข้างบนจางมากกว่า")]
    [SerializeField, Range(0.02f, 0.5f)] private float _edgeSoftness = 0.3f;

    [Header("กันผู้เล่น")]
    [Tooltip("ติ๊กเมื่อไม่มีกำแพงอื่นกั้นอยู่แล้ว (ถ้าวางทับ Barrier ของเพื่อน ไม่ต้องติ๊ก)")]
    [SerializeField] private bool _blockPlayer = false;

    [Header("แผ่นหมอกเสริม (ไม่บังคับ)")]
    [Tooltip("0 = ใช้หมอกจริงอย่างเดียว / ใส่ 1-3 ถ้าอยากได้ลายก้อนชัดขึ้น แต่จะดูเป็นแผ่นมากขึ้น")]
    [SerializeField, Range(0, 6)] private int _cardLayers = 0;
    [SerializeField] private float _cardSpacing = 1.5f;
    [SerializeField] private Material _material;

    [Header("จังหวะ")]
    [SerializeField] private bool _startClosed = false;
    [Tooltip("วินาทีที่ควันม้วนเข้ามาจนเต็ม")]
    [SerializeField] private float _rollInTime = 2f;
    [Tooltip("วินาทีที่ควันถอยออกไปจนสลายหมด")]
    [SerializeField] private float _retreatTime = 5f;
    [Tooltip("ควันถอยออกไปไกลกี่เมตรก่อนสลาย")]
    [SerializeField] private float _retreatDistance = 10f;
    [Tooltip("โชว์หมอกในหน้า Scene ตอนไม่ได้กด Play จะได้วางตำแหน่งง่าย")]
    [SerializeField] private bool _previewInEditor = true;

    [Header("เรียกตอน")]
    [SerializeField] private UnityEvent _onClosed;
    [SerializeField] private UnityEvent _onOpened;

    private const float ThinFog = 300f;

    private static readonly int RevealId = Shader.PropertyToID("_Reveal");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static Mesh _quad;
    private static Texture3D _mask;

    private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
    private Transform _root;
    private LocalVolumetricFog _volume;
    private float _retreatSign = 1f;
    private MaterialPropertyBlock _block;
    private BoxCollider _collider;
    private float _reveal;
    private float _target;
    private bool _rebuildQueued;

    public bool IsClosed { get; private set; }

    private void OnEnable()
    {
        _collider = GetComponent<BoxCollider>();
        Build();

        if (Application.isPlaying)
        {
            IsClosed = _startClosed;
            _reveal = _target = IsClosed ? 1f : 0f;
            _collider.enabled = IsClosed && _blockPlayer;
        }
        else
        {
            _reveal = _target = _previewInEditor ? 1f : 0f;
        }
        ApplyReveal();
    }

    private void OnDisable() => DestroyChildren();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_rebuildQueued) return;
        _rebuildQueued = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            _rebuildQueued = false;
            if (this == null || !isActiveAndEnabled) return;
            Build();
            if (!Application.isPlaying) _reveal = _target = _previewInEditor ? 1f : 0f;
            ApplyReveal();
        };
    }
#endif

    public void Close()
    {
        IsClosed = true;
        _retreatSign = AwayFromCameraSign();
        if (_collider != null) _collider.enabled = _blockPlayer;
        _target = 1f;
        _onClosed?.Invoke();
    }

    public void Open()
    {
        IsClosed = false;
        _retreatSign = AwayFromCameraSign();
        if (_collider != null) _collider.enabled = false;
        _target = 0f;
        _onOpened?.Invoke();
    }

    // ใช้ตอนโหลดเซฟ ไม่เล่นอนิเมชัน ไม่เรียก event
    public void SetClosedInstant(bool closed)
    {
        IsClosed = closed;
        if (_collider != null) _collider.enabled = closed && _blockPlayer;
        _reveal = _target = closed ? 1f : 0f;
        ApplyReveal();
    }

    private void Update()
    {
        if (!Application.isPlaying || Mathf.Approximately(_reveal, _target)) return;

        float duration = _target > _reveal ? _rollInTime : _retreatTime;
        _reveal = Mathf.MoveTowards(_reveal, _target, Time.deltaTime / Mathf.Max(0.01f, duration));
        ApplyReveal();
    }

    // ฝั่งที่ไกลจากกล้อง (ผู้เล่น) ควันจะถอยไปทางนั้น
    private float AwayFromCameraSign()
    {
        Camera cam = Camera.main;
        if (cam == null) return 1f;
        Vector3 toCam = cam.transform.position - transform.position;
        return Vector3.Dot(toCam, transform.forward) > 0f ? -1f : 1f;
    }

    private void ApplyReveal()
    {
        bool visible = _reveal > 0.001f;
        float gone = 1f - _reveal;   // 0 = ควันเต็มที่ / 1 = หายหมด

        // ถอย: เริ่มช้าแล้วเร่ง / สลาย: ยังข้นอยู่ช่วงแรก แล้วค่อยบางลงช่วงหลัง
        float retreat = gone * gone;
        float thin = Mathf.Clamp01((gone - 0.35f) / 0.65f);
        float density = 1f - thin * thin * (3f - 2f * thin);

        if (_root != null)
            _root.localPosition = new Vector3(0f, 0f, _retreatSign * _retreatDistance * retreat);

        if (_volume != null)
        {
            _volume.enabled = visible;
            var p = _volume.parameters;
            p.meanFreePath = Mathf.Lerp(ThinFog, Mathf.Max(1f, _seeThrough), density);
            _volume.parameters = p;
        }

        float eased = density;

        if (_renderers.Count == 0) return;
        if (_block == null) _block = new MaterialPropertyBlock();
        foreach (MeshRenderer r in _renderers)
        {
            if (r == null) continue;
            r.enabled = visible;
            r.GetPropertyBlock(_block);
            _block.SetFloat(RevealId, eased);
            r.SetPropertyBlock(_block);
        }
    }

    private void Build()
    {
        DestroyChildren();

        if (_collider == null) _collider = GetComponent<BoxCollider>();
        _collider.center = new Vector3(0f, _height * 0.5f, 0f);
        _collider.size = new Vector3(_width, _height, 0.5f);

        BuildVolume();
        BuildCards();
    }

    private void BuildVolume()
    {
        GameObject go = CreateChild("FogVolume");
        go.transform.localPosition = new Vector3(0f, (_height - _sinkIntoGround) * 0.5f, 0f);

        float depth = Mathf.Max(0.5f, _fogDepth);
        float puff = Mathf.Max(0.5f, _puffSize);
        float soft = _edgeSoftness;

        _volume = go.AddComponent<LocalVolumetricFog>();
        var p = _volume.parameters;
        p.albedo = _smokeColor;
        p.meanFreePath = Mathf.Max(1f, _seeThrough);
        p.size = new Vector3(_width, _height + _sinkIntoGround, depth);
        p.positiveFade = new Vector3(soft, Mathf.Min(0.9f, soft * 2f), soft);
        p.negativeFade = new Vector3(soft, 0.02f, soft);
        p.volumeMask = GetMask();
        p.textureTiling = new Vector3(_width / puff, (_height + _sinkIntoGround) / puff, depth / puff);
        p.textureScrollingSpeed = _drift;
        _volume.parameters = p;
    }

    private void BuildCards()
    {
        if (_cardLayers <= 0 || _material == null) return;
        if (_block == null) _block = new MaterialPropertyBlock();

        float wallSeed = Mathf.Abs(transform.position.x * 0.137f + transform.position.z * 0.291f) % 10f;
        float puff = Mathf.Max(0.5f, _puffSize * 0.7f);
        var noiseScale = new Vector4(_width / puff, (_height + _sinkIntoGround) / puff, 0f, 0f);

        for (int i = 0; i < _cardLayers; i++)
        {
            GameObject go = CreateChild("FogLayer_" + i);
            float z = (i - (_cardLayers - 1) * 0.5f) * _cardSpacing;
            go.transform.localPosition = new Vector3(0f, -_sinkIntoGround, z);
            go.transform.localScale = new Vector3(_width, _height + _sinkIntoGround, 1f);

            go.AddComponent<MeshFilter>().sharedMesh = GetQuad();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            _block.Clear();
            _block.SetFloat(SeedId, wallSeed + i * 0.37f);
            _block.SetVector(NoiseScaleId, noiseScale);
            r.SetPropertyBlock(_block);

            _renderers.Add(r);
        }
    }

    // ทุกชิ้นอยู่ใต้ root ตัวเดียว เวลาถอยจะได้ขยับไปพร้อมกัน
    private GameObject CreateChild(string childName)
    {
        if (_root == null)
        {
            var rootGo = new GameObject("FogRoot") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            _root = rootGo.transform;
            _root.SetParent(transform, false);
        }

        var go = new GameObject(childName);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.layer = gameObject.layer;
        go.transform.SetParent(_root, false);
        return go;
    }

    private void DestroyChildren()
    {
        _renderers.Clear();
        _volume = null;
        if (_root != null) DestroyObject(_root.gameObject);
        _root = null;
    }

    private static void DestroyObject(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    // ---------- ลายก้อนหมอก 3 มิติ (สร้างครั้งเดียว ใช้ร่วมกันทุกกำแพง) ----------

    private static Texture3D GetMask()
    {
        if (_mask != null) return _mask;

        const int n = 32;
        var values = new float[n * n * n];
        float min = float.MaxValue, max = float.MinValue;

        for (int z = 0; z < n; z++)
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n, w = (float)z / n;
                    float value = 0.55f * PeriodicNoise(u, v, w, 4, 11)
                                + 0.30f * PeriodicNoise(u, v, w, 8, 23)
                                + 0.15f * PeriodicNoise(u, v, w, 16, 37);
                    values[x + y * n + z * n * n] = value;
                    if (value < min) min = value;
                    if (value > max) max = value;
                }

        var colors = new Color32[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float t = (values[i] - min) / Mathf.Max(0.0001f, max - min);
            t = t * t * (3f - 2f * t);
            // ไม่ให้เป็นรูโหว่จนมองทะลุ ส่วนบางสุดยังมีควัน 35%
            colors[i] = new Color32(255, 255, 255, (byte)(255f * (0.35f + 0.65f * t)));
        }

        _mask = new Texture3D(n, n, n, TextureFormat.RGBA32, false)
        {
            name = "FogWallMask",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        _mask.SetPixels32(colors);
        _mask.Apply(false, false);
        return _mask;
    }

    // value noise ที่ต่อขอบได้ไม่มีรอย (วนทุก period ช่อง)
    private static float PeriodicNoise(float u, float v, float w, int period, int seed)
    {
        float x = u * period, y = v * period, z = w * period;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), z0 = Mathf.FloorToInt(z);
        float fx = Smooth(x - x0), fy = Smooth(y - y0), fz = Smooth(z - z0);

        int x1 = (x0 + 1) % period, y1 = (y0 + 1) % period, z1 = (z0 + 1) % period;
        x0 %= period; y0 %= period; z0 %= period;

        float a = Mathf.Lerp(Hash(x0, y0, z0, seed), Hash(x1, y0, z0, seed), fx);
        float b = Mathf.Lerp(Hash(x0, y1, z0, seed), Hash(x1, y1, z0, seed), fx);
        float c = Mathf.Lerp(Hash(x0, y0, z1, seed), Hash(x1, y0, z1, seed), fx);
        float d = Mathf.Lerp(Hash(x0, y1, z1, seed), Hash(x1, y1, z1, seed), fx);
        return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Hash(int x, int y, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ seed * 2654435761);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    // แผ่นสี่เหลี่ยมหน้า-หลัง จุดหมุนอยู่ขอบล่าง กว้าง 1 สูง 1
    private static Mesh GetQuad()
    {
        if (_quad != null) return _quad;

        var bl = new Vector3(-0.5f, 0f, 0f);
        var br = new Vector3(0.5f, 0f, 0f);
        var tl = new Vector3(-0.5f, 1f, 0f);
        var tr = new Vector3(0.5f, 1f, 0f);

        _quad = new Mesh { name = "FogWallQuad", hideFlags = HideFlags.HideAndDontSave };
        _quad.vertices = new[] { bl, br, tl, tr, bl, br, tl, tr };
        _quad.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
        };
        _quad.triangles = new[] { 0, 2, 1, 2, 3, 1, 4, 5, 6, 6, 5, 7 };
        _quad.RecalculateNormals();
        _quad.RecalculateTangents();
        _quad.RecalculateBounds();
        return _quad;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(0f, (_height - _sinkIntoGround) * 0.5f, 0f),
            new Vector3(_width, _height + _sinkIntoGround, Mathf.Max(0.5f, _fogDepth)));
        if (_blockPlayer)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(0f, _height * 0.5f, 0f), new Vector3(_width, _height, 0.5f));
        }
    }
}