using UnityEngine;

public class LinearProjectile : BaseProjectileMovement
{
    protected override void Update()
    {
        MoveForward();
    }

    private void MoveForward()
    {
        transform.position +=
            transform.forward * moveSpeed * Time.deltaTime;
    }
}
