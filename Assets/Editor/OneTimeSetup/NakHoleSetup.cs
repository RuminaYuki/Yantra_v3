#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

/// <summary>
/// ใส่หลุม VFX_OccultHole (ของคนทำ VFX) ให้ผีนาค — กดครั้งเดียว (กดซ้ำได้ สร้างใหม่ทับ)
///   1. หา prefab VFX_OccultHole — ถ้ายังไม่มี จะเอาตัวในฉากที่เปิดอยู่มาทำเป็น prefab ให้ (ไม่แตะของในฉาก)
///   2. ใส่หลุมใต้ EnemyController ของ PNak TypeA — TypeB เป็น Variant ได้ไปด้วยเอง
///   3. ซ่อน Decal Projector หลุมเก่า (Fade Factor = 0) — ไม่ลบ เพราะ table ยังสั่ง fade มันอยู่
///   4. ใส่ NakHoleLink ให้หลุมเปลี่ยนตามท่ามือ (Idle / Charging / Activated)
/// ไม่แก้ table / ไฟล์ของคนทำ VFX
/// </summary>
public static class NakHoleSetup
{
    private const string NakPrefabPath = "Assets/Prefab/Character/Enemy/Boss/Pret/PNak TypeA(PretMinnion).prefab";
    private const string HoleFolder = "Assets/Prefab/Character/Enemy/Boss/Pret/Data";
    private const string HoleName = "VFX_OccultHole";
    private const string OldDecalName = "Decal Projector";

    [MenuItem("Tools/Yantra/Setup (ใช้ครั้งเดียว)/Setup Nak Hole (หลุม VFX ให้ PNak A + B)")]
    private static void Run()
    {
        var report = new StringBuilder();
        GameObject holePrefab = FindOrMakeHolePrefab(report);
        if (holePrefab == null)
        {
            EditorUtility.DisplayDialog("Nak Hole", report.ToString(), "OK");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(NakPrefabPath);
        try
        {
            Transform ctrl = root.transform.Find("EnemyController");
            if (ctrl == null)
            {
                report.AppendLine("✗ PNak TypeA ไม่มีลูกชื่อ EnemyController — ไม่ได้ใส่หลุม");
                EditorUtility.DisplayDialog("Nak Hole", report.ToString(), "OK");
                return;
            }

            Transform old = ctrl.Find(HoleName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var hole = (GameObject)PrefabUtility.InstantiatePrefab(holePrefab, ctrl);
            hole.name = HoleName;
            hole.transform.localPosition = Vector3.zero;   // ตรงจุดที่มือโผล่ — จมไป / ลอยไป ปรับ Position Y ตัวนี้
            hole.transform.localRotation = Quaternion.identity;

            var holeCtrl = hole.GetComponent<HoleStateController>();
            if (holeCtrl == null)
            {
                report.AppendLine("✗ VFX_OccultHole ไม่มี HoleStateController");
                EditorUtility.DisplayDialog("Nak Hole", report.ToString(), "OK");
                return;
            }
            holeCtrl.selectedState = HoleStateController.HoleState.Idle;

            // หลุมเก่า: ซ่อนด้วย Fade Factor (ตัว fader เปิด projector กลับเองทุกครั้งที่ fade ปิด component ไม่อยู่)
            Transform oldDecal = ctrl.Find(OldDecalName);
            if (oldDecal != null && oldDecal.TryGetComponent(out DecalProjector projector))
            {
                projector.fadeFactor = 0f;
                report.AppendLine("✓ ซ่อน Decal Projector หลุมเก่า (Fade Factor = 0) — อยากได้คืนตั้งกลับเป็น 1");
            }

            var link = ctrl.GetComponent<NakHoleLink>();
            if (link == null) link = ctrl.gameObject.AddComponent<NakHoleLink>();
            var so = new SerializedObject(link);
            so.FindProperty("_hole").objectReferenceValue = holeCtrl;
            so.FindProperty("_animator").objectReferenceValue = ctrl.GetComponent<Animator>();
            so.FindProperty("_grab").objectReferenceValue = root.GetComponentInChildren<GrabQTEInteractAddition>(true);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, NakPrefabPath);
            report.Insert(0, "✓ ใส่หลุม VFX_OccultHole ให้ PNak TypeA แล้ว (TypeB ได้ด้วย)\n" +
                             "✓ มือโผล่ = หลุมสะสมพลัง / จับผู้เล่น = ควันระเบิด / มุดกลับ = หลุมนิ่ง\n");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[NakHole]\n{report}");
        EditorUtility.DisplayDialog("Nak Hole", report.ToString(), "OK");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(NakPrefabPath);
    }

    private static GameObject FindOrMakeHolePrefab(StringBuilder report)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{HoleName} t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null && go.name == HoleName && go.GetComponent<HoleStateController>() != null)
            {
                report.AppendLine($"✓ ใช้ prefab หลุม: {path}");
                return go;
            }
        }

        // ยังไม่มี prefab → หาในฉากที่เปิดอยู่
        GameObject source = null;
        for (int i = 0; i < SceneManager.sceneCount && source == null; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject top in scene.GetRootGameObjects())
            {
                foreach (HoleStateController c in top.GetComponentsInChildren<HoleStateController>(true))
                {
                    if (c.gameObject.name != HoleName) continue;
                    source = c.gameObject;
                    break;
                }
                if (source != null) break;
            }
        }

        if (source == null)
        {
            report.AppendLine($"✗ ไม่เจอ {HoleName} — เปิดฉากที่มี {HoleName} อยู่ แล้วกดเมนูนี้อีกที");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(HoleFolder))
            AssetDatabase.CreateFolder("Assets/Prefab/Character/Enemy/Boss/Pret", "Data");

        string prefabPath = $"{HoleFolder}/{HoleName}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, prefabPath, out bool ok);
        if (!ok || saved == null)
        {
            report.AppendLine($"✗ ทำ prefab จาก {HoleName} ในฉากไม่สำเร็จ");
            return null;
        }

        // ตำแหน่งในฉากติดมาด้วย → ล้างให้เป็น 0 (ตัวใน PNak ตั้งเองอีกที)
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            contents.transform.localPosition = Vector3.zero;
            contents.transform.localRotation = Quaternion.identity;
            var c = contents.GetComponent<HoleStateController>();
            if (c != null) c.selectedState = HoleStateController.HoleState.Idle;
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        report.AppendLine($"✓ ทำ prefab หลุมจากตัวในฉาก ({source.scene.name}) → {prefabPath} (ของในฉากไม่โดนแตะ)");
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }
}
#endif
