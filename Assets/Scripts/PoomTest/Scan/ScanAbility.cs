using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using Yantra.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// สแกน — กดเมาส์กลาง จอเปลี่ยนเป็นขาวดำ คลื่นโซนาร์วิ่งไปตามพื้นและสิ่งก่อสร้าง
// คลื่นผ่านศัตรูที่มองเห็นได้ (ไม่มีกำแพงบัง) = จุดแดงขึ้นเหนือหัว / หาศัตรูจาก Tag / มีระยะ มีคูลดาวน์
// วางบนตัวผู้เล่น — โค้ดสร้าง Volume ขาวดำ, คลื่นโซนาร์, Canvas จุดแดง และรูปทั้งหมดเอง
// ต้องลาก Shader "SonarScan" ใส่ช่อง Sonar Shader อย่างเดียว
public class ScanAbility : MonoBehaviour
{
    [Header("หาศัตรู")]
    [Tooltip("Tag ของศัตรู — ใส่ได้หลายอัน")]
    [SerializeField] private string[] _enemyTags = { "Enemy" };

    [Tooltip("สแกนเจอไกลสุดกี่เมตร")]
    [SerializeField, Min(1f)] private float _range = 25f;

    [Tooltip("ติ๊ก = มีกำแพงบังระหว่างเรากับศัตรู จะไม่ขึ้นจุด (สแกนทะลุบ้านไม่ได้)")]
    [SerializeField] private bool _blockedByWalls = true;

    [Tooltip("อะไรบังคลื่นได้บ้าง — ตัวผู้เล่นกับศัตรูไม่นับเป็นที่บังอยู่แล้ว")]
    [SerializeField] private LayerMask _wallLayers = ~0;

    [Header("จังหวะ")]
    [Tooltip("คลื่นสแกนวิ่งออกไปเร็วแค่ไหน (เมตรต่อวินาที) — ศัตรูใกล้ขึ้นก่อน ไกลขึ้นทีหลัง")]
    [SerializeField, Min(1f)] private float _waveSpeed = 20f;

    [Tooltip("จุดแดงโชว์ค้างกี่วินาที (นับจากกดสแกน)")]
    [SerializeField, Min(0.5f)] private float _duration = 4f;

    [Tooltip("ใช้ซ้ำได้อีกทีหลังกี่วินาที (นับจากกดสแกน)")]
    [SerializeField, Min(0f)] private float _cooldown = 8f;

    [Header("จอขาวดำ")]
    [Tooltip("เว้นว่าง = สร้างให้เอง (ขาวดำ + ขอบจอมืด)\nใส่ Volume Profile ของตัวเองได้ถ้าอยากแต่งเอง")]
    [SerializeField] private VolumeProfile _customProfile;

    [Tooltip("ความอิ่มสี — -100 = ขาวดำสนิท")]
    [SerializeField, Range(-100f, 0f)] private float _saturation = -100f;

    [Tooltip("ความคมชัด เพิ่มนิดหน่อยให้ภาพขาวดำไม่จืด")]
    [SerializeField, Range(-50f, 50f)] private float _contrast = 15f;

    [Tooltip("ย้อมสีทับเบา ๆ — ขาว = ไม่ย้อม")]
    [SerializeField] private Color _tint = Color.white;

    [Tooltip("ขอบจอมืด — 0 = ไม่มี")]
    [SerializeField, Range(0f, 1f)] private float _vignette = 0.35f;

    [Tooltip("เปลี่ยนเป็นขาวดำ / กลับเป็นสี ภายในกี่วินาที")]
    [SerializeField, Min(0.01f)] private float _fadeTime = 0.25f;

    [Tooltip("Layer ของ Volume — กล้องต้องรวม Layer นี้ใน Volume Layer Mask ถึงจะเห็นผล (ปกติ Default)")]
    [SerializeField] private int _volumeLayer = 0;

    [Header("จุดแดง")]
    [Tooltip("เว้นว่าง = ใช้จุดแดงเรืองแสงที่สร้างให้เอง")]
    [SerializeField] private Sprite _markerSprite;
    [SerializeField] private Color _markerColor = new(1f, 0.12f, 0.1f, 1f);

    [Tooltip("ขนาดจุด (พิกเซลที่จอสูง 1080) — ศัตรูไกลจุดจะเล็กลง")]
    [SerializeField, Min(1f)] private float _markerSize = 26f;

