using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// ต้องอยู่ในโฟลเดอร์ Editor
// ไม่ได้แก้อะไรในฉาก แค่รายงานว่าชิ้นที่ติ๊ก Contribute GI กินพื้นที่กว้างแค่ไหน
public static class BakeAreaReport
{
    private const int TopCount = 10;

    private struct Item
    {
        public Object Target;
        public string Name;
        public Bounds Bounds;
    }

    [MenuItem("Tools/Lighting/รายงานขนาดพื้นที่ Bake")]
    private static void Run()
    {
        var items = new List<Item>();

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (!r.enabled || !HasContributeGI(r.gameObject)) continue;
                    items.Add(new Item { Target = r, Name = PathOf(r.transform), Bounds = r.bounds });
                }

                foreach (Terrain t in root.GetComponentsInChildren<Terrain>(false))
                {
                    if (!t.enabled || t.terrainData == null || !HasContributeGI(t.gameObject)) continue;
                    Vector3 size = t.terrainData.size;
                    items.Add(new Item { Target = t, Name = PathOf(t.transform) + " (Terrain)", Bounds = new Bounds(t.GetPosition() + size * 0.5f, size) });
                }
            }
        }

        if (items.Count == 0)
        {
            Debug.LogWarning("[BakeArea] ไม่มีชิ้นไหนติ๊ก Contribute GI เลย");
            return;
        }

        Bounds total = items[0].Bounds;
        foreach (var item in items) total.Encapsulate(item.Bounds);

        Vector3 middle = MedianCenter(items);

        Debug.Log($"[BakeArea] ชิ้นที่ติ๊ก Contribute GI: {items.Count} ชิ้น\n" +
                  $"พื้นที่รวม กว้าง {total.size.x:0} × สูง {total.size.y:0} × ลึก {total.size.z:0} เมตร\n" +
                  $"จุดกลางของฉาก (ประมาณ) {middle}");

        foreach (var item in items.OrderByDescending(i => Vector3.Distance(i.Bounds.center, middle)).Take(TopCount))
        {
            float dist = Vector3.Distance(item.Bounds.center, middle);
            Debug.Log($"[BakeArea] ไกลจากกลางฉาก {dist:0} ม. — {item.Name}", item.Target);
        }

        foreach (var item in items.OrderByDescending(i => i.Bounds.size.magnitude).Take(TopCount))
        {
            Vector3 size = item.Bounds.size;
            Debug.Log($"[BakeArea] ใหญ่ {size.x:0} × {size.y:0} × {size.z:0} ม. — {item.Name}", item.Target);
        }
    }

    private static bool HasContributeGI(GameObject go)
    {
        return (GameObjectUtility.GetStaticEditorFlags(go) & StaticEditorFlags.ContributeGI) != 0;
    }

    // ใช้ median ไม่ใช่ค่าเฉลี่ย — ของไกลๆ ชิ้นเดียวจะได้ไม่ดึงจุดกลางเพี้ยน
    private static Vector3 MedianCenter(List<Item> items)
    {
        float Median(IEnumerable<float> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[sorted.Count / 2];
        }

        return new Vector3(
            Median(items.Select(i => i.Bounds.center.x)),
            Median(items.Select(i => i.Bounds.center.y)),
            Median(items.Select(i => i.Bounds.center.z)));
    }

    private static string PathOf(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
