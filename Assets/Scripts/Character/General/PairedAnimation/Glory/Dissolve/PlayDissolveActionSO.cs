using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Self Anchor ว่าง = ใช้ DissolveVFXPlayer บน Owner / ใส่ Anchor = ใช้ตัวที่ Anchor ชี้ (เช่น อยู่ตัวลูก)
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(DissolveVFXPlayer))]
[CreateAssetMenu(
    fileName = "PlayPairedDissolve_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Paired Animation/Play Dissolve After Glory")]
public class PlayDissolveActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine) => new PlayDissolveAction();
}

public class PlayDissolveAction : StateAction
{
    private AnchorReference<DissolveVFXPlayer> _dissolve;

    public override void Awake(StateMachine stateMachine)
    {
        _dissolve = new AnchorReference<DissolveVFXPlayer>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        // Entering the final Glory state is the trigger. The outgoing action has already
        // cleared actor.IsFinished/CurrentRequest, and Health may already have been zero.
        if (_dissolve.TryGet(out DissolveVFXPlayer dissolve))
            dissolve.PlayDissolve();
    }

    public override void OnUpdate()
    {
        // The victim component runs the fade independently of the state update.
    }
}
