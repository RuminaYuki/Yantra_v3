using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yantra.TerrainTools
{
    internal static class TTTerrain
    {
        public static Terrain[] SelectedOrAll()
        {
            var list = new List<Terrain>();
            foreach (GameObject go in Selection.gameObjects)
            {
                var t = go.GetComponent<Terrain>();
                if (t != null) list.Add(t);
            }
            return list.Count > 0 ? list.ToArray() : Terrain.activeTerrains;
        }

        public static string TargetLabel()
        {
            int selected = 0;
            foreach (GameObject go in Selection.gameObjects)
                if (go.GetComponent<Terrain>() != null) selected++;
            return selected > 0
                ? "ทำกับ Terrain ที่เลือกอยู่ " + selected + " ชิ้น"
                : "ไม่ได้เลือก Terrain ไว้ จะทำกับ Terrain ทั้งฉาก (" + Terrain.activeTerrains.Length + " ชิ้น)";
        }

        public static bool RaycastTerrain(Ray ray, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            bool found = false;
            float best = float.MaxValue;
            foreach (Terrain t in Terrain.activeTerrains)
            {
                var col = t.GetComponent<TerrainCollider>();
                if (col == null || !col.enabled) continue;
                RaycastHit h;
                if (col.Raycast(ray, out h, 100000f) && h.distance < best)
                {
                    best = h.distance;
                    hit = h;
                    found = true;
                }
            }
            return found;
        }

        public static bool Raycast(Ray ray, bool terrainOnly, Transform ignore, out RaycastHit hit)
        {
            if (terrainOnly) return RaycastTerrain(ray, out hit);

            RaycastHit[] hits = Physics.RaycastAll(ray, 100000f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
                hit = h;
                return true;
            }
            hit = default(RaycastHit);
            return false;
        }

        public static bool GroundBelow(Vector3 position, bool terrainOnly, Transform ignore, out RaycastHit hit)
        {
            var ray = new Ray(position + Vector3.up * 500f, Vector3.down);
            return Raycast(ray, terrainOnly, ignore, out hit);
        }

        public static CharacterController FindPlayer()
        {
            foreach (CharacterController cc in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude))
            {
                if (cc.CompareTag("Player") || cc.transform.root.CompareTag("Player"))
                    return cc;
            }
            return null;
        }

        public static string PrototypeName(DetailPrototype p)
        {
            if (p.usePrototypeMesh) return p.prototype != null ? p.prototype.name : "(ไม่มีโมเดล)";
            return p.prototypeTexture != null ? p.prototypeTexture.name + " (รูปแบน)" : "(ไม่มีรูป)";
        }

        public static bool SamePrototype(DetailPrototype a, DetailPrototype b)
        {
            if (a.usePrototypeMesh != b.usePrototypeMesh) return false;
            return a.usePrototypeMesh ? a.prototype == b.prototype : a.prototypeTexture == b.prototypeTexture;
        }

        public static Transform FindOrCreateRoot(string name)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;

            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "สร้าง " + name);
            return root.transform;
        }

        public static Transform FindRoot(string name)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;
            return null;
        }

        public static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "สร้าง " + name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
