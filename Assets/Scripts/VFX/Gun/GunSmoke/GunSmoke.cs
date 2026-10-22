using System;
using System.Collections.Generic;
using UnityEngine;

// ควันปืน flintlock: ควันพุ่งตอนยิง + คลื่นกระแทก + ควันจานดินปืน + ควันลอยค้างหลังยิง + ไอร้อน
// ใส่ที่ Muzzle (ตัวเดียวกับ MuzzlePoint) — สร้าง/อัปเดตด้วย Tools > VFX > ควันปืน
// ปืนวาดด้วยกล้องแยก (View Model) ควันเลยถูกวาดในโลกจริง แล้วย้ายตำแหน่งให้ตรงกับปากกระบอกบนจอ
public class GunSmoke : MonoBehaviour
{
    public enum ShotType { Normal, Ultimate }

    [Serializable]
    public class ShotProfile
    {
        [Header("ควันพุ่งตอนยิง")]
        [Tooltip("จำนวนก้อน ยิ่งเยอะยิ่งหนาและเนียน")]
        public int PuffCount = 40;
        public Vector2 PuffSpeed = new Vector2(0.6f, 4f);
        public Vector2 PuffSize = new Vector2(0.06f, 0.13f);
        public Vector2 PuffLifetime = new Vector2(2f, 3.5f);
        [Range(0f, 1f)] public float PuffSpread = 0.3f;

        [Header("คลื่นกระแทก (ภาพบิดเป็นวง)")]
        [Tooltip("ขนาดวงตอนขยายสุด (เมตร) 0 = ไม่มี")]
        public float ShockwaveSize = 0.6f;

        [Header("ควันจากจานดินปืน")]
        public int PanCount = 3;

        [Header("ควันลอยค้างหลังยิง")]
        [Tooltip("ลอยนานกี่วินาที")]
        public float SmokeDuration = 4f;
        [Tooltip("ควันที่ม้วนออกจากปากกระบอก ต่อวินาที")]
        public float LingerRate = 14f;

        [Header("ไอร้อน")]
        public float HazeDuration = 1.2f;
        public float HazeRate = 14f;

        [Header("ประกายไฟ")]
        public int EmberCount = 0;
    }

    [Header("ชิ้นเอฟเฟกต์ (สร้างให้เองจากเมนู)")]
    [SerializeField] private Transform _fxRoot;
    [SerializeField] private ParticleSystem _puff;
    [SerializeField] private ParticleSystem _shockwave;
    [SerializeField] private ParticleSystem _pan;
    [SerializeField] private ParticleSystem _linger;
    [SerializeField] private ParticleSystem _haze;
    [SerializeField] private ParticleSystem _embers;

    [Header("ปืน")]
    [Tooltip("รัศมีปากกระบอก (เมตร) flintlock ปากกว้าง ควันจะออกเป็นวงกว้างตามนี้")]
    [SerializeField] private float _muzzleRadius = 0.012f;
    [Tooltip("จุดจานดินปืน — เว้นว่าง = ไม่มีควันตรงนี้")]
    [SerializeField] private Transform _panPoint;

    [Header("ยิงปกติ (สตั้น)")]
    [SerializeField] private ShotProfile _normal = new ShotProfile();

    [Header("Ultimate (นัดเดียวตาย)")]
    [SerializeField]
    private ShotProfile _ultimate = new ShotProfile
    {
        PuffCount = 90,
        PuffSpeed = new Vector2(1f, 7f),
        PuffSize = new Vector2(0.1f, 0.24f),
        PuffLifetime = new Vector2(3f, 5f),
        PuffSpread = 0.4f,
        ShockwaveSize = 1.4f,
        PanCount = 8,
        SmokeDuration = 9f,
        LingerRate = 24f,
        HazeDuration = 3f,
        HazeRate = 24f,
        EmberCount = 0
    };

    [Header("กล้อง")]
    [Tooltip("กล้องที่วาดปืน — เว้นว่าง = หาให้เอง (กล้องที่วาดลง RenderTexture)")]
    [SerializeField] private Camera _viewModelCamera;
    [SerializeField] private bool _alignToViewModel = true;

