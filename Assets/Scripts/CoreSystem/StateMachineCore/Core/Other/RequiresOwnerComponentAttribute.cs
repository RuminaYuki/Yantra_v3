using System;
using System.Diagnostics;

namespace Yuki.Learning.StateMachine
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class RequiresOwnerComponentAttribute : Attribute
    {
        public Type ComponentType { get; }

        /// <summary>
        /// Name of a GameObjectAnchor field on the SO. When that field is assigned, the component
        /// lives on the anchor's object instead, which is only known at runtime, so the check is skipped.
        /// </summary>
        public string UnlessAnchorField { get; set; }

        public RequiresOwnerComponentAttribute(Type componentType)
        {
            ComponentType = componentType;
        }
    }
}
