using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using Effekseer;

/// <summary>ตัวเลือกตอนเล่น — ตรงกับช่อง Duration / Speed / Fade Out ในสคริปต์เล่น VFX ของเรา</summary>
public struct VFXPlayOptions
{
    /// <summary>เล่นนานกี่วิแล้วสั่งหยุดพ่น — 0 = ใช้ Max Lifetime ของ prefab (0 ทั้งคู่ = เล่นจนจบเอง)</summary>
    public float Duration;

    /// <summary>ความเร็ว 1 = ปกติ (0 หรือติดลบ ถือเป็น 1)</summary>
    public float Speed;

    /// <summary>หลังสั่งหยุดพ่น รอให้จางได้นานสุดกี่วิแล้วตัดทิ้ง — ติดลบ = รอจนหมดเอง</summary>
    public float FadeOut;

    /// <summary>มากกว่า 0 = โหมดค้าง ต้องเรียก handle.Ping() เรื่อยๆ ขาดเกินกี่วิถึงเริ่มหยุดพ่น</summary>
    public float HoldGrace;

    public static VFXPlayOptions Default => new VFXPlayOptions { Speed = 1f, FadeOut = -1f };
}

/// <summary>
/// ติดอยู่บนเอฟเฟกต์แต่ละตัวในพูล — VFXPooler ใส่ให้เองถ้า prefab ไม่มี (ไม่ต้องไปแก้ prefab)
/// รองรับ Particle System / VFX Graph / Effekseer Emitter หรือผสมกันใน prefab เดียว
/// </summary>
[DisallowMultipleComponent]
public class VFXPlayer : MonoBehaviour
{
    [Tooltip("อายุสูงสุด (วินาที) ถ้าตอนเล่นไม่ได้กำหนด Duration มา — ครบแล้วสั่งหยุดพ่น รอจางแล้วคืนพูล\n" +
             "0 = ไม่จำกัด รอจน particle ตายหมดเอง")]
    [SerializeField, Min(0f)] private float _maxLifetime = 0f;

    private const float CheckInterval = 0.1f;      // เช็คว่าจบหรือยังทุก 0.1 วิ ไม่ต้องทุกเฟรม
    private const float NoEffectFallback = 2f;     // prefab ไม่มีตัวพ่นอะไรเลย และไม่ได้กำหนดเวลา
    private const float StartGrace = 0.2f;         // เฟรมแรกๆ อนุภาคอาจยังไม่ทันเกิด อย่าเพิ่งเช็ค

    // VFX Graph นับ particle บน GPU แล้วค่อยส่งตัวเลขกลับมา → ช้ากว่าความจริงไม่กี่เฟรม
    private const float GraphWarmup = 0.3f;
    // เกินเวลานี้แล้วยังไม่เคยเห็น particle สักตัว ถือว่าจบ (เช่นเล่นนอกจอแล้วไม่ได้ simulate)
    private const float GraphNoParticleTimeout = 1.5f;

    private ParticleSystem[] _roots = new ParticleSystem[0];        // เฉพาะตัวบนสุด ไว้สั่ง Play/Stop
    private ParticleSystem[] _allParticles = new ParticleSystem[0]; // ทุกตัว ไว้ปรับความเร็ว
    private float[] _baseSimSpeed = new float[0];
    private VisualEffect[] _graphs = new VisualEffect[0];
    private float[] _baseGraphRate = new float[0];
    private EffekseerEmitter[] _effekseer = new EffekseerEmitter[0];
    private float[] _baseEfkSpeed = new float[0];
    private bool[] _baseEfkLoop = new bool[0];
    private TrailRenderer[] _trails = new TrailRenderer[0];
    private IPooledObject[] _spawnCallbacks = new IPooledObject[0];

    private Transform _home;
    private Vector3 _baseScale;
    private Quaternion _baseRotation;
    private bool _isSetup;

    // ตามเป้าหมาย (เหมือน SetParent แต่ไม่ได้ SetParent จริง — เป้าหมายโดน Destroy เอฟเฟกต์ไม่หายตาม)
    private Transform _follow;
    private bool _hasFollow;
    private bool _followRotation;
    private Vector3 _followLocalPos;
    private Quaternion _followLocalRot;

    private float _nextCheckTime;
    private float _stopAt;
    private float _releaseAt;
    private float _fadeOutCap;
    private float _holdGrace;
    private float _lastPing;
    private bool _stopRequested;
    private bool _graphSeenAlive;

