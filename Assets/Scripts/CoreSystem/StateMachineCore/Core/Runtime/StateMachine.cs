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

        public T GetComponent<T>() where T : Component
        {
            return _owner.GetComponent<T>();
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
