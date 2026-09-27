using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yuki.Learning.StateMachine
{
    public class StateMachine
    {
        private readonly GameObject _owner;
        private readonly HashSet<Condition> _conditions = new HashSet<Condition>();

        private State _currentState;
        private StateTransition[] _anyTransitions = Array.Empty<StateTransition>();
        private string _pendingExitId;
        private bool _hasPendingExit;
        private bool _isDisposed;

        public GameObject Owner => _owner;
        public bool IsDisposed => _isDisposed;
        public string CurrentStateName => _currentState?.DebugName ?? "None";
        public State CurrentState => _currentState;


        public event Action<string, string> StateChanged;
        public event Action<string, string> ChildStateChanged;
        public event Action<string> Exited;
        public event Action<string> ChildStateMachineExited;

        public StateMachine(GameObject owner)
        {
            _owner = owner != null
                ? owner
                : throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>
        /// Gets a required component from the owner. If it's missing: logs an error, disables
        /// the requester (a StateAction is skipped from then on, a Condition never passes)
        /// and returns false.
        /// Pass <c>this</c> as <paramref name="requester"/>. For optional components use <c>Owner.TryGetComponent</c>.
        /// </summary>
        public bool TryGetRequired<T>(out T component, object requester) where T : Component
        {
            if (_owner.TryGetComponent(out component))
            {
                return true;
            }

            Debug.LogError(
                $"[{requester.GetType().Name}] needs {typeof(T).Name} on '{_owner.name}'.",
                _owner);
            DisableRequester(requester);
            return false;
        }

        /// <summary>
        /// Switches off the action / condition whose setup failed: State drops disabled actions,
        /// and a disabled condition never passes. Anything else (e.g. a MonoBehaviour) is left alone.
        /// Also used by AnchorComponentExtensions.
        /// </summary>
        internal static void DisableRequester(object requester)
        {
            switch (requester)
            {
                case StateAction action:
                    action.Disable();
                    break;

                case Condition condition:
                    condition.Disable();
                    break;
            }
        }

        public void RegisterCondition(Condition condition)
        {
            if (condition == null)
            {
                Debug.LogError(
                    $"StateMachine on {_owner.name} cannot register a null condition.",
                    _owner);
                return;
            }

            if (_isDisposed)
            {
                Debug.LogWarning(
                    $"StateMachine on {_owner.name} is already disposed.",
                    _owner);
                return;
            }

            _conditions.Add(condition);
        }

        public void OnUpdate()
        {
            if (_isDisposed || _currentState == null)
            {
                return;
            }

            if (TryGetNextState(out State nextState))
            {
                ChangeState(nextState);
            }

            _currentState?.OnUpdate();
            ProcessExitRequest();
        }

        public void OnFixedUpdate()
        {
            if (_isDisposed || _currentState == null)
            {
                return;
            }

            _currentState.OnFixedUpdate();
        }

        public void SetInitialState(State initialState)
        {
            if (_isDisposed)
            {
                Debug.LogWarning(
                    $"StateMachine on {_owner.name} is already disposed.",
                    _owner);
                return;
            }

            if (initialState == null)
            {
                Debug.LogError(
                    $"StateMachine on {_owner.name} cannot use a null initial state.",
                    _owner);
                return;
            }

            string previousStateName = CurrentStateName;

            _currentState = initialState;
            StateChanged?.Invoke(previousStateName,CurrentStateName);
            _currentState.OnStateEnter();
            NotifyAnyTransitionsStateEnter();
        }

        public void ChangeState(State nextState)
        {
            if (_isDisposed ||
                nextState == null ||
                ReferenceEquals(_currentState, nextState))
            {
                return;
            }

            string previousStateName = CurrentStateName;

            _currentState?.OnStateExit();
            _currentState = nextState;
            StateChanged?.Invoke(previousStateName,CurrentStateName);
            _currentState.OnStateEnter();
            NotifyAnyTransitionsStateEnter();
        }

        private void NotifyAnyTransitionsStateEnter()
        {
            foreach (StateTransition transition in _anyTransitions)
            {
                transition?.OnStateEnter();
            }
        }

        public void SetAnyTransitions(StateTransition[] transitions)
        {
            if (_isDisposed)
            {
                return;
            }

            _anyTransitions = transitions ?? Array.Empty<StateTransition>();
        }

        public void RequestExit(string exitId)
        {
            if (_isDisposed || _hasPendingExit)
            {
                return;
            }

            _pendingExitId = exitId;
            _hasPendingExit = true;
        }

        public void NotifyChildStateMachineExited(string exitId)
        {
            if (_isDisposed)
            {
                return;
            }

            ChildStateMachineExited?.Invoke(exitId);
        }

        public void NotifyChildStateChanged(string previousStateName, string currentStateName)
        {
            if (_isDisposed)
                return;

            ChildStateChanged?.Invoke(
                previousStateName,
                currentStateName);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _currentState?.OnStateExit();

            foreach (Condition condition in _conditions)
            {
                condition.Dispose();
            }

            _conditions.Clear();
            _currentState = null;
            _anyTransitions = Array.Empty<StateTransition>();
            _pendingExitId = null;
            _hasPendingExit = false;
            StateChanged = null;
            ChildStateChanged = null;
            Exited = null;
            ChildStateMachineExited = null;
        }

        private void ProcessExitRequest()
        {
            if (!_hasPendingExit)
            {
                return;
            }

            string exitId = _pendingExitId;
            _pendingExitId = null;
            _hasPendingExit = false;

            _currentState?.OnStateExit();
            _currentState = null;
            Exited?.Invoke(exitId);
        }

        private bool TryGetNextState(out State nextState)
        {
            if (TryGetAnyTransition(out nextState))
            {
                return true;
            }

            return _currentState.TryGetTransition(out nextState);
        }

        private bool TryGetAnyTransition(out State nextState)
        {
            nextState = null;

            foreach (StateTransition transition in _anyTransitions)
            {
                if (transition == null ||
                    !transition.TryGetNextState(out State candidate))
                {
                    continue;
                }

                if (ReferenceEquals(candidate, _currentState))
                {
                    continue;
                }

                nextState = candidate;
                break;
            }

            foreach (StateTransition transition in _anyTransitions)
            {
                transition?.ClearConditionsCache();
            }

            return nextState != null;
        }
    }
}
