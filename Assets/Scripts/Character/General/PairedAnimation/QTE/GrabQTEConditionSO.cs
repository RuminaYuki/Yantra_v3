using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// ให้ State Machine ของผีอ่านผล QTE — เช่น ผู้เล่นหลุด → ผีมึน / ผู้เล่นแพ้ → กลับไปไล่ล่า
[CreateAssetMenu(
    fileName = "GrabQTE_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Paired Animation/Grab QTE")]
public class GrabQTEConditionSO : StateConditionSO
{
    public enum Check
    {
        IsRunning,   // กำลังจับอยู่ (รวมช่วงเล่นท่าดิ้นหลุด / ท่าแพ้)
        Escaped,     // ผลล่าสุด: ผู้เล่นดิ้นหลุด
        Failed       // ผลล่าสุด: ผู้เล่นแพ้
    }

    [SerializeField] private Check _check = Check.Escaped;

    protected override Condition OnCreateCondition() => new GrabQTECondition(_check);
}

public class GrabQTECondition : Condition
{
    private readonly GrabQTEConditionSO.Check _check;
    private GrabQTEInteractAddition _qte;

    public GrabQTECondition(GrabQTEConditionSO.Check check)
    {
        _check = check;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _qte = stateMachine.Owner.GetComponentInChildren<GrabQTEInteractAddition>(true);
        if (_qte == null)
            _qte = stateMachine.Owner.GetComponentInParent<GrabQTEInteractAddition>();
    }

    protected override bool Statement()
    {
        if (_qte == null)
            return false;

        switch (_check)
        {
            case GrabQTEConditionSO.Check.IsRunning: return _qte.IsRunning;
            case GrabQTEConditionSO.Check.Escaped: return _qte.LastResult == GrabQTEInteractAddition.Result.Escaped;
            default: return _qte.LastResult == GrabQTEInteractAddition.Result.Failed;
        }
    }
}