    [Tooltip("จุดอยู่สูงกว่าหัวกี่เมตร")]
    [SerializeField] private float _headOffset = 0.35f;

    [Tooltip("ชื่อกระดูกหัว — หาไม่เจอจะใช้ขอบบนของโมเดลแทน")]
    [SerializeField] private string _headBoneName = "Head";

    [Header("คลื่นโซนาร์")]
    [Tooltip("ลาก Shader \"SonarScan\" มาใส่ — เว้นว่าง = ไม่มีคลื่นบนพื้น (จุดแดงยังทำงาน)")]
    [SerializeField] private Shader _sonarShader;

    [Tooltip("สีคลื่น — ทำขาวดำก่อนแล้วค่อยวาดคลื่น เลยยังเป็นสี")]
    [SerializeField] private Color _sonarColor = new(0.55f, 0.9f, 1f, 0.9f);

    [Tooltip("คลื่นสว่างแค่ไหน — มากกว่า 1 จะเรืองแสง (Bloom)")]
    [SerializeField, Min(0.1f)] private float _sonarBrightness = 1.6f;

    [Tooltip("หน้าคลื่นหนากี่เมตร")]
    [SerializeField, Min(0.05f)] private float _sonarWidth = 1.2f;

    [Tooltip("หางคลื่นยาวกี่เมตร — หมึกจาง ๆ ที่เหลือหลังหน้าคลื่นผ่าน")]
    [SerializeField, Min(0f)] private float _sonarTrail = 8f;

    [Tooltip("เส้นวงซ้อนห่างกันกี่เมตร — 0 = ไม่มี")]
    [SerializeField, Min(0f)] private float _sonarRings = 2.5f;

    [Tooltip("คลื่นถึงขอบระยะแล้วจางหายในกี่วินาที")]
    [SerializeField, Min(0.05f)] private float _sonarFadeOut = 0.5f;

    [Tooltip("ทดสอบ: ติ๊กแล้วกดสแกน ทุกอย่างที่ไม่ใช่ท้องฟ้าจะเป็นสีแดง\nแดง = คลื่นทำงาน ปัญหาอยู่ที่ค่าตั้ง / ไม่แดง = คลื่นไม่ได้ถูกวาดเลย")]
    [SerializeField] private bool _debugSonarTint;

    [Header("เสียง (เว้นว่างได้)")]
    [SerializeField] private SoundID _scanSound;
    [Tooltip("กดตอนยังคูลดาวน์อยู่")]
    [SerializeField] private SoundID _notReadySound;

#if ENABLE_INPUT_SYSTEM
    [Header("ปุ่ม")]
    [Tooltip("Action สแกนจาก Input Actions — เว้นว่าง = เมาส์กลาง")]
    [SerializeField] private InputActionReference _scanAction;
#endif

    [Header("อ้างอิง (เว้นว่าง = หาเอง)")]
    [SerializeField] private Camera _camera;

    [Header("Gizmo (ไว้เช็คระยะ)")]
    [Tooltip("โชว์วงระยะสแกนบนพื้นในฉาก — ไม่ต้องคลิกเลือกตัวผู้เล่นก็เห็น\nอยากเห็นในหน้า Game ด้วย ให้กดปุ่ม Gizmos มุมขวาบนของหน้าต่าง Game")]
    [SerializeField] private bool _showGizmo = true;
    [SerializeField] private Color _gizmoColor = new(1f, 0.25f, 0.2f, 0.9f);

    [Tooltip("ตอนสแกน วาดเส้นจากตาไปหาศัตรูแต่ละตัว — เขียว = เห็น / แดง = มีกำแพงบัง")]
    [SerializeField] private bool _gizmoSightLines = true;

    [Header("Debug")]
    [SerializeField] private bool _logScan;

    public bool IsScanning => _scanTime >= 0f && _scanTime < _duration;
    public float CooldownLeft => Mathf.Max(0f, _cooldown - (Time.time - _lastScan));

    private class Target
    {
        public Transform Root;
        public bool Revealed;
        public bool Checked;
        public float RevealTime = -1f;
        public Transform Head;
        public Renderer[] Renderers;
        public Health Health;
        public RectTransform Marker;
        public Image Image;
    }

    private readonly List<Target> _targets = new();
    private readonly List<Image> _markerPool = new();
    private readonly HashSet<Transform> _found = new();

