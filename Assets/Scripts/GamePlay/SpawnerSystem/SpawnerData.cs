using UnityEngine;

public class SpawnerData : MonoBehaviour
{
    [SerializeField] private Transform[] _transforms;
    public int Count => _transforms.Length;
    public Pose GetSpawnPoint(int index)
    {
        return new Pose(_transforms[index].position,_transforms[index].rotation);
    }
}
