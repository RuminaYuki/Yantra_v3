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
        // controller ถูกปิด (เช่น state ตาย / ถูกจับ) — Move ไม่ได้ และไม่ควรสะสมแรงโน้มถ่วงไว้
        if (!characterController.enabled)
        {
            CancelKnockback();
            playerVelocity.y = 0f;
            return;
        }

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

    // ปลดเฉพาะล็อกของตัวนี้ — ล็อกของระบบอื่น (เช่น ท่าคู่) ยังอยู่
    private void CancelKnockback()
    {
        if (!IsKnockback)
            return;

        IsKnockback = false;
        knockbackVelocity = Vector3.zero;
        locomotionLock?.UnlockLocomotion(this);
    }

}
