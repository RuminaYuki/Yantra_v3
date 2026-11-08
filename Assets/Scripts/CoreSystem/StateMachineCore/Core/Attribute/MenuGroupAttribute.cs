using System;
using System.Diagnostics;
namespace Yuki.Learning.StateMachine
{
    /// <summary>
    /// Puts this class in a chosen group of the menus built by FolderMenu, such as
    /// Create > State Machine SO..., instead of under its script's full folder path. The folder the
    /// script is directly in stays as a subgroup, and the entry's name still comes from the class.
    /// Example: [MenuGroup("Standard")] on Action/Animator/PlayAnimatorStateActionSO.cs gives
    /// Standard > Animator > PlayAnimatorStateAction
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class MenuGroupAttribute : Attribute
    {
        public string Group { get; }

        public MenuGroupAttribute(string group)
        {
            Group = group;
        }
    }
}
