using UnityEngine;

namespace Yuki.Learning.StateMachine
{
    /// <summary>
    /// Anchor side of the component guard. Lives in the Anchor addon so the core StateMachine
    /// never has to know anchors exist: this calls into the core, never the other way round.
    /// It also works when the anchor field is null (nothing assigned in the SO);
    /// that case falls back to <see cref="StateMachine.TryGetRequired{T}"/> on the owner.
    /// </summary>
    public static class AnchorComponentExtensions
    {
        /// <summary>
        /// Gets a component from the GameObject the anchor points at.
        /// Anchor left empty in the SO: looks on the owner instead.
        /// If it can't be found (anchor not provided yet, or component missing) the requester is
        /// disabled, same as <see cref="StateMachine.TryGetRequired{T}"/>.
        /// </summary>
        /// <param name="anchor">The anchor from the SO. May be null.</param>
        /// <param name="stateMachine">Used for the owner fallback and as the log context.</param>
        /// <param name="component">The component found, or null.</param>
        /// <param name="requester">The action / condition asking for it; pass <c>this</c>.</param>
        /// <returns>True if the component was found.</returns>
        /// <example>
        /// <code>
        /// _gunAnchor.TryGetComponentOrOwner(stateMachine, out _gunController, this);
        /// </code>
        /// </example>
        public static bool TryGetComponentOrOwner<T>(
            this GameObjectAnchor anchor,
            StateMachine stateMachine,
            out T component,
            object requester)
            where T : Component
        {
            if (anchor == null)
            {
                return stateMachine.TryGetRequired(out component, requester);
            }

            component = null;

            if (!anchor.IsSet)
            {
                // Warning, not error: usually the provider just hasn't run yet.
                // Still disabled: the component is only looked up once, in Awake.
                Debug.LogWarning(
                    $"[{requester.GetType().Name}] {anchor.name} has no value yet, so it is disabled.",
                    stateMachine.Owner);
                StateMachine.DisableRequester(requester);
                return false;
            }

            if (anchor.Value.TryGetComponent(out component))
            {
                return true;
            }

            Debug.LogError(
                $"[{requester.GetType().Name}] needs {typeof(T).Name} on '{anchor.name}' ({anchor.Value.name}).",
                anchor.Value);
            StateMachine.DisableRequester(requester);
            return false;

        }
    }
}