    [Header("Debug")]
    [SerializeField] private bool _logShots = false;

    private static readonly List<GunSmoke> _active = new List<GunSmoke>();
    public static GunSmoke Active => _active.Count > 0 ? _active[_active.Count - 1] : null;

    private Camera _mainCamera;
    private float _nextCameraSearch;
    private int _gunLayer;
    private float _heat;
    private float _heatDuration = 1f;
    private float _lingerRate;
    private float _hazeLeft;
    private float _hazeRate;
    private int _lastShotFrame = -1;

    private void Awake()
    {
        _gunLayer = FindGunLayer();

        // แยกเอฟเฟกต์ออกไปอยู่ในโลก ควันจะได้ค้างกลางอากาศแล้วลากตามเวลาส่ายปืน
        // รีเซ็ตขนาดด้วย: โมเดลปืนมักถูกย่อ ถ้าไม่รีเซ็ต ควันจะเล็กจนมองไม่เห็น
        if (_fxRoot != null)
        {
            _fxRoot.SetParent(null, false);
            _fxRoot.localScale = Vector3.one;
        }
    }

    private void OnEnable() => _active.Add(this);

    private void OnDisable()
    {
        _active.Remove(this);
        _heat = 0f;
        _hazeLeft = 0f;
        SetRate(_linger, 0f);
        SetRate(_haze, 0f);
    }

    private void OnDestroy()
    {
        if (_fxRoot != null)
            Destroy(_fxRoot.gameObject);
    }

    public void PlayNormal() => Play(ShotType.Normal);
    public void PlayUltimate() => Play(ShotType.Ultimate);

    [ContextMenu("ทดสอบ: ยิงปกติ")]
    private void TestNormal() => Play(ShotType.Normal);

    [ContextMenu("ทดสอบ: Ultimate")]
    private void TestUltimate() => Play(ShotType.Ultimate);

    public void Play(ShotType type)
    {
        if (_lastShotFrame == Time.frameCount)
            return;
        _lastShotFrame = Time.frameCount;

        ShotProfile p = type == ShotType.Ultimate ? _ultimate : _normal;
        UpdateEmitters();
        if (_fxRoot == null)
            return;

        if (_logShots)
            Debug.Log($"[GunSmoke] ยิง {type} | กล้องปืน: {(_viewModelCamera != null ? _viewModelCamera.name : "ไม่เจอ")}", this);

        Vector3 origin = _fxRoot.position;
        Vector3 dir = _fxRoot.forward;
        Vector3 up = _fxRoot.up;

        EmitJet(_puff, origin, dir, p.PuffCount, p.PuffSpeed, p.PuffSize, p.PuffLifetime, p.PuffSpread, _muzzleRadius);

        if (_shockwave != null && p.ShockwaveSize > 0f)
        {
            var ep = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = origin + dir * 0.03f,
                startSize = p.ShockwaveSize
            };
            _shockwave.Emit(ep, 1);
        }

        if (_pan != null && _panPoint != null)
            EmitJet(_pan, _pan.transform.position, (up + dir * 0.3f).normalized, p.PanCount,
                new Vector2(0.3f, 1f), p.PuffSize * 0.5f, p.PuffLifetime * 0.6f, 0.5f, 0.005f);

        if (_embers != null && p.EmberCount > 0)
            EmitJet(_embers, origin, dir, p.EmberCount,
                new Vector2(2f, 6f), new Vector2(0.006f, 0.015f), new Vector2(0.4f, 1.1f), 0.6f, _muzzleRadius);

        // ปืนร้อน: ถ้ายังร้อนจากนัดที่แรงกว่าอยู่ ใช้ค่าของนัดที่แรงกว่าต่อ
        float heatLeft = _heat * _heatDuration;
        if (p.SmokeDuration >= heatLeft)
        {
            _heatDuration = Mathf.Max(0.1f, p.SmokeDuration);
            _lingerRate = p.LingerRate;
        }
        _heat = 1f;

