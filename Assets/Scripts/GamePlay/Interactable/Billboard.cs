using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool lockYAxis = true;
    [SerializeField] private bool reverseZ = false;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        Vector3 direction = (targetCamera.transform.position - transform.position);

        if (lockYAxis)
            direction.y = 0f;

        if (reverseZ)
            direction = -direction;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}