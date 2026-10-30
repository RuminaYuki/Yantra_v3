using UnityEngine;
using UnityEngine.Splines;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Self Anchor ว่าง = ใช้ SplineInstantiate บน Owner / ใส่ Anchor = ใช้ตัวที่ Anchor ชี้ (เช่น อยู่ตัวลูก)
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(SplineInstantiate))]
[CreateAssetMenu(
    fileName = "SwapSplineItem_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Spline/Swap Spline Item")]
public class SwapSplineItemActionSO : StateActionSO
{
    [Tooltip("Prefab ที่จะใช้แทน Item บน SplineInstantiate ตอนเข้า State นี้")]
    [SerializeField] private GameObject _prefab;

    protected override StateAction OnCreateAction(StateMachine stateMachine) => new SwapSplineItemAction(_prefab);
}

public class SwapSplineItemAction : StateAction
{
    private readonly GameObject _prefab;
    private AnchorReference<SplineInstantiate> _splineInstantiate;

    public SwapSplineItemAction(GameObject prefab)
    {
        _prefab = prefab;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _splineInstantiate = new AnchorReference<SplineInstantiate>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (_prefab == null || !_splineInstantiate.TryGet(out SplineInstantiate spline)) return;

        spline.itemsToInstantiate = new[]
        {
            new SplineInstantiate.InstantiableItem { Prefab = _prefab, Probability = 100f }
        };
        spline.UpdateInstances(); // ตัวเก่าจะถูกลบ แล้วสร้างใหม่ทันที
    }

    public override void OnUpdate() { }
}
