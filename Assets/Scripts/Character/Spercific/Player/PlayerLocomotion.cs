using UnityEngine;
using UnityEngine.InputSystem;

//RuminaYuki Owner
[RequireComponent(typeof(CharacterController),typeof(Animator))]
public class PlayerLocomotion : BaseLocomotion
{
    private Vector3 _directionMove;

    
    [Header("Player Input")]
    private InputSystem_Actions _playerInput;

    //===================== Camera ============================
    [Header("ReferencePoint")]
    [SerializeField] private Transform _referencePoint;

    //Turn
    [Header("Turn Animation Setting")]
    [SerializeField] private float _angleTurnbyCamera = 60;

    [Header("Turn Logic Setting")]
    [SerializeField] private bool _turnbyCamera;
    [SerializeField] private float _angleTurnExit = 10f;

    private bool _isTurning;
    private float _lastRawAngle;
    private float _unwrappedAngle;


    protected override void Awake()
    {
        base.Awake();
        _playerInput = new InputSystem_Actions();
    }

    void OnEnable()
    {
        _playerInput.Enable();
        _playerInput.Player.Move.performed += OnMovePerformed;
        _playerInput.Player.Move.canceled += OnMoveCanceled;
    }

    void OnDisable()
    {
        _playerInput.Disable();
        _playerInput.Player.Move.performed -= OnMovePerformed;
        _playerInput.Player.Move.canceled -= OnMoveCanceled;
    }
    void Start()
    {
        SetEnableTurn(false);
    }
    protected override void Update()
    {
        Vector3 direction = GetDirectionWithReferencePoint();
        SetMovementDirection(direction);
        SetFacingDirection(direction.sqrMagnitude > 0.01f
            ? GetCameraForwardFlat()
            : Vector3.zero);

        base.Update();

        if (_turnbyCamera)
            UpdateTurnByCamera(direction);
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        _directionMove = ctx.ReadValue<Vector3>();
    }
    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        _directionMove = Vector3.zero;
    }

    #region SetAnimation
    private void UpdateTurnByCamera(Vector3 direction)
    {
        bool isMoving = direction.sqrMagnitude > 0.01f;

        if (_isTurning)
        {
            float rawAngle = GetSignedAngleToCamera();
            _unwrappedAngle += Mathf.DeltaAngle(_lastRawAngle, rawAngle);
            _lastRawAngle = rawAngle;

            LocomotionAnim.SetTurnAngleContinuous(_unwrappedAngle);

            if (isMoving || Mathf.Abs(_unwrappedAngle) <= _angleTurnExit)
            {
                _isTurning = false;
                LocomotionAnim.SetIsTurning(false);
                UnlockLocomotion(this);
            }
            return;
        }

        if (IsMovementLocked || isMoving)
            return;

        float startAngle = GetSignedAngleToCamera();
        if (Mathf.Abs(startAngle) >= _angleTurnbyCamera)
        {
            _isTurning = true;
            _lastRawAngle = startAngle;
            _unwrappedAngle = startAngle;
            LockLocomotion(this);
            LocomotionAnim.SetIsTurning(true);
            LocomotionAnim.SetTurnAngleContinuous(startAngle);
        }
    }

    private float GetSignedAngleToCamera()
    {
        Vector3 playerForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 cameraForward = Vector3.ProjectOnPlane(_referencePoint.forward, Vector3.up).normalized;

        return Vector3.SignedAngle(playerForward, cameraForward, Vector3.up );
    }

    #endregion

    #region HelperMethod
    private Vector3 GetWorldDirectionRelativeTo(
    Vector3 inputDirection,
    Transform referenceTransform)
    {
        if (_referencePoint== null)
            return _directionMove;

        Vector3 referenceForward = referenceTransform.forward;
        Vector3 referenceRight = referenceTransform.right;

        referenceForward.y = 0f;
        referenceRight.y = 0f;

        referenceForward.Normalize();
        referenceRight.Normalize();

        return referenceForward * inputDirection.z + referenceRight * inputDirection.x;
    }

    private Vector3 GetCameraForwardFlat()
    {
        if (_referencePoint == null)
            return transform.forward;

        Vector3 flatForward =
            _referencePoint.forward;

        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.0001f)
            return transform.forward;

        return flatForward.normalized;
    }
    #endregion

    #region //API
    // Direction
    public void SetDirection(Vector3 direction) => _directionMove = direction;
    public Vector3 GetDirection() => _directionMove;
    public Vector3 GetDirectionWithReferencePoint() => GetWorldDirectionRelativeTo(_directionMove,_referencePoint);

    // Get,Set TurnbyCamera
    public bool GetTurnByCamera() => _turnbyCamera;
    public void SetTurnByCamera(bool value) => _turnbyCamera = value;

    #endregion
}
