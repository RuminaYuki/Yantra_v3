using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// ต้องอยู่ในโฟลเดอร์ Editor
public static class ReceiveGIProbesTool
{
    private const int MaxMissingLogs = 30;

    [MenuItem("Tools/Lighting/Receive GI → Light Probes (ทั้งฉาก)")]
    private static void Run()
    {
        int changed = 0, already = 0, missing = 0;

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                    if ((flags & StaticEditorFlags.ContributeGI) == 0) continue;

                    if (IsMissingAttributes(r))
                    {
                        if (missing < MaxMissingLogs)
                            Debug.LogWarning($"[ReceiveGI] '{r.name}' mesh ไม่มี normal หรือ UV — bake แสงให้ชิ้นนี้ไม่ได้", r);
                        missing++;
                    }

                    if (r.receiveGI == ReceiveGI.LightProbes)
                    {
                        already++;
                        continue;
                    }

                    Undo.RecordObject(r, "Receive GI → Light Probes");
                    r.receiveGI = ReceiveGI.LightProbes;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                    changed++;
                }
            }
        }

        Debug.Log($"[ReceiveGI] เปลี่ยนเป็น Light Probes {changed} ชิ้น (เป็นอยู่แล้ว {already} ชิ้น) — mesh มีปัญหา {missing} ชิ้น");
    }

    private static bool IsMissingAttributes(MeshRenderer r)
    {
        var filter = r.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null) return true;

        return !mesh.HasVertexAttribute(VertexAttribute.Normal)
            || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0);
    }
}