    public bool InUse { get; private set; }
    public int Version { get; private set; }
    public float SpawnTime { get; private set; }
    public bool IsStopping => _stopRequested;
    public float MaxLifetime => _maxLifetime;
    public bool HasEffects => _roots.Length > 0 || _graphs.Length > 0 || _effekseer.Length > 0;

    /// <summary>VFX Graph ตัวแรกใน prefab (null ถ้าไม่มี)</summary>
    public VisualEffect Graph => _graphs.Length > 0 ? _graphs[0] : null;

    // ---------- ตั้งค่าครั้งเดียวตอนสร้าง ----------

    internal void Setup(Transform home, float maxLifetimeOverride)
    {
        _home = home;
        _baseScale = transform.localScale;
        _baseRotation = transform.localRotation;
        if (maxLifetimeOverride > 0f) _maxLifetime = maxLifetimeOverride;

        _allParticles = GetComponentsInChildren<ParticleSystem>(true);
        _baseSimSpeed = new float[_allParticles.Length];
        var roots = new List<ParticleSystem>();

        for (int i = 0; i < _allParticles.Length; i++)
        {
            ParticleSystem ps = _allParticles[i];
            var main = ps.main;

            // Stop Action = Destroy → ตัวในพูลหายทิ้ง / Disable → ลูกบางตัวโดนปิดค้าง
            if (main.stopAction == ParticleSystemStopAction.Destroy ||
                main.stopAction == ParticleSystemStopAction.Disable)
            {
                main.stopAction = ParticleSystemStopAction.None;
            }

            // จำความเร็วเดิมไว้ — ถ้าคูณทับทุกรอบ ตัวที่ถูกใช้ซ้ำจะเร็วขึ้นเรื่อยๆ
            _baseSimSpeed[i] = main.simulationSpeed;

            if (!HasParticleAncestor(ps.transform)) roots.Add(ps);
        }
        _roots = roots.ToArray();

        _graphs = GetComponentsInChildren<VisualEffect>(true);
        _baseGraphRate = new float[_graphs.Length];
        for (int i = 0; i < _graphs.Length; i++) _baseGraphRate[i] = _graphs[i].playRate;

        _effekseer = GetComponentsInChildren<EffekseerEmitter>(true);
        _baseEfkSpeed = new float[_effekseer.Length];
        _baseEfkLoop = new bool[_effekseer.Length];
        for (int i = 0; i < _effekseer.Length; i++)
        {
            _baseEfkSpeed[i] = _effekseer[i].speed;
            _baseEfkLoop[i] = _effekseer[i].isLooping;
            _effekseer[i].playOnStart = false;   // เราสั่งเล่นเอง ไม่งั้นรอบแรกเล่นซ้อน 2 ชุด
        }

        _trails = GetComponentsInChildren<TrailRenderer>(true);
        _spawnCallbacks = GetComponentsInChildren<IPooledObject>(true);
        _isSetup = true;
    }

    private bool HasParticleAncestor(Transform t)
    {
        if (t == transform) return false;

        for (Transform p = t.parent; p != null; p = p.parent)
        {
            if (p.TryGetComponent<ParticleSystem>(out _)) return true;
            if (p == transform) break;
        }
        return false;
    }

    /// <summary>ใช้กับตัวพา Effekseer ของพูล — เปลี่ยน effect ก่อนเล่น</summary>
    internal void SetEffekseerAsset(EffekseerEffectAsset asset)
    {
        for (int i = 0; i < _effekseer.Length; i++)
        {
            if (_effekseer[i] != null) _effekseer[i].effectAsset = asset;
        }
    }

    // ---------- เล่น ----------

