using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

// Self Anchor = the object that has the TwoBoneIKConstraint (empty = this state machine's owner).
[UseSelfAnchor][MenuGroup("Standard")]
[RequiresOwnerComponent(typeof(StateMachineController))]
[CreateAssetMenu(
    fileName = "New_SetTwoBoneIK_Action",
    menuName = "YUKI Learning State Machine/StateMachineList/Actions/Standard/Animator/Set Two Bone IK")]
public class SetTwoBoneIKActionSO : StateActionSO
{
    public enum MaintainTargetOffset { None, Position, Rotation, PositionAndRotation }

    [Header("Weight")]
    [SerializeField, Range(0f, 1f),Exposed] private float _enterWeight = 1f;
    [SerializeField, Min(0f),Exposed] private float _enterDuration = 0.2f;
    [SerializeField,Exposed] private bool _setWeightOnExit = true;
    [SerializeField, Range(0f, 1f),Exposed] private float _exitWeight;
    [SerializeField, Min(0f),Exposed] private float _exitDuration = 0.2f;

    [Header("Source Objects")]
    [Tooltip("Changing Target / Hint / Maintain Target Offset rebuilds the RigBuilder, which is costly. " +
             "If you only need the hand somewhere else, move the IK target object instead.")]
    [SerializeField] private bool _changeTarget;
    [Tooltip("Object to use as the IK Target. Empty = this state machine's owner.")]
    [SerializeField] private AnchorIDSO _targetAnchor;
    [SerializeField] private bool _changeHint;
    [Tooltip("Object to use as the IK Hint. Empty = this state machine's owner.")]
    [SerializeField] private AnchorIDSO _hintAnchor;

    [Header("Settings")]
    [SerializeField] private bool _changeMaintainTargetOffset;
    [SerializeField] private MaintainTargetOffset _maintainTargetOffset;
    [SerializeField] private bool _changeSettingWeights;
    [SerializeField, Range(0f, 1f)] private float _targetPositionWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float _targetRotationWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float _hintWeight = 1f;

    protected override StateAction OnCreateAction(StateMachine stateMachine)
    {
        return new SetTwoBoneIKAction(this);
    }

    public class SetTwoBoneIKAction : StateAction
    {
        private readonly SetTwoBoneIKActionSO _so;

        // Constraint follows the Self Anchor; the coroutine always runs on this state machine's own controller.
        private AnchorReference<TwoBoneIKConstraint> _constraint;
        private AnchorReference<StateMachineController> _coroutineRunner;
        private AnchorReference<Transform> _target;
        private AnchorReference<Transform> _hint;
        private Coroutine _weightCoroutine;

        public SetTwoBoneIKAction(SetTwoBoneIKActionSO so)
        {
            _so = so;
        }

        public override void Awake(StateMachine stateMachine)
        {
            _constraint = new AnchorReference<TwoBoneIKConstraint>(stateMachine, this);
            _coroutineRunner = new AnchorReference<StateMachineController>(stateMachine, null, this);
            if (_so._changeTarget) _target = new AnchorReference<Transform>(stateMachine, _so._targetAnchor, this);
            if (_so._changeHint) _hint = new AnchorReference<Transform>(stateMachine, _so._hintAnchor, this);
        }

        public override void OnStateEnter()
        {
            if (!_constraint.TryGet(out TwoBoneIKConstraint constraint)) return;

            ApplySettings(constraint);
            StartWeightLerp(constraint, _so._enterWeight, _so._enterDuration);
        }

        public override void OnUpdate()
        {
        }

        public override void OnStateExit()
        {
            if (!_so._setWeightOnExit) return;
            if (!_constraint.TryGet(out TwoBoneIKConstraint constraint)) return;

            StartWeightLerp(constraint, _so._exitWeight, _so._exitDuration);
        }

        private void ApplySettings(TwoBoneIKConstraint constraint)
        {
            // These weights are read every frame, so no rebuild is needed.
            if (_so._changeSettingWeights)
            {
                constraint.data.targetPositionWeight = _so._targetPositionWeight;
                constraint.data.targetRotationWeight = _so._targetRotationWeight;
                constraint.data.hintWeight = _so._hintWeight;
            }

            // Target, Hint and Maintain Offset are baked when the rig is built, so they need a rebuild.
            bool needsRebuild = false;

            if (_so._changeTarget && _target.TryGet(out Transform target))
            {
                constraint.data.target = target;
                needsRebuild = true;
            }

            if (_so._changeHint && _hint.TryGet(out Transform hint))
            {
                constraint.data.hint = hint;
                needsRebuild = true;
            }

            if (_so._changeMaintainTargetOffset)
            {
                MaintainTargetOffset offset = _so._maintainTargetOffset;
                constraint.data.maintainTargetPositionOffset =
                    offset == MaintainTargetOffset.Position || offset == MaintainTargetOffset.PositionAndRotation;
                constraint.data.maintainTargetRotationOffset =
                    offset == MaintainTargetOffset.Rotation || offset == MaintainTargetOffset.PositionAndRotation;
                needsRebuild = true;
            }

            if (needsRebuild) Rebuild(constraint);
        }

        private static void Rebuild(TwoBoneIKConstraint constraint)
        {
            RigBuilder rigBuilder = constraint.GetComponentInParent<RigBuilder>();
            if (rigBuilder == null)
            {
                Debug.LogError($"No RigBuilder found above {constraint.name}, so the IK change was not applied.", constraint);
                return;
            }
            rigBuilder.Build();
        }

        private void StartWeightLerp(TwoBoneIKConstraint constraint, float targetWeight, float duration)
        {
            if (!_coroutineRunner.TryGet(out StateMachineController runner)) return;

            if (_weightCoroutine != null)
            {
                runner.StopCoroutine(_weightCoroutine);
                _weightCoroutine = null;
            }

            // Inactive or being destroyed (e.g. StateMachine.Dispose on OnDestroy): a coroutine can't
            // start there, so set the weight right away.
            if (duration <= 0f || !runner.isActiveAndEnabled)
            {
                constraint.weight = targetWeight;
                return;
            }

            _weightCoroutine = runner.StartCoroutine(LerpWeight(constraint, targetWeight, duration));
        }

        private IEnumerator LerpWeight(TwoBoneIKConstraint constraint, float targetWeight, float duration)
        {
            float startWeight = constraint.weight;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.DeltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);

                if (constraint == null) yield break;   // destroyed mid-lerp

                constraint.weight = Mathf.Lerp(startWeight, targetWeight, t);
                yield return null;
            }

            if (constraint != null) constraint.weight = targetWeight;
            _weightCoroutine = null;
        }
    }
}
