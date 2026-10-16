using UnityEditor;
using UnityEngine;
using Yuki.Learning.StateMachine.ScriptableObjects;

namespace Yuki.Learning.StateMachine.Editor
{
    // Draws every AnchorIDSO field in the project:
    // - hides a blueprint's Self Anchor unless the class has [UseSelfAnchor]
    // - shows "(Owner)" in grey when the field is empty
    [CustomPropertyDrawer(typeof(AnchorIDSO))]
    public class AnchorIDSODrawer : PropertyDrawer
    {
        private static GUIStyle _ownerStyle;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return IsHidden(property) ? 0f : EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (IsHidden(property))
            {
                return;
            }

            EditorGUI.PropertyField(position, property, label);

            // Paint "(Owner)" over the "None (Anchor IDSO)" text. Repaint only, so clicks
            // and drag-and-drop still reach the real field underneath.
            if (property.objectReferenceValue == null && Event.current.type == EventType.Repaint)
            {
                _ownerStyle ??= new GUIStyle(EditorStyles.objectField)
                {
                    normal = { textColor = Color.gray }
                };

                Rect field = position;
                field.xMin += EditorGUIUtility.labelWidth + 2f;
                field.xMax -= 19f; // leave the picker button visible
                _ownerStyle.Draw(field, new GUIContent("(Owner)"), false, false, false, false);
            }
        }

        private static bool IsHidden(SerializedProperty property)
        {
            if (property.name != StateElementSO.SelfAnchorFieldName)
            {
                return false;
            }

            System.Type blueprintType = property.serializedObject.targetObject.GetType();
            return !blueprintType.IsDefined(typeof(UseSelfAnchorAttribute), true);
        }
    }
}
