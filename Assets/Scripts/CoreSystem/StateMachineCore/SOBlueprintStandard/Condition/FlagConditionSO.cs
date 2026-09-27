using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

[RequiresOwnerComponent(typeof(StateFlagsAccess), UnlessAnchorField = "targetAnchor")]
[CreateAssetMenu(
    fileName = "NewFlag_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Standard/Check Flag")]
public class FlagConditionSO : StateConditionSO
{
    [SerializeField] private FlagSO flag;

    [Header("If the TargetAnchor is set, Check target flag instead")]
    [SerializeField] private GameObjectAnchor targetAnchor;
    public override Condition CreateCondition()
    {
        return new FlagCondition(flag,targetAnchor);
    }
}

public class FlagCondition : Condition
{
    private readonly FlagSO flag;
    private readonly GameObjectAnchor targetAnchor;

    private StateMachine stateMachine;
    private StateFlagsAccess stateFlags;

    public FlagCondition(FlagSO flag,GameObjectAnchor targetAnchor)
    {
        this.flag = flag;
        this.targetAnchor = targetAnchor;
    }

    public override void Awake(StateMachine stateMachine)
    {
        this.stateMachine = stateMachine;

        // กรณีตรวจ Flag ของ StateMachine ตัวเอง
        if (targetAnchor == null)
        {
            // Logs once here if missing; TryResolveTarget retries quietly.
            stateMachine.TryGetRequired(out stateFlags, this);
        }
    }
    public override void OnStateEnter()
    {
        // Object ตัวเองถูกหาไว้ตั้งแต่ Awake แล้ว
        if (targetAnchor == null)
        {
            return;
        }

        // ล้าง reference เดิม เพราะ Anchor อาจชี้ Object ใหม่
        stateFlags = null;

        TryResolveTarget();
    }
    protected override bool Statement()
    {
        if (flag == null)
        {
            return false;
        }

        // ป้องกันกรณี Provider ยังไม่พร้อมตอน OnStateEnter
        if (stateFlags == null & !TryResolveTarget())
        {
            return false;
        }

        return stateFlags.Get(flag);
    }

    private bool TryResolveTarget()
    {
        // หาเจอแล้ว ไม่ต้อง GetComponent ซ้ำ
        if (stateFlags != null)
        {
            return true;
        }

        // ไม่มี Anchor หมายถึงใช้ StateMachine ตัวเอง
        if (targetAnchor == null)
        {
            if (stateMachine == null)
            {
                return false;
            }

            // Called every frame from Statement(), so no log here (Awake already logged).
            return stateMachine.Owner.TryGetComponent(out stateFlags);
        }

        // Anchor ยังไม่ได้รับ Object จาก Provider
        if (!targetAnchor.IsSet || targetAnchor.Value == null)
        {
            return false;
        }

        stateFlags = targetAnchor.Value.GetComponent<StateFlagsAccess>();

        if (stateFlags == null)
        {
            Debug.LogError(
                $"FlagCondition: Object " +
                $"'{targetAnchor.Value.name}' does not have " +
                $"{nameof(StateFlagsAccess)}.",
                targetAnchor.Value);
        }

        return stateFlags != null;
    }
}
