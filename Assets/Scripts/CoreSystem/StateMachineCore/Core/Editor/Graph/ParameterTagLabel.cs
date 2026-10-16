#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // A small "⚙ 2" badge inside a state / beside an arrow: how many exposed parameters it uses.
    // Hovering shows their names. Kept tiny so the graph stays readable.
    internal class ParameterTagLabel : VisualElement
    {
        private static readonly Color TextColor = new Color(0.55f, 0.8f, 1f);
        private static readonly Color BackgroundColor = new Color(0.1f, 0.25f, 0.4f, 0.85f);
        private const float IconSize = 11f;

        private readonly Label _count;

        public ParameterTagLabel()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.alignSelf = Align.FlexStart;
            style.backgroundColor = BackgroundColor;
            style.borderTopLeftRadius = 6f;
            style.borderTopRightRadius = 6f;
            style.borderBottomLeftRadius = 6f;
            style.borderBottomRightRadius = 6f;
            style.paddingLeft = 3f;
            style.paddingRight = 4f;
            style.paddingTop = 1f;
            style.paddingBottom = 1f;

            Texture icon = EditorGUIUtility.FindTexture(EditorGUIUtility.isProSkin ? "d_Settings" : "Settings");
            if (icon != null)
            {
                var image = new Image { image = icon, tintColor = TextColor, pickingMode = PickingMode.Ignore };
                image.style.width = IconSize;
                image.style.height = IconSize;
                image.style.marginRight = 2f;
                Add(image);
            }

            _count = new Label { pickingMode = PickingMode.Ignore };
            _count.style.fontSize = 10;
            _count.style.color = TextColor;
            _count.style.unityFontStyleAndWeight = FontStyle.Bold;
            _count.style.paddingLeft = 0f;
            _count.style.paddingRight = 0f;
            Add(_count);
        }

        // Hidden when there's nothing to show.
        public void Show(IReadOnlyList<string> names)
        {
            _count.text = names.Count.ToString();
            tooltip = "Exposed parameters:\n" + string.Join("\n", names);
            style.display = names.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

#endif
