#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // A small "type a name" box, e.g. New Category / Rename Category in the Parameters panel.
    // Enter or OK confirms, Escape or clicking elsewhere cancels.
    public class NamePromptWindow : EditorWindow
    {
        private Action<string> _onConfirm;
        private TextField _field;

        public static void Show(string title, string initialName, Action<string> onConfirm)
        {
            var window = CreateInstance<NamePromptWindow>();
            window.titleContent = new GUIContent(title);
            window._onConfirm = onConfirm;

            // Centered on whichever window was clicked (the State Machine Graph).
            Rect anchor = focusedWindow != null ? focusedWindow.position : new Rect(200f, 200f, 400f, 300f);
            var size = new Vector2(260f, 64f);
            window.position = new Rect(anchor.center - size * 0.5f, size);

            // CreateGUI may run later than ShowAuxWindow, so it reads the starting text from here.
            window._initialName = initialName ?? string.Empty;
            window.ShowAuxWindow();
        }

        private string _initialName = string.Empty;

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 6f;
            rootVisualElement.style.paddingRight = 6f;
            rootVisualElement.style.paddingTop = 6f;

            _field = new TextField { value = _initialName };
            _field.schedule.Execute(() =>
            {
                _field.Focus();
                _field.SelectAll();
            });
            _field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Confirm();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                }
            }, TrickleDown.TrickleDown);
            rootVisualElement.Add(_field);

            var ok = new Button(Confirm) { text = "OK" };
            ok.style.alignSelf = Align.FlexEnd;
            ok.style.width = 70f;
            rootVisualElement.Add(ok);
        }

        private void Confirm()
        {
            string name = _field.value?.Trim();
            Action<string> onConfirm = _onConfirm;
            Close();

            if (!string.IsNullOrEmpty(name))
            {
                onConfirm?.Invoke(name);
            }
        }
    }
}

#endif
