using System.Security.Claims;
using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewReser_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Reservation/Claim or Release Reservation")]
public class ClaimRelaseReservationActionSO : StateActionSO
{
    [SerializeField] private ReservationSO _reservationSO;
    [SerializeField] private ReserOnEnterMode _reserOnEnterMode;
    [Tooltip("This bool will work with ClaimMode")]
    [SerializeField] private bool _relaseOnExit = false;

    public override StateAction CreateAction(StateMachine stateMachine)
    {
        return new ClaimReservationAction(_reservationSO, _reserOnEnterMode, _relaseOnExit, stateMachine);
    }
}

public class ClaimReservationAction : StateAction
{
    private readonly ReservationSO _reservationSO;
    private readonly ReserOnEnterMode _reserOnEnterMode;
    private readonly bool _relaseOnExit;
    private readonly StateMachine _owner;

    public ClaimReservationAction(ReservationSO reservationSO, ReserOnEnterMode reserOnEnterMode, bool relaseOnExit, StateMachine owner)
    {
        _reservationSO = reservationSO;
        _reserOnEnterMode = reserOnEnterMode;
        _relaseOnExit = relaseOnExit;
        _owner = owner;
    }

    public override void OnStateEnter()
    {
        switch (_reserOnEnterMode)
        {
            case ReserOnEnterMode.Claim:
                _reservationSO.Claim(_owner);
                break;
            case ReserOnEnterMode.Release:
                _reservationSO.Release(_owner);
                break;
        }
    }
    

    public override void OnStateExit()
    {
        if(_relaseOnExit && _reserOnEnterMode == ReserOnEnterMode.Claim)
            _reservationSO.Release(_owner); 
    } 

    public override void OnUpdate() { }
}
public enum ReserOnEnterMode
{
    Claim,
    Release
}
