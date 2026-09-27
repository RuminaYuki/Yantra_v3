#if UNITY_EDITOR

using UnityEngine;
using UnityEngine.UIElements;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // The line that follows the mouse after "Make Transition", until a target state is clicked.
    // Covers the whole GraphView and never takes clicks.
    public class TransitionPreviewView : VisualElement
    {
        private const float ArrowLength = 10f;
        private const float ArrowHalfWidth = 6f;

        private static readonly Color LineColor = new Color(0.27f, 0.6f, 1f);

        private Vector2 _start;
        private Vector2 _end;

        public TransitionPreviewView()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;

            generateVisualContent += OnGenerateVisualContent;
        }

        // Both points in this element's local space.
        public void SetPoints(Vector2 start, Vector2 end)
        {
            _start = start;
            _end = end;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Vector2 line = _end - _start;
            if (line.sqrMagnitude < 1f)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            painter.strokeColor = LineColor;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(_start);
            painter.LineTo(_end);
            painter.Stroke();

            Vector2 direction = line.normalized;
            Vector2 side = new Vector2(-direction.y, direction.x) * ArrowHalfWidth;
            Vector2 back = _end - direction * ArrowLength;

            painter.fillColor = LineColor;
            painter.BeginPath();
            painter.MoveTo(_end);
            painter.LineTo(back + side);
            painter.LineTo(back - side);
            painter.ClosePath();
            painter.Fill();
        }
    }
}

#endif
