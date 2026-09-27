#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yuki.Learning.StateMachine.Editor.Graph
{
    // Animator-style transition: a straight line between two node borders with an arrow near the source.
    // One edge stands for every transition with the same From -> To (Animator shows 3 arrows for that).
    //
    // GraphView's built-in Edge needs ports and draws curves, so this draws the line itself.
    public class TransitionEdgeView : GraphElement
    {
        // Sideways shift so A -> B and B -> A don't draw on top of each other.
        private const float SideOffset = 8f;
        private const float ArrowLength = 10f;
        private const float ArrowHalfWidth = 6f;
        // Arrow sits this many pixels after the source node's border, the same on every line.
        private const float ArrowDistanceFromStart = 15f;
        private const float LineWidth = 2f;
        private const float ClickDistance = 6f;
        private const float BoundsPadding = 12f;

        private static readonly Color NormalColor = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color SelectedColor = new Color(0.27f, 0.6f, 1f);

        public Node From { get; }
        public Node To { get; }

        // Indexes into _transitions (or _anyTransitions). Empty for Entry -> initial state.
        public IReadOnlyList<int> TransitionIndices { get; }
        public bool IsAnyState { get; }

        // Line ends in this element's local space.
        private Vector2 _start;
        private Vector2 _end;

        public TransitionEdgeView(Node from, Node to, IReadOnlyList<int> transitionIndices, bool isAnyState)
        {
            From = from;
            To = to;
            TransitionIndices = transitionIndices;
            IsAnyState = isAnyState;

            // Lower layer = drawn behind the nodes.
            layer = -1;
            // The Entry edge (no indices) just shows the initial state, so it can't be deleted.
            capabilities = transitionIndices.Count > 0
                ? Capabilities.Selectable | Capabilities.Deletable
                : Capabilities.Selectable;
            style.position = Position.Absolute;

            generateVisualContent += OnGenerateVisualContent;

            // Nodes fire GeometryChangedEvent when they are dragged or first laid out.
            from.RegisterCallback<GeometryChangedEvent>(_ => UpdateGeometry());
            to.RegisterCallback<GeometryChangedEvent>(_ => UpdateGeometry());
            RegisterCallback<AttachToPanelEvent>(_ => UpdateGeometry());
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        // SelectionDragger ignores elements that can't move, so without this the click falls
        // through to RectangleSelector and selects every edge around the click point.
        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            GraphView graphView = GetFirstAncestorOfType<GraphView>();
            if (graphView == null)
            {
                return;
            }

            // Shift / Ctrl toggles this edge, a plain click selects only this edge.
            if (evt.shiftKey || evt.actionKey)
            {
                if (selected)
                {
                    graphView.RemoveFromSelection(this);
                }
                else
                {
                    graphView.AddToSelection(this);
                }
            }
            else
            {
                graphView.ClearSelection();
                graphView.AddToSelection(this);
            }

            evt.StopPropagation();
        }

        private void UpdateGeometry()
        {
            if (parent == null || From.parent == null || To.parent == null)
            {
                return;
            }

            // Node rects in the same space this element is positioned in.
            Rect fromRect = From.parent.ChangeCoordinatesTo(parent, From.layout);
            Rect toRect = To.parent.ChangeCoordinatesTo(parent, To.layout);

            // Before the first layout pass the size is NaN.
            if (float.IsNaN(fromRect.width) || float.IsNaN(toRect.width))
            {
                return;
            }

            Vector2 direction = toRect.center - fromRect.center;
            if (direction.sqrMagnitude < 1f)
            {
                return;
            }

            direction.Normalize();
            Vector2 side = new Vector2(-direction.y, direction.x) * SideOffset;

            Vector2 start = PointOnBorder(fromRect, direction) + side;
            Vector2 end = PointOnBorder(toRect, -direction) + side;

            // This element only covers the line, so clicks elsewhere reach the nodes/background.
            Rect bounds = Rect.MinMaxRect(
                Mathf.Min(start.x, end.x) - BoundsPadding,
                Mathf.Min(start.y, end.y) - BoundsPadding,
                Mathf.Max(start.x, end.x) + BoundsPadding,
                Mathf.Max(start.y, end.y) + BoundsPadding);

            style.left = bounds.x;
            style.top = bounds.y;
            style.width = bounds.width;
            style.height = bounds.height;

            _start = start - bounds.position;
            _end = end - bounds.position;
            MarkDirtyRepaint();
        }

        // Where a ray from the rect's center in this direction leaves the rect.
        private static Vector2 PointOnBorder(Rect rect, Vector2 direction)
        {
            float toSide = direction.x != 0f
                ? rect.width * 0.5f / Mathf.Abs(direction.x)
                : float.PositiveInfinity;
            float toTopBottom = direction.y != 0f
                ? rect.height * 0.5f / Mathf.Abs(direction.y)
                : float.PositiveInfinity;

            return rect.center + direction * Mathf.Min(toSide, toTopBottom);
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Vector2 line = _end - _start;
            if (line.sqrMagnitude < 1f)
            {
                return;
            }

            Color color = selected ? SelectedColor : NormalColor;
            Painter2D painter = context.painter2D;

            painter.strokeColor = color;
            painter.lineWidth = LineWidth;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(_start);
            painter.LineTo(_end);
            painter.Stroke();

            Vector2 direction = line.normalized;
            // On lines too short for the full distance, stop at the middle so the arrow stays on the line.
            float distanceFromStart = Mathf.Min(ArrowDistanceFromStart, line.magnitude * 0.5f);
            Vector2 middle = _start + direction * distanceFromStart;

            if (TransitionIndices.Count > 1)
            {
                // Several transitions A -> B: three arrows, like Animator.
                DrawArrow(painter, color, middle - direction * ArrowLength, direction);
                DrawArrow(painter, color, middle, direction);
                DrawArrow(painter, color, middle + direction * ArrowLength, direction);
            }
            else
            {
                DrawArrow(painter, color, middle, direction);
            }
        }

        private static void DrawArrow(Painter2D painter, Color color, Vector2 center, Vector2 direction)
        {
            Vector2 side = new Vector2(-direction.y, direction.x) * ArrowHalfWidth;
            Vector2 tip = center + direction * (ArrowLength * 0.5f);
            Vector2 back = center - direction * (ArrowLength * 0.5f);

            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(back + side);
            painter.LineTo(back - side);
            painter.ClosePath();
            painter.Fill();
        }

        // Only count clicks near the line, not the whole bounding box.
        public override bool ContainsPoint(Vector2 localPoint)
        {
            Vector2 line = _end - _start;
            float t = line.sqrMagnitude > 0f
                ? Mathf.Clamp01(Vector2.Dot(localPoint - _start, line) / line.sqrMagnitude)
                : 0f;

            Vector2 closest = _start + line * t;
            return (localPoint - closest).sqrMagnitude <= ClickDistance * ClickDistance;
        }

        // Used by RectangleSelector (rect is in local space). The default checks the bounding
        // box, which for a diagonal line covers a lot of empty space.
        public override bool Overlaps(Rect rectangle)
        {
            // Liang-Barsky: clip the line against the rect, overlapping if anything is left.
            Vector2 line = _end - _start;
            float enter = 0f;
            float exit = 1f;

            bool Clip(float denominator, float numerator)
            {
                if (denominator == 0f)
                {
                    // Parallel to this side: inside only if on the inner side of it.
                    return numerator >= 0f;
                }

                float t = numerator / denominator;
                if (denominator < 0f)
                {
                    if (t > exit)
                    {
                        return false;
                    }

                    enter = Mathf.Max(enter, t);
                }
                else
                {
                    if (t < enter)
                    {
                        return false;
                    }

                    exit = Mathf.Min(exit, t);
                }

                return true;
            }

            return Clip(-line.x, _start.x - rectangle.xMin)
                && Clip(line.x, rectangle.xMax - _start.x)
                && Clip(-line.y, _start.y - rectangle.yMin)
                && Clip(line.y, rectangle.yMax - _start.y);
        }

        public override void OnSelected()
        {
            base.OnSelected();
            MarkDirtyRepaint();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            MarkDirtyRepaint();
        }
    }
}

#endif
