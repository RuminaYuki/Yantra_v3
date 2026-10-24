#if UNITY_EDITOR

namespace Yuki.Learning.StateMachine.Editor
{
    // Names of the private serialized fields the editor tools read and write through SerializedObject.
    // Kept in one place, so renaming a field in a runtime class only has to be followed here.
    internal static class StateMachineFields
    {
        // TransitionTableSO
        public const string InitialState = "_initialState";
        public const string Transitions = "_transitions";
        public const string AnyTransitions = "_anyTransitions";
        public const string NodePositions = "_nodePositions";
        public const string ContentsFolders = "_contentsFolders";
        public const string ContentsFolderPaths = "_contentsFolderPaths";
        public const string ExposedParameters = "_exposedParameters";
        public const string Base = "_base";
        public const string FieldOverrides = "_fieldOverrides";
        public const string StateDisplayNames = "_stateDisplayNames";
        public const string RemovedParameters = "_removedParameters";

        // StateSO
        public const string Actions = "_actions";

        // SubStateMachineActionSO
        public const string TransitionTable = "_transitionTable";

        // ExitSubStateMachineActionSO and SubStateMachineExitedConditionSO
        public const string ExitId = "_exitId";
    }
}

#endif