    private Volume _volume;
    private VolumeProfile _runtimeProfile;
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private PlayerCameraController _playerCamera;

    private Material _sonarMaterial;
    private CustomPassVolume _sonarVolume;
    private FullScreenCustomPass _sonarPass;
    private bool _sonarChecked;
    private readonly RaycastHit[] _hits = new RaycastHit[16];

    private static readonly int SonarOriginId = Shader.PropertyToID("_SonarOrigin");
    private static readonly int SonarParamsId = Shader.PropertyToID("_SonarParams");
    private static readonly int SonarColorId = Shader.PropertyToID("_SonarColor");
    private static readonly int SonarShapeId = Shader.PropertyToID("_SonarShape");
    private static readonly int SonarGrayId = Shader.PropertyToID("_SonarGray");

    private float _scanTime = -1f;
    private float _lastScan = -999f;
    private Vector3 _scanOrigin;      // จุดตา — ใช้เช็คกำแพงบัง
    private Vector3 _waveOrigin;      // จุดเท้า — คลื่นบนพื้นเริ่มจากตรงนี้

#if ENABLE_INPUT_SYSTEM
    private bool _enabledAction;
#endif

    private static Sprite _dotSprite;

    private void Awake()
    {
        _playerCamera = transform.root.GetComponentInChildren<PlayerCameraController>(true);
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (_scanAction != null && _scanAction.action != null && !_scanAction.action.enabled)
        {
            _scanAction.action.Enable();
            _enabledAction = true;
        }
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (_enabledAction && _scanAction != null && _scanAction.action != null)
            _scanAction.action.Disable();
        _enabledAction = false;
#endif
        EndScan();
        if (_volume != null) _volume.weight = 0f;

        if (_sonarVolume != null) _sonarVolume.enabled = false;
    }

    private void OnDestroy()
    {
        if (_sonarVolume != null) Destroy(_sonarVolume.gameObject);
        if (_sonarMaterial != null) Destroy(_sonarMaterial);
        if (_volume != null) Destroy(_volume.gameObject);
        if (_runtimeProfile != null) Destroy(_runtimeProfile);
        if (_canvas != null) Destroy(_canvas.gameObject);
    }

    private void Update()
    {
        if (ScanPressed() && CanUse())
        {
            if (CooldownLeft > 0f) PlaySound(_notReadySound);
            else StartScan();
        }

        if (_scanTime < 0f) return;

        _scanTime += Time.deltaTime;   // หยุดตามเกมตอน Pause
        UpdateVolume();
        UpdateSonar();
        RevealTargets();
        UpdateMarkers();

        if (_scanTime >= _duration + _fadeTime) EndScan();
    }

    // ---------- เริ่ม / จบ ----------

    private void StartScan()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        EnsureSonar();      // ก่อน Volume — มีคลื่นแล้ว Volume ไม่ต้องทำขาวดำ (pass ทำเอง)
        EnsureVolume();
        EnsureCanvas();

        _lastScan = Time.time;
        _scanTime = 0f;
        _scanOrigin = _camera.transform.position;
        _waveOrigin = FindGround(_scanOrigin);

        ClearTargets();
        FindTargets();
        PlaySound(_scanSound);

