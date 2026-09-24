// Assets/Editor/ShadowCasterTool.cs
// Editor-only. ไม่ถูกรวมใน build และไม่แตะไฟล์ของทีม
// เปิดใช้: Tools → Perf → Shadow Caster Tool

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class ShadowCasterTool : EditorWindow
{
    // ── การตั้งค่า ──────────────────────────────────────────────
    private bool includeChildren = true;
    private bool skipSkinnedMesh = true;      // กันไม่ให้โดนตัวละคร
    private bool skipTaggedPlayer = true;     // กัน tag Player

    private Transform distanceOrigin;         // จุดอ้างอิง (ลาก Player มาใส่)
    private float minDistance = 30f;          // ไกลกว่านี้ = ปิดเงา

    private float maxBoundsSize = 1.5f;       // เล็กกว่านี้ = ปิดเงา

    private Vector2 scroll;
    private string lastReport = "";

    [MenuItem("Tools/Perf/Shadow Caster Tool")]
    private static void Open()
    {
        var w = GetWindow<ShadowCasterTool>("Shadow Casters");
        w.minSize = new Vector2(340, 480);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        // ── สถานะปัจจุบัน ───────────────────────────────────────
        EditorGUILayout.LabelField("สถานะซีน", EditorStyles.boldLabel);
        int live = CountActiveCasters();
        EditorGUILayout.HelpBox($"Shadow casters ที่เปิดอยู่: {live}", MessageType.Info);
        if (GUILayout.Button("รายงานตัวปั่นเงา (Audit)")) Audit();

        Space();

        // ── ตัวกรองความปลอดภัย ──────────────────────────────────
        EditorGUILayout.LabelField("ตัวกรองความปลอดภัย", EditorStyles.boldLabel);
        includeChildren = EditorGUILayout.Toggle("รวม children ทั้งหมด", includeChildren);
        skipSkinnedMesh = EditorGUILayout.Toggle("ข้าม SkinnedMesh (ตัวละคร)", skipSkinnedMesh);
        skipTaggedPlayer = EditorGUILayout.Toggle("ข้าม tag = Player", skipTaggedPlayer);

        Space();

        // ── โหมด 1: ทำกับ selection ─────────────────────────────
        EditorGUILayout.LabelField("1 · ทำกับสิ่งที่เลือก", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"เลือกอยู่: {Selection.gameObjects.Length} object",
                                   EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
        {
            if (GUILayout.Button("ปิดเงาบน selection"))
                ApplyToSelection(ShadowCastingMode.Off);
            if (GUILayout.Button("เปิดเงากลับบน selection"))
                ApplyToSelection(ShadowCastingMode.On);
            if (GUILayout.Button("เลือกทุกตัวที่ใช้ mesh เดียวกัน"))
                SelectSameMesh();
        }

        Space();

        // ── โหมด 2: กรองตามระยะทาง ──────────────────────────────
        EditorGUILayout.LabelField("2 · ปิดตามระยะทาง", EditorStyles.boldLabel);
        distanceOrigin = (Transform)EditorGUILayout.ObjectField(
            "จุดอ้างอิง", distanceOrigin, typeof(Transform), true);
        minDistance = EditorGUILayout.FloatField("ไกลเกิน (เมตร)", minDistance);

        using (new EditorGUI.DisabledScope(distanceOrigin == null))
        {
            if (GUILayout.Button($"ปิดเงาทุกตัวที่ไกลเกิน {minDistance:0} m"))
                DisableByDistance();
        }
        if (distanceOrigin == null)
            EditorGUILayout.HelpBox("ลาก Nawin(player) มาใส่ช่องจุดอ้างอิง", MessageType.None);

        Space();

        // ── โหมด 3: กรองตามขนาด ─────────────────────────────────
        EditorGUILayout.LabelField("3 · ปิดตามขนาดชิ้นงาน", EditorStyles.boldLabel);
        maxBoundsSize = EditorGUILayout.FloatField("เล็กกว่า (เมตร)", maxBoundsSize);
        if (GUILayout.Button($"ปิดเงาทุกชิ้นที่เล็กกว่า {maxBoundsSize:0.0} m"))
            DisableBySize();

        Space();

        // ── ผลลัพธ์ ─────────────────────────────────────────────
        if (!string.IsNullOrEmpty(lastReport))
        {
            EditorGUILayout.LabelField("ผลล่าสุด", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(lastReport, MessageType.None);
        }

        EditorGUILayout.HelpBox(
            "ทุกปุ่มรองรับ Ctrl+Z\nอย่าลืม Ctrl+S เซฟซีนหลังพอใจแล้ว",
            MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    private static void Space() { EditorGUILayout.Space(8); }

    // ── ตัวช่วยกลาง ─────────────────────────────────────────────
    private static Renderer[] AllRenderers() =>
        Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,
                                           FindObjectsSortMode.None);

    private static int CountActiveCasters() =>
        AllRenderers().Count(r => r.shadowCastingMode != ShadowCastingMode.Off);

    private bool IsProtected(Renderer r)
    {
        if (skipSkinnedMesh && r is SkinnedMeshRenderer) return true;
        if (skipTaggedPlayer && r.CompareTag("Player")) return true;
        return false;
    }

    private void Commit(IList<Renderer> targets, ShadowCastingMode mode, string label)
    {
        if (targets.Count == 0) { lastReport = "ไม่มีอะไรเข้าเงื่อนไข"; return; }

        Undo.RecordObjects(targets.Cast<Object>().ToArray(), label);
        foreach (var r in targets)
        {
            r.shadowCastingMode = mode;
            EditorUtility.SetDirty(r);
        }

        lastReport = $"{label}\nแก้ไป {targets.Count} renderers\n" +
                     $"casters เหลือ {CountActiveCasters()}";
        Debug.Log($"[ShadowCasterTool] {lastReport.Replace('\n', ' ')}");
        Repaint();
    }

    // ── โหมด 1 ─────────────────────────────────────────────────
    private void ApplyToSelection(ShadowCastingMode mode)
    {
        var targets = new List<Renderer>();

        foreach (var go in Selection.gameObjects)
        {
            var rs = includeChildren
                ? go.GetComponentsInChildren<Renderer>(true)
                : go.GetComponents<Renderer>();

            foreach (var r in rs)
                if (!IsProtected(r) && !targets.Contains(r))
                    targets.Add(r);
        }

        Commit(targets, mode, mode == ShadowCastingMode.Off
            ? "ปิดเงาบน selection" : "เปิดเงาบน selection");
    }

    private void SelectSameMesh()
    {
        var go = Selection.activeGameObject;
        if (go == null || !go.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null)
        {
            lastReport = "เลือก GameObject ที่มี MeshFilter ก่อน";
            return;
        }

        var mesh = mf.sharedMesh;
        var matches = Object.FindObjectsByType<MeshFilter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(f => f.sharedMesh == mesh)
            .Select(f => f.gameObject).ToArray();

        Selection.objects = matches;
        lastReport = $"เลือก {matches.Length} ตัวที่ใช้ mesh '{mesh.name}'";
    }

    // ── โหมด 2 ─────────────────────────────────────────────────
    private void DisableByDistance()
    {
        var origin = distanceOrigin.position;
        float sqr = minDistance * minDistance;

        var targets = AllRenderers()
            .Where(r => r.shadowCastingMode != ShadowCastingMode.Off)
            .Where(r => !IsProtected(r))
            .Where(r => (r.bounds.center - origin).sqrMagnitude > sqr)
            .ToList();

        Commit(targets, ShadowCastingMode.Off, $"ปิดเงาที่ไกลเกิน {minDistance:0} m");
    }

    // ── โหมด 3 ─────────────────────────────────────────────────
    private void DisableBySize()
    {
        var targets = AllRenderers()
            .Where(r => r.shadowCastingMode != ShadowCastingMode.Off)
            .Where(r => !IsProtected(r))
            .Where(r => r.bounds.size.magnitude < maxBoundsSize)
            .ToList();

        Commit(targets, ShadowCastingMode.Off, $"ปิดเงาชิ้นเล็กกว่า {maxBoundsSize:0.0} m");
    }

    // ── Audit ──────────────────────────────────────────────────
    private void Audit()
    {
        var groups = new Dictionary<string, (int count, long tris)>();
        long totalTris = 0;
        int total = 0;

        foreach (var r in AllRenderers())
        {
            if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
            total++;

            string key = "(ไม่ทราบ mesh)";
            long tris = 0;

            if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf)
                && mf.sharedMesh != null)
            {
                key = mf.sharedMesh.name;
                tris = mf.sharedMesh.triangles.Length / 3;
            }
            else if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                key = smr.sharedMesh.name + " [skinned]";
                tris = smr.sharedMesh.triangles.Length / 3;
            }

            totalTris += tris;
            groups.TryGetValue(key, out var g);
            groups[key] = (g.count + 1, g.tris + tris);
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== SHADOW CASTER AUDIT ===");
        sb.AppendLine($"casters ทั้งหมด: {total}");
        sb.AppendLine($"tris รวมใน shadow pass: {totalTris:N0}");
        sb.AppendLine();
        sb.AppendLine($"{"MESH",-42}{"จำนวน",8}{"TRIS รวม",16}");

        foreach (var kv in groups.OrderByDescending(k => k.Value.tris).Take(30))
            sb.AppendLine($"{kv.Key,-42}{kv.Value.count,8}{kv.Value.tris,16:N0}");

        Debug.Log(sb.ToString());
        lastReport = $"ดูรายงานเต็มใน Console\ncasters: {total}, tris: {totalTris:N0}";
    }
}