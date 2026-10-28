using UnityEngine;

public class PatternSpawner : MonoBehaviour
{
    // What SpawnPattern spawns. Set here in the Inspector, or with SetParameter before SpawnPattern.
    [SerializeField] private GameObject _prefab;
    [SerializeField] private SpawnerData _pattern;
    [Tooltip("Where the pattern is placed. Empty = this object.")]
    [SerializeField] private Transform _anchor;

    // For Animation Events. Its own name, since an event calls every component on the Animator's
    // GameObject that has a method by that name (ProjectileShooter has Execute too).
    public void SpawnPattern()
    {
        SpawnPrefab(_prefab,_pattern,_anchor);
    }
    public void SetParameter(GameObject prefab, SpawnerData spawnerData, Transform anchor)
    {
        _prefab = prefab;
        _pattern = spawnerData;
        _anchor = anchor;
    }
    public GameObject[] SpawnPrefab(GameObject spawnerPrefab, SpawnerData spawnerData, Transform anchor = null)
    {
        if (spawnerPrefab == null || spawnerData == null)
        {
            Debug.LogWarning("[PatternSpawner] Nothing to spawn: no prefab or no pattern.", this);
            return new GameObject[0];
        }

        Transform root = spawnerData.transform;
        var spawned = new GameObject[spawnerData.Count];

        for (int i = 0; i < spawnerData.Count; i++)
        {
            Pose point = spawnerData.GetSpawnPoint(i);

            if (anchor != null)
            {
                // From the pattern's root to the anchor. Rotation only, so a scaled anchor doesn't
                // stretch the pattern.
                Quaternion offsetRotation = Quaternion.Inverse(root.rotation);
                Vector3 offset = offsetRotation * (point.position - root.position);
                Quaternion turn = offsetRotation * point.rotation;

                point = new Pose(anchor.position + anchor.rotation * offset, anchor.rotation * turn);
            }

            spawned[i] = Instantiate(spawnerPrefab, point.position, point.rotation);
        }

        return spawned;
    }
}
