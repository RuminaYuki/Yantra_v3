using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SDFcl.Tool.TreeGenerator
{
    /// <summary>แก้โครง spline ของต้นไม้ใน Scene View</summary>
    public class TreeSceneEditor
    {
        public enum Mode { Select, AddPoint, Branch }

        static readonly Color UserColor = new Color(0.30f, 0.55f, 0.82f);
        static readonly Color UserSelColor = new Color(0.62f, 0.80f, 1f);
        static readonly Color AutoColor = new Color(0.88f, 0.63f, 0.31f);
        static readonly Color SelColor = new Color(1f, 0.82f, 0.4f);

        readonly TreeGeneratorWindow owner;
        public bool active;
        public Mode mode = Mode.Select;
        public int selBranch = -1;
        public int selKnot = -1;
        public int selTwig = -1;
        /// <summary>0 = จุด, 1 = ปลายขาเข้า, 2 = ปลายขาออก</summary>
        public int selHandle;
        public bool showAllTwigs;

        static readonly Color TangentColor = new Color(0.35f, 0.85f, 0.95f);
        Vector3 pivot;
        GameObject previewGO;
        Rect overlayRect;

        public TreeSceneEditor(TreeGeneratorWindow owner)
        {
            this.owner = owner;
        }

        // ------------------------------------------------------------------ เปิด / ปิด

        public void Begin()
        {
            if (active) return;
            active = true;
            SceneView sv = SceneView.lastActiveSceneView;
            pivot = sv != null ? GroundAt(sv.pivot) : Vector3.zero;
            SceneView.duringSceneGui += OnScene;
            owner.ForceRebuild();
            if (sv != null)
            {
                TreeBuildResult r = owner.Result;
                if (r != null && r.mesh != null)
                {
                    Bounds b = r.mesh.bounds;
                    b.center += pivot;
                    sv.Frame(b, false);
                }
                sv.Focus();
            }
        }

        public void End()
        {
            if (!active) return;
            active = false;
            SceneView.duringSceneGui -= OnScene;
            if (previewGO != null) Object.DestroyImmediate(previewGO);
            previewGO = null;
            selBranch = selKnot = selTwig = -1;
            SceneView.RepaintAll();
        }

        static Vector3 GroundAt(Vector3 p)
        {
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 500f, Vector3.down, out hit, 2000f)) return hit.point;
            return p;
        }

        public void UpdateMesh(Mesh mesh, Material[] mats)
        {
            if (!active) return;
            if (previewGO == null)
            {
                previewGO = EditorUtility.CreateGameObjectWithHideFlags("TreeGenerator_Preview",
                    HideFlags.DontSave | HideFlags.NotEditable | HideFlags.HideInHierarchy,
                    typeof(MeshFilter), typeof(MeshRenderer));
                previewGO.transform.position = pivot;
                SceneVisibilityManager.instance.DisablePicking(previewGO, true);
            }
            previewGO.GetComponent<MeshFilter>().sharedMesh = mesh;
            previewGO.GetComponent<MeshRenderer>().sharedMaterials = mats;
        }

        // ------------------------------------------------------------------ แปลงพิกัด

        Vector3 ToWorld(Vector3 local)
        {
            return pivot + local;
        }

        static Vector3 ToKnotSpace(TreePreset p, BuiltBranch bb, Vector3 local)
        {
            Quaternion inv = Quaternion.Inverse(TreeBuilder.Lean(p));
            float s = TreeBuilder.Scale(p);
            TreeBranch b = p.branches[bb.userIndex];
            Vector3 rel = b.parent < 0 ? local : local - bb.origin;
            return inv * rel / s;
        }

        BuiltBranch FindBuiltUser(TreeBuildResult r, int userIndex)
        {
            if (r == null) return null;
            foreach (BuiltBranch bb in r.branches)
                if (bb.userIndex == userIndex) return bb;
            return null;
        }

        // ------------------------------------------------------------------ Scene GUI

        void OnScene(SceneView sv)
        {
            TreePreset p = owner.Working;
            TreeBuildResult res = owner.Result;
            if (p == null || res == null) return;

            Event e = Event.current;
            int passive = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(passive);

            DrawOverlay(sv, p, res);
            bool overOverlay = overlayRect.Contains(e.mousePosition);

            // ---------- เส้นกิ่ง
            foreach (BuiltBranch bb in res.branches)
            {
                if (bb.pts.Count < 2) continue;
                if (bb.userIndex >= 0)
                {
                    Handles.color = bb.userIndex == selBranch ? UserSelColor : UserColor;
                    Handles.DrawAAPolyLine(bb.userIndex == selBranch ? 6f : 4f, WorldPts(bb));
                }
                else if (bb.autoLevel == 0 || showAllTwigs)
                {
                    bool sel = selTwig >= 0 && selTwig < res.branches.Count && res.branches[selTwig] == bb;
                    Handles.color = sel ? SelColor : AutoColor;
                    Handles.DrawAAPolyLine(sel ? 4f : 1.5f, WorldPts(bb));
                }
            }

            // ---------- จุด knot
            foreach (BuiltBranch bb in res.branches)
            {
                if (bb.userIndex < 0 || bb.knotWorld == null) continue;
                for (int k = 0; k < bb.knotWorld.Length; k++)
                {
                    Vector3 w = ToWorld(bb.knotWorld[k]);
                    float size = HandleUtility.GetHandleSize(w) * 0.07f;
                    bool sel = bb.userIndex == selBranch && k == selKnot;
                    Handles.color = sel ? SelColor : (bb.userIndex == selBranch ? UserSelColor : Color.white);
                    if (Handles.Button(w, Quaternion.identity, size, size * 1.4f, Handles.SphereHandleCap))
                    {
                        selBranch = bb.userIndex;
                        selKnot = k;
                        selTwig = -1;
                        selHandle = 0;
                        owner.Repaint();
                    }
                }
            }

            // ---------- จุดที่เลือก: ย้าย + ปรับรัศมี
            BuiltBranch selBuilt = selBranch >= 0 ? FindBuiltUser(res, selBranch) : null;
            if (selBuilt != null && selKnot >= 0 && selKnot < selBuilt.knotWorld.Length && selBranch < p.branches.Count)
            {
                TreeBranch br = p.branches[selBranch];
                TreeKnot kn = br.knots[selKnot];
                Vector3 w = ToWorld(selBuilt.knotWorld[selKnot]);
                Vector3 tOut = selBuilt.knotTanOut[selKnot];
                Vector3 tIn = selBuilt.knotTanIn[selKnot];
                Vector3 tan = KnotTangent(selBuilt, selKnot);
                Quaternion frame = KnotFrame(tan);
                float r = selBuilt.knotRadius[selKnot];
                bool hasIn = selKnot > 0;
                bool hasOut = selKnot < selBuilt.knotWorld.Length - 1;

                // ---------- ขา tangent (เส้นฟ้า + ปลายขาสี่เหลี่ยม)
                if (hasIn) DrawTangentTip(w, tIn, frame, 1);
                if (hasOut) DrawTangentTip(w, tOut, frame, 2);

                if ((selHandle == 1 && hasIn) || (selHandle == 2 && hasOut))
                {
                    Vector3 tip = w + (selHandle == 1 ? tIn : tOut);
                    EditorGUI.BeginChangeCheck();
                    Vector3 nt = Handles.PositionHandle(tip, frame);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(p, "ดัดขา tangent");
                        Materialize(p, kn, tOut, tIn);
                        if (e.alt) kn.brokenTangent = true;
                        Vector3 v = ToTangentSpace(p, nt - w);
                        if (selHandle == 2)
                        {
                            kn.tangentOut = v;
                            if (!kn.brokenTangent) kn.tangentIn = -v;
                        }
                        else
                        {
                            kn.tangentIn = v;
                            if (!kn.brokenTangent) kn.tangentOut = -v;
                        }
                        owner.MarkEdited();
                        return;
                    }
                }
                else if (Tools.current == UnityEditor.Tool.Rotate)
                {
                    // หมุนจุดแบบ pivot: ขาทั้งสองหมุนตาม
                    EditorGUI.BeginChangeCheck();
                    Quaternion nrot = Handles.RotationHandle(frame, w);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Quaternion delta = nrot * Quaternion.Inverse(frame);
                        Undo.RecordObject(p, "หมุนจุดกิ่ง");
                        Materialize(p, kn, tOut, tIn);
                        kn.tangentOut = ToTangentSpace(p, delta * tOut);
                        kn.tangentIn = ToTangentSpace(p, delta * tIn);
                        owner.MarkEdited();
                        return;
                    }
                }
                else
                {
                    Quaternion handleRot = Tools.pivotRotation == PivotRotation.Local ? frame : Quaternion.identity;
                    EditorGUI.BeginChangeCheck();
                    Vector3 nw = Handles.PositionHandle(w, handleRot);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(p, "ย้ายจุดกิ่ง");
                        kn.position = ToKnotSpace(p, selBuilt, nw - pivot);
                        owner.MarkEdited();
                        return;
                    }

                    EditorGUI.BeginChangeCheck();
                    float nr = Handles.RadiusHandle(frame, w, r, true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(p, "ปรับรัศมีกิ่ง");
                        kn.radius = Mathf.Max(0.003f, nr) / TreeBuilder.Scale(p);
                        kn.overrideRadius = true;
                        owner.MarkEdited();
                        return;
                    }
                }

                Handles.color = SelColor;
                Handles.DrawWireDisc(w, tan, r);
                Handles.Label(w + Vector3.up * (r + HandleUtility.GetHandleSize(w) * 0.25f),
                    "r = " + r.ToString("0.00") + " m" + (br.knots[selKnot].overrideRadius ? " (แก้เอง)" : " (อัตโนมัติ)"));

                if (e.type == EventType.ScrollWheel && e.control && !overOverlay)
                {
                    Undo.RecordObject(p, "ปรับรัศมีกิ่ง");
                    float factor = 1f - e.delta.y * 0.04f;
                    br.knots[selKnot].radius = Mathf.Max(0.003f, r * factor) / TreeBuilder.Scale(p);
                    br.knots[selKnot].overrideRadius = true;
                    e.Use();
                    owner.MarkEdited();
                    return;
                }
            }

            // ---------- คลิกใน scene
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && !overOverlay)
            {
                if (mode == Mode.AddPoint && selBuilt != null)
                {
                    AddPointAt(sv, p, selBuilt, e.mousePosition);
                    e.Use();
                    return;
                }
                if (mode == Mode.Branch || (mode == Mode.Select && e.shift))
                {
                    if (BranchFrom(sv, p, res, e.mousePosition)) e.Use();
                    return;
                }
                if (mode == Mode.Select)
                {
                    int twig = PickTwig(res, e.mousePosition);
                    if (twig >= 0)
                    {
                        selTwig = twig;
                        selBranch = selKnot = -1;
                        owner.Repaint();
                        e.Use();
                    }
                    else if (selBranch >= 0 || selTwig >= 0)
                    {
                        selBranch = selKnot = selTwig = -1;
                        owner.Repaint();
                    }
                }
            }

            // ---------- คีย์ลัด
            if (e.type == EventType.KeyDown)
            {
                if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && selBranch >= 0)
                {
                    DeleteSelection(p);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    selBranch = selKnot = selTwig = -1;
                    mode = Mode.Select;
                    owner.Repaint();
                    e.Use();
                }
            }
        }

        Vector3[] WorldPts(BuiltBranch bb)
        {
            var arr = new Vector3[bb.pts.Count - bb.flareCount];
            for (int i = 0; i < arr.Length; i++) arr[i] = ToWorld(bb.pts[i + bb.flareCount]);
            return arr;
        }

        static Vector3 KnotTangent(BuiltBranch bb, int k)
        {
            Vector3 t = bb.knotTanOut != null && k < bb.knotTanOut.Length ? bb.knotTanOut[k] : Vector3.zero;
            if (t.sqrMagnitude < 1e-10f)
            {
                Vector3[] kw = bb.knotWorld;
                t = k < kw.Length - 1 ? kw[k + 1] - kw[k] : kw[k] - kw[k - 1];
            }
            return t.sqrMagnitude > 1e-10f ? t.normalized : Vector3.up;
        }

        /// <summary>แกนของจุด: แกน Z ชี้ไปตามเส้น (ไปทางจุดถัดไป)</summary>
        static Quaternion KnotFrame(Vector3 tan)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(tan, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(tan, up);
        }

        void DrawTangentTip(Vector3 w, Vector3 t, Quaternion frame, int which)
        {
            Vector3 tip = w + t;
            Handles.color = TangentColor;
            Handles.DrawAAPolyLine(2f, w, tip);
            float hs = HandleUtility.GetHandleSize(tip) * 0.05f;
            Handles.color = selHandle == which ? SelColor : TangentColor;
            if (Handles.Button(tip, frame, hs, hs * 1.6f, Handles.CubeHandleCap))
            {
                selHandle = selHandle == which ? 0 : which;
                owner.Repaint();
            }
        }

        static Vector3 ToTangentSpace(TreePreset p, Vector3 v)
        {
            return Quaternion.Inverse(TreeBuilder.Lean(p)) * v / TreeBuilder.Scale(p);
        }

        /// <summary>ก่อนแก้ขาครั้งแรก: เก็บขาอัตโนมัติปัจจุบันไว้เป็นค่าเริ่ม เส้นจะได้ไม่กระโดด</summary>
        static void Materialize(TreePreset p, TreeKnot kn, Vector3 tOutWorld, Vector3 tInWorld)
        {
            if (kn.customTangent) return;
            kn.tangentOut = ToTangentSpace(p, tOutWorld);
            kn.tangentIn = ToTangentSpace(p, tInWorld);
            kn.customTangent = true;
        }

        // ------------------------------------------------------------------ actions

        void AddPointAt(SceneView sv, TreePreset p, BuiltBranch bb, Vector2 mouse)
        {
            Vector3 last = ToWorld(bb.knotWorld[bb.knotWorld.Length - 1]);
            var plane = new Plane(-sv.camera.transform.forward, last);
            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            float d;
            if (!plane.Raycast(ray, out d)) return;
            Vector3 hit = ray.GetPoint(d);
            Undo.RecordObject(p, "เพิ่มจุดกิ่ง");
            TreeBranch br = p.branches[bb.userIndex];
            br.knots.Add(new TreeKnot(ToKnotSpace(p, bb, hit - pivot)));
            selKnot = br.knots.Count - 1;
            owner.MarkEdited();
        }

        bool BranchFrom(SceneView sv, TreePreset p, TreeBuildResult res, Vector2 mouse)
        {
            BuiltBranch best = null;
            int bestIdx = -1;
            float bestDist = 15f;
            foreach (BuiltBranch bb in res.branches)
            {
                if (bb.userIndex < 0) continue;
                for (int i = bb.flareCount; i < bb.pts.Count; i++)
                {
                    float dist = Vector2.Distance(HandleUtility.WorldToGUIPoint(ToWorld(bb.pts[i])), mouse);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = bb;
                        bestIdx = i;
                    }
                }
            }
            if (best == null) return false;

            float t = TreeBuilder.ParamAtSample(best, bestIdx);
            Vector3 pos, tan;
            float pr;
            TreeBuilder.Eval(best, t, out pos, out tan, out pr);
            Vector3 side = Vector3.ProjectOnPlane(sv.camera.transform.right, tan);
            if (side.sqrMagnitude < 1e-6f) side = TreeSpline.AnyPerpendicular(tan);
            Vector3 dir = (side.normalized + Vector3.up * 0.6f).normalized;
            float len = Mathf.Clamp(best.length * 0.4f, 0.5f, 3f);

            Quaternion inv = Quaternion.Inverse(TreeBuilder.Lean(p));
            float s = TreeBuilder.Scale(p);
            Undo.RecordObject(p, "แตกกิ่งใหม่");
            var nb = new TreeBranch { parent = best.userIndex, parentT = t };
            nb.knots.Add(new TreeKnot(Vector3.zero));
            nb.knots.Add(new TreeKnot(inv * (dir * len * 0.5f) / s));
            nb.knots.Add(new TreeKnot(inv * (dir * len) / s));
            p.branches.Add(nb);
            selBranch = p.branches.Count - 1;
            selKnot = 2;
            selTwig = -1;
            owner.MarkEdited();
            return true;
        }

        int PickTwig(TreeBuildResult res, Vector2 mouse)
        {
            int best = -1;
            float bestDist = 8f;
            for (int i = 0; i < res.branches.Count; i++)
            {
                BuiltBranch bb = res.branches[i];
                if (bb.userIndex >= 0 || (bb.autoLevel != 0 && !showAllTwigs)) continue;
                for (int k = bb.flareCount; k < bb.pts.Count; k++)
                {
                    float dist = Vector2.Distance(HandleUtility.WorldToGUIPoint(ToWorld(bb.pts[k])), mouse);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = i;
                    }
                }
            }
            return best;
        }

        public void ConvertSelectedTwig()
        {
            TreePreset p = owner.Working;
            TreeBuildResult res = owner.Result;
            if (p == null || res == null || selTwig < 0 || selTwig >= res.branches.Count) return;
            BuiltBranch tw = res.branches[selTwig];
            if (tw.userIndex >= 0 || tw.parentBuilt < 0) return;
            BuiltBranch parent = res.branches[tw.parentBuilt];
            if (parent.userIndex < 0)
            {
                EditorUtility.DisplayDialog("Tree Generator", "แปลงได้เฉพาะกิ่งย่อยชั้นแรก (ที่ออกจากกิ่งที่วาดเอง)", "OK");
                return;
            }

            Quaternion inv = Quaternion.Inverse(TreeBuilder.Lean(p));
            float s = TreeBuilder.Scale(p);
            Undo.RecordObject(p, "แปลงกิ่งอัตโนมัติเป็น Spline");
            var nb = new TreeBranch { parent = parent.userIndex, parentT = tw.parentT };
            int first = tw.flareCount;
            int n = tw.pts.Count - first;
            const int K = 4;
            for (int k = 0; k < K; k++)
            {
                int idx = first + Mathf.RoundToInt(k / (float)(K - 1) * (n - 1));
                nb.knots.Add(new TreeKnot(inv * (tw.pts[idx] - tw.origin) / s));
            }
            p.branches.Add(nb);
            if (!p.suppressedTwigs.Contains(tw.twigKey)) p.suppressedTwigs.Add(tw.twigKey);
            selBranch = p.branches.Count - 1;
            selKnot = K - 1;
            selTwig = -1;
            owner.MarkEdited();
        }

        public void DeleteSelection(TreePreset p)
        {
            if (selBranch < 0 || selBranch >= p.branches.Count) return;
            TreeBranch br = p.branches[selBranch];

            if (selKnot >= 0 && br.knots.Count > 2)
            {
                Undo.RecordObject(p, "ลบจุดกิ่ง");
                br.knots.RemoveAt(selKnot);
                selKnot = Mathf.Min(selKnot, br.knots.Count - 1);
                owner.MarkEdited();
                return;
            }

            if (br.parent < 0)
            {
                EditorUtility.DisplayDialog("Tree Generator", "ลบลำต้นไม่ได้ (ลบได้เฉพาะจุดบนลำต้น ถ้ามีมากกว่า 2 จุด)", "OK");
                return;
            }

            Undo.RecordObject(p, "ลบกิ่ง");
            RemoveBranchTree(p, selBranch);
            selBranch = selKnot = -1;
            owner.MarkEdited();
        }

        /// <summary>ลบกิ่งพร้อมกิ่งลูกทั้งหมด แล้วจัดเลข parent ใหม่</summary>
        public static void RemoveBranchTree(TreePreset p, int index)
        {
            var remove = new HashSet<int> { index };
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < p.branches.Count; i++)
                    if (!remove.Contains(i) && remove.Contains(p.branches[i].parent))
                    {
                        remove.Add(i);
                        grew = true;
                    }
            }

            var map = new int[p.branches.Count];
            var kept = new List<TreeBranch>();
            for (int i = 0; i < p.branches.Count; i++)
            {
                if (remove.Contains(i))
                {
                    map[i] = -1;
                    continue;
                }
                map[i] = kept.Count;
                kept.Add(p.branches[i]);
            }
            foreach (TreeBranch b in kept)
                if (b.parent >= 0) b.parent = map[b.parent];
            p.branches = kept;
        }

        // ------------------------------------------------------------------ overlay

        void DrawOverlay(SceneView sv, TreePreset p, TreeBuildResult res)
        {
            float w = 290f, h = 430f;
            overlayRect = new Rect(sv.position.width - w - 10f, 10f, w, h);
            Handles.BeginGUI();
            GUILayout.BeginArea(overlayRect, "Tree Spline", GUI.skin.window);

            mode = (Mode)GUILayout.Toolbar((int)mode, new[] { "เลือก", "เพิ่มจุด", "แตกกิ่ง" });
            GUILayout.Space(4);

            if (selBranch >= 0 && selBranch < p.branches.Count)
            {
                TreeBranch br = p.branches[selBranch];
                GUILayout.Label(br.parent < 0 ? "ลำต้น" : "กิ่งที่วาดเอง #" + selBranch, EditorStyles.boldLabel);
                if (selKnot >= 0 && selKnot < br.knots.Count)
                {
                    TreeKnot kn = br.knots[selKnot];
                    BuiltBranch bb = FindBuiltUser(res, selBranch);
                    float r = bb != null && selKnot < bb.knotRadius.Length ? bb.knotRadius[selKnot] : kn.radius;
                    GUILayout.Label("จุดที่ " + (selKnot + 1) + " / " + br.knots.Count);
                    EditorGUI.BeginChangeCheck();
                    float nr = EditorGUILayout.FloatField("รัศมี (m)", r);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(p, "ปรับรัศมีกิ่ง");
                        kn.radius = Mathf.Max(0.003f, nr) / TreeBuilder.Scale(p);
                        kn.overrideRadius = true;
                        owner.MarkEdited();
                    }
                    using (new EditorGUI.DisabledScope(!kn.overrideRadius))
                    {
                        if (GUILayout.Button("คืนค่าเรียวอัตโนมัติ"))
                        {
                            Undo.RecordObject(p, "คืนค่ารัศมีอัตโนมัติ");
                            kn.overrideRadius = false;
                            owner.MarkEdited();
                        }
                    }

                    GUILayout.Space(2);
                    GUILayout.Label("ขา tangent: " + (kn.customTangent ? "ปรับเอง" : "อัตโนมัติ (ชี้ไปจุดถัดไป)"), EditorStyles.miniLabel);
                    EditorGUI.BeginChangeCheck();
                    bool brk = GUILayout.Toggle(kn.brokenTangent, " แยกขา (ให้กิ่งหักมุมได้)");
                    if (EditorGUI.EndChangeCheck() && bb != null)
                    {
                        Undo.RecordObject(p, "แยกขา tangent");
                        Materialize(p, kn, bb.knotTanOut[selKnot], bb.knotTanIn[selKnot]);
                        kn.brokenTangent = brk;
                        if (!brk) kn.tangentIn = -kn.tangentOut;
                        owner.MarkEdited();
                    }
                    using (new EditorGUI.DisabledScope(!kn.customTangent))
                    {
                        if (GUILayout.Button("คืนค่าขาอัตโนมัติ"))
                        {
                            Undo.RecordObject(p, "คืนค่าขาอัตโนมัติ");
                            kn.customTangent = false;
                            kn.brokenTangent = false;
                            selHandle = 0;
                            owner.MarkEdited();
                        }
                    }
                }

                EditorGUI.BeginChangeCheck();
                bool lk = GUILayout.Toggle(br.locked, " ล็อกกิ่งนี้ (ไม่ถูกสุ่ม)");
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(p, "ล็อกกิ่ง");
                    br.locked = lk;
                    owner.MarkEdited();
                }

                if (GUILayout.Button(selKnot >= 0 && br.knots.Count > 2 ? "ลบจุดนี้ (Delete)" : "ลบกิ่งนี้ (Delete)"))
                    DeleteSelection(p);
            }
            else if (selTwig >= 0 && selTwig < res.branches.Count)
            {
                GUILayout.Label("กิ่งอัตโนมัติ (ชั้น " + (res.branches[selTwig].autoLevel + 1) + ")", EditorStyles.boldLabel);
                if (GUILayout.Button("แปลงกิ่งนี้เป็น Spline")) ConvertSelectedTwig();
            }
            else
            {
                GUILayout.Label("คลิกจุดสีขาวเพื่อเลือก\nคลิกเส้นสีส้มเพื่อเลือกกิ่งอัตโนมัติ", EditorStyles.wordWrappedMiniLabel);
            }

            GUILayout.FlexibleSpace();
            showAllTwigs = GUILayout.Toggle(showAllTwigs, " แสดงกิ่งอัตโนมัติทุกชั้น");
            GUILayout.Label("W = ย้ายจุด · E = หมุนจุด (ขาหมุนตาม)\nคลิกปลายขาสีฟ้า = ดัดโค้ง · Alt+ลากขา = แยกขา\n" +
                            "เพิ่มจุด: คลิกเพื่อต่อปลายกิ่งที่เลือก\nแตกกิ่ง / Shift+คลิก: คลิกบนเส้นสีฟ้า\nCtrl+Scroll = รัศมี · Delete = ลบ · Esc = ยกเลิก",
                EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("เสร็จ")) owner.StopSceneEdit();

            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
