using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[CreateAssetMenu(
    fileName = "PairedAnimationPlayer_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Paired Animation/Paired Animation Player")]
public class PairedAnimationPlayerConditionSO : StateConditionSO
{
    public enum Check
    {
        IsPlaying,     // ใช้เข้า State เปล่าตอนโดนจับ (ฝ่ายโดนที่ไม่ได้มาจาก Interact)
        HasFinished    // ใช้ออกจาก State เปล่าหลังท่าจบ
    }

    [SerializeField] private Check _check = Check.HasFinished;

    protected override Condition OnCreateCondition() => new PairedAnimationPlayerCondition(_check);
}

public class PairedAnimationPlayerCondition : Condition
{
    private readonly PairedAnimationPlayerConditionSO.Check _check;
    private AnchorReference<PairedAnimationPlayer> _player;

    public PairedAnimationPlayerCondition(PairedAnimationPlayerConditionSO.Check check)
    {
        _check = check;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _player = new AnchorReference<PairedAnimationPlayer>(stateMachine, this);
    }

    protected override bool Statement()
    {
        if (!_player.TryGet(out PairedAnimationPlayer player))
            return false;

        return _check == PairedAnimationPlayerConditionSO.Check.IsPlaying
            ? player.IsPlaying
            : player.HasFinished;
    }
}
