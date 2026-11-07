using UnityEngine;
using UnityEngine.Splines;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Self Anchor ว่าง = ใช้ SplineInstantiate บน Owner / ใส่ Anchor = ใช้ตัวที่ Anchor ชี้ (เช่น อยู่ตัวลูก)
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(SplineInstantiate))]
[CreateAssetMenu(
    fileName = "NewOnSplineItemsHit_Condition",
    menuName = "YUKI Learning State Machine/StateMachineList/Conditions/Spline/On Spline Items Hit")]
public class OnSplineItemsHitConditionSO : StateConditionSO
{
    protected override Condition OnCreateCondition()
    {
        return new OnSplineItemsHitCondition();
    }
}

// True once any DamageHitbox on the SplineInstantiate's items has hit something since entering the state,
// and stays true until the state is entered again.
public class OnSplineItemsHitCondition : Condition
{
    private AnchorReference<SplineInstantiate> _splineInstantiate;
    private SplineInstantiate _subscribedSpline;   // the one whose items we are listening to right now
    private DamageHitbox[] _subscribed = System.Array.Empty<DamageHitbox>();
    private bool _hasHit;

    public override void Awake(StateMachine stateMachine)
    {
        _splineInstantiate = new AnchorReference<SplineInstantiate>(stateMachine, this);
        SyncSubscription();
    }

    public override void OnStateEnter()
    {
        SyncSubscription();
        // A hit from before this state doesn't count.
        _hasHit = false;
    }

    protected override void OnTick()
    {
        SyncSubscription();
    }

    // Follows the anchor and the items: if the anchor points elsewhere, or the items were
    // regenerated (old ones destroyed / none spawned yet), move the listeners to the current items.
    private void SyncSubscription()
    {
        _splineInstantiate.TryGet(out SplineInstantiate current);
        if (current == _subscribedSpline && !ItemsChanged())
            return;

        Unsubscribe();

        _subscribedSpline = current;
        if (_subscribedSpline == null)
            return;

        // Instances live under a hidden root child of SplineInstantiate - still found by GetComponentsInChildren.
        _subscribed = _subscribedSpline.GetComponentsInChildren<DamageHitbox>(true);
        foreach (DamageHitbox hitbox in _subscribed)
            hitbox.OnHit += HandleHit;
    }

    private bool ItemsChanged()
    {
        if (_subscribedSpline == null)
            return false;

        // Nothing spawned yet - keep looking until items appear.
        if (_subscribed.Length == 0)
            return true;

        // SplineInstantiate destroys the old items when it regenerates.
        foreach (DamageHitbox hitbox in _subscribed)
        {
            if (hitbox == null)
                return true;
        }

        return false;
    }

    private void Unsubscribe()
    {
        foreach (DamageHitbox hitbox in _subscribed)
        {
            if (hitbox != null)
                hitbox.OnHit -= HandleHit;
        }

        _subscribed = System.Array.Empty<DamageHitbox>();
    }

    protected override bool Statement()
    {
        return _hasHit;
    }

    public override void Dispose()
    {
        Unsubscribe();
        _subscribedSpline = null;
    }

    private void HandleHit()
    {
        _hasHit = true;
    }
}
