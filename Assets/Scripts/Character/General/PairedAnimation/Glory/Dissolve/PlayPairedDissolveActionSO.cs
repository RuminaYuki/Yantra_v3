using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "PlayPairedDissolve_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Paired Animation/Play Dissolve After Glory")]
public class PlayPairedDissolveActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine) => new PlayPairedDissolveAction();
}

public class PlayPairedDissolveAction : StateAction
{
    private DissolveVFXPlayer _dissolve;

    public override void Awake(StateMachine stateMachine)
    {
        _dissolve = stateMachine.Owner.GetComponent<DissolveVFXPlayer>();
        if (_dissolve == null)
            _dissolve = stateMachine.Owner.GetComponentInChildren<DissolveVFXPlayer>(true);
        if (_dissolve == null)
            _dissolve = stateMachine.Owner.GetComponentInParent<DissolveVFXPlayer>();

        if (_dissolve == null)
            Debug.LogWarning("[Paired Dissolve] เพิ่ม DissolveVFXPlayer บน EnemyController ของผีตัวนี้ก่อน", stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        // Entering the final Glory state is the trigger. The outgoing action has already
        // cleared actor.IsFinished/CurrentRequest, and Health may already have been zero.
        if (_dissolve != null)
            _dissolve.PlayDissolve();
    }

    public override void OnUpdate()
    {
        // The victim component runs the fade independently of the state update.
    }
}
