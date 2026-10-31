using UnityEngine;

public class LinearProjectile : BaseProjectileMovement
{
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        MoveForward();
    }

    private void MoveForward()
    {
        transform.position +=
            transform.forward * moveSpeed * Time.deltaTime;
    }
}
