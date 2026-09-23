using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "NewHasReser_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Reservation/Has Reservation")]
public class HasReservationConditionSO : StateConditionSO
{
    [SerializeField] private ReservationSO _reservationSet;

    public override Condition CreateCondition() => new HasReservationCondition(_reservationSet);
}

public class HasReservationCondition : Condition
{
    private readonly ReservationSO _reservationSet;

    public HasReservationCondition(ReservationSO reservationSet)
    {
        _reservationSet = reservationSet;
    }

    protected override bool Statement() => _reservationSet.HasAny;
}
