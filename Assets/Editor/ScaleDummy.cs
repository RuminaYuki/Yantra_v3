using UnityEditor;
using UnityEngine;

namespace Yantra.TerrainTools
{
    // หุ่นเทียบสเกล — tag EditorOnly ไม่ติดไปใน Build และไม่มี Collider ไม่ขวางตอนกด Play
    internal static class ScaleDummy
    {
        const string Prefix = "ScaleRef_";

        public static float PlayerHeight()
        {
            CharacterController cc = TTTerrain.FindPlayer();
            return cc != null ? cc.height * cc.transform.lossyScale.y : 1.8f;
        }

        public static GameObject Create(float height)
        {
            Vector3 pos = Vector3.zero;
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                pos = sv.pivot;
                RaycastHit hit;
                if (TTTerrain.GroundBelow(sv.pivot, true, null, out hit)) pos = hit.point;
            }

            var root = new GameObject(Prefix + height.ToString("0.0") + "m");
            root.tag = "EditorOnly";
            root.transform.position = pos;

            AddPart(root.transform, PrimitiveType.Capsule, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.45f, height * 0.5f, 0.45f));
            AddPart(root.transform, PrimitiveType.Cube, new Vector3(0f, height * 0.93f, 0.2f), new Vector3(0.2f, 0.06f, 0.12f));

            Undo.RegisterCreatedObjectUndo(root, "วางหุ่นเทียบสเกล");
            Selection.activeGameObject = root;
            return root;
        }

        static void AddPart(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.tag = "EditorOnly";
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
        }

        public static int Count()
        {
            int n = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.parent == null && t.name.StartsWith(Prefix)) n++;
            return n;
        }

        public static int DeleteAll()
        {
            int n = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null || t.parent != null || !t.name.StartsWith(Prefix)) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
                n++;
            }
            return n;
        }
    }
}
