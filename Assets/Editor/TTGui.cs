using UnityEditor;
using UnityEngine;

namespace Yantra.TerrainTools
{
    internal static class TTGui
    {
        public static readonly Color Accent = new Color(0.85f, 0.68f, 0.32f);
        public static readonly Color Danger = new Color(0.95f, 0.42f, 0.36f);

        static GUIStyle _title, _section, _big, _hint, _banner, _sectionTitle;

        public static GUIStyle Title
        {
            get
            {
                if (_title == null)
                    _title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, margin = new RectOffset(6, 6, 10, 2) };
                return _title;
            }
        }

        public static GUIStyle SectionTitle
        {
            get
            {
                if (_sectionTitle == null)
                    _sectionTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, margin = new RectOffset(0, 0, 0, 4) };
                return _sectionTitle;
            }
        }

        public static GUIStyle Section
        {
            get
            {
                if (_section == null)
                    _section = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 12), margin = new RectOffset(6, 6, 6, 10) };
                return _section;
            }
        }

        public static GUIStyle Big
        {
            get
            {
                if (_big == null)
                    _big = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, fixedHeight = 34 };
                return _big;
            }
        }

        public static GUIStyle Hint
        {
            get
            {
                if (_hint == null)
                    _hint = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { fontSize = 11, richText = true };
                return _hint;
            }
        }

        public static GUIStyle Banner
        {
            get
            {
                if (_banner == null)
                {
                    _banner = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                    _banner.normal.textColor = Color.white;
                }
                return _banner;
            }
        }

        public static GUIContent C(string text, string tooltip = null)
        {
            return new GUIContent(text, tooltip);
        }

        public static void BeginSection(string title, string hint = null)
        {
            EditorGUILayout.BeginVertical(Section);
            GUILayout.Label(title, SectionTitle);
            if (!string.IsNullOrEmpty(hint))
            {
                GUILayout.Label(hint, Hint);
                GUILayout.Space(4);
            }
        }

        public static void EndSection()
        {
            EditorGUILayout.EndVertical();
        }

        public static bool BigButton(string text, string tooltip = null, bool primary = false)
        {
            Color prev = GUI.backgroundColor;
            if (primary) GUI.backgroundColor = Accent;
            bool clicked = GUILayout.Button(new GUIContent(text, tooltip), Big);
            GUI.backgroundColor = prev;
            return clicked;
        }

        public static bool DangerButton(string text, string tooltip = null)
        {
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = Danger;
            bool clicked = GUILayout.Button(new GUIContent(text, tooltip), Big);
            GUI.backgroundColor = prev;
            return clicked;
        }

        public static void Note(string text)
        {
            GUILayout.Label(text, Hint);
        }

        // แถบข้อความลอยในหน้าต่าง Scene
        public static void SceneBanner(SceneView sv, int slot, string text, Color color, string buttonText, System.Action onButton)
        {
            Handles.BeginGUI();
            float w = Mathf.Min(460f, sv.position.width - 20f);
            float h = 34f;
            var rect = new Rect((sv.position.width - w) * 0.5f, 8f + slot * (h + 6f), w, h);
            Color prev = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.88f));
            GUI.color = prev;

            var labelRect = rect;
            if (buttonText != null) labelRect.width -= 96f;
            GUI.Label(labelRect, text, Banner);
            if (buttonText != null && GUI.Button(new Rect(rect.xMax - 92f, rect.y + 5f, 86f, h - 10f), buttonText))
                onButton?.Invoke();
            Handles.EndGUI();
        }
    }
}
