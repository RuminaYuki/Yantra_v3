using UnityEditor;
using UnityEngine;

namespace Yantra.TerrainTools
{
    internal static class GroundSnap
    {
        public enum AlignMode { Keep, Upright, FollowGround }

        public static int SnapSelection(AlignMode align, float follow, float sink, bool slopeSink, float baseRadius, bool useBottom, bool terrainOnly)
        {
            Transform[] targets = Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable);
            Physics.SyncTransforms();

            int moved = 0;
            foreach (Transform t in targets)
            {
                if (t.GetComponent<Terrain>() != null) continue;

                RaycastHit hit;
                if (!TTTerrain.GroundBelow(t.position, terrainOnly, t, out hit)) continue;

                Undo.RecordObject(t, "ติดพื้น");

                float yaw = t.eulerAngles.y;
                if (align == AlignMode.Upright)
                    t.rotation = Quaternion.Euler(0f, yaw, 0f);
                else if (align == AlignMode.FollowGround)
                    t.rotation = Quaternion.Slerp(Quaternion.Euler(0f, yaw, 0f),
                        Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, yaw, 0f), follow);

                float bottomOffset = 0f;
                if (useBottom)
                {
                    Bounds b;
                    if (TryGetBounds(t, out b)) bottomOffset = t.position.y - b.min.y;
                }

                float slope = Vector3.Angle(hit.normal, Vector3.up);
                float extra = slopeSink ? baseRadius * Mathf.Tan(Mathf.Min(slope, 60f) * Mathf.Deg2Rad) : 0f;

                t.position = new Vector3(t.position.x, hit.point.y + bottomOffset - sink - extra, t.position.z);
                moved++;
            }
            return moved;
        }

        static bool TryGetBounds(Transform t, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        // ---------- ต้นไม้ที่ปลูกด้วย Paint Trees ----------

        public static int CountFloating(Terrain terrain, float tolerance)
        {
            TerrainData td = terrain.terrainData;
            if (td == null) return 0;

            float height = td.size.y;
            int count = 0;
            foreach (TreeInstance tree in td.treeInstances)
            {
                float ground = td.GetInterpolatedHeight(tree.position.x, tree.position.z);
                if (tree.position.y * height - ground > tolerance) count++;
            }
            return count;
        }

        public static int FixTerrainTrees(Terrain[] terrains, float sink, bool slopeSink, float baseRadius)
        {
            int changed = 0;
            foreach (Terrain terrain in terrains)
            {
                TerrainData td = terrain.terrainData;
                if (td == null) continue;

                TreeInstance[] trees = td.treeInstances;
                if (trees.Length == 0) continue;

                Undo.RegisterCompleteObjectUndo(td, "ต้นไม้ติดพื้น");
                float height = td.size.y;

                for (int i = 0; i < trees.Length; i++)
                {
                    TreeInstance tree = trees[i];
                    float ground = td.GetInterpolatedHeight(tree.position.x, tree.position.z);
                    float steep = td.GetSteepness(tree.position.x, tree.position.z);

                    float down = sink;
                    if (slopeSink)
                        down += baseRadius * tree.widthScale * Mathf.Tan(Mathf.Min(steep, 60f) * Mathf.Deg2Rad);

                    float y = Mathf.Max(0f, (ground - down) / height);
                    if (Mathf.Abs(y - tree.position.y) * height > 0.001f) changed++;

                    tree.position = new Vector3(tree.position.x, y, tree.position.z);
                    trees[i] = tree;
                }

                td.SetTreeInstances(trees, false);
                terrain.Flush();
                EditorUtility.SetDirty(td);
            }
            return changed;
        }
    }
}
