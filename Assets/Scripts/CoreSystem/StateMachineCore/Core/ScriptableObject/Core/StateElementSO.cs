using UnityEngine;
using UnityEngine.Serialization;
namespace Yuki.Learning.StateMachine.ScriptableObjects
{
    public abstract class StateElementSO : ScriptableObject
    {
        public const string SelfAnchorFieldName = nameof(_selfAnchor);
        // TEMPORARY (Self Anchor migration): pulls the old per-blueprint anchor fields into _selfAnchor.
        // Remove these attributes after running "Tools/State Machine/Reserialize Blueprints" once.
        [FormerlySerializedAs("targetAnchor")]
        [FormerlySerializedAs("meshRendererAnchor")]
        [FormerlySerializedAs("animatorAnchor")]
        [FormerlySerializedAs("anchorTarget")]
        [FormerlySerializedAs("handelerAnchor")]
        [FormerlySerializedAs("_cameraAnchor")]
        [FormerlySerializedAs("_gunAnchor")]
        [FormerlySerializedAs("_spellDrawAnchor")]
        [SerializeField] private AnchorIDSO _selfAnchor;
        protected AnchorIDSO SelfAnchor => _selfAnchor;
    }
}