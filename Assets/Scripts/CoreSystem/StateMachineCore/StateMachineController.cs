using System;
using UnityEngine;
using Yuki.Learning.StateMachine;
using Yuki.Learning.StateMachine.ScriptableObjects;

public class StateMachineController : MonoBehaviour
{
    [SerializeField] private TransitionTableSO[] _transitionTables;
    public event Action<string, string> MainStateChanged;
    public event Action<string, string> ChildStateChanged;

    private StateMachine[] _stateMachines;

    private void Start()
    {
        _stateMachines = new StateMachine[_transitionTables.Length];

        for (int i = 0; i < _transitionTables.Length; i++)
        {
            _stateMachines[i] = CreateStateMachine(_transitionTables[i]);
        }
    }

    private void Update()
    {
        if (_stateMachines == null)
        {
            return;
        }

        foreach (StateMachine stateMachine in _stateMachines)
        {
            stateMachine?.OnUpdate();
        }
    }

    private void FixedUpdate()
    {
        if (_stateMachines == null)
        {
            return;
        }

        foreach (StateMachine stateMachine in _stateMachines)
        {
            stateMachine?.OnFixedUpdate();
        }
    }

    public void ChangeTable(int tableIndex, TransitionTableSO newTable)
    {
        if (_stateMachines == null)
        {
            Debug.LogWarning("StateMachineController has not started yet.", this);
            return;
        }

        if (tableIndex < 0 || tableIndex >= _stateMachines.Length)
        {
            Debug.LogError($"Invalid state machine index: {tableIndex}.", this);
            return;
        }

        if (newTable == null)
        {
            Debug.LogError("Cannot change to a null TransitionTableSO.", this);
            return;
        }

        // ออกจาก State และยกเลิก Condition ของ Table เก่า
        _stateMachines[tableIndex]?.Dispose();

        _transitionTables[tableIndex] = newTable;
        _stateMachines[tableIndex] = CreateStateMachine(newTable);
    }

    private StateMachine CreateStateMachine(TransitionTableSO table)
    {
        if (table == null)
        {
            return null;
        }

        StateMachine stateMachine = new StateMachine(gameObject);

        stateMachine.StateChanged += HandleMainStateChanged;
        stateMachine.ChildStateChanged += HandleChildStateChanged;
        State initialState = table.CreateInitialState(stateMachine);

        stateMachine.SetInitialState(initialState);

        return stateMachine;
    }
    public void RestartTable(int tableIndex)
    {
        if (_stateMachines == null)
        {
            Debug.LogWarning(
                "StateMachineController has not started yet.",
                this);

            return;
        }

        if (tableIndex < 0 || tableIndex >= _stateMachines.Length)
        {
            Debug.LogError(
                $"Invalid state machine index: {tableIndex}.",
                this);

            return;
        }

        TransitionTableSO currentTable = _transitionTables[tableIndex];

        if (currentTable == null)
        {
            Debug.LogError(
                $"Transition table at index {tableIndex} is null.",
                this);

            return;
        }

        _stateMachines[tableIndex]?.Dispose();
        _stateMachines[tableIndex] =
            CreateStateMachine(currentTable);
    }

    private void OnDestroy()
    {
        if (_stateMachines == null)
        {
            return;
        }

        foreach (StateMachine stateMachine in _stateMachines)
        {
            stateMachine?.Dispose();
        }
    }

    public string GetCurrentStateName(int tableIndex)
    {
        if (_stateMachines == null)
            return "Not Started";

        if (tableIndex < 0 || tableIndex >= _stateMachines.Length)
            return "Invalid Index";

        StateMachine stateMachine = _stateMachines[tableIndex];

        return stateMachine != null
            ? stateMachine.CurrentStateName
            : "None";
    }

    private void HandleChildStateChanged(
        string previousStateName,
        string currentStateName)
    {
        ChildStateChanged?.Invoke(
            previousStateName,
            currentStateName);
    }

    private void HandleMainStateChanged(
        string previousStateName,
        string currentStateName)
    {
        MainStateChanged?.Invoke(
            previousStateName,
            currentStateName);
    }
}