    /// <param name="rotation">null = rotation เดิมของ prefab (ถ้าตามเป้าหมายแบบหมุนตาม = หมุนตามเป้าหมาย)</param>
    internal void Spawn(Vector3 position, Quaternion? rotation, Transform follow, bool followRotation,
                        VFXPlayOptions options, System.Action<VFXPlayer> beforePlay)
    {
        if (!_isSetup) Setup(transform.parent, 0f);

        if (_home != null && transform.parent != _home) transform.SetParent(_home, false);

        Quaternion rot;
        if (rotation.HasValue) rot = rotation.Value;
        else if (follow != null && followRotation) rot = follow.rotation * _baseRotation;
        else rot = _baseRotation;

        // ตั้งตำแหน่งก่อน SetActive — particle แบบ World Space / Effekseer จะเกิดถูกที่ตั้งแต่เฟรมแรก
        transform.SetPositionAndRotation(position, rot);
        transform.localScale = _baseScale;

        _follow = follow;
        _hasFollow = follow != null;
        _followRotation = followRotation;
        if (_hasFollow)
        {
            // จำตำแหน่ง/มุมเทียบกับเป้าหมายไว้ แล้วขยับตามทุกเฟรม = เหมือนเป็นลูกของมัน
            Quaternion inv = Quaternion.Inverse(follow.rotation);
            _followLocalPos = inv * (position - follow.position);
            _followLocalRot = inv * rot;
        }

        float speed = options.Speed > 0f ? options.Speed : 1f;
        float duration = options.Duration > 0f ? options.Duration : _maxLifetime;

        Version++;
        InUse = true;
        SpawnTime = Time.time;
        _nextCheckTime = SpawnTime + CheckInterval;
        _stopAt = duration > 0f ? SpawnTime + duration : float.PositiveInfinity;
        _releaseAt = float.PositiveInfinity;
        _fadeOutCap = options.FadeOut;
        _holdGrace = options.HoldGrace;
        _lastPing = SpawnTime;
        _stopRequested = false;
        _graphSeenAlive = false;

        gameObject.SetActive(true);

        for (int i = 0; i < _trails.Length; i++)
        {
            if (_trails[i] != null) _trails[i].Clear();
        }

        for (int i = 0; i < _effekseer.Length; i++)
        {
            if (_effekseer[i] != null) _effekseer[i].isLooping = _baseEfkLoop[i];
        }

        ApplySpeed(speed);

        beforePlay?.Invoke(this);

        for (int i = 0; i < _roots.Length; i++)
        {
            if (_roots[i] == null) continue;
            _roots[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _roots[i].Play(true);
        }

        for (int i = 0; i < _graphs.Length; i++)
        {
            VisualEffect g = _graphs[i];
            if (g == null) continue;

            g.Reinit();
            if (string.IsNullOrEmpty(g.initialEventName)) g.Play();
        }

        for (int i = 0; i < _effekseer.Length; i++)
        {
            EffekseerEmitter e = _effekseer[i];
            if (e == null || e.effectAsset == null) continue;

            e.Stop();
            e.Play();
        }

        for (int i = 0; i < _spawnCallbacks.Length; i++)
        {
            _spawnCallbacks[i].OnObjectSpawn();
        }
    }

    private void ApplySpeed(float speed)
    {
        for (int i = 0; i < _allParticles.Length; i++)
        {
            if (_allParticles[i] == null) continue;
            var main = _allParticles[i].main;
            main.simulationSpeed = _baseSimSpeed[i] * speed;
        }

        for (int i = 0; i < _graphs.Length; i++)
        {
            if (_graphs[i] != null) _graphs[i].playRate = _baseGraphRate[i] * speed;
        }

        for (int i = 0; i < _effekseer.Length; i++)
        {
            if (_effekseer[i] != null) _effekseer[i].speed = _baseEfkSpeed[i] * speed;
        }
    }

    // ---------- หยุด ----------

    /// <summary>ต่ออายุโหมดค้าง — เรียกทุกเฟรมที่ยังต้องการให้อยู่</summary>
    public void Ping()
    {
        if (InUse) _lastPing = Time.time;
    }

    /// <summary>หยุดพ่นใหม่ ตัวที่ลอยอยู่ค่อยๆ จางไป แล้วคืนพูลเอง</summary>
    public void Stop()
    {
        if (!InUse || _stopRequested) return;

        _stopRequested = true;

        if (!HasEffects || _fadeOutCap == 0f)
        {
            Release();
            return;
        }

        _releaseAt = _fadeOutCap > 0f ? Time.time + _fadeOutCap : float.PositiveInfinity;

        for (int i = 0; i < _roots.Length; i++)
        {
            if (_roots[i] != null) _roots[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        for (int i = 0; i < _graphs.Length; i++)
        {
            if (_graphs[i] != null) _graphs[i].Stop();
        }

        for (int i = 0; i < _effekseer.Length; i++)
        {
            if (_effekseer[i] == null) continue;
            _effekseer[i].isLooping = false;   // ไม่งั้นมันเล่นใหม่เองทันทีที่จบ
            _effekseer[i].StopRoot();
        }
    }

    /// <summary>ตัดจบทันที ไม่รอให้จาง</summary>
    public void StopImmediate()
    {
        Release();
    }

    private void Release()
    {
        if (!InUse) return;

        InUse = false;
        ParkInPool();
    }

    private void ParkInPool()
    {
        _hasFollow = false;
        _follow = null;

        for (int i = 0; i < _roots.Length; i++)
        {
            if (_roots[i] != null) _roots[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        for (int i = 0; i < _effekseer.Length; i++)
        {
            if (_effekseer[i] != null) _effekseer[i].Stop();
        }

        if (_home != null && transform.parent != _home) transform.SetParent(_home, false);
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // โดนปิดจากข้างนอก → ถือว่าคืนพูลแล้ว ไม่งั้นช่องนี้ค้างเป็น "กำลังใช้" ตลอดไป
        InUse = false;
        _hasFollow = false;
        _follow = null;
    }

    // ---------- อัปเดต ----------

    private void LateUpdate()
    {
        if (!InUse)
        {
            ParkInPool();
            return;
        }

        if (_hasFollow)
        {
            UpdateFollow();
            if (!InUse) return;
        }

        float now = Time.time;

        if (!_stopRequested)
        {
            if (now >= _stopAt) Stop();
            else if (_holdGrace > 0f && now - _lastPing > _holdGrace) Stop();

            if (!InUse) return;
        }
        else if (now >= _releaseAt)
        {
            Release();
            return;
        }

        if (now < _nextCheckTime) return;
        _nextCheckTime = now + CheckInterval;

        // โหมดค้าง: ตราบที่ยังมีคน Ping ห้ามคืนพูล ถึง particle จะหมดแล้วก็ตาม (เช่นโล่ที่เป็น mesh)
        if (_holdGrace > 0f && !_stopRequested) return;

        float age = now - SpawnTime;

        if (!HasEffects)
        {
            if (float.IsPositiveInfinity(_stopAt) && age >= NoEffectFallback) Release();
            return;
        }

        if (!IsAnyAlive(age)) Release();
    }

    private void UpdateFollow()
    {
        // เป้าหมายถูก Destroy หรือถูกปิด (เช่นผีโดนคืนเข้า ObjectPooler) → เลิกตาม แล้วจางที่จุดสุดท้าย
        if (_follow == null || !_follow.gameObject.activeInHierarchy)
        {
            _hasFollow = false;
            _follow = null;
            Stop();
            return;
        }

        Vector3 pos = _follow.position + _follow.rotation * _followLocalPos;
        if (_followRotation) transform.SetPositionAndRotation(pos, _follow.rotation * _followLocalRot);
        else transform.position = pos;
    }

    private bool IsAnyAlive(float age)
    {
        if (age < StartGrace) return true;

        for (int i = 0; i < _roots.Length; i++)
        {
            if (_roots[i] != null && _roots[i].IsAlive(true)) return true;
        }

        for (int i = 0; i < _effekseer.Length; i++)
        {
            if (_effekseer[i] != null && _effekseer[i].exists) return true;
        }

        if (_graphs.Length == 0) return false;
        if (age < GraphWarmup) return true;

        for (int i = 0; i < _graphs.Length; i++)
        {
            VisualEffect g = _graphs[i];
            if (g == null || g.aliveParticleCount <= 0) continue;

            // สั่งหยุดแล้ว + อยู่นอกจอ → ไม่มีใครเห็น คืนพูลได้เลย
            if (_stopRequested && g.culled) continue;

            _graphSeenAlive = true;
            return true;
        }

        return !_graphSeenAlive && !_stopRequested && age < GraphNoParticleTimeout;
    }
}

/// <summary>
/// ตัวอ้างอิงเอฟเฟกต์แบบปลอดภัย — พอเอฟเฟกต์จบแล้วถูกพูลเอาไปใช้ใหม่ handle เก่าจะหมดอายุทันที
/// สั่ง Stop() ทีหลังก็ไม่ไปดับเอฟเฟกต์ของคนอื่น
/// </summary>
public readonly struct VFXHandle
{
    private readonly VFXPlayer _player;
    private readonly int _version;

    internal VFXHandle(VFXPlayer player)
    {
        _player = player;
        _version = player != null ? player.Version : 0;
    }

    /// <summary>ยังเป็นเอฟเฟกต์รอบเดียวกับที่เราสั่งเล่นอยู่ไหม (รวมช่วงกำลังจาง)</summary>
    public bool IsValid => _player != null && _player.InUse && _player.Version == _version;

    /// <summary>ยังเล่นอยู่และยังไม่ได้สั่งหยุด (ช่วงกำลังจางจะเป็น false)</summary>
    public bool IsPlaying => IsValid && !_player.IsStopping;

    public Transform Transform => IsValid ? _player.transform : null;
    public VisualEffect Graph => IsValid ? _player.Graph : null;

    /// <summary>ต่ออายุโหมดค้าง</summary>
    public void Ping()
    {
        if (IsValid) _player.Ping();
    }

    public void Stop()
    {
        if (IsValid) _player.Stop();
    }

    public void StopImmediate()
    {
        if (IsValid) _player.StopImmediate();
    }
}