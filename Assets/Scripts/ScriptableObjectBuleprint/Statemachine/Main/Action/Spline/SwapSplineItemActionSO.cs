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

    [Tooltip("เปิด = สร้าง Item ครั้งแรกครั้งเดียวแล้วเก็บไว้เปิด/ปิดใช้ซ้ำ (SplineItemCache) ไม่กระตุกตอนเด้งครั้งต่อไป\n" +
             "ปิด = ให้ SplineInstantiate ลบแล้วสร้างใหม่ทุกครั้งแบบเดิม")]
    [SerializeField] private bool _reuseItems = true;

    protected override StateAction OnCreateAction(StateMachine stateMachine) => new SwapSplineItemAction(_prefab, _reuseItems);
}

public class SwapSplineItemAction : StateAction
{
    private readonly GameObject _prefab;
    private readonly bool _reuseItems;
    private AnchorReference<SplineInstantiate> _splineInstantiate;

    public SwapSplineItemAction(GameObject prefab, bool reuseItems)
    {
        _prefab = prefab;
        _reuseItems = reuseItems;
    }

    public override void Awake(StateMachine stateMachine)
    {
        _splineInstantiate = new AnchorReference<SplineInstantiate>(stateMachine, this);
    }

    public override void OnStateEnter()
    {
        if (_prefab == null || !_splineInstantiate.TryGet(out SplineInstantiate spline)) return;

        if (_reuseItems)
        {
            if (!spline.TryGetComponent(out SplineItemCache cache))
                cache = spline.gameObject.AddComponent<SplineItemCache>();
            cache.Show(_prefab);
            return;
        }

        spline.itemsToInstantiate = new[]
        {
            new SplineInstantiate.InstantiableItem { Prefab = _prefab, Probability = 100f }
        };
        spline.UpdateInstances(); // ตัวเก่าจะถูกลบ แล้วสร้างใหม่ทันที
    }

    public override void OnUpdate() { }
}
