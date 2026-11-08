using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Changes Follow3D settings when the state starts. Only the settings with "Change" ticked are applied.
// Self Anchor = the object that has Follow3D (empty = this state machine's owner).
[UseSelfAnchor]
[RequiresOwnerComponent(typeof(Follow3D))]
[CreateAssetMenu(
    fileName = "NewSetFollow3D_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/GamePlayMechanic/Follow/Set Follow 3D")]
public class SetFollow3DActionSO : StateActionSO
{
    [Header("Follow")]
    [SerializeField] private bool changeFollow = true;
    [SerializeField] private bool followOnEnter = true;
    [Tooltip("Also set Follow when the state ends.")]
    [SerializeField] private bool changeFollowOnExit;
    [SerializeField] private bool followOnExit;

    [Header("Target")]
    [SerializeField] private bool changeTarget;
    [Tooltip("The object to follow. Empty = this state machine's owner.")]
    [SerializeField] private AnchorIDSO targetAnchor;

    [Header("Constraint")]
    [SerializeField] private bool changeConstrainPosition;
    [SerializeField] private bool constrainPosition = true;
    [SerializeField] private bool changeConstrainRotation;
    [SerializeField] private bool constrainRotation = true;
    [SerializeField] private bool changeMaintainOffset;
    [SerializeField] private bool maintainOffset;

    [Header("Offset")]
    [SerializeField] private bool changePositionOffset;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private bool changeRotationOffset;
    [SerializeField] private Vector3 rotationOffset;

    [Header("Lerp Speed")]
    [SerializeField] private bool changePositionLerpSpeed;
    [Tooltip("0 = follow tightly with no lag. Higher = more lag.")]
    [SerializeField, Min(0f)] private float positionLerpSpeed;
    [SerializeField] private bool changeRotationLerpSpeed;
    [Tooltip("0 = follow tightly with no lag. Higher = more lag.")]
    [SerializeField, Min(0f)] private float rotationLerpSpeed;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetFollow3DAction(this);
    }

    public class SetFollow3DAction : StateAction
    {
        private readonly SetFollow3DActionSO so;
        private AnchorReference<Follow3D> followRef;
        private AnchorReference<Transform> targetRef;

        public SetFollow3DAction(SetFollow3DActionSO so)
        {
            this.so = so;
        }

        public override void Awake(StateMachine stateMachine)
        {
            followRef = new AnchorReference<Follow3D>(stateMachine, this);
            if (so.changeTarget)
            {
                targetRef = new AnchorReference<Transform>(stateMachine, so.targetAnchor, this);
            }
        }

        public override void OnStateEnter()
        {
            if (!followRef.TryGet(out Follow3D follow)) return;

            // Constraints and offsets first, so SetTarget caches the maintained offset with the new settings.
            if (so.changeConstrainPosition) follow.SetConstrainPosition(so.constrainPosition);
            if (so.changeConstrainRotation) follow.SetConstrainRotation(so.constrainRotation);
            if (so.changePositionOffset) follow.SetPositionOffset(so.positionOffset);
            if (so.changeRotationOffset) follow.SetRotationOffset(so.rotationOffset);
            if (so.changePositionLerpSpeed) follow.SetPositionLerpSpeed(so.positionLerpSpeed);
            if (so.changeRotationLerpSpeed) follow.SetRotationLerpSpeed(so.rotationLerpSpeed);
            if (so.changeMaintainOffset) follow.SetMaintainOffset(so.maintainOffset);

            if (so.changeTarget && targetRef.TryGet(out Transform target))
            {
                follow.SetTarget(target);
            }

            if (so.changeFollow) follow.SetFollow(so.followOnEnter);
        }

        public override void OnStateExit()
        {
            if (!so.changeFollowOnExit) return;
            if (!followRef.TryGet(out Follow3D follow)) return;
            follow.SetFollow(so.followOnExit);
        }

        public override void OnUpdate() { }
    }
}
