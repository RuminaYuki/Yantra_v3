using UnityEngine;

public class HitscanShooterDebug : MonoBehaviour
{
    public HitscanShooter HitscanShooter;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            HitscanShooter.Fire();
        }
    }
}
