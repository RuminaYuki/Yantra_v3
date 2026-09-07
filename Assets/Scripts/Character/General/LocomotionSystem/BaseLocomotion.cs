using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterController),typeof(Animator))]
public class BaseLocomotion : MonoBehaviour,ILocomotionLock,IRootMotionControl
{
    private const float MovementThreshold = 0.01f;

    [Header("Locomotion")]
    [SerializeField] private float _dampTime = 0.25f;
    [SerializeField] private float _multiply = 1f;
    [SerializeField] private string _nameParameterMoveZ = "VelocityZ";
    [SerializeField] private string _nameParameterMoveX = "VelocityX";

    [Header("Turn Animation")]
    [SerializeField] private string _nameTurnAngle = "StartTurnAngle";
    [SerializeField] private string _nameStartTurn = "StartTurn";
    [SerializeField] private float _angleTurn = 45f;

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
    private bool _wasMoving;
    private bool _rootMotionEnabled = true;

    public bool IsMovementLocked => _movementLockOwners.Count > 0;
    public bool ShouldResetMoveAnimation => _moveAnimationResetOwners.Count > 0;

    protected virtual void Awake()
    {
        Animator = GetComponent<Animator>();
        CharacterController = GetComponent<CharacterController>();
        LocomotionAnim = new LocomotionAnim(Animator,_dampTime,_multiply);
        LocomotionAnim.SetMoveParameter(_nameParameterMoveX,_nameParameterMoveZ);
        LocomotionAnim.SetTurnParameter(_nameTurnAngle,_nameStartTurn);
        Rotation = new RotationTransform(transform,_rotateSpeed);
        Gravity = new GravityCharacterCon(CharacterController,_gravityMultiplier);
    }

    protected virtual void Update()
    {
        if (IsMovementLocked)
        {
            ResetLockedMovement();
            return;
        }

        UpdateTurnAnimation();
        UpdateMoveAnimation();
        UpdateDebugValues();
    }

    protected virtual void FixedUpdate()
    {
        Vector3 direction = IsMovementLocked ? Vector3.zero : _facingDirection;
        Rotation.Rotate(direction);
    }

    protected virtual void OnAnimatorMove()
    {
        Vector3 rootMotion = _rootMotionEnabled ? Animator.deltaPosition : Vector3.zero;
        CharacterController.Move(rootMotion + Gravity.Gravity());

        if (_rootMotionEnabled && IsMovementLocked)
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

    public void ClearMovementDirection()
    {
        _movementDirection = Vector3.zero;
    }

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

    public void SetRootMotionEnabled(bool enabled) => _rootMotionEnabled = enabled;
    public float GetMoveMultiply() => LocomotionAnim.Multiply;
    public void SetMoveMultiply(float multiply) => LocomotionAnim.Multiply = multiply;
    public float GetRotateSmoothSpeed() => Rotation.Speed;
    public void SetRotateSmoothSpeed(float value) => Rotation.Speed = value;
    public float GetGravityMultiplier() => Gravity._GravityMultiplier;
    public void SetGravityMultiplier(float value) => Gravity._GravityMultiplier = value;

    private void ResetLockedMovement()
    {
        _wasMoving = false;
        if (ShouldResetMoveAnimation)
            LocomotionAnim.SetMove(0f,0f);
    }

    private void UpdateMoveAnimation()
    {
        Vector3 localDirection = transform.InverseTransformDirection(_movementDirection);
        LocomotionAnim.SetMove(localDirection.x,localDirection.z);
    }

    private void UpdateTurnAnimation()
    {
        bool isMoving = _movementDirection.sqrMagnitude > MovementThreshold;
        bool justStartedMoving = isMoving && !_wasMoving;

        if (justStartedMoving && _facingDirection.sqrMagnitude > MovementThreshold)
        {
            float angle = Vector3.SignedAngle(transform.forward,_facingDirection,Vector3.up);
            if (Mathf.Abs(angle) >= _angleTurn)
                LocomotionAnim.SetTurn(angle);
        }

        _wasMoving = isMoving;
    }

    private void UpdateDebugValues()
    {
#if UNITY_EDITOR
        _debugMoveMultiply = LocomotionAnim.Multiply;
        _debugRotateSpeed = Rotation.Speed;
#endif
    }
}
