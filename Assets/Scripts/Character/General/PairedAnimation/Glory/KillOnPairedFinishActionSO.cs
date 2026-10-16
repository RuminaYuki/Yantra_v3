using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "KillOnPairedFinish_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Paired Animation/Kill On Paired Finish")]
public class KillOnPairedFinishActionSO : StateActionSO
{
    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new KillOnPairedFinishAction();
    }
}

public class KillOnPairedFinishAction : StateAction
{
    private AnchorReference<PairedAnimationActor> _actor;
    private Health _health;
    private bool _hasKilled;

    public override void Awake(StateMachine stateMachine)
    {
        _actor = new AnchorReference<PairedAnimationActor>(stateMachine, this);

        // Health ของผีอาจอยู่ตัวแม่หรือตัวลูก ไม่จำเป็นต้องอยู่ตัวเดียวกับ StateMachine
        _health = stateMachine.Owner.GetComponentInParent<Health>();
        if (_health == null)
            _health = stateMachine.Owner.GetComponentInChildren<Health>();

        if (_health == null)
            Debug.LogError($"[KillOnPairedFinishAction] หา Health ของ {stateMachine.Owner.name} ไม่เจอ", stateMachine.Owner);
    }

    public override void OnStateEnter()
    {
        _hasKilled = false;
    }

    // ฆ่าผ่าน Health.Kill() ให้ตายตามระบบปกติ — OnDead → State ตาย, เสียง, VFX ทำงานเหมือนตายจากหมัด
    public override void OnUpdate()
    {
        if (_hasKilled || _health == null ||
            !_actor.TryGet(out PairedAnimationActor actor) || !actor.IsFinished)
            return;

        _hasKilled = true;
        _health.Kill();
    }
}