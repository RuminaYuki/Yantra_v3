using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yantra.TerrainTools
{
    // คลิกจุดตามขอบแมพ แล้วสร้างกำแพงล่องหน (BoxCollider ไม่มีหน้าตา) ให้ตลอดแนว
    internal static class WallDrawer
    {
        public const string RootName = "Boundaries";

        public static bool Active;
        public static readonly List<Vector3> Points = new List<Vector3>();
        public static float Height = 6f;
        public static float Thickness = 0.6f;
        public static float Depth = 2f;
        public static bool CloseLoop;
        public static int Layer = 2;

        static readonly int Hint = "YantraWallDrawer".GetHashCode();
        static readonly Color WallColor = new Color(1f, 0.55f, 0.15f);

        public static void OnSceneGUI(SceneView sv)
        {
            DrawExistingWalls();
            if (!Active) return;

            Event e = Event.current;
            int id = GUIUtility.GetControlID(Hint, FocusType.Passive);
            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(id);
                return;
            }

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape) { Active = false; e.Use(); TerrainToolsWindow.RepaintOpen(); return; }
                if ((e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.Delete) && Points.Count > 0) { Points.RemoveAt(Points.Count - 1); e.Use(); TerrainToolsWindow.RepaintOpen(); }
                if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && Points.Count >= 2) { Build(); e.Use(); TerrainToolsWindow.RepaintOpen(); }
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;
            bool onGround = TTTerrain.Raycast(ray, true, null, out hit) || TTTerrain.Raycast(ray, false, TTTerrain.FindRoot(RootName), out hit);

            if (e.type == EventType.Repaint) DrawPreview(onGround ? (Vector3?)hit.point : null);
            if (e.type == EventType.MouseMove) sv.Repaint();

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && onGround)
            {
                Points.Add(hit.point);
                e.Use();
                TerrainToolsWindow.RepaintOpen();
            }
        }

        static void DrawPreview(Vector3? hover)
        {
            Handles.color = WallColor;
            for (int i = 0; i < Points.Count; i++)
            {
                Vector3 p = Points[i];
                Handles.DrawLine(p, p + Vector3.up * Height, 2f);
                Handles.DotHandleCap(0, p, Quaternion.identity, HandleUtility.GetHandleSize(p) * 0.06f, EventType.Repaint);
                if (i > 0) DrawSegment(Points[i - 1], p);
            }
            if (CloseLoop && Points.Count > 2) DrawSegment(Points[Points.Count - 1], Points[0]);

            if (hover.HasValue && Points.Count > 0)
            {
                Handles.color = new Color(WallColor.r, WallColor.g, WallColor.b, 0.5f);
                DrawSegment(Points[Points.Count - 1], hover.Value);
            }
        }

        static void DrawSegment(Vector3 a, Vector3 b)
        {
            Handles.DrawLine(a, b, 2f);
            Handles.DrawLine(a + Vector3.up * Height, b + Vector3.up * Height, 2f);
        }

        static void DrawExistingWalls()
        {
            if (Event.current.type != EventType.Repaint) return;
            Transform root = TTTerrain.FindRoot(RootName);
            if (root == null) return;

            Handles.color = new Color(WallColor.r, WallColor.g, WallColor.b, 0.7f);
            Matrix4x4 prev = Handles.matrix;
            foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>())
            {
                Handles.matrix = box.transform.localToWorldMatrix;
                Handles.DrawWireCube(box.center, box.size);
            }
            Handles.matrix = prev;
        }

        public static void Build()
        {
            if (Points.Count < 2) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("สร้างกำแพงล่องหน");

            Transform root = TTTerrain.FindOrCreateRoot(RootName);
            var line = new GameObject("InvisibleWall_" + (root.childCount + 1));
            Undo.RegisterCreatedObjectUndo(line, "สร้างกำแพงล่องหน");
            line.transform.SetParent(root, false);

            int segments = CloseLoop && Points.Count > 2 ? Points.Count : Points.Count - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = Points[i];
                Vector3 b = Points[(i + 1) % Points.Count];
                Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                if (flat.sqrMagnitude < 0.0001f) continue;

                float bottom = Mathf.Min(a.y, b.y) - Depth;
                float top = Mathf.Max(a.y, b.y) + Height;

                var seg = new GameObject("Wall_" + (i + 1));
                Undo.RegisterCreatedObjectUndo(seg, "สร้างกำแพงล่องหน");
                seg.layer = Layer;
                seg.transform.SetParent(line.transform, false);
                seg.transform.position = new Vector3((a.x + b.x) * 0.5f, (bottom + top) * 0.5f, (a.z + b.z) * 0.5f);
                seg.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

                var box = seg.AddComponent<BoxCollider>();
                box.size = new Vector3(Thickness, top - bottom, flat.magnitude + Thickness);
            }

            Undo.CollapseUndoOperations(group);
            Points.Clear();
            Selection.activeGameObject = line;
        }
    }
}
