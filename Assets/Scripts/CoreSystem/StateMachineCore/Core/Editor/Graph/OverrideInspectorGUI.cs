#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The look shared by the "override the Base" Inspectors (a Base's state: VariantStatePanel, a Base's
    // arrow: TransitionOverrideSelectionEditor), so both read the same way:
    //   OVERRIDE BASE
    //   <name>
    //   Only affects <variant>.
    //   ┌ <box title>            <status> [button] ┐
    //   │ ...                                      │
    //   └──────────────────────────────────────────┘
    //   [Edit in Base]
    // Blue = different from the Base, red = removed here, grey = same as the Base.
    internal static class OverrideInspectorGUI
    {
        public static readonly Color ChangedColor = new Color(0.35f, 0.65f, 1f);
        public static readonly Color RemovedColor = new Color(1f, 0.5f, 0.5f);
        public static readonly Color SameColor = new Color(0.6f, 0.6f, 0.6f);

        private const float BoxHeaderHeight = 20f;

        private static GUIStyle _headingStyle;
        private static GUIStyle _titleStyle;
        private static GUIStyle _statusStyle;
        private static GUIStyle _boxHeaderStyle;
        private static GUIStyle _boxBodyStyle;

        private static void CreateStyles()
        {
            if (_headingStyle != null)
            {
                return;
            }

            _headingStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = ChangedColor } };
            _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _statusStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            _boxHeaderStyle = new GUIStyle("RL Header");
            _boxBodyStyle = new GUIStyle("RL Background") { padding = new RectOffset(8, 8, 6, 8) };
        }

        public static void DrawHeader(string title, string variantName)
        {
            CreateStyles();
            EditorGUILayout.LabelField("OVERRIDE BASE", _headingStyle);
            EditorGUILayout.LabelField(title, _titleStyle);
            EditorGUILayout.LabelField($"Only affects {variantName}.", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);
        }

        // A box header like a Unity list's: the title on the left, then (from the right) up to two
        // mini buttons and a colored status. Returns which button was clicked (0, 1) or -1.
        public static int DrawBoxHeader(
            string title,
            string status,
            Color statusColor,
            GUIContent firstButton = null,
            GUIContent secondButton = null)
        {
            CreateStyles();
            Rect rect = GUILayoutUtility.GetRect(0f, BoxHeaderHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                _boxHeaderStyle.Draw(rect, false, false, false, false);
            }

            int clicked = -1;
            float right = rect.xMax - 4f;

            foreach ((GUIContent content, int index) in new[] { (secondButton, 1), (firstButton, 0) })
            {
                if (content == null)
                {
                    continue;
                }

                float width = EditorStyles.miniButton.CalcSize(content).x + 6f;
                var buttonRect = new Rect(right - width, rect.y + 2f, width, rect.height - 4f);
                if (GUI.Button(buttonRect, content, EditorStyles.miniButton))
                {
                    clicked = index;
                }

                right -= width + 4f;
            }

            var titleRect = new Rect(rect.x + 6f, rect.y, right - rect.x - 6f, rect.height);
            EditorGUI.LabelField(titleRect, title);

            if (!string.IsNullOrEmpty(status))
            {
                Color previous = GUI.contentColor;
                GUI.contentColor = statusColor;
                EditorGUI.LabelField(titleRect, status, _statusStyle);
                GUI.contentColor = previous;
            }

            return clicked;
        }

        public static void BeginBoxBody()
        {
            CreateStyles();
            EditorGUILayout.BeginVertical(_boxBodyStyle);
        }

        public static void EndBoxBody()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6f);
        }

        // A line of text inside a box, colored like a status.
        public static void DrawNote(string text, Color color)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = color;
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedLabel);
            GUI.contentColor = previous;
        }

        public static void DrawEditInBase(string tooltip, Action onClick)
        {
            EditorGUILayout.Space(4f);
            if (GUILayout.Button(new GUIContent("Edit in Base", tooltip), GUILayout.Width(100f)))
            {
                onClick();
            }
        }
    }
}

#endif
