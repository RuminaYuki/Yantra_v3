using UnityEngine;

public class BulletSpawner : MonoBehaviour
{
    [SerializeField] GameObject _prefab;
    [SerializeField] VoidEventChannelSO _startShooting;

    public void spawnObject()
    {
        if (_prefab == null) return;

        GameObject newBullet = Instantiate(_prefab, transform.position, transform.rotation);
    }

    private void OnEnable()
    {
        if (_startShooting != null)
        {
            _startShooting.Raised += spawnObject;
        }
    }

    private void OnDisable()
    {
        if (_startShooting != null)
        {
            _startShooting.Raised -= spawnObject;
        }
    }

    private void OnDestroy()
    {
        if (_startShooting != null)
        {
            _startShooting.Raised -= spawnObject;
        }
    }

    public void SetSpawner(VoidEventChannelSO startShooting, GameObject prefab) 
    {
        _startShooting = startShooting;
        _prefab = prefab;
        _startShooting.Raised += spawnObject;
    } 
}
