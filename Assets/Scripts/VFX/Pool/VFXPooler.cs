using System.Collections.Generic;
using UnityEngine;
using Effekseer;

/// <summary>
/// พูลเอฟเฟกต์ — แยกพูลตาม prefab อัตโนมัติ ไม่ต้องลงทะเบียน
/// รองรับ Particle System / VFX Graph / prefab ที่มี Effekseer Emitter และ Effekseer Effect Asset ตรงๆ
/// ไม่ได้วางไว้ใน scene ก็ได้ — VFXPlayback จะสร้างให้เองตอนเล่นครั้งแรก
/// </summary>
[DefaultExecutionOrder(-200)]
public class VFXPooler : MonoBehaviour
{
    public static VFXPooler Instance { get; private set; }

    public enum FullPolicy { RecycleOldest, Skip }

    [System.Serializable]
    public class Entry
    {
        [Tooltip("ชื่อไว้เรียกแบบ string — เว้นว่างได้")]
        public string id;

        public GameObject prefab;

        [Tooltip("จำนวนที่สร้างรอไว้ตั้งแต่เริ่ม scene")]
        [Min(0)] public int initialSize = 5;

        [Tooltip("เพดาน — ชนแล้วจะทำตาม When Full")]
        [Min(1)] public int maxSize = 20;

        [Tooltip("ใช้เมื่อตอนเล่นไม่ได้กำหนด Duration มา — 0 = ไม่จำกัด")]
        [Min(0f)] public float maxLifetime = 0f;
    }

    [Header("Preload (ไม่ใส่ก็ได้)")]
    [Tooltip("เอฟเฟกต์ที่ใช้บ่อย — สร้างรอไว้ตั้งแต่ต้น จะได้ไม่กระตุกตอนเล่นครั้งแรก")]
    [SerializeField] private List<Entry> _entries = new List<Entry>();

    [Header("Auto Pool")]
    [Tooltip("prefab ที่ไม่อยู่ใน Entries จะสร้างพูลให้อัตโนมัติตอนเล่นครั้งแรก")]
    [SerializeField, Min(1)] private int _defaultInitialSize = 2;
    [SerializeField, Min(1)] private int _defaultMaxSize = 15;

    [Header("Effekseer (ตัวพาเอฟเฟกต์จาก Effect Asset)")]
    [SerializeField, Min(1)] private int _effekseerInitialSize = 8;
    [SerializeField, Min(1)] private int _effekseerMaxSize = 40;

    [Header("Options")]
    [Tooltip("พูลเต็มแล้วทำไง\nRecycleOldest = ตัดตัวที่เล่นนานสุดมาใช้\nSkip = ไม่เล่นอันใหม่")]
    [SerializeField] private FullPolicy _whenFull = FullPolicy.RecycleOldest;

    [Tooltip("ขึ้น log ตอนพูลไม่พอจนต้องปั๊มเพิ่ม — ใช้จูนขนาดพูล")]
    [SerializeField] private bool _logOverflow = false;

    private sealed class PrefabPool
    {
        public GameObject prefab;
        public string id;
        public int maxSize;
        public float maxLifetime;
        public Transform container;
        public bool warnedFull;
        public readonly List<VFXPlayer> players = new List<VFXPlayer>();
    }

    private readonly Dictionary<GameObject, PrefabPool> _byPrefab = new Dictionary<GameObject, PrefabPool>();
    private readonly Dictionary<string, PrefabPool> _byId = new Dictionary<string, PrefabPool>();
    private readonly List<GameObject> _deadKeys = new List<GameObject>();
    private Transform _staging;   // ที่พักที่ปิดไว้ตอน Instantiate — สคริปต์บน prefab จะได้ไม่ตื่นก่อนเวลา
    private PrefabPool _effekseerPool;

    private static bool _quitting;

    public int ActiveCount
    {
        get
        {
            int count = 0;
            foreach (var pool in _byPrefab.Values)
            {
                for (int i = 0; i < pool.players.Count; i++)
                {
                    if (pool.players[i] != null && pool.players[i].InUse) count++;
                }
            }
            return count;
        }
    }