        if (_logScan)
            Debug.Log($"[Scan] เจอศัตรู {_targets.Count} ตัว ในระยะ {_range} ม.", this);
    }

    private void EndScan()
    {
        _scanTime = -1f;
        ClearTargets();
        if (_volume != null) _volume.weight = 0f;
        if (_sonarVolume != null) _sonarVolume.enabled = false;
        Shader.SetGlobalVector(SonarParamsId, Vector4.zero);
    }

    // จุดเท้า — ยิงลงพื้นจากตา ข้ามคอลไลเดอร์ของตัวเราเอง
    private Vector3 FindGround(Vector3 eye)
    {
        int count = Physics.RaycastNonAlloc(eye, Vector3.down, _hits, 4f, _wallLayers, QueryTriggerInteraction.Ignore);
        Transform player = transform.root;
        float best = float.MaxValue;
        Vector3 point = eye - Vector3.up * 1.6f;

        for (int i = 0; i < count; i++)
        {
            if (_hits[i].transform == null || _hits[i].transform.IsChildOf(player)) continue;
            if (_hits[i].distance >= best) continue;
            best = _hits[i].distance;
            point = _hits[i].point;
        }

        return point + Vector3.up * 0.05f;
    }

    private bool CanUse()
    {
        if (Time.timeScale <= 0f) return false;
        if (StateMachineController.IsPaused) return false;

        var ui = UIManager.Instance;
        if (ui != null && ui.HasAnyOpen) return false;

        if (_playerCamera != null && _playerCamera.IsCutsceneMode) return false;
        return true;
    }

    // ---------- หาศัตรู ----------

    private void FindTargets()
    {
        _found.Clear();
        float searchSqr = (_range + 5f) * (_range + 5f);

        foreach (string tag in _enemyTags)
        {
            if (string.IsNullOrEmpty(tag)) continue;

            GameObject[] tagged;
            try
            {
                tagged = GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException)
            {
                Debug.LogWarning($"[Scan] ยังไม่มี Tag ชื่อ '{tag}' ในโปรเจกต์ — เพิ่มที่ Tags and Layers ก่อน", this);
                continue;
            }

            foreach (GameObject obj in tagged)
            {
                if (!obj.activeInHierarchy) continue;

                // ศัตรูตัวเดียวติด Tag หลายชิ้น — รวมเป็นตัวเดียว ไม่งั้นขึ้นจุดซ้อน
                Transform root = EnemyRoot(obj.transform, tag, out Health health);
                if (!_found.Add(root)) continue;
                if (health != null && health.IsDead) continue;

                var target = new Target { Root = root, Health = health };
                target.Head = FindHead(root);
                target.Renderers = target.Head == null ? BodyRenderers(root) : null;

                if ((MarkerPoint(target) - _scanOrigin).sqrMagnitude > searchSqr) continue;
                _targets.Add(target);
            }
        }
    }

    // ตัวบนสุดของศัตรูตัวนี้: มี Health = ใช้ตัวที่ถือ Health / ไม่มี = ไต่ขึ้นไปหาตัวบนสุดที่ติด Tag เดียวกัน
    private static Transform EnemyRoot(Transform t, string tag, out Health health)
    {
        Transform top = t;
        for (Transform p = t.parent; p != null; p = p.parent)
            if (p.CompareTag(tag)) top = p;

        health = t.GetComponentInParent<Health>();
        if (health == null) health = top.GetComponentInChildren<Health>();

        if (health == null) return top;
        return top.IsChildOf(health.transform) ? health.transform : top;
    }

    // ใช้แค่ตัวโมเดลจริง — Particle / Trail มีขอบเขตใหญ่เกินตัว จุดจะลอยขึ้นฟ้า
    private static Renderer[] BodyRenderers(Transform root)
    {
        var list = new List<Renderer>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            if (r is SkinnedMeshRenderer || r is MeshRenderer) list.Add(r);
        return list.ToArray();
    }

    // คลื่นวิ่งไปถึงตัวไหนแล้ว — เช็คกำแพงบังตอนคลื่นถึงตัว ไม่ใช่ตอนกด
    private void RevealTargets()
    {
        float radius = _scanTime * _waveSpeed;

        foreach (Target target in _targets)
        {
            if (target.Checked || target.Root == null) continue;

            Vector3 head = MarkerPoint(target) - Vector3.up * _headOffset;
            float distance = Vector3.Distance(_waveOrigin, head);
            if (distance > _range) continue;          // ยังนอกระยะ — ถ้าเดินเข้ามาระหว่างคลื่นยังวิ่งอยู่ก็ยังมีสิทธิ์
            if (distance > radius) continue;          // คลื่นยังไม่ถึง

            target.Checked = true;
            target.Revealed = !_blockedByWalls || CanSee(target, head);

            if (_logScan)
                Debug.Log($"[Scan] คลื่นถึง {target.Root.name} ({distance:0.0} ม.) — {(target.Revealed ? "เห็น" : "มีกำแพงบัง")}", this);
        }
    }

    // มองเห็นหัวหรือกลางตัวอย่างใดอย่างหนึ่ง = เห็น
    private bool CanSee(Target target, Vector3 head)
    {
        Vector3 chest = target.Root.position + Vector3.up * 1.0f;
        return Clear(target, head) || Clear(target, chest);
    }

    private bool Clear(Target target, Vector3 point)
    {
        Vector3 dir = point - _scanOrigin;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;

        int count = Physics.RaycastNonAlloc(_scanOrigin, dir / dist, _hits, dist, _wallLayers, QueryTriggerInteraction.Ignore);
        Transform player = transform.root;

        for (int i = 0; i < count; i++)
        {
            Transform hit = _hits[i].transform;
            if (hit == null) continue;
            if (hit.IsChildOf(player)) continue;        // ตัวเราเอง
            if (IsEnemy(hit)) continue;                 // ตัวศัตรู (ตัวนี้หรือตัวอื่น) ไม่นับเป็นกำแพง
            return false;
        }
        return true;
    }

    private bool IsEnemy(Transform t)
    {
        foreach (Target target in _targets)
            if (target.Root != null && t.IsChildOf(target.Root)) return true;
        return false;
    }

    private Transform FindHead(Transform root)
    {
        Animator animator = root.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (bone != null) return bone;
        }

        if (string.IsNullOrEmpty(_headBoneName)) return null;

        foreach (Transform t in root.GetComponentsInChildren<Transform>())
            if (t.name.Equals(_headBoneName, System.StringComparison.OrdinalIgnoreCase))
                return t;

        return null;
    }

    private Vector3 MarkerPoint(Target target)
    {
        if (target.Head != null)
            return target.Head.position + Vector3.up * _headOffset;

        // ไม่มีกระดูกหัว — ใช้ขอบบนของโมเดล
        bool any = false;
        Bounds bounds = default;
        if (target.Renderers != null)
        {
            foreach (Renderer r in target.Renderers)
            {
                if (r == null || !r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
        }

        if (!any) return target.Root.position + Vector3.up * (1.8f + _headOffset);
        return new Vector3(bounds.center.x, bounds.max.y + _headOffset, bounds.center.z);
    }

    // ---------- ภาพ ----------

    private void UpdateVolume()
    {
        float target = _scanTime < _duration ? 1f : 0f;
        _volume.weight = Mathf.MoveTowards(_volume.weight, target, Time.deltaTime / _fadeTime);
    }

    private void UpdateSonar()
    {
        if (_sonarVolume == null) return;

        float radius = _scanTime * _waveSpeed;
        float reachEnd = (_range + _sonarWidth) / _waveSpeed;
        float strength = 1f - Mathf.Clamp01((_scanTime - reachEnd) / _sonarFadeOut);
        float gray = (_volume != null ? _volume.weight : 0f) * (-_saturation / 100f);

        // เปิดตลอดช่วงสแกน — pass นี้ทำขาวดำด้วย
        _sonarVolume.enabled = true;
        Shader.SetGlobalVector(SonarOriginId, _waveOrigin);
        Shader.SetGlobalVector(SonarParamsId, new Vector4(radius, _sonarWidth, _sonarTrail, strength));
        Shader.SetGlobalVector(SonarShapeId, new Vector4(_range, _sonarRings, _debugSonarTint ? 1f : 0f, _sonarBrightness));
        Shader.SetGlobalVector(SonarGrayId, new Vector4(gray, 0f, 0f, 0f));
        Shader.SetGlobalColor(SonarColorId, _sonarColor);
    }

    private void UpdateMarkers()
    {
        bool hidden = MenuOpen();
        float uiScale = Screen.height / 1080f;
        float fadeOut = Mathf.Clamp01((_duration - _scanTime) / _fadeTime);

        foreach (Target target in _targets)
        {
            bool alive = target.Root != null && target.Root.gameObject.activeInHierarchy
                         && (target.Health == null || !target.Health.IsDead);

            if (!alive || hidden || !target.Revealed)
            {
                if (target.Image != null) target.Image.enabled = false;
                continue;
            }

            Vector3 screen = _camera.WorldToScreenPoint(MarkerPoint(target));
            if (screen.z <= 0f)
            {
                if (target.Image != null) target.Image.enabled = false;   // อยู่ข้างหลังกล้อง
                continue;
            }

            if (target.Image == null) AttachMarker(target);

            // โผล่: เด้งจากใหญ่ / ค้าง: เต้นเบา ๆ / ท้าย: จางหาย
            if (target.RevealTime < 0f) target.RevealTime = _scanTime;
            float age = _scanTime - target.RevealTime;
            float pop = Mathf.Clamp01(age / 0.18f);
            float scale = Mathf.Lerp(1.8f, 1f, 1f - (1f - pop) * (1f - pop));
            scale *= 1f + 0.08f * Mathf.Sin(age * 9f);

            float distance = Vector3.Distance(_camera.transform.position, target.Root.position);
            float far = Mathf.Lerp(1.15f, 0.6f, Mathf.Clamp01(distance / _range));
            float size = _markerSize * uiScale * far * scale;

            Color c = _markerColor;
            c.a *= pop * fadeOut;

            target.Image.enabled = true;
            target.Image.color = c;
            target.Marker.anchoredPosition = new Vector2(screen.x, screen.y);
            target.Marker.sizeDelta = new Vector2(size, size);
        }
    }

    private void AttachMarker(Target target)
    {
        Image image = null;
        foreach (Image pooled in _markerPool)
        {
            if (!pooled.gameObject.activeSelf) { image = pooled; break; }
        }

        if (image == null)
        {
            image = CreateImage("ScanMarker", _markerSprite != null ? _markerSprite : DotSprite());
            _markerPool.Add(image);
        }

        image.gameObject.SetActive(true);
        target.Image = image;
        target.Marker = image.rectTransform;
    }

    private void ClearTargets()
    {
        foreach (Target target in _targets)
        {
            if (target.Image == null) continue;
            target.Image.enabled = false;
            target.Image.gameObject.SetActive(false);
        }
        _targets.Clear();
    }

    private static bool MenuOpen()
    {
        var ui = UIManager.Instance;
        return ui != null && ui.HasAnyOpen;
    }

    // ---------- สร้างของเอง ----------

    private void EnsureVolume()
    {
        if (_volume != null) return;

        var go = new GameObject("ScanVolume") { layer = _volumeLayer };
        _volume = go.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 50f;   // ทับ Volume ปกติของฉาก
        _volume.weight = 0f;

        if (_customProfile != null)
        {
            _volume.sharedProfile = _customProfile;
            return;
        }

        _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _runtimeProfile.name = "ScanProfile (Runtime)";

        ColorAdjustments color = _runtimeProfile.Add<ColorAdjustments>(true);
        // มีคลื่นโซนาร์ = pass ทำขาวดำเอง (คลื่นจะได้ยังเป็นสี) / ไม่มี = ให้ Volume ทำ
        color.saturation.Override(_sonarVolume != null ? 0f : _saturation);
        color.contrast.Override(_contrast);
        color.colorFilter.Override(_tint);

        if (_vignette > 0f)
        {
            Vignette vignette = _runtimeProfile.Add<Vignette>(true);
            vignette.intensity.Override(_vignette);
        }

        _volume.sharedProfile = _runtimeProfile;
    }

    private void EnsureCanvas()
    {
        if (_canvas != null) return;

        var go = new GameObject("ScanMarkers", typeof(RectTransform), typeof(Canvas));
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;   // ไม่โดนจอขาวดำ จุดแดงเลยเด่น
        _canvas.sortingOrder = 5;
        _canvasRect = (RectTransform)go.transform;
    }

    // คลื่นโซนาร์ = Full Screen Custom Pass — โค้ดสร้าง Custom Pass Volume ให้เอง ไม่ต้องมีในฉาก
    private void EnsureSonar()
    {
        if (!_sonarChecked)
        {
            _sonarChecked = true;
            CheckSonarSetup();
        }

        if (_sonarVolume != null || _sonarShader == null || !_sonarShader.isSupported) return;

        if (_sonarMaterial == null)
            _sonarMaterial = new Material(_sonarShader) { name = "SonarScan (Runtime)" };

        // ทำก่อน Post Process — เกมเรนเดอร์เล็กแล้วค่อยขยายตอน Post Process
        // ถ้าทำหลัง ภาพจะใหญ่กว่าข้อมูลความลึก คลื่นเลยไปอยู่มุมจอเป็นแผนที่ย่อ
        // ผูกกับกล้องหลักตัวเดียว — กล้องมือ (เรนเดอร์ลง RenderTexture) จะไม่โดน
        var go = new GameObject("SonarScanPass");
        _sonarVolume = go.AddComponent<CustomPassVolume>();
        _sonarVolume.isGlobal = true;
        _sonarVolume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
        _sonarVolume.targetCamera = _camera;

        _sonarPass = _sonarVolume.AddPassOfType<FullScreenCustomPass>();
        _sonarPass.name = "SonarScan";
        _sonarPass.fullscreenPassMaterial = _sonarMaterial;
        _sonarPass.fetchColorBuffer = true;   // อ่านภาพเดิมมาทำขาวดำ

        _sonarVolume.enabled = false;
    }

    // บอกใน Console ว่าทำไมไม่เห็นคลื่น — เช็คครั้งเดียวตอนสแกนครั้งแรก
    private void CheckSonarSetup()
    {
        if (_sonarShader == null)
        {
            Debug.LogWarning("[Scan] ช่อง Sonar Shader ยังว่าง — ลากไฟล์ SonarScan.shader มาใส่ ไม่งั้นไม่มีคลื่นบนพื้น", this);
            return;
        }

        if (!_sonarShader.isSupported)
            Debug.LogWarning("[Scan] Shader SonarScan คอมไพล์ไม่ผ่าน — คลิกไฟล์ shader ดู error ใน Inspector แล้วส่งมาดู", _sonarShader);

        if (GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset hdrp &&
            !hdrp.currentPlatformRenderPipelineSettings.supportCustomPass)
            Debug.LogWarning("[Scan] HDRP Asset ปิด Custom Pass อยู่ — เปิดที่ HDRP Asset > Rendering > ติ๊ก Custom Pass", hdrp);

        try
        {
            if (_camera != null && !HDCamera.GetOrCreate(_camera).frameSettings.IsEnabled(FrameSettingsField.CustomPass))
                Debug.LogWarning("[Scan] กล้องปิด Custom Pass ใน Frame Settings — เปิดที่ Project Settings > Graphics > HDRP Global Settings > Frame Settings (Camera) > Rendering > Custom Pass", _camera);
        }
        catch (System.Exception)
        {
            // เช็คไม่ได้ก็ข้ามไป ไม่ใช่เรื่องใหญ่
        }

        if (_logScan)
            Debug.Log($"[Scan] คลื่นโซนาร์: shader '{_sonarShader.name}' พร้อมใช้", this);
    }

    private Image CreateImage(string objectName, Sprite sprite)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_canvasRect, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    // จุดแดง: แกนทึบ + เรืองแสงจางออก
    private static Sprite DotSprite()
    {
        if (_dotSprite == null)
            _dotSprite = RadialSprite("ScanDot", r =>
            {
                float core = 1f - Mathf.Clamp01((r - 0.28f) / 0.06f);
                float glow = (1f - r) * (1f - r) * 0.55f;
                return Mathf.Max(core, glow);
            });
        return _dotSprite;
    }

    private static Sprite RadialSprite(string name, System.Func<float, float> alpha)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = r >= 1f ? 0f : Mathf.Clamp01(alpha(r));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // ---------- ปุ่ม / เสียง ----------

    private bool ScanPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (_scanAction != null && _scanAction.action != null)
            return _scanAction.action.WasPressedThisFrame();
        return Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(2);
#endif
    }

    private static void PlaySound(SoundID id)
    {
        if (id == null || SoundManager.Instance == null) return;
        SoundManager.Instance.PlayEventSFX(id);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showGizmo) return;

        bool scanning = Application.isPlaying && _scanTime >= 0f;
        Vector3 center = scanning ? _waveOrigin : transform.position;

        // วงระยะบนพื้น + ทรงกลมจาง ๆ
        UnityEditor.Handles.color = _gizmoColor;
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, _range, 2f);
        Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, _gizmoColor.a * 0.2f);
        Gizmos.DrawWireSphere(center, _range);
        UnityEditor.Handles.Label(center + Vector3.forward * _range + Vector3.up * 0.3f, $"สแกน {_range:0} ม.");

        if (!scanning) return;

        // หน้าคลื่นตอนนี้
        UnityEditor.Handles.color = _sonarColor;
        UnityEditor.Handles.DrawWireDisc(_waveOrigin, Vector3.up, Mathf.Min(_scanTime * _waveSpeed, _range), 3f);

        if (!_gizmoSightLines) return;

        foreach (Target target in _targets)
        {
            if (!target.Checked || target.Root == null) continue;
            Gizmos.color = target.Revealed ? Color.green : Color.red;
            Gizmos.DrawLine(_scanOrigin, MarkerPoint(target) - Vector3.up * _headOffset);
        }
    }
#endif
}