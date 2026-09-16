using UnityEngine;

public class BulletSpawner : MonoBehaviour
{
    private VoidEventChannelSO _startShooting;

    public GameObject spawnObject(GameObject prefab)
    {
        if (prefab == null) return null;

        GameObject newBullet = Instantiate(prefab, transform.position, transform.rotation);
        return newBullet;
    }

    public void SetEventChannel(VoidEventChannelSO startShooting) => _startShooting = startShooting;
}
