using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] GameObject _prefab;

    public GameObject spawnObject()
    {
        if (_prefab == null) return null;
        GameObject newBullet = null;
        return newBullet = Instantiate(_prefab, transform.position, transform.rotation);
    }

    public void SetPrefab(GameObject prefab) => _prefab = prefab; 
}
