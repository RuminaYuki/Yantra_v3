using UnityEngine;
using UnityEngine.Splines;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Self Anchor ว่าง = ใช้ SplineInstantiate บน Owner / ใส่ Anchor = ใช้ตัวที่ Anchor ชี้ (เช่น อยู่ตัวลูก)
// ตั้ง damage ให้ DamageHitbox ของทุก instance ที่ SplineInstantiate สร้างไว้ (เช่น หนามทุกอัน)
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(SplineInstantiate))]
[CreateAssetMenu(
    fileName = "SetSplineItemsDamage_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Spline/Set Spline Items Damage")]
public class SetSplineItemsDamageActionSO : StateActionSO
{
    [SerializeField, Min(0f), Exposed] private float _damageAmount = 10f;
    [Header("Damage Type (Optional)")]
    [Tooltip("Not required - leave empty to keep each item's own damage type.")]
    [SerializeField] private DamageTypeID _damageType;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetSplineItemsDamageAction(_damageAmount, _damageType);
    }
}

public class SetSplineItemsDamageAction : StateAction
{
    private readonly float _damageAmount;
    private readonly DamageTypeID _damageType;

    private AnchorReference<SplineInstantiate> _splineInstantiate;

    public SetSplineItemsDamageAction(float damageAmount, DamageTypeID damageType)
    {
        _damageAmount = damageAmount;
        _damageType = damageType;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _splineInstantiate = new AnchorReference<SplineInstantiate>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (!_splineInstantiate.TryGet(out SplineInstantiate spline)) return;

        // Instances live under a hidden root child of SplineInstantiate - still found by GetComponentsInChildren.
        foreach (DamageHitbox hitbox in spline.GetComponentsInChildren<DamageHitbox>(true))
        {
            hitbox.SetDamage(_damageAmount);
            if (_damageType != null)
                hitbox.SetDamageType(_damageType);
        }
    }

    public override void OnUpdate() { }
}
