using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]   // ต้องพร้อมก่อน SoundManager (ซึ่งอยู่ที่ 0)
public class SoundPooler : MonoBehaviour
{
    public static SoundPooler Instance { get; private set; }

    [Header("Prefab")]
    [Tooltip("prefab ที่มี AudioSource + SFXPlayer\nเว้นว่างได้ จะสร้างให้เองตอนรัน")]
    [SerializeField] private GameObject _playerPrefab;

    [Header("Pool Size")]
    [Tooltip("จำนวนลำโพงที่เตรียมไว้ตั้งแต่เริ่ม")]
    [SerializeField] private int _initialSize = 24;

    [Tooltip("เพดานสูงสุด — Unity เล่นได้จริงแค่ 32 เสียงพร้อมกัน\n" +
             "เกินกว่านี้ไม่มีประโยชน์ มีแต่กิน memory เปล่า")]
    [SerializeField] private int _maxSize = 40;

    [Header("Options")]
    [Tooltip("อยู่ข้าม scene — ติ๊กถ้าอยากให้เสียงต่อเนื่องตอนเปลี่ยนฉาก\n" +
             "ปิดไว้จะทำงานเหมือน ObjectPooler เดิม คือแต่ละ scene มีของตัวเอง")]
    [SerializeField] private bool _persistAcrossScenes = false;

    [Tooltip("ขึ้น log ตอนพูลเต็มจนต้องปั๊มเพิ่ม — ใช้จูนขนาดพูลให้พอดี")]
    [SerializeField] private bool _logOverflow = true;

    private readonly List<SFXPlayer> _pool = new List<SFXPlayer>();
    private Transform _container;

    /// <summary>จำนวนลำโพงที่กำลังเล่นอยู่ — ใช้ debug ว่าพูลพอไหม</summary>
    public int ActiveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null && _pool[i].gameObject.activeInHierarchy) count++;
            }
            return count;
        }
    }

    public int TotalCount => _pool.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_persistAcrossScenes) DontDestroyOnLoad(gameObject);

        BuildPool();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // static ไม่รีเซ็ตเองถ้าปิด Domain Reload ใน Editor
        // ไม่เคลียร์ = กด Play รอบสองจะจำตัวเก่าที่ตายไปแล้ว
        Instance = null;
    }

    // ---------- สร้างพูล ----------

    private void BuildPool()
    {
        _container = new GameObject("SFX_Players").transform;
        _container.SetParent(transform, false);

        for (int i = 0; i < _initialSize; i++) CreatePlayer();
    }

    private SFXPlayer CreatePlayer()
    {
        GameObject obj;

        if (_playerPrefab != null)
        {
            obj = Instantiate(_playerPrefab, _container);
        }
        else
        {
            // ไม่มี prefab ก็สร้างเองได้ — กันเคสลืม setup แล้วเสียงเงียบทั้งเกม
            obj = new GameObject("SFXPlayer");
            obj.transform.SetParent(_container, false);
            obj.AddComponent<AudioSource>();
            obj.AddComponent<SFXPlayer>();
        }

        obj.SetActive(false);

        if (!obj.TryGetComponent(out SFXPlayer player))
        {
            Debug.LogError("[SoundPooler] prefab ไม่มี SFXPlayer", this);
            Destroy(obj);
            return null;
        }

        player.SetOwnerPool(this);
        _pool.Add(player);
        return player;
    }

    // ---------- ขอลำโพง ----------

 
    public SFXPlayer Get(Vector3 position)
    {
        SFXPlayer found = null;

        // เดินถอยหลังเพื่อเก็บกวาดตัวที่ถูก Destroy ไปแล้วได้อย่างปลอดภัย
        for (int i = _pool.Count - 1; i >= 0; i--)
        {
            if (_pool[i] == null)
            {
                _pool.RemoveAt(i);
                continue;
            }

            if (!_pool[i].gameObject.activeInHierarchy)
            {
                found = _pool[i];
                break;
            }
        }

        if (found == null)
        {
            if (_pool.Count >= _maxSize)
            {
                if (_logOverflow)
                    Debug.LogWarning($"[SoundPooler] พูลเต็มที่ {_maxSize} ตัว — เสียงนี้ถูกข้าม", this);
                return null;
            }

            if (_logOverflow)
                Debug.Log($"[SoundPooler] พูลเต็ม ปั๊มเพิ่มเป็น {_pool.Count + 1} — " +
                          "ถ้าขึ้นบ่อยให้เพิ่ม Initial Size", this);

            found = CreatePlayer();
            if (found == null) return null;
        }

        found.transform.position = position;
        found.gameObject.SetActive(true);
        return found;
    }

    /// <summary>คืนลำโพงเข้าพูล — SFXPlayer เรียกเองตอนเล่นจบ</summary>
    public void Return(SFXPlayer player)
    {
        if (player == null) return;

        // เผื่อระหว่างทางมีใครเปลี่ยน parent (เช่นเสียงติดตามผี)
        if (_container != null && player.transform.parent != _container)
            player.transform.SetParent(_container, false);

        player.gameObject.SetActive(false);
    }

    /// <summary>หยุดเสียงทั้งหมดทันที — ใช้ตอนเปลี่ยน scene หรือรีเซ็ต</summary>
    public void StopAll()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i] == null) continue;
            if (_pool[i].gameObject.activeInHierarchy) _pool[i].Stop();
        }
    }
}