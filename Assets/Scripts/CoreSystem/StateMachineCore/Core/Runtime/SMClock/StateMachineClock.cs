using UnityEngine;

namespace Yuki.Learning.StateMachine
{
    public sealed class StateMachineClock
    {
        private readonly IStateMachineClock _source;

        public StateMachineClock(GameObject owner)
        {
            // รวม parent เผื่อ state machine อยู่บน child
            _source = owner != null ? owner.GetComponentInParent<IStateMachineClock>() : null;
            #if UNITY_EDITOR
                WarnIfUpdatesLate(_source);
            #endif
        }

        public float Now => HasSource ? _source.Now : Time.time;
        public float DeltaTime => HasSource ? _source.DeltaTime : Time.deltaTime;

        private bool HasSource => _source is Object unityObject ? unityObject != null : _source != null;

        // Debug Console Warning
        #if UNITY_EDITOR
        private static void WarnIfUpdatesLate(IStateMachineClock source)
        {
            if (source is not MonoBehaviour clock)
            {
                return;
            }

            if (ExecutionOrderOf(clock.GetType()) >= ExecutionOrderOf(typeof(StateMachineController)))
            {
                Debug.LogWarning(
                    $"{clock.GetType().Name} should update before StateMachineController. " +
                    "Add [DefaultExecutionOrder(-50)] to it.", clock);
            }
        }
        private static int ExecutionOrderOf(System.Type type)
        {
            var attribute = (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(type, typeof(DefaultExecutionOrder));
            if (attribute != null)
            {
                return attribute.order;
            }

            foreach (UnityEditor.MonoScript script in UnityEditor.MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (script.GetClass() == type)
                {
                    return UnityEditor.MonoImporter.GetExecutionOrder(script);
                }
            }

            return 0;
        }
        #endif
    }
}
