using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[UseSelfAnchor]
[RequiresOwnerComponent(typeof(TriggerZone))]
[CreateAssetMenu(
    fileName = "NewIsTriggerZoneOccupied_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/GamePlayMechanic/TriggerZone/Is Trigger Zone Occupied")]
public class IsTriggerZoneOccupiedConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
    {
        return new IsTriggerZoneOccupiedCondition();
    }
}

// True while at least one object (passing the zone's layer filter) is inside the TriggerZone.
// It's a state, not an event, so it's just read when checked: no Tick needed.
public class IsTriggerZoneOccupiedCondition : Condition
{
    private AnchorReference<TriggerZone> zoneRef;

    public override void Awake(StateMachine stateMachine)
    {
        zoneRef = new AnchorReference<TriggerZone>(stateMachine, this);
    }

    protected override bool Statement()
    {
        return zoneRef.TryGet(out TriggerZone zone) && zone.IsOccupied;
    }
}
