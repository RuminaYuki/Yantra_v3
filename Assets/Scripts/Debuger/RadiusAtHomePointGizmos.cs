using UnityEngine;

public class RadiusAtHomePointGizmos : MonoBehaviour
{
    public SetRandomPositionNavRadiusActionSO _randomPositionRadius;
    public WaypointPath _rootPosition;
    public bool showGizmos = true;
    void OnDrawGizmosSelected()
    {
        if(!showGizmos)
        return;

        if (_rootPosition == null || _rootPosition.PathRoot == null || _randomPositionRadius == null)
        return;

        Gizmos.color = Color.orange;
        Gizmos.DrawWireSphere(_rootPosition.PathRoot.position, _randomPositionRadius.Radius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_rootPosition.PathRoot.position, _randomPositionRadius.MinDistance);
    }
}
