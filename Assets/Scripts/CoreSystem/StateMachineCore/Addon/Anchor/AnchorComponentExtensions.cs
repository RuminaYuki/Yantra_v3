using UnityEngine;

namespace Yuki.Learning.StateMachine
{
    /// <summary>
    /// Anchor side of the component guard. Lives in the Anchor addon so the core StateMachine
    /// never has to know anchors exist: this calls into the core, never the other way round.
    /// Both methods also work when the anchor field is null (nothing assigned in the SO);
    /// that case falls back to <see cref="StateMachine.TryGetRequired{T}"/> on the owner.
    /// </summary>
    public static class AnchorComponentExtensions
    {
        /// <summary>
        /// For an anchor that points at a GameObject, when the component needed is another one on it.
        /// Anchor left empty in the SO: looks on the owner instead.
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
        public static bool TryGetComponentOrOwner<TValue, T>(
            this RuntimeAnchorBase<TValue> anchor,
            StateMachine stateMachine,
            out T component,
            object requester)
            where TValue : Object
            where T : Component
        {
            if (anchor == null)
            {
                return stateMachine.TryGetRequired(out component, requester);
            }

            component = null;

            if (!anchor.IsSet)
            {
                LogAnchorNotSet(anchor, stateMachine, requester);
                return false;
            }

            // GameObjectAnchor holds a GameObject; TransformAnchor, AnimatorAnchor... hold a Component.
            GameObject target = anchor.Value is Component valueComponent
                ? valueComponent.gameObject
                : anchor.Value as GameObject;

            if (target != null && target.TryGetComponent(out component))
            {
                return true;
            }

            Debug.LogError(
                $"[{requester.GetType().Name}] needs {typeof(T).Name} on '{anchor.name}' " +
                $"({(target != null ? target.name : "null")}).",
                target);
            return false;
        }

        // Warning, not error: usually the provider just hasn't run yet.
        private static void LogAnchorNotSet<TValue>(
            RuntimeAnchorBase<TValue> anchor,
            StateMachine stateMachine,
            object requester)
            where TValue : Object
        {
            Debug.LogWarning(
                $"[{requester.GetType().Name}] {anchor.name} has no value yet.",
                stateMachine.Owner);
        }
    }
}
