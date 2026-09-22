using UnityEngine;
using UnityEngine.VFX;


/// <summary>
/// ใช้สร้าง VFX ตามเส้น (ยังไม่เสร็จ)
/// </summary>
[RequireComponent(typeof(HitscanShooter))]
public class HitscanVFX : MonoBehaviour
{
    [Tooltip("Path-following VFX")]
    [SerializeField] string _poolTag = string.Empty;
    HitscanShooter hitscan;

    private void Awake()
    {
        hitscan = GetComponent<HitscanShooter>();
    }

    private void OnEnable()
    {
        hitscan.ShootCompleted += SpawnVFX;
    }

    private void OnDisable()
    {
        hitscan.ShootCompleted -= SpawnVFX;
    }

    private void SpawnVFX(Vector3 origin, Vector3 endPoint)
    {
        GameObject vfx = SpawnFromPool(transform.position, transform.rotation);
        if (vfx != null)
        {
            VisualEffect effect = vfx.GetComponent<VisualEffect>();
            //To do add position to VFX
        }
    }

    private GameObject SpawnFromPool(Vector3 position, Quaternion rotation)
    {
        GameObject result = null;

        ObjectPooler.Instance.SpawnFromPool(_poolTag, position, rotation, obj =>
        {
            if (obj.TryGetComponent(out GameObject p))
            {
                result = p;
            }
        });

        return result;
    }
}
