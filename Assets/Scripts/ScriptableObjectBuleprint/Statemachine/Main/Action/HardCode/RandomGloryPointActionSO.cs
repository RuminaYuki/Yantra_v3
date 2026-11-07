using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(RandomGloryPoint))]
[CreateAssetMenu(
    fileName = "RandomGloryPointAction",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/HardCode/Random Glory Point")]
public class RandomGloryPointActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new RandomGloryPointAction();
    }
}

public class RandomGloryPointAction : StateAction
{
    private AnchorReference<RandomGloryPoint> _randomGloryPoint;

    public override void Awake(StateMachine stateMachine)
    {
        _randomGloryPoint = new AnchorReference<RandomGloryPoint>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_randomGloryPoint.TryGet(out RandomGloryPoint randomGloryPoint)) return;
        randomGloryPoint.RandomizeProviderGloryPoint();
    }

    public override void OnUpdate() { }
    public override void OnStateExit() { }
}
