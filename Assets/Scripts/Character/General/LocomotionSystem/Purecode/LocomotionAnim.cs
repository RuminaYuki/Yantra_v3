using UnityEngine;
public class LocomotionAnim
{
    private Animator _animator;

    private float _dampTime;
    private float _multiply;

    private int _moveZ; //Set parameter
    private int _moveX; //Set parameter

    private int _TurnAngle;
    private int _TurnTrigger;

    public LocomotionAnim(Animator animator, float dampTime = 0.25f, float multiply = 1)
    {
        _animator = animator;
        _dampTime = dampTime;
        _multiply = multiply;
    }

    //======================SetParameter========================
    //Set Parameter Method Overload
    public void SetMoveParameter(string nameParameterMoveZ)
    {
        _moveZ = Animator.StringToHash(nameParameterMoveZ);
    }

    public void SetMoveParameter(string nameParameterMoveX, string nameParameterMoveZ)
    {
        _moveX = Animator.StringToHash(nameParameterMoveX);
        _moveZ = Animator.StringToHash(nameParameterMoveZ);
    }
    //Turn
    public void SetTurnParameter(string nameTurnAngle, string nameTurnTrigger)
    {
        _TurnAngle = Animator.StringToHash(nameTurnAngle);
        _TurnTrigger = Animator.StringToHash(nameTurnTrigger);
    }

    //=========================SetKey===========================
    //SetMove Method Overload
    public void SetMove(float velocityX, float velocityZ)
    {
        float finalVelocityX = velocityX * _multiply;
        float finalVelocityZ = velocityZ * _multiply;
        _animator.SetFloat(_moveX, finalVelocityX, _dampTime, Time.deltaTime);
        _animator.SetFloat(_moveZ, finalVelocityZ, _dampTime, Time.deltaTime);
    }
    public void SetMove(float velocityZ)
    {
        float finalVelocity = velocityZ * _multiply;
        _animator.SetFloat(_moveZ, finalVelocity, _dampTime, Time.deltaTime);
    }

    public void SetTurn(float turnAngle)
    {
        _animator.SetFloat(_TurnAngle, turnAngle);
        _animator.SetTrigger(_TurnTrigger);
    }

    #region Secondary API
    public float DampTime{get => _dampTime; set => _dampTime = value;}
    public float Multiply { get => _multiply; set => _multiply = value; }
    #endregion
    
}