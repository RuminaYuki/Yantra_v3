using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterController),typeof(Animator))]
public class BaseLocomotion : MonoBehaviour,ILocomotionLock,ILocomotionMoveMode
{
    [Header("Velocity Logic")]
    [SerializeField] protected float _currentspeed = 5;
    [Header("Locomotion")]
    [SerializeField] private float _dampTime = 0.25f;
    [SerializeField] private float _multiply = 1f;
    [SerializeField] private string _nameParameterMoveZ = "VelocityZ";
    [SerializeField] private string _nameParameterMoveX = "VelocityX";

    [Header("Movement")]
    [SerializeField] protected LocomotionMoveMode _moveMode = LocomotionMoveMode.RootMotion;

    [Header("Rotation")]
    [SerializeField] private float _rotateSpeed = 1f;

    [Header("Gravity")]
    [FormerlySerializedAs("gravityMultiplier")]
    [SerializeField] private float _gravityMultiplier = 1f;

#if UNITY_EDITOR
    [Header("Runtime Debug")]
    [SerializeField] private float _debugMoveMultiply;
    [SerializeField] private float _debugRotateSpeed;
#endif

    protected CharacterController CharacterController { get; private set; }
    protected Animator Animator { get; private set; }
    protected LocomotionAnim LocomotionAnim { get; private set; }
    protected RotationTransform Rotation { get; private set; }
    protected GravityCharacterCon Gravity { get; private set; }

    private readonly HashSet<object> _movementLockOwners = new();
    private readonly HashSet<object> _moveAnimationResetOwners = new();

    private Vector3 _movementDirection;
    private Vector3 _facingDirection;

    // ตัวคูณความเร็วเดิน (debug / buff) — 1 = ปกติ, state machine ไม่ได้ตั้งค่านี้ เลยไม่ถูกเขียนทับ
    public float SpeedMultiplier { get; set; } = 1f;

    public bool IsMovementLocked => _movementLockOwners.Count > 0;
    public bool ShouldResetMoveAnimation => _moveAnimationResetOwners.Count > 0;

    protected virtual void Awake()
    {
        Animator = GetComponent<Animator>();
        CharacterController = GetComponent<CharacterController>();
        LocomotionAnim = new LocomotionAnim(Animator,_dampTime,_multiply);
        LocomotionAnim.SetMoveParameter(_nameParameterMoveX,_nameParameterMoveZ);
        Rotation = new RotationTransform(transform,_rotateSpeed);
        Gravity = new GravityCharacterCon(CharacterController,_gravityMultiplier);
    }

    protected virtual void Update()
    {
        // ต้องอยู่ก่อนเช็ค Lock — ล็อกอยู่ก็ยังต้องตก
        UpdateVelocityMovement();

        if (IsMovementLocked)
        {
            ResetLockedMovement();
            return;
        }

        UpdateMoveAnimation();
        UpdateDebugValues();
    }

    protected virtual void FixedUpdate()
    {
        // Full = คลิปคุมการหมุนแล้ว / External = ระบบอื่นคุมเอง — โค้ดห้ามหมุนทับ
        if (_moveMode == LocomotionMoveMode.FullRootMotion
         || _moveMode == LocomotionMoveMode.External)
            return;

        Vector3 direction = IsMovementLocked ? Vector3.zero : _facingDirection;
        Rotation.Rotate(direction);
    }

    protected virtual void OnAnimatorMove()
    {
        bool useRootPosition = _moveMode == LocomotionMoveMode.RootMotion
                            || _moveMode == LocomotionMoveMode.FullRootMotion;
        if (!useRootPosition)
            return;

        if (!CanMoveController())
            return;

        // คูณเฉพาะแนวนอน — แนวตั้งของคลิป (กระโดด / ลง) คงเดิม
        Vector3 delta = Animator.deltaPosition;
        delta.x *= SpeedMultiplier;
        delta.z *= SpeedMultiplier;
        CharacterController.Move(delta + Gravity.Gravity());

        if (_moveMode == LocomotionMoveMode.FullRootMotion)
            transform.rotation *= Animator.deltaRotation;
    }

    public void SetMovementDirection(Vector3 direction)
    {
        direction.y = 0f;
        _movementDirection = Vector3.ClampMagnitude(direction,1f);
    }

    public void SetFacingDirection(Vector3 direction)
    {
        direction.y = 0f;
        _facingDirection = direction.normalized;
    }

    public void ClearMovementDirection() => _movementDirection = Vector3.zero;
    public void ClearFacingDirection() => _facingDirection = Vector3.zero;

    public void LockLocomotion(object owner,bool resetMoveAnimation = true)
    {
        if (owner == null)
            return;

        _movementLockOwners.Add(owner);
        if (resetMoveAnimation)
            _moveAnimationResetOwners.Add(owner);
    }

    public void UnlockLocomotion(object owner)
    {
        if (owner == null)
            return;

        _movementLockOwners.Remove(owner);
        _moveAnimationResetOwners.Remove(owner);
    }

    #region API
    public LocomotionMoveMode MoveMode
    {
        get => _moveMode;
        set => _moveMode = value;
    }
    public float GetMoveMultiply() => LocomotionAnim.Multiply;
    public void SetMoveMultiply(float multiply) => LocomotionAnim.Multiply = multiply;
    public float GetRotateSmoothSpeed() => Rotation.Speed;
    public void SetRotateSmoothSpeed(float value) => Rotation.Speed = value;
    public float GetGravityMultiplier() => Gravity._GravityMultiplier;
    public void SetGravityMultiplier(float value) => Gravity._GravityMultiplier = value;
    public float CurrentSpeed
    {
        get => _currentspeed;
        set
        {
            if(value < 0)
            {
                Debug.LogWarning("currentspeed value cant below zero");
                return;
            }
            _currentspeed = value;
        }
    }

    // Velocity = เดินด้วยโค้ด — _movementDirection ถูก clamp ไว้แล้วใน SetMovementDirection
    private void UpdateVelocityMovement()
    {
        if (_moveMode != LocomotionMoveMode.Velocity)
            return;

        if (!CanMoveController())
            return;

        Vector3 movement = IsMovementLocked
            ? Vector3.zero
            : _movementDirection * _currentspeed * SpeedMultiplier * Time.deltaTime;
        CharacterController.Move(movement + Gravity.Gravity());
    }

    // controller ถูกปิด (เช่น state ตาย / ถูกจับ / วาร์ป) — Move ไม่ได้ และไม่ควรสะสมแรงตกไว้
    // ไม่งั้นพอเปิดกลับมาจะพุ่งลงพื้นแรงผิดปกติ
    private bool CanMoveController()
    {
        if (CharacterController.enabled)
            return true;

        Gravity._velocityY = 0f;
        return false;
    }

    private void ResetLockedMovement()
    {
        if (ShouldResetMoveAnimation)
            LocomotionAnim.SetMove(0f,0f);
    }

    private void UpdateMoveAnimation()
    {
        Vector3 localDirection = transform.InverseTransformDirection(_movementDirection);
        LocomotionAnim.SetMove(localDirection.x,localDirection.z);
    }

    private void UpdateDebugValues()
    {
#if UNITY_EDITOR
        _debugMoveMultiply = LocomotionAnim.Multiply;
        _debugRotateSpeed = Rotation.Speed;
#endif
    }
    #endregion
}