    /// <summary>คืนตัวที่อยู่ใน scene หรือสร้างให้ใหม่ถ้ายังไม่มี (null ตอนกำลังปิดเกม)</summary>
    public static VFXPooler GetOrCreate()
    {
        if (Instance != null) return Instance;
        if (_quitting || !Application.isPlaying) return null;

        new GameObject("VFXPooler (auto)").AddComponent<VFXPooler>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        var staging = new GameObject("VFX_Staging");
        staging.SetActive(false);
        staging.transform.SetParent(transform, false);
        _staging = staging.transform;

        foreach (var e in _entries)
        {
            if (e == null || e.prefab == null || _byPrefab.ContainsKey(e.prefab)) continue;
            CreatePool(e.prefab, e.id, e.initialSize, e.maxSize, e.maxLifetime);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // ปิด Domain Reload ไว้ static ไม่รีเซ็ตเอง
        Instance = null;
        _quitting = false;
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
    }

    // ตอนออกจาก Play mode ห้ามสร้าง GameObject ใหม่ ไม่งั้น Unity ฟ้อง "objects were not cleaned up"
    private static void OnQuitting() => _quitting = true;

    // ---------- เล่น ----------

    public VFXHandle Play(GameObject prefab, Vector3 position, System.Action<VFXPlayer> beforePlay = null)
    {
        return SpawnInternal(GetPool(prefab), null, position, null, null, false, VFXPlayOptions.Default, beforePlay);
    }

    public VFXHandle Play(GameObject prefab, Vector3 position, Quaternion rotation, System.Action<VFXPlayer> beforePlay = null)
    {
        return SpawnInternal(GetPool(prefab), null, position, rotation, null, false, VFXPlayOptions.Default, beforePlay);
    }

    /// <param name="follow">null = ค้างอยู่ที่เดิม / ใส่ = ตามไปเหมือนเป็นลูกของมัน</param>
    public VFXHandle Play(GameObject prefab, Vector3 position, Quaternion rotation, Transform follow,
                          VFXPlayOptions options, System.Action<VFXPlayer> beforePlay = null)
    {
        return SpawnInternal(GetPool(prefab), null, position, rotation, follow, true, options, beforePlay);
    }

    public VFXHandle Play(string id, Vector3 position, System.Action<VFXPlayer> beforePlay = null)
    {
        return SpawnInternal(GetPool(id), null, position, null, null, false, VFXPlayOptions.Default, beforePlay);
    }

    public VFXHandle Play(string id, Vector3 position, Quaternion rotation, System.Action<VFXPlayer> beforePlay = null)
    {
        return SpawnInternal(GetPool(id), null, position, rotation, null, false, VFXPlayOptions.Default, beforePlay);
    }

    /// <param name="offset">ระยะห่างจากเป้าหมาย คิดตามทิศที่เป้าหมายหันอยู่</param>
    public VFXHandle PlayAttached(GameObject prefab, Transform target, Vector3 offset = default,
                                  bool followRotation = true, System.Action<VFXPlayer> beforePlay = null)
    {
        if (target == null) return default;
        Vector3 pos = target.position + target.rotation * offset;
        return SpawnInternal(GetPool(prefab), null, pos, null, target, followRotation, VFXPlayOptions.Default, beforePlay);
    }

    /// <summary>เล่น Effekseer Effect Asset ผ่านตัวพาในพูล (ไม่ต้องสร้าง GameObject ใหม่ทุกครั้ง)</summary>
    public VFXHandle PlayEffekseer(EffekseerEffectAsset asset, Vector3 position, Quaternion rotation,
                                   Transform follow, VFXPlayOptions options)
    {
        if (asset == null) return default;
        return SpawnInternal(GetEffekseerPool(), asset, position, rotation, follow, true, options, null);
    }

    public void StopAll(bool immediate = true)
    {
        foreach (var pool in _byPrefab.Values)
        {
            for (int i = 0; i < pool.players.Count; i++)
            {
                VFXPlayer p = pool.players[i];
                if (p == null || !p.InUse) continue;

                if (immediate) p.StopImmediate();
                else p.Stop();
            }
        }
    }

    private VFXHandle SpawnInternal(PrefabPool pool, EffekseerEffectAsset effekseerAsset, Vector3 position,
                                    Quaternion? rotation, Transform follow, bool followRotation,
                                    VFXPlayOptions options, System.Action<VFXPlayer> beforePlay)
    {
        if (pool == null) return default;

        VFXPlayer player = Acquire(pool);
        if (player == null) return default;

        if (effekseerAsset != null) player.SetEffekseerAsset(effekseerAsset);

        player.Spawn(position, rotation, follow, followRotation, options, beforePlay);
        return new VFXHandle(player);
    }

    // ---------- พูล ----------

    private PrefabPool GetPool(GameObject prefab)
    {
        if (prefab == null) return null;
        if (_byPrefab.TryGetValue(prefab, out PrefabPool pool)) return pool;

        if (_logOverflow)
            Debug.Log($"[VFXPooler] '{prefab.name}' ไม่อยู่ใน Entries — สร้างพูลอัตโนมัติ", this);

        return CreatePool(prefab, null, _defaultInitialSize, _defaultMaxSize, 0f);
    }

    private PrefabPool GetPool(string id)
    {
        if (!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out PrefabPool pool)) return pool;

        Debug.LogWarning($"[VFXPooler] ไม่มีเอฟเฟกต์ id '{id}' ใน Entries", this);
        return null;
    }

