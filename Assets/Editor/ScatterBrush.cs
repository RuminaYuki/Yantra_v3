using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yantra.TerrainTools
{
    internal static class ScatterBrush
    {
        public const string RootName = "_Scatter";
        const string GroupPrefix = "Scatter_";

        public static bool Active;
        public static ScatterPreset Preset;

        static readonly int Hint = "YantraScatterBrush".GetHashCode();
        static readonly List<Vector3> Placed = new List<Vector3>();
        static Vector3 _lastStamp;
        static int _undoGroup;

        public static void OnSceneGUI(SceneView sv)
        {
            if (!Active || Preset == null) return;

            Event e = Event.current;
            int id = GUIUtility.GetControlID(Hint, FocusType.Passive);
            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(id);
                return;
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Active = false;
                e.Use();
                TerrainToolsWindow.RepaintOpen();
                return;
            }

            if (e.type == EventType.ScrollWheel && e.control)
            {
                Undo.RecordObject(Preset, "ขนาดแปรง");
                Preset.radius = Mathf.Clamp(Preset.radius * (e.delta.y > 0f ? 0.9f : 1.1f), 0.5f, 60f);
                EditorUtility.SetDirty(Preset);
                TerrainToolsWindow.RepaintOpen();
                e.Use();
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;
            Transform root = TTTerrain.FindRoot(RootName);
            if (!TTTerrain.Raycast(ray, Preset.terrainOnly, root, out hit)) return;

            bool erase = e.shift;
            if (e.type == EventType.Repaint) DrawCursor(hit, erase);
            if (e.type == EventType.MouseMove) sv.Repaint();
            if (e.alt) return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                GUIUtility.hotControl = id;
                Undo.IncrementCurrentGroup();
                _undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(erase ? "ลบของด้วยแปรง" : "วางของด้วยแปรง");
                CollectExisting();
                Stamp(hit.point, erase);
                _lastStamp = hit.point;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id)
            {
                if (Vector3.Distance(hit.point, _lastStamp) >= Preset.spacing)
                {
                    Stamp(hit.point, erase);
                    _lastStamp = hit.point;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
            {
                GUIUtility.hotControl = 0;
                Undo.CollapseUndoOperations(_undoGroup);
                e.Use();
            }
        }

        static void DrawCursor(RaycastHit hit, bool erase)
        {
            Color c = erase ? TTGui.Danger : TTGui.Accent;
            Handles.color = new Color(c.r, c.g, c.b, 0.12f);
            Handles.DrawSolidDisc(hit.point, hit.normal, Preset.radius);
            Handles.color = c;
            Handles.DrawWireDisc(hit.point, hit.normal, Preset.radius, 2f);
            Handles.Label(hit.point + Vector3.up * 0.5f, erase ? "  ลบ" : "  วาง");
        }

        static void CollectExisting()
        {
            Placed.Clear();
            Transform root = TTTerrain.FindRoot(RootName);
            if (root == null) return;
            foreach (Transform group in root)
                foreach (Transform child in group)
                    Placed.Add(child.position);
        }

        static List<ScatterPreset.Item> UsableItems(out float totalWeight)
        {
            var list = new List<ScatterPreset.Item>();
            totalWeight = 0f;
            foreach (ScatterPreset.Item item in Preset.items)
            {
                if (item == null || !item.enabled || item.prefab == null) continue;
                list.Add(item);
                totalWeight += item.weight;
            }
            return list;
        }

        static void Stamp(Vector3 center, bool erase)
        {
            if (erase)
            {
                EraseAt(center);
                return;
            }

            float total;
            List<ScatterPreset.Item> items = UsableItems(out total);
            if (items.Count == 0) return;

            Transform root = TTTerrain.FindOrCreateRoot(RootName);
            for (int i = 0; i < Preset.perStamp; i++)
            {
                Vector2 r = Random.insideUnitCircle * Preset.radius;
                Vector3 p = center + new Vector3(r.x, 0f, r.y);

                RaycastHit ground;
                if (!TTTerrain.GroundBelow(p, Preset.terrainOnly, root, out ground)) continue;

                float slope = Vector3.Angle(ground.normal, Vector3.up);
                if (slope > Preset.maxSlope) continue;
                if (TooClose(ground.point)) continue;

                ScatterPreset.Item item = Pick(items, total);
                var go = PrefabUtility.InstantiatePrefab(item.prefab) as GameObject;
                if (go == null) continue;
                Undo.RegisterCreatedObjectUndo(go, "วางของ");

                Transform group = TTTerrain.FindOrCreateChild(root, GroupPrefix + item.prefab.name);
                go.transform.SetParent(group, true);

                float yaw = Preset.randomYaw ? Random.Range(0f, 360f) : 0f;
                Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, ground.normal), Preset.alignToGround);
                go.transform.rotation = tilt * Quaternion.Euler(0f, yaw, 0f) * item.prefab.transform.rotation;

                float s = Random.Range(Mathf.Min(Preset.scale.x, Preset.scale.y), Mathf.Max(Preset.scale.x, Preset.scale.y));
                go.transform.localScale = item.prefab.transform.localScale * s;

                float sink = Preset.sink;
                if (Preset.slopeSink)
                    sink += Preset.baseRadius * s * Mathf.Tan(Mathf.Min(slope, 60f) * Mathf.Deg2Rad);
                go.transform.position = ground.point - Vector3.up * sink;

                Placed.Add(go.transform.position);
            }
        }

        static bool TooClose(Vector3 p)
        {
            if (Preset.minDistance <= 0f) return false;
            float min2 = Preset.minDistance * Preset.minDistance;
            foreach (Vector3 q in Placed)
            {
                float dx = q.x - p.x, dz = q.z - p.z;
                if (dx * dx + dz * dz < min2) return true;
            }
            return false;
        }

        static ScatterPreset.Item Pick(List<ScatterPreset.Item> items, float total)
        {
            float roll = Random.value * total;
            foreach (ScatterPreset.Item item in items)
            {
                roll -= item.weight;
                if (roll <= 0f) return item;
            }
            return items[items.Count - 1];
        }

        static void EraseAt(Vector3 center)
        {
            Transform root = TTTerrain.FindRoot(RootName);
            if (root == null) return;

            var names = new HashSet<string>();
            foreach (ScatterPreset.Item item in Preset.items)
                if (item != null && item.enabled && item.prefab != null)
                    names.Add(GroupPrefix + item.prefab.name);

            float r2 = Preset.radius * Preset.radius;
            foreach (Transform group in root)
            {
                if (!names.Contains(group.name)) continue;
                for (int i = group.childCount - 1; i >= 0; i--)
                {
                    Transform c = group.GetChild(i);
                    float dx = c.position.x - center.x, dz = c.position.z - center.z;
                    if (dx * dx + dz * dz <= r2)
                        Undo.DestroyObjectImmediate(c.gameObject);
                }
            }
        }
    }
}
