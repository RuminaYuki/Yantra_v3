#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor
{
    // Like Unity's Prefab Variant: right-click a Transition Table in the Project window >
    // Create > YUKI Learning State Machine > StateMachineList > Transition Table Variant (right under
    // Transition Table), or "Create Variant" in its Inspector. Makes an empty table next to it with Base
    // set to it, named "<Base>_Variant", and starts renaming it. It runs everything the Base has until
    // something is changed in it.
    public static class TransitionTableVariantMenu
    {
        private const string MenuPath = "Assets/Create/YUKI Learning State Machine/StateMachineList/Transition Table Variant";

        [MenuItem(MenuPath, false, 0)]
        private static void CreateFromSelection()
        {
            CreateVariant((TransitionTableSO)Selection.activeObject);
        }

        // Greyed out unless a Transition Table is selected.
        [MenuItem(MenuPath, true)]
        private static bool CanCreateFromSelection()
        {
            return Selection.activeObject is TransitionTableSO;
        }

        public static void CreateVariant(TransitionTableSO baseTable)
        {
            var variant = ScriptableObject.CreateInstance<TransitionTableSO>();

            // _base is private: set it the way the Inspector does.
            var serializedVariant = new SerializedObject(variant);
            serializedVariant.FindProperty("_base").objectReferenceValue = baseTable;
            serializedVariant.ApplyModifiedPropertiesWithoutUndo();

            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(baseTable))?.Replace('\\', '/');
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseTable.name}_Variant.asset");

            // Saves it and puts its name in rename mode in the Project window, like Create > anything.
            ProjectWindowUtil.CreateAsset(variant, path);
        }
    }
}

#endif