    private PrefabPool GetEffekseerPool()
    {
        if (_effekseerPool != null) return _effekseerPool;

        var template = new GameObject("EffekseerCarrier");
        template.transform.SetParent(_staging, false);
        template.AddComponent<EffekseerEmitter>();

        _effekseerPool = CreatePool(template, null, _effekseerInitialSize, _effekseerMaxSize, 0f);
        return _effekseerPool;
    }

    private PrefabPool CreatePool(GameObject prefab, string id, int initialSize, int maxSize, float maxLifetime)
    {
        SweepDeadPools();

        var container = new GameObject($"VFX_{prefab.name}").transform;
        container.SetParent(transform, false);

        var pool = new PrefabPool
        {
            prefab = prefab,
            id = id,
            maxSize = Mathf.Max(1, maxSize),
            maxLifetime = maxLifetime,
            container = container,
        };

        _byPrefab.Add(prefab, pool);

        if (!string.IsNullOrEmpty(id) && !_byId.ContainsKey(id)) _byId.Add(id, pool);

        int count = Mathf.Min(initialSize, pool.maxSize);
        for (int i = 0; i < count; i++) CreatePlayer(pool);

        return pool;
    }

    // ต้นแบบที่เป็น object ในฉาก (เช่นลูกของผี) ถูกลบไปแล้ว → ทิ้งพูลของมันด้วย ไม่ให้ค้างกิน memory
    private void SweepDeadPools()
    {
        _deadKeys.Clear();
        foreach (var kv in _byPrefab)
        {
            if (kv.Key == null) _deadKeys.Add(kv.Key);
        }

        foreach (GameObject key in _deadKeys)
        {
            PrefabPool pool = _byPrefab[key];
            _byPrefab.Remove(key);

            if (!string.IsNullOrEmpty(pool.id) && _byId.TryGetValue(pool.id, out PrefabPool byId) && byId == pool)
                _byId.Remove(pool.id);

            if (pool.container != null) Destroy(pool.container.gameObject);
        }
    }

    private VFXPlayer CreatePlayer(PrefabPool pool)
    {
        GameObject obj = Instantiate(pool.prefab, _staging);
        obj.name = pool.prefab.name;
        obj.SetActive(false);
        obj.transform.SetParent(pool.container, false);

        if (!obj.TryGetComponent(out VFXPlayer player))
            player = obj.AddComponent<VFXPlayer>();

        player.Setup(pool.container, pool.maxLifetime);
        pool.players.Add(player);
        return player;
    }

    private VFXPlayer Acquire(PrefabPool pool)
    {
        List<VFXPlayer> list = pool.players;
        VFXPlayer oldest = null;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            VFXPlayer p = list[i];
            if (p == null)
            {
                list.RemoveAt(i);
                continue;
            }

            if (!p.InUse) return p;

            if (oldest == null || p.SpawnTime < oldest.SpawnTime) oldest = p;
        }

        if (list.Count < pool.maxSize)
        {
            if (_logOverflow)
                Debug.Log($"[VFXPooler] '{pool.prefab.name}' ไม่พอ ปั๊มเพิ่มเป็น {list.Count + 1}", this);

            return CreatePlayer(pool);
        }

        if (_whenFull == FullPolicy.RecycleOldest && oldest != null)
        {
            oldest.StopImmediate();
            return oldest;
        }

        if (!pool.warnedFull)
        {
            pool.warnedFull = true;
            Debug.LogWarning($"[VFXPooler] '{pool.prefab.name}' เต็มที่ {pool.maxSize} ตัว — เอฟเฟกต์ถูกข้าม", this);
        }
        return null;
    }
}