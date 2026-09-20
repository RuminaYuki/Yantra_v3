using UnityEngine;

public interface IKnockbackReceiver
{
    void ApplyKnockback(Vector3 direction, KnockbackParameters parameters);
}

[RequireComponent(typeof(CharacterController))]
public class CharacterControllerKnockbackReceiver : MonoBehaviour, IKnockbackReceiver
{
    [SerializeField] Vector3 playerVelocity;
    [SerializeField] float gravityValue;
    [SerializeField, Min(0f)] float knockbackDeceleration = 10f;

    private CharacterController characterController;
    private bool IsGrounded;
    private ILocomotionLock locomotionLock;

    private bool IsKnockback;
    private Vector3 knockbackVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        locomotionLock = GetComponent<ILocomotionLock>();
    }

    public void ApplyKnockback(Vector3 direction, KnockbackParameters parameters)
    {
        knockbackVelocity = direction.normalized * parameters.Power;
        IsKnockback = true;
        locomotionLock?.LockLocomotion(this);
    }

    private void LateUpdate()
    {
        IsGrounded = characterController.isGrounded;

        if (IsKnockback)
        {
            knockbackVelocity = Vector3.MoveTowards(
                knockbackVelocity,
                Vector3.zero,
                knockbackDeceleration * Time.deltaTime);
            characterController.Move(knockbackVelocity * Time.deltaTime);

            if (knockbackVelocity == Vector3.zero)
            {
                IsKnockback = false;
                locomotionLock?.UnlockLocomotion(this);
            }
        }

        if (IsGrounded)
        {
            // Keep the controller grounded without accumulating downward velocity.
            playerVelocity.y = 0f;
            UpdateGravity(gravityValue);
        }
        else
        {
            UpdateGravity(gravityValue * 2);
        }
    }

    private void UpdateGravity(float gravityValue)
    {
        playerVelocity.y += gravityValue * Time.deltaTime;
        characterController.Move(playerVelocity * Time.deltaTime);
    }

}
