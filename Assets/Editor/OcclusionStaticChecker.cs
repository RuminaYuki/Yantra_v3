// วางไฟล์นี้ใน Assets/Editor/ (ต้องอยู่ในโฟลเดอร์ชื่อ Editor)
// เปิดใช้: Tools > Yantra > Occlusion Static Checker
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class OcclusionStaticChecker : EditorWindow
{
    [MenuItem("Tools/Yantra/Occlusion Static Checker")]
    static void Open() => GetWindow<OcclusionStaticChecker>("Occlusion Checker");

    const StaticEditorFlags Occ = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
    const int MaxRows = 100;

    enum FixKind { ClearOcclusion, ClearOccluder, AddOccluderAndOccludee, AddOccludee, ReceiveGIProbes, TerrainTreeProbesOff }

    class Item { public GameObject go; public string reason; }

    class Category
    {
        public string title, help, fixLabel;
        public MessageType level;
        public FixKind fix;
        public bool open = true;
        public bool groupByReason;
        public List<Item> items = new List<Item>();
    }

    List<Category> cats = new List<Category>();
    bool onlySelection;
    float bigSize = 5f;
    Vector2 scroll;
    bool scanned;
    int occluderCount, occludeeCount;

    Dictionary<Transform, string> moverCache, scriptCache;

    // ---------------- Scan ----------------

    void Scan()
    {
        moverCache = new Dictionary<Transform, string>();
        scriptCache = new Dictionary<Transform, string>();
        occluderCount = occludeeCount = 0;

        var cMover = Cat("1. ขยับได้ แต่ติ๊ก Static   [ต้องแก้]",
            "ของพวกนี้ขยับได้ (Rigidbody / Animator / NavMeshAgent / CharacterController) ถ้าเป็น Static พอขยับแล้วจะหายหรือโผล่ผิดที่",
            MessageType.Error, FixKind.ClearOcclusion, "เอา Occluder/Occludee ออก", true);
        var cLeaf = Cat("2. ของโปร่ง/ใบไม้ เป็นตัวบัง (Occluder)   [ต้องแก้]",
            "material มีรูโปร่ง (alpha) ถ้าเป็นตัวบัง ของข้างหลังจะหายทั้งที่ควรมองเห็น",
            MessageType.Error, FixKind.ClearOccluder, "เอา Occluder ออก");
        var cScript = Cat("3. มีสคริปต์ + ติ๊ก Static   [เช็คเอง]",
            "สคริปต์อาจทำให้ของขยับหรือถูกหยิบได้ ถ้าขยับ/หยิบได้จริง ให้กดแก้ ถ้าอยู่กับที่ (เช่น สคริปต์เล่นเสียง) ปล่อยไว้ได้",
            MessageType.Warning, FixKind.ClearOcclusion, "เอา Occluder/Occludee ออก", true);
        var cGI = Cat("4. ของโปร่ง/พืช ใช้ Lightmap   [ทำให้ bake แสงช้า]",
            "พืชควรรับแสงจาก Light Probes แทน หน้าตาแทบไม่ต่าง แต่ bake แสงเร็วขึ้นมาก",
            MessageType.Warning, FixKind.ReceiveGIProbes, "เปลี่ยนเป็น Light Probes");
        var cTerrain = Cat("5. Terrain สร้าง Light Probe ทุกต้นไม้   [ทำให้ bake แสงพัง]",
            "Bake Light Probes For Trees เปิดอยู่ ทุกต้นบน Terrain (รวมหญ้า/หิน) จะได้ probe ของตัวเอง ใช้ APV อยู่แล้วไม่จำเป็น",
            MessageType.Warning, FixKind.TerrainTreeProbesOff, "ปิด Bake Light Probes For Trees");
        var cBig = Cat($"6. ของใหญ่ทึบ (≥ {bigSize:0.#} m) ยังไม่เป็นตัวบัง   [แนะนำ]",
            "ของใหญ่และไม่โปร่ง เหมาะเป็น Occluder ดูรายชื่อก่อนกด ถ้าอันไหนมีช่องโปร่ง/หน้าต่างทะลุ ไม่ต้องแก้อันนั้น",
            MessageType.Info, FixKind.AddOccluderAndOccludee, "ใส่ Occluder + Occludee");
        var cMissing = Cat("7. ยังไม่ได้ติ๊ก Occludee   [แนะนำ]",
            "ของอยู่กับที่ที่ยังไม่ถูกซ่อนตอนอยู่หลังของใหญ่ ติ๊ก Occludee แล้วจะถูกซ่อนอัตโนมัติ",
            MessageType.Info, FixKind.AddOccludee, "ใส่ Occludee");

        cats = new List<Category> { cMover, cLeaf, cScript, cGI, cTerrain, cBig, cMissing };

        foreach (var g in AllObjects())
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(g);
            bool isOccluder = (flags & StaticEditorFlags.OccluderStatic) != 0;
            bool isOccludee = (flags & StaticEditorFlags.OccludeeStatic) != 0;
            if (isOccluder) occluderCount++;
            if (isOccludee) occludeeCount++;

            var terrain = g.GetComponent<Terrain>();
            if (terrain != null)
            {
                if (terrain.bakeLightProbesForTrees) Add(cTerrain, g, "Bake Light Probes For Trees = เปิด");
                continue;
            }

            string mover = Inherited(g.transform, SelfMover, moverCache);
            string script = Inherited(g.transform, OwnScript, scriptCache);

            if ((isOccluder || isOccludee) && mover != null)
            {
                Add(cMover, g, mover);
                continue;
            }
            if ((isOccluder || isOccludee) && script != null)
                Add(cScript, g, script);

            var r = g.GetComponent<MeshRenderer>();
            if (r == null) continue;

            bool seeThrough = r.sharedMaterials.Any(SeeThrough);

            if (isOccluder && seeThrough)
                Add(cLeaf, g, "material โปร่ง/ตัดขอบ (alpha)");

            if (seeThrough && (flags & StaticEditorFlags.ContributeGI) != 0 && r.receiveGI == ReceiveGI.Lightmaps)
                Add(cGI, g, "รับแสงจาก Lightmaps");

            if (mover != null || script != null || !g.activeInHierarchy) continue;

            var size = r.bounds.size;
            float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

            if (!seeThrough && !isOccluder && maxDim >= bigSize)
                Add(cBig, g, $"ขนาด {maxDim:0.#} m");
            else if (!isOccluder && !isOccludee)
                Add(cMissing, g, seeThrough ? "พืช/ของโปร่ง" : $"ขนาด {maxDim:0.#} m");
        }

        scanned = true;
        Repaint();
    }

    static Category Cat(string title, string help, MessageType level, FixKind fix, string fixLabel, bool group = false) =>
        new Category { title = title, help = help, level = level, fix = fix, fixLabel = fixLabel, groupByReason = group };

    static void Add(Category c, GameObject g, string reason) =>
        c.items.Add(new Item { go = g, reason = reason });

    IEnumerable<GameObject> AllObjects()
    {
        var roots = new List<GameObject>();
        if (onlySelection)
        {
            roots.AddRange(Selection.gameObjects.Where(g => g.scene.IsValid()));
        }
        else
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) roots.AddRange(s.GetRootGameObjects());
            }
        }

        var seen = new HashSet<GameObject>();
        foreach (var root in roots)
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (seen.Add(t.gameObject))
                    yield return t.gameObject;
    }

    // หาเหตุผลจากตัวเอง ถ้าไม่มีให้ดูจากพ่อขึ้นไปเรื่อย ๆ (cache ไว้ให้เร็ว)
    static string Inherited(Transform t, Func<GameObject, string> selfCheck, Dictionary<Transform, string> cache)
    {
        if (t == null) return null;
        if (cache.TryGetValue(t, out var r)) return r;

        r = selfCheck(t.gameObject);
        if (r == null && t.parent != null)
        {
            var p = Inherited(t.parent, selfCheck, cache);
            if (p != null) r = p.StartsWith("อยู่ใต้") ? p : $"อยู่ใต้ '{t.parent.name}' ที่{p}";
        }
        cache[t] = r;
        return r;
    }

    static string SelfMover(GameObject g)
    {
        if (g.GetComponent<Rigidbody>()) return "มี Rigidbody";
        if (g.GetComponent<Animator>()) return "มี Animator";
        if (g.GetComponent<Animation>()) return "มี Animation";
        if (g.GetComponent<NavMeshAgent>()) return "มี NavMeshAgent";
        if (g.GetComponent<CharacterController>()) return "มี CharacterController";
        return null;
    }

    // สคริปต์ที่อยู่ในโฟลเดอร์ Assets (ของทีม/asset pack) ไม่นับของ Unity เอง
    static string OwnScript(GameObject g)
    {
        foreach (var mb in g.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue; // missing script
            var ms = MonoScript.FromMonoBehaviour(mb);
            if (ms == null) continue;
            var path = AssetDatabase.GetAssetPath(ms);
            if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/"))
                return "มีสคริปต์ " + mb.GetType().Name;
        }
        return null;
    }

    static bool SeeThrough(Material m)
    {
        if (m == null) return false;
        if (m.renderQueue >= 2450) return true; // AlphaTest / Transparent
        if (m.IsKeywordEnabled("_ALPHATEST_ON") || m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")) return true;
        if (m.HasProperty("_AlphaCutoffEnable") && m.GetFloat("_AlphaCutoffEnable") > 0.5f) return true;
        if (m.HasProperty("_SurfaceType") && m.GetFloat("_SurfaceType") > 0.5f) return true;
        return false;
    }

    // ---------------- Fix ----------------

    void Apply(Category c, IEnumerable<Item> items)
    {
        Undo.SetCurrentGroupName("Occlusion Checker: " + c.fixLabel);
        int group = Undo.GetCurrentGroup();
        var scenes = new HashSet<Scene>();

        foreach (var it in items)
        {
            if (it.go == null) continue;
            switch (c.fix)
            {
                case FixKind.ClearOcclusion:
                    SetFlags(it.go, 0, Occ);
                    break;
                case FixKind.ClearOccluder:
                    SetFlags(it.go, 0, StaticEditorFlags.OccluderStatic);
                    break;
                case FixKind.AddOccluderAndOccludee:
                    SetFlags(it.go, Occ, 0);
                    break;
                case FixKind.AddOccludee:
                    SetFlags(it.go, StaticEditorFlags.OccludeeStatic, 0);
                    break;
                case FixKind.ReceiveGIProbes:
                    var r = it.go.GetComponent<MeshRenderer>();
                    if (r != null)
                    {
                        Undo.RecordObject(r, "Receive GI");
                        r.receiveGI = ReceiveGI.LightProbes;
                    }
                    break;
                case FixKind.TerrainTreeProbesOff:
                    var t = it.go.GetComponent<Terrain>();
                    if (t != null)
                    {
                        Undo.RecordObject(t, "Terrain Tree Probes");
                        t.bakeLightProbesForTrees = false;
                    }
                    break;
            }
            scenes.Add(it.go.scene);
        }

        foreach (var s in scenes)
            if (s.IsValid()) EditorSceneManager.MarkSceneDirty(s);

        Undo.CollapseUndoOperations(group);
        Scan();
    }

    static void SetFlags(GameObject g, StaticEditorFlags add, StaticEditorFlags remove)
    {
        var f = GameObjectUtility.GetStaticEditorFlags(g);
        var nf = (f | add) & ~remove;
        if (nf == f) return;
        Undo.RecordObject(g, "Static Flags");
        GameObjectUtility.SetStaticEditorFlags(g, nf);
    }

    // ---------------- GUI ----------------

    void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("เช็ค Static สำหรับ Occlusion Culling", EditorStyles.boldLabel);
        onlySelection = EditorGUILayout.ToggleLeft("เช็คเฉพาะ object ที่เลือกใน Hierarchy (รวมลูก ๆ)", onlySelection);
        bigSize = EditorGUILayout.FloatField("ขนาดของใหญ่ (m) = Smallest Occluder", bigSize);

        if (GUILayout.Button("สแกนฉาก", GUILayout.Height(30))) Scan();

        if (!scanned)
        {
            EditorGUILayout.HelpBox("กด 'สแกนฉาก' เพื่อเริ่ม", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"ตอนนี้มี Occluder {occluderCount} ชิ้น / Occludee {occludeeCount} ชิ้น", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("ทุกปุ่มแก้ กด Ctrl+Z ย้อนได้ / แก้แล้วอย่าลืม Ctrl+S", EditorStyles.miniLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var c in cats) DrawCategory(c);
        EditorGUILayout.EndScrollView();
    }

    void DrawCategory(Category c)
    {
        EditorGUILayout.Space(8);
        c.open = EditorGUILayout.Foldout(c.open, $"{c.title}   ({c.items.Count})", true, EditorStyles.foldoutHeader);
        if (!c.open) return;

        if (c.items.Count == 0)
        {
            EditorGUILayout.LabelField("      ✔ ไม่มี", EditorStyles.miniLabel);
            return;
        }

        EditorGUILayout.HelpBox(c.help, c.level);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("เลือกทั้งหมด"))
                Selection.objects = c.items.Where(i => i.go != null).Select(i => (UnityEngine.Object)i.go).ToArray();

            if (GUILayout.Button(c.fixLabel + " ทั้งหมด"))
            {
                if (EditorUtility.DisplayDialog("ยืนยัน",
                        $"{c.fixLabel} ทั้งหมด {c.items.Count} ชิ้น?\n(กด Ctrl+Z ย้อนได้)", "ทำเลย", "ยกเลิก"))
                {
                    Apply(c, c.items.ToList());
                    GUIUtility.ExitGUI();
                }
            }
        }

        if (c.groupByReason)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("สรุปตามสาเหตุ (ชิ้นที่สาเหตุเดียวกัน แก้ทีเดียวทั้งกลุ่มได้):", EditorStyles.miniBoldLabel);
            var groups = c.items.Where(i => i.go != null)
                                .GroupBy(i => i.reason)
                                .OrderByDescending(gr => gr.Count())
                                .ToList();
            foreach (var gr in groups)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{gr.Key}   ({gr.Count()} ชิ้น)", EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("เลือก", GUILayout.Width(50)))
                        Selection.objects = gr.Select(i => (UnityEngine.Object)i.go).ToArray();
                    if (GUILayout.Button("แก้กลุ่มนี้", GUILayout.Width(80)))
                    {
                        Apply(c, gr.ToList());
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("รายชื่อทีละชิ้น:", EditorStyles.miniBoldLabel);
        }

        int n = Mathf.Min(c.items.Count, MaxRows);
        for (int i = 0; i < n; i++)
        {
            var it = c.items[i];
            if (it.go == null) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(it.go.name, EditorStyles.linkLabel, GUILayout.Width(position.width * 0.4f)))
                {
                    Selection.activeGameObject = it.go;
                    EditorGUIUtility.PingObject(it.go);
                }
                EditorGUILayout.LabelField(it.reason, EditorStyles.miniLabel);
                if (GUILayout.Button("แก้", GUILayout.Width(40)))
                {
                    Apply(c, new[] { it });
                    GUIUtility.ExitGUI();
                }
            }
        }

        if (c.items.Count > MaxRows)
            EditorGUILayout.LabelField($"      ...และอีก {c.items.Count - MaxRows} ชิ้น (กด 'เลือกทั้งหมด' เพื่อดูใน Hierarchy)", EditorStyles.miniLabel);
    }
}
#endif
