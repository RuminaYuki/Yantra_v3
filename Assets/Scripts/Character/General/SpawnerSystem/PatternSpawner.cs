using UnityEngine;

public class PatternSpawner : MonoBehaviour
{
    // What SpawnPattern spawns. Set here in the Inspector, or with SetParameter before SpawnPattern.
    [Tooltip("Spawned with Instantiate. Used when Pool Tag is empty.")]
    [SerializeField] private GameObject _prefab;
    [Tooltip("Spawned from ObjectPooler. Wins over Prefab when both are set.")]
    [SerializeField] private string _poolTag;
    [SerializeField] private SpawnerData _pattern;
    [Tooltip("Where the pattern is placed. Empty = this object.")]
    [SerializeField] private Transform _anchor;

    // For Animation Events. Its own name, since an event calls every component on the Animator's
    // GameObject that has a method by that name (ProjectileShooter has Execute too).
    public void SpawnPattern()
    {
        // Pool tag first; the prefab is the fallback.
        if (!string.IsNullOrEmpty(_poolTag))
            SpawnPrefab(_poolTag, _pattern, _anchor);
        else
            SpawnPrefab(_prefab, _pattern, _anchor);
    }

    // Each SetParameter clears the other source, so the last call decides what SpawnPattern spawns.
    public void SetParameter(GameObject prefab, SpawnerData spawnerData, Transform anchor)
    {
        _prefab = prefab;
        _poolTag = null;
        _pattern = spawnerData;
        _anchor = anchor;
    }

    public void SetParameter(string poolTag, SpawnerData spawnerData, Transform anchor)
    {
        _poolTag = poolTag;
        _prefab = null;
        _pattern = spawnerData;
        _anchor = anchor;
    }

    public GameObject[] SpawnPrefab(GameObject spawnerPrefab, SpawnerData spawnerData, Transform anchor = null)
    {
        if (spawnerPrefab == null)
        {
            Debug.LogWarning("[PatternSpawner] Nothing to spawn: no prefab.", this);
            return new GameObject[0];
        }

        int count = GetSpawnCount(spawnerData);
        var spawned = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            Pose point = GetSpawnPose(spawnerData, i, anchor);
            spawned[i] = Instantiate(spawnerPrefab, point.position, point.rotation);
        }

        return spawned;
    }

    public GameObject[] SpawnPrefab(string poolTag, SpawnerData spawnerData, Transform anchor = null)
    {
        if (string.IsNullOrEmpty(poolTag))
        {
            Debug.LogWarning("[PatternSpawner] Nothing to spawn: no pool tag.", this);
            return new GameObject[0];
        }

        if (ObjectPooler.Instance == null)
        {
            Debug.LogWarning("[PatternSpawner] No ObjectPooler in the scene.", this);
            return new GameObject[0];
        }

        int count = GetSpawnCount(spawnerData);
        var spawned = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            Pose point = GetSpawnPose(spawnerData, i, anchor);
            // Null when the tag doesn't exist - ObjectPooler already logs a warning.
            spawned[i] = ObjectPooler.Instance.SpawnFromPool(poolTag, point.position, point.rotation);
        }

        return spawned;
    }

    // No pattern = spawn a single one at the owner.
    private static int GetSpawnCount(SpawnerData spawnerData)
    {
        return spawnerData != null ? spawnerData.Count : 1;
    }

    private Pose GetSpawnPose(SpawnerData spawnerData, int index, Transform anchor)
    {
        if (spawnerData == null)
        {
            // Owner = the anchor if set, otherwise this object.
            Transform owner = anchor != null ? anchor : transform;
            return new Pose(owner.position, owner.rotation);
        }

        Pose point = spawnerData.GetSpawnPoint(index);
        if (anchor == null)
            return point;

        // From the pattern's root to the anchor. Rotation only, so a scaled anchor doesn't
        // stretch the pattern.
        Transform root = spawnerData.transform;
        Quaternion offsetRotation = Quaternion.Inverse(root.rotation);
        Vector3 offset = offsetRotation * (point.position - root.position);
        Quaternion turn = offsetRotation * point.rotation;

        return new Pose(anchor.position + anchor.rotation * offset, anchor.rotation * turn);
    }
}
