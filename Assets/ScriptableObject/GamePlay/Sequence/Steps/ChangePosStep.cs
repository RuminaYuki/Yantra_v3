using UnityEngine;

public class ChangePosStep : MonoBehaviour
{
    [SerializeField] GameObject targetGameObject;
    [SerializeField] Transform targetTransform;

    public void change()
    {
        if (targetTransform != null)
        {
            targetGameObject.transform.position = targetTransform.position;
            targetGameObject.transform.rotation = targetTransform.rotation;
        }
    }
}