        _hazeRate = _hazeLeft > 0f ? Mathf.Max(_hazeRate, p.HazeRate) : p.HazeRate;
        _hazeLeft = Mathf.Max(_hazeLeft, p.HazeDuration);
    }

    private void LateUpdate()
    {
        UpdateEmitters();

        float dt = Time.deltaTime;
        if (_heat > 0f)
            _heat = Mathf.Max(0f, _heat - dt / _heatDuration);
        float k = Mathf.Pow(_heat, 1.3f);
        SetRate(_linger, _lingerRate * k);

        if (_hazeLeft > 0f)
        {
            _hazeLeft -= dt;
            SetRate(_haze, _hazeLeft > 0f ? _hazeRate : 0f);
        }
    }

    // ย้ายตัวปล่อยควันไปตรงกับปากกระบอกที่เห็นบนจอ
    private void UpdateEmitters()
    {
        if (_fxRoot == null)
            return;

        Vector3 tip = Project(transform.position);
        Vector3 ahead = Project(transform.position + transform.forward * 0.25f);
        Vector3 dir = ahead - tip;
        if (dir.sqrMagnitude < 0.000001f)
            dir = transform.forward;
        _fxRoot.SetPositionAndRotation(tip, Quaternion.LookRotation(dir.normalized, Vector3.up));

        if (_pan != null && _panPoint != null)
            _pan.transform.position = Project(_panPoint.position);
    }

    private Vector3 Project(Vector3 worldInGunSpace)
    {
        if (!_alignToViewModel)
            return worldInGunSpace;

        FindCameras();
        if (_viewModelCamera == null || _mainCamera == null)
            return worldInGunSpace;

        Vector3 viewport = _viewModelCamera.WorldToViewportPoint(worldInGunSpace);
        return _mainCamera.ViewportToWorldPoint(viewport);
    }

    private int FindGunLayer()
    {
        Transform search = transform.parent != null ? transform.parent : transform;
        foreach (Renderer r in search.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer)
                continue;
            return r.gameObject.layer;
        }
        return gameObject.layer;
    }

    private void FindCameras()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;
        if (_viewModelCamera != null || Time.unscaledTime < _nextCameraSearch)
            return;

        _nextCameraSearch = Time.unscaledTime + 1f;
        Camera fallback = null;
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam == _mainCamera || cam.targetTexture == null)
                continue;
            if ((cam.cullingMask & (1 << _gunLayer)) != 0)
            {
                _viewModelCamera = cam;
                return;
            }
            fallback = cam;
        }
        if (fallback != null)
            _viewModelCamera = fallback;
    }

    // พ่นเป็นลำจากปากกระบอกกว้าง: ส่วนใหญ่ช้า ค้างใกล้ปาก ไม่กี่ก้อนพุ่งไกล ได้รูปกรวยแบบดินปืนดำ
    private static void EmitJet(ParticleSystem ps, Vector3 origin, Vector3 dir, int count,
        Vector2 speed, Vector2 size, Vector2 lifetime, float spread, float radius)
    {
        if (ps == null || count <= 0)
            return;

        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.Cross(dir, Vector3.right);
        side.Normalize();
        Vector3 up = Vector3.Cross(side, dir);

        var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < count; i++)
        {
            Vector2 disc = UnityEngine.Random.insideUnitCircle * radius;
            float fast = Mathf.Pow(UnityEngine.Random.value, 2f);
            Vector3 jitter = UnityEngine.Random.insideUnitSphere * spread * (1f - fast * 0.6f);
            ep.position = origin + side * disc.x + up * disc.y + dir * (UnityEngine.Random.value * 0.03f);
            ep.velocity = (dir + jitter).normalized * Mathf.Lerp(speed.x, speed.y, fast);
            ep.startSize = Mathf.Lerp(size.x, size.y, UnityEngine.Random.value) * Mathf.Lerp(0.8f, 1.3f, fast);
            ep.startLifetime = Mathf.Lerp(lifetime.x, lifetime.y, UnityEngine.Random.value);
            ps.Emit(ep, 1);
        }
    }

    private static void SetRate(ParticleSystem ps, float rate)
    {
        if (ps == null)
            return;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        if (rate > 0f && !ps.isPlaying)
            ps.Play();
    }
}