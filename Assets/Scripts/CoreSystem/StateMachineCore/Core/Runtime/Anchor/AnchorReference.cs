using UnityEngine;

namespace Yuki.Learning.StateMachine
{
    public class AnchorReference<T>
    {
        private readonly StateMachine _stateMachine;
        private readonly Provider _provider;
        private readonly AnchorIDSO _anchorIDSO;
        private readonly object _requester;

        private T _cached;
        private int _seenVersion;
        private bool _hasWarned;
        private int CurrentVersion => _provider != null ? _provider.Version : 0;


        public AnchorReference(StateMachine stateMachine, AnchorIDSO anchorIDSO , object requester)
        {
           _stateMachine = stateMachine;
           _anchorIDSO = anchorIDSO;
           _requester = requester;

           if(anchorIDSO != null && !stateMachine.Owner.TryGetComponent(out _provider))
            {
                Debug.LogError(
                    $"[{requester.GetType().Name}] uses anchor '{anchorIDSO.name}', but '{stateMachine.Owner.name}' has no Provider.",
                    stateMachine.Owner);
            }
        }
        
        public AnchorReference(StateMachine stateMachine, object requester)
            : this(stateMachine, GetSelfAnchor(requester), requester){}

        private static AnchorIDSO GetSelfAnchor(object requester)
        {
            switch (requester)
            {
                case StateAction action:
                    return action.SelfAnchor;
                case Condition condition:
                    return condition.SelfAnchor;
                default:
                    return null;
            }
        }
        private bool TryFindTarget(out GameObject target)
        {
            if(_anchorIDSO == null)
            {
                target = _stateMachine.Owner;
                return true;
            }
            if(_provider == null)
            {
                target = null;
                return false;
            }
            return _provider.TryGet(_anchorIDSO, out target);
        }

        public bool TryGet(out T component)
        {
            if(_seenVersion == CurrentVersion && IsCachedAlive())
            {
                component = _cached;
                return true;
            }
            if(!TryFindTarget(out GameObject target))
            {
                WarnOnce(
                    $"anchor '{_anchorIDSO.name}' has no target on '{_stateMachine.Owner.name}' yet.",
                    _stateMachine.Owner);
                component = default;
                return false;
            }
            if (!target.TryGetComponent(out component))
            {
                WarnOnce($"needs {typeof(T).Name} on '{target.name}', but it has none.", target);
                return false;
            }
            _cached = component;
            _seenVersion = CurrentVersion;
            return true;
        }

        private bool IsCachedAlive()
        {
            if (_cached is Object unityObject)
            {
                return unityObject != null;   // Unity's null check: catches destroyed objects
            }
            return _cached != null;
        }

        // TryGet runs every frame, so only the first failure is logged.
        private void WarnOnce(string message, Object context)
        {
            if (_hasWarned)
            {
                return;
            }

            _hasWarned = true;
            Debug.LogWarning($"[{_requester.GetType().Name}] {message}", context);
        }
    }
}
