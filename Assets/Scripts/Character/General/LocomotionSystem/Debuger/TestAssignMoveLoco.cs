using UnityEngine;

public class TestAssignMoveLoco : MonoBehaviour
{
    public BaseLocomotion baseLocomotion;
    public Vector3 vector3;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        baseLocomotion.SetMovementDirection(vector3);
    }
}
