using UnityEngine;
using UnityEngine.VFX;


/// <summary>
/// �����ҧ VFX ������ (�ѧ�������)
/// </summary>
[RequireComponent(typeof(HitscanShooter))]
public class HitscanVFX : MonoBehaviour
{
    [Tooltip("Path-following VFX")]
    //[SerializeField] string _poolTag = string.Empty;
    [SerializeField] GameObject prefabVFX;
    HitscanShooter hitscan;

    private static readonly int endPointID =
    Shader.PropertyToID("EndPoint");
    private static readonly int startPointID =
    Shader.PropertyToID("StartPoint");

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

    private void SpawnVFX(Vector3 _, Vector3 endPoint)
    {
        GameObject vfx = Instantiate(prefabVFX, transform.position, Quaternion.identity, transform);
        if (vfx.TryGetComponent(out VisualEffect effect))
        {
            effect.Reinit();
            effect.SetVector3(startPointID, vfx.transform.position);
            effect.SetVector3(endPointID, endPoint);
        }
    }

    /*private GameObject SpawnFromPool(Vector3 position, Quaternion rotation)
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
    }*/
}
