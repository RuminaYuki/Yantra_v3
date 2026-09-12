using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Pool
{
    public string tag;
    public GameObject prefab;
    public int size;

    // [ADD] ตัวช่วยก๊อปปี้ — สำคัญมาก ดูคำอธิบายใน Awake()
    public Pool Clone()
    {
        return new Pool { tag = this.tag, prefab = this.prefab, size = this.size };
    }
}

public class ObjectPooler : Singleton<ObjectPooler>
{
    protected override bool UseDontDestroyOnLoad => false;

    public ObjectPoolTableSO poolTable;
    public bool useScriptableObject = true;
    public List<Pool> pools;

    [Header("Options")]
    [Tooltip("สร้าง GameObject เปล่าเป็นแม่ของแต่ละพูล ไม่ให้ Hierarchy รก")]
    [SerializeField] private bool groupUnderContainer = true;

    private List<Pool> finalPools;
    public Dictionary<string, List<GameObject>> poolDictionary;
    private Dictionary<string, Transform> containers;

    protected override void Awake()
    {
        base.Awake();

        // ถ้าเป็นตัวซ้ำ base.Awake() สั่ง Destroy ไปแล้ว ห้ามสร้างพูลซ้อน
        if (Instance != this) return;

        finalPools = new List<Pool>();
        containers = new Dictionary<string, Transform>();

        if (pools != null)
        {
            foreach (var p in pools)
            {
                if (p == null || p.prefab == null) continue;
                finalPools.Add(p.Clone());
            }
        }

        if (useScriptableObject && poolTable != null && poolTable.poolTableList != null)
        {
            foreach (var listSO in poolTable.poolTableList)
            {
                if (listSO == null || listSO.pools == null) continue;

                foreach (var tablePool in listSO.pools)
                {
                    if (tablePool == null || tablePool.prefab == null) continue;

                    var existing = finalPools.Find(p => p.tag == tablePool.tag);

                    if (existing == null)
                        finalPools.Add(tablePool.Clone()); // ← Clone ตรงนี้ด้วย
                    else
                        existing.size += tablePool.size;   // ตอนนี้บวกใส่สำเนา ปลอดภัยแล้ว
                }
            }
        }

        poolDictionary = new Dictionary<string, List<GameObject>>();

        foreach (Pool pool in finalPools)
        {
            if (string.IsNullOrEmpty(pool.tag))
            {
                Debug.LogWarning("[ObjectPooler] เจอพูลที่ไม่มี tag ข้ามไป");
                continue;
            }

            if (poolDictionary.ContainsKey(pool.tag))
            {
                Debug.LogWarning($"[ObjectPooler] tag '{pool.tag}' ซ้ำ ข้ามตัวที่สอง");
                continue;
            }

            Transform parent = GetContainer(pool.tag);
            List<GameObject> objectPool = new List<GameObject>();

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, parent);
                obj.SetActive(false);
                objectPool.Add(obj);
            }

            poolDictionary.Add(pool.tag, objectPool);
        }
    }

    // [ADD] สร้าง/หาแม่ของพูลนั้นๆ
    private Transform GetContainer(string tag)
    {
        if (!groupUnderContainer) return null;

        if (containers.TryGetValue(tag, out Transform existing) && existing != null)
            return existing;

        GameObject container = new GameObject($"Pool_{tag}");
        container.transform.SetParent(transform);
        containers[tag] = container.transform;

        return container.transform;
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation, System.Action<GameObject> beforeSpawn = null)
    {
        if (poolDictionary == null || !poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning("[ObjectPooler] Pool with tag " + tag + " doesn't exist.");
            return null;
        }

        List<GameObject> pool = poolDictionary[tag];
        GameObject objectToSpawn = null;

        // [FIX] เดินถอยหลังเพื่อเก็บกวาด object ที่ถูก Destroy ทิ้งไปแล้วแต่ยังค้างใน list
        // (กัน MissingReferenceException ที่เคยทำให้ระบบเสียงตายทั้งเกม)
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] == null)
            {
                pool.RemoveAt(i);
                continue;
            }

            if (!pool[i].activeInHierarchy)
            {
                objectToSpawn = pool[i];
                break;   // [FIX] เดิมไม่มี break เลยสแกนทั้งลิสต์ทุกครั้งที่เล่นเสียง
            }
        }

        // ถ้าไม่มีตัวว่างเลย → ปั๊มเพิ่ม (overflow)
        if (objectToSpawn == null)
        {
            Pool poolConfig = finalPools.Find(p => p.tag == tag);

            if (poolConfig == null || poolConfig.prefab == null)
            {
                Debug.LogWarning("[ObjectPooler] No pool config found for tag: " + tag);
                return null;
            }

            objectToSpawn = Instantiate(poolConfig.prefab, GetContainer(tag));
            objectToSpawn.SetActive(false);
            pool.Add(objectToSpawn);
        }

        objectToSpawn.transform.SetPositionAndRotation(position, rotation);

        // เซ็ตค่าต่างๆ ก่อน object จะตื่น (สำคัญกับ SFXPlayer.myPoolTag)
        beforeSpawn?.Invoke(objectToSpawn);

        objectToSpawn.SetActive(true);

        if (objectToSpawn.TryGetComponent(out IPooledObject pooledObj))
            pooledObj.OnObjectSpawn();

        return objectToSpawn;
    }

    public void ReturnToPool(string tag, GameObject obj)
    {
        if (obj == null) return;

        if (poolDictionary != null && poolDictionary.TryGetValue(tag, out List<GameObject> poolList))
        {
            // หาว่าตอนแรกเราตั้ง Size ไว้เท่าไหร่ (เช่น ตั้งไว้ 10)
            Pool poolConfig = finalPools.Find(p => p.tag == tag);
            int originalSize = poolConfig != null ? poolConfig.size : 0;

            // ถ้าตอนนี้ในโกดังมีการสร้างโคลนนิ่งงอกมา "เกินโควต้า" ดั้งเดิม
            if (poolList.Count > originalSize)
            {
                poolList.Remove(obj); // ลบชื่อออกจากบัญชี
                Destroy(obj);         // ลบตัวเองทิ้งถาวร คืน RAM ให้ระบบ
                return;
            }

            // จับกลับเข้าแม่ เผื่อระหว่างทางมีใครไปเปลี่ยน parent (เช่น ติดตามมือผี)
            Transform parent = GetContainer(tag);
            if (parent != null && obj.transform.parent != parent)
                obj.transform.SetParent(parent, false);
        }

        // ถ้ายังไม่เกินโควต้า ก็ให้แค่ปิดตา (SetActive) ตามปกติ
        obj.SetActive(false);
    }
}