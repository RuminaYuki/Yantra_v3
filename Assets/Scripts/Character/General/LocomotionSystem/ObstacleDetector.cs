using UnityEngine;

public class ObstacleDetector : MonoBehaviour
{
    [SerializeField] private LayerMask _obstacleLayers;
    [SerializeField] private bool _isColliding;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        int hitLayer = hit.gameObject.layer;

        if ((_obstacleLayers.value & (1 << hitLayer)) != 0)
            _isColliding = true;
    }

    public bool ConsumeCollision()
    {
        if (!_isColliding)
            return false;

        _isColliding = false;
        return true;
    }
}
