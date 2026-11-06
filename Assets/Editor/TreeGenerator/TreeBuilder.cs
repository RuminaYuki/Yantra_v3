using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SDFcl.Tool.TreeGenerator
{
    public class TreeBuildOptions
    {
        public float twigFactor = 1f;
        public float leafFactor = 1f;
        public float radialFactor = 1f;
        public float sampleStep = 0.22f;

        public static TreeBuildOptions ForLOD(int lod)
        {
            switch (lod)
            {
                case 1: return new TreeBuildOptions { twigFactor = 0.45f, leafFactor = 0.5f, radialFactor = 0.6f, sampleStep = 0.4f };
                case 2: return new TreeBuildOptions { twigFactor = 0.12f, leafFactor = 0.2f, radialFactor = 0.4f, sampleStep = 0.8f };
                default: return new TreeBuildOptions();
            }
        }
    }

    /// <summary>กิ่งหนึ่งกิ่งหลังคำนวณแล้ว (ทั้งกิ่งที่วาดเองและกิ่งอัตโนมัติ) พิกัดเป็นพิกัดของต้นไม้</summary>
    public class BuiltBranch
    {
        public int userIndex = -1;   // index ใน preset.branches (-1 = กิ่งอัตโนมัติ)
        public int twigKey;
        public int parentBuilt = -1;
        public int level;
        public int autoLevel = -1;    // -1 = วาดเอง, 0.. = ชั้นกิ่งย่อย
        public int rootUser = -1;     // กิ่งที่วาดเองที่ใกล้ที่สุด
        public int flareCount;
        public int leafType;
        public float parentT;
        public float length;
        public float phase;
        public float flexBase;
        public float uvRadius;
        public bool meshed = true;
        public bool flareBlend;
        public Vector3 origin;
        public Vector3 attachAxis;
        public readonly List<Vector3> pts = new List<Vector3>();
        public readonly List<float> radii = new List<float>();
        public readonly List<float> cum = new List<float>();
        public Vector3[] knotWorld;
        public float[] knotRadius;
        /// <summary>ขาออก/ขาเข้าของแต่ละจุด (พิกัดต้นไม้ ระยะถึงปลายขา)</summary>
        public Vector3[] knotTanOut;
        public Vector3[] knotTanIn;
    }

    public class TreeBuildResult
    {
        public Mesh mesh;
        public readonly List<BuiltBranch> branches = new List<BuiltBranch>();
        public int leafTypeCount = 1;
        public int barkIndex = -1;
        public int triangleCount;
        public int barkTriangles;
        public int leafTriangles;
        public int leafCount;
        public int autoTwigCount;
        public float trunkBaseRadius = 0.2f;
        public float trunkHeight = 1f;
        public float firstForkHeight = 1f;
    }

    /// <summary>สร้าง mesh ต้นไม้จาก TreePreset</summary>
    public static class TreeBuilder
    {
        struct LeafInst
        {
            public Vector3 pos;
            public Vector3 dir;
            public Vector3 tan;
            public float size;
            public float hue;
            public float phase;
            public int type;
            public int key;
        }

        static readonly List<Vector3> TmpPts = new List<Vector3>();
        static readonly List<float> TmpParam = new List<float>();

        public static float Scale(TreePreset p)
        {
            return Mathf.Max(0.05f, p.heightScale);
        }

        public static Quaternion Lean(TreePreset p)
        {
            return Quaternion.Euler(p.leanAngles.x, 0f, p.leanAngles.y);
        }

        // ------------------------------------------------------------------ Build

        public static TreeBuildResult Build(TreePreset p, TreeBuildOptions o)
        {
            var res = new TreeBuildResult();
            var list = res.branches;
            float scale = Scale(p);
            Quaternion lean = Lean(p);
            res.leafTypeCount = Mathf.Max(1, p.leafMaterials.Count);
            var suppressed = new HashSet<int>(p.suppressedTwigs);

            // ---------- กิ่งที่วาดเอง
            int[] userBuilt = new int[p.branches.Count];
            for (int i = 0; i < p.branches.Count; i++)
            {
                userBuilt[i] = -1;
                TreeBranch b = p.branches[i];
                if (b == null || b.knots == null || b.knots.Count < 2) continue;

                BuiltBranch parent = null;
                if (b.parent >= 0)
                {
                    if (b.parent >= i || userBuilt[b.parent] < 0) continue;
                    parent = list[userBuilt[b.parent]];
                }

                var bb = new BuiltBranch { userIndex = i, rootUser = i, twigKey = i + 1 };
                Vector3 origin = Vector3.zero;
                float pr = 0f;
                if (parent != null)
                {
                    Vector3 ptan;
                    Eval(parent, b.parentT, out origin, out ptan, out pr);
                    bb.parentBuilt = userBuilt[b.parent];
                    bb.level = parent.level + 1;
                    bb.parentT = b.parentT;
                }

                bb.origin = origin;
                bb.attachAxis = origin;

                int n = b.knots.Count;
                var kw = new Vector3[n];
                var kr = new float[n];
                var kd = new float[n];
                for (int k = 0; k < n; k++) kw[k] = origin + lean * (b.knots[k].position * scale);
                float total = 0f;
                for (int k = 1; k < n; k++)
                {
                    total += Vector3.Distance(kw[k - 1], kw[k]);
                    kd[k] = total;
                }

                float baseR = parent == null
                    ? p.trunkRadius * p.thickness * scale * (0.8f + 0.4f * p.age)
                    : pr * p.childRadiusRatio;

                for (int k = 0; k < n; k++)
                {
                    float s = total > 0f ? kd[k] / total : 0f;
                    float r = baseR * Mathf.Lerp(1f, p.tipRatio, s);
                    if (parent == null) r *= 1f + p.age * 0.8f * Mathf.Exp(-s * 10f);
                    if (b.knots[k].overrideRadius) r = b.knots[k].radius * scale;
                    kr[k] = Mathf.Max(0.003f, r);
                }

                // ขา tangent: ปรับเอง หรือระบบคำนวณให้ (ชี้ไปทางจุดถัดไป)
                var tOut = new Vector3[n];
                var tIn = new Vector3[n];
                for (int k = 0; k < n; k++)
                {
                    TreeKnot kn = b.knots[k];
                    if (kn.customTangent)
                    {
                        tOut[k] = lean * (kn.tangentOut * scale);
                        tIn[k] = kn.brokenTangent ? lean * (kn.tangentIn * scale) : -tOut[k];
                    }
                    else
                    {
                        tOut[k] = TreeSpline.AutoTangent(kw, k);
                        tIn[k] = -tOut[k];
                    }
                }

                bb.knotWorld = kw;
                bb.knotRadius = kr;
                bb.knotTanOut = tOut;
                bb.knotTanIn = tIn;

                TreeSpline.SampleBezier(kw, tOut, tIn, o.sampleStep * Mathf.Max(0.5f, scale) / LengthDetail(p), TmpPts, TmpParam);
                for (int s = 0; s < TmpPts.Count; s++)
                {
                    float kp = TmpParam[s];
                    int k0 = Mathf.Clamp(Mathf.FloorToInt(kp), 0, n - 2);
                    float f = Mathf.Clamp01(kp - k0);
                    bb.pts.Add(TmpPts[s]);
                    bb.radii.Add(Mathf.Lerp(kr[k0], kr[k0 + 1], f));
                }
                Simplify(bb, p.simplifyAngle);

                if (parent != null) AddFlare(bb, origin, pr, p.junction);
                bb.uvRadius = bb.radii[Mathf.Min(bb.flareCount + 1, bb.radii.Count - 1)];
                Finish(bb);
                bb.phase = Hash01(bb.twigKey * 7919);
                bb.flexBase = parent == null ? 0f : FlexAt(parent, b.parentT);
                userBuilt[i] = list.Count;
                list.Add(bb);
            }

            // ---------- กิ่งย่อยอัตโนมัติ
            var hosts = new List<int>();
            for (int i = 0; i < list.Count; i++) hosts.Add(i);
            int usedLevels = 0;
            for (int L = 0; L < p.twigLevels.Count; L++)
            {
                TwigLevel rule = p.twigLevels[L];
                if (rule == null || !rule.enabled || rule.perMeter <= 0f) break;
                var next = new List<int>();
                for (int h = 0; h < hosts.Count; h++)
                    GrowTwigs(p, o, rule, L, hosts[h], list, next, suppressed, res);
                if (next.Count == 0) break;
                hosts = next;
                usedLevels++;
            }

            var leafHosts = new List<int>();
            if (usedLevels > 0) leafHosts.AddRange(hosts);
            if (usedLevels == 0 || p.leafMode == LeafMode.Blob)
            {
                // ไม่มีกิ่งย่อย หรือโหมดก้อนพุ่ม: ใส่ใบที่กิ่งที่วาดเองด้วย (ยกเว้นลำต้น)
                for (int i = 0; i < list.Count; i++)
                    if (list[i].userIndex >= 0 && list[i].level > 0) leafHosts.Add(i);
            }
            if (leafHosts.Count == 0)
                for (int i = 0; i < list.Count; i++) leafHosts.Add(i);

            // ---------- จุดใบ
            var leaves = new List<LeafInst>();
            if (p.leafDensity > 0f)
            {
                bool blob = p.leafMode == LeafMode.Blob;
                float fullMul = Mathf.Lerp(0.5f, 1.5f, p.fullness);
                float sizeBoost = Mathf.Min(2.2f, 1f / Mathf.Sqrt(Mathf.Max(0.05f, o.leafFactor)));
                foreach (int hi in leafHosts)
                {
                    BuiltBranch h = list[hi];
                    var r = new System.Random(unchecked(p.seed * 7 + h.twigKey * 92821));
                    int count = blob
                        ? 1 + Mathf.FloorToInt(h.length * 1.2f * p.leafDensity * fullMul)
                        : Mathf.RoundToInt(h.length * 10f * p.leafDensity * fullMul);
                    for (int i = 0; i < count; i++)
                    {
                        double ra = r.NextDouble(), rb = r.NextDouble(), rc = r.NextDouble(), rd = r.NextDouble(), re = r.NextDouble();
                        Vector3 rv = RandVec(r);
                        int key = unchecked(h.twigKey * 31 + i * 7349);
                        if (Hash01(key ^ 0x68E31DA4) >= o.leafFactor) continue;

                        float ls = Mathf.Min(p.leafStartT, p.leafEndT), le = Mathf.Max(p.leafStartT, p.leafEndT);
                        float t = blob
                            ? (count == 1 ? le : Mathf.Lerp(Mathf.Lerp(ls, le, 0.5f), le, i / (float)(count - 1)))
                            : Mathf.Lerp(ls, le, (float)ra);
                        Vector3 pos, tan;
                        float rad;
                        Eval(h, t, out pos, out tan, out rad);
                        float size = p.leafSize * (1f + ((float)rb * 2f - 1f) * Mathf.Clamp01(p.leafSizeJitter)) * sizeBoost;
                        var li = new LeafInst
                        {
                            pos = p.leafAttach ? pos : pos + rv * size * 0.25f,
                            tan = tan,
                            dir = rv.sqrMagnitude > 1e-6f ? rv.normalized : Vector3.up,
                            size = size,
                            hue = (float)re,
                            phase = h.phase,
                            key = key,
                            type = p.leafDistribution == LeafDistribution.Clustered && rc < 0.85
                                ? h.leafType
                                : PickWeighted(p.leafMaterials, rd)
                        };
                        li.type = Mathf.Clamp(li.type, 0, res.leafTypeCount - 1);
                        leaves.Add(li);
                    }
                }
            }

            // ---------- Mesh
            var V = new List<Vector3>();
            var N = new List<Vector3>();
            var UV = new List<Vector2>();
            var UV2 = new List<Vector2>();
            var C = new List<Color>();
            var tris = new List<int>[1 + res.leafTypeCount];
            for (int i = 0; i < tris.Length; i++) tris[i] = new List<int>();

            bool stylized = p.style == TreeStyle.Stylized;
            foreach (BuiltBranch bb in list)
                if (bb.meshed) Tube(bb, p, o.radialFactor, V, N, UV, UV2, C, tris[0]);

            if (stylized) Flatten(V, N, UV, UV2, C, tris[0]);

            if (leaves.Count > 0)
            {
                Vector3 center = Vector3.zero;
                foreach (LeafInst l in leaves) center += l.pos;
                center /= leaves.Count;
                foreach (LeafInst l in leaves)
                {
                    if (p.leafMode == LeafMode.Blob) Blob(l, center, p.messiness, V, N, UV, UV2, C, tris[1 + l.type]);
                    else Card(l, center, p, V, N, UV, UV2, C, tris[1 + l.type]);
                }
            }

            var mesh = new Mesh { name = "Tree" };
            if (V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            mesh.SetUVs(0, UV);
            mesh.SetUVs(1, UV2);
            if (p.windData) mesh.SetColors(C);
            mesh.subMeshCount = tris.Length;
            int triCount = 0;
            for (int s = 0; s < tris.Length; s++)
            {
                mesh.SetTriangles(tris[s], s, false);
                triCount += tris[s].Count / 3;
            }
            mesh.RecalculateBounds();
            if (V.Count > 0) mesh.RecalculateTangents();
            res.mesh = mesh;
            res.triangleCount = triCount;
            res.barkTriangles = tris[0].Count / 3;
            res.leafTriangles = triCount - res.barkTriangles;
            res.leafCount = leaves.Count;

            // ---------- ข้อมูลลำต้น (สำหรับ Collider)
            if (p.branches.Count > 0 && userBuilt[0] >= 0)
            {
                BuiltBranch trunk = list[userBuilt[0]];
                res.trunkBaseRadius = trunk.knotRadius[0];
                res.trunkHeight = trunk.pts[trunk.pts.Count - 1].y;
                float fork = float.MaxValue;
                foreach (BuiltBranch bb in list)
                    if (bb.userIndex >= 0 && bb.parentBuilt == userBuilt[0])
                        fork = Mathf.Min(fork, bb.origin.y);
                res.firstForkHeight = fork < float.MaxValue ? fork : res.trunkHeight * 0.5f;
            }

            if (p.barkMaterials.Count > 0)
            {
                var br = new System.Random(unchecked(p.seed * 13 + p.barkSeed));
                res.barkIndex = PickWeighted(p.barkMaterials, br.NextDouble());
            }

            return res;
        }

        // ------------------------------------------------------------------ กิ่งย่อย

        static void GrowTwigs(TreePreset p, TreeBuildOptions o, TwigLevel rule, int L, int hostIndex,
            List<BuiltBranch> list, List<int> next, HashSet<int> suppressed, TreeBuildResult res)
        {
            BuiltBranch par = list[hostIndex];
            float start = rule.startT;
            float end = Mathf.Max(rule.startT, rule.endT);
            if (L == 0 && par.userIndex >= 0 && par.level == 0) start = Mathf.Max(start, p.trunkTwigStart);
            if (end <= start || par.length <= 0f) return;

            float span = (end - start) * par.length;
            int count = Mathf.RoundToInt(rule.perMeter * span * Mathf.Lerp(0.4f, 1.6f, p.fullness));
            if (count <= 0) return;

            bool locked = par.rootUser >= 0 && par.rootUser < p.branches.Count && p.branches[par.rootUser].locked;
            int seed = locked && p.branches[par.rootUser].seedOverride != 0 ? p.branches[par.rootUser].seedOverride : p.seed;
            float jit = locked ? 0f : p.twigJitter;
            var r = new System.Random(unchecked(seed * 486187739 + par.twigKey * 16777619 + L));
            var jr = new System.Random(unchecked(p.jitterSeed * 31 + par.twigKey));
            float m = p.messiness;

            for (int i = 0; i < count; i++)
            {
                // ดึงค่าสุ่มทุกตัวก่อนเสมอ ให้ลำดับคงที่ (ต้นเดิมได้ผลเดิม)
                double r1 = r.NextDouble(), r2 = r.NextDouble(), r3 = r.NextDouble(), r4 = r.NextDouble(),
                    r5 = r.NextDouble(), r6 = r.NextDouble();
                Vector3 rv1 = RandVec(r);
                Vector3 rv2 = RandVec(r);
                float j1 = (float)(jr.NextDouble() * 2.0 - 1.0);
                float j2 = (float)(jr.NextDouble() * 2.0 - 1.0);
                float j3 = (float)(jr.NextDouble() * 2.0 - 1.0);

                int key = unchecked((par.twigKey * 73856093) ^ ((i + 1) * 19349663) ^ ((L + 1) * 83492791));
                if (suppressed.Contains(key)) continue;

                float t = start + (i + 0.5f + (float)(r1 - 0.5) * (0.3f + m)) / count * (end - start);
                t = Mathf.Clamp(t, start, end);
                Vector3 pos, tan;
                float pr;
                Eval(par, t, out pos, out tan, out pr);

                float phi = i * 137.508f + (float)(r2 * 2.0 - 1.0) * m * 70f + j1 * jit * 180f;
                Vector3 side = Quaternion.AngleAxis(phi, tan) * TreeSpline.AnyPerpendicular(tan);
                float ang = rule.angle + (float)(r3 * 2.0 - 1.0) * (8f + m * 25f) + j2 * jit * 40f;
                Vector3 axis = Vector3.Cross(tan, side);
                if (axis.sqrMagnitude < 1e-8f) axis = TreeSpline.AnyPerpendicular(tan);
                Vector3 dir = (Quaternion.AngleAxis(ang, axis.normalized) * tan).normalized;

                float len = par.length * rule.lengthRatio * Mathf.Lerp(1f, 1f - t * 0.85f, rule.taperLength) *
                            (0.75f + 0.5f * (float)r4) * (1f + j3 * jit * 0.4f);
                len = Mathf.Max(0.04f, len);
                float g = Mathf.Clamp(rule.gravity + p.droop * 0.8f, -1f, 2f);

                Vector3 p0 = pos;
                Vector3 p1 = p0 + dir * len * 0.34f;
                Vector3 d2 = (dir + Vector3.down * g * 0.6f + rv1 * m * 0.35f + Vector3.up * rule.curl * 0.5f).normalized;
                Vector3 p2 = p1 + d2 * len * 0.33f;
                Vector3 d3 = (d2 + Vector3.down * g * 0.8f + rv2 * m * 0.4f + Vector3.up * rule.curl).normalized;
                Vector3 p3 = p2 + d3 * len * 0.33f;

                float rb = Mathf.Max(0.002f, pr * rule.radiusRatio);
                float rt = Mathf.Max(0.0015f, rb * 0.2f);

                var bb = new BuiltBranch
                {
                    twigKey = key,
                    parentBuilt = hostIndex,
                    level = par.level + 1,
                    autoLevel = L,
                    rootUser = par.rootUser,
                    parentT = t,
                    origin = pos,
                    attachAxis = pos
                };

                TreeSpline.Sample(new[] { p0, p1, p2, p3 }, o.sampleStep * 1.4f / LengthDetail(p), TmpPts, TmpParam);
                for (int s = 0; s < TmpPts.Count; s++)
                {
                    bb.pts.Add(TmpPts[s]);
                    bb.radii.Add(Mathf.Lerp(rb, rt, TmpParam[s] / 3f));
                }
                Simplify(bb, p.simplifyAngle);

                bb.pts.Insert(0, pos - dir * pr * 0.5f);
                bb.radii.Insert(0, rb * 1.15f);
                bb.flareCount = 1;
                bb.uvRadius = rb;
                Finish(bb);
                bb.phase = Hash01(key);
                bb.flexBase = FlexAt(par, t);
                bb.meshed = Hash01(key ^ 0x2545F491) < o.twigFactor;
                bb.leafType = par.autoLevel >= 0 && r5 < 0.7 ? par.leafType : PickWeighted(p.leafMaterials, r6);

                list.Add(bb);
                next.Add(list.Count - 1);
                res.autoTwigCount++;
            }
        }

        // ------------------------------------------------------------------ helpers ของกิ่ง

        static void AddFlare(BuiltBranch bb, Vector3 origin, float parentRadius, JunctionQuality q)
        {
            if (bb.pts.Count < 2) return;
            Vector3 dir = (bb.pts[1] - bb.pts[0]).normalized;
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.up;
            float r0 = bb.radii[0];
            if (q == JunctionQuality.Beautiful)
            {
                bb.radii[0] = r0 * 1.35f;
                bb.pts.Insert(0, origin - dir * parentRadius * 0.7f);
                bb.radii.Insert(0, Mathf.Min(r0 * 1.8f, Mathf.Max(r0, parentRadius * 0.95f)));
                bb.flareBlend = true;
            }
            else
            {
                bb.pts.Insert(0, origin - dir * parentRadius * 0.6f);
                bb.radii.Insert(0, r0 * 1.25f);
            }
            bb.flareCount = 1;
        }

        static void Finish(BuiltBranch bb)
        {
            bb.cum.Clear();
            float acc = 0f;
            for (int i = 0; i < bb.pts.Count; i++)
            {
                if (i > 0) acc += Vector3.Distance(bb.pts[i - 1], bb.pts[i]);
                bb.cum.Add(acc);
            }

            float offset = bb.cum[Mathf.Min(bb.flareCount, bb.cum.Count - 1)];
            for (int i = 0; i < bb.cum.Count; i++) bb.cum[i] -= offset;
            bb.length = Mathf.Max(0.0001f, bb.cum[bb.cum.Count - 1]);
        }

        static float FlexAt(BuiltBranch b, float t)
        {
            return Mathf.Clamp01(b.flexBase + t * (b.level == 0 ? 0.15f : 0.35f));
        }

        /// <summary>หาจุดบนกิ่งที่ระยะ t (0 = โคน, 1 = ปลาย)</summary>
        public static void Eval(BuiltBranch b, float t, out Vector3 pos, out Vector3 tan, out float radius)
        {
            int n = b.pts.Count;
            if (n < 2)
            {
                pos = n == 1 ? b.pts[0] : Vector3.zero;
                tan = Vector3.up;
                radius = n == 1 ? b.radii[0] : 0f;
                return;
            }

            float d = Mathf.Clamp01(t) * b.length;
            int i = Mathf.Clamp(b.flareCount, 0, n - 2);
            while (i < n - 2 && b.cum[i + 1] < d) i++;
            float seg = b.cum[i + 1] - b.cum[i];
            float f = seg > 1e-6f ? Mathf.Clamp01((d - b.cum[i]) / seg) : 0f;
            pos = Vector3.Lerp(b.pts[i], b.pts[i + 1], f);
            tan = b.pts[i + 1] - b.pts[i];
            tan = tan.sqrMagnitude > 1e-10f ? tan.normalized : Vector3.up;
            radius = Mathf.Lerp(b.radii[i], b.radii[i + 1], f);
        }

        /// <summary>หา t (0..1) ของจุด sample ลำดับ index</summary>
        public static float ParamAtSample(BuiltBranch b, int index)
        {
            index = Mathf.Clamp(index, 0, b.cum.Count - 1);
            return Mathf.Clamp01(b.cum[index] / b.length);
        }

        // ------------------------------------------------------------------ mesh parts

        static float LengthDetail(TreePreset p)
        {
            return Mathf.Clamp(p.lengthDetail, 0.1f, 4f);
        }

        /// <summary>ตัดจุดที่เส้นเกือบตรงและขนาดแทบไม่เปลี่ยนออก (ลด poly ตามความยาวกิ่ง)</summary>
        static void Simplify(BuiltBranch bb, float angleDeg)
        {
            int n = bb.pts.Count;
            if (angleDeg <= 0f || n <= 3) return;
            var keepP = new List<Vector3>(n) { bb.pts[0] };
            var keepR = new List<float>(n) { bb.radii[0] };
            int last = 0;
            for (int i = 1; i < n - 1; i++)
            {
                Vector3 a = bb.pts[i] - bb.pts[last];
                Vector3 b = bb.pts[i + 1] - bb.pts[i];
                float ang = Vector3.Angle(a, b);
                float rLast = bb.radii[last];
                bool radiusOk = Mathf.Abs(bb.radii[i + 1] - rLast) < rLast * 0.2f;
                if (ang < angleDeg && radiusOk) continue;
                keepP.Add(bb.pts[i]);
                keepR.Add(bb.radii[i]);
                last = i;
            }
            keepP.Add(bb.pts[n - 1]);
            keepR.Add(bb.radii[n - 1]);
            bb.pts.Clear();
            bb.pts.AddRange(keepP);
            bb.radii.Clear();
            bb.radii.AddRange(keepR);
        }

        static void Tube(BuiltBranch bb, TreePreset p, float radialFactor, List<Vector3> V, List<Vector3> N,
            List<Vector2> UV, List<Vector2> UV2, List<Color> C, List<int> tr)
        {
            int count = bb.pts.Count;
            if (count < 2) return;

            bool stylized = p.style == TreeStyle.Stylized;
            float detail = Mathf.Clamp(p.radialDetail, 0.1f, 4f);
            int maxSides = Mathf.Clamp(p.maxSides, 3, 32);
            float rr = Mathf.Max(0.001f, bb.uvRadius);
            int sides = stylized
                ? Mathf.Clamp(Mathf.RoundToInt((3f + rr * 14f) * Mathf.Lerp(1f, radialFactor, 0.5f) * detail), 3, Mathf.Min(7, maxSides))
                : Mathf.Clamp(Mathf.RoundToInt((4f + rr * 40f) * radialFactor * detail), 3, maxSides);
            int ring = sides + 1;
            int start = V.Count;

            Vector3 tan0 = (bb.pts[1] - bb.pts[0]).normalized;
            if (tan0.sqrMagnitude < 1e-8f) tan0 = Vector3.up;
            Vector3 nrm = TreeSpline.AnyPerpendicular(tan0);
            float circ = 2f * Mathf.PI * Mathf.Max(0.01f, rr);
            Color last = Color.white;

            for (int i = 0; i < count; i++)
            {
                Vector3 t;
                if (i == 0) t = tan0;
                else if (i == count - 1) t = bb.pts[i] - bb.pts[i - 1];
                else t = bb.pts[i + 1] - bb.pts[i - 1];
                t = t.sqrMagnitude > 1e-10f ? t.normalized : tan0;

                nrm = Vector3.ProjectOnPlane(nrm, t);
                if (nrm.sqrMagnitude < 1e-8f) nrm = TreeSpline.AnyPerpendicular(t);
                nrm.Normalize();
                Vector3 bin = Vector3.Cross(t, nrm);

                float rad = bb.radii[i];
                float v = bb.cum[i] / circ;
                float flex = Mathf.Clamp01(bb.flexBase + (bb.cum[i] / bb.length) * (bb.level == 0 ? 0.15f : 0.35f));
                last = new Color(flex, bb.phase, 0f, Mathf.Clamp01(bb.level / 4f));
                bool blend = bb.flareBlend && i <= bb.flareCount;

                for (int j = 0; j <= sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2f;
                    Vector3 dv = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    Vector3 pos = bb.pts[i] + dv * rad;
                    Vector3 n = dv;
                    if (blend)
                    {
                        Vector3 pn = pos - bb.attachAxis;
                        if (pn.sqrMagnitude > 1e-8f) n = Vector3.Lerp(n, pn.normalized, 0.5f).normalized;
                    }
                    V.Add(pos);
                    N.Add(n);
                    UV.Add(new Vector2(j / (float)sides, v));
                    UV2.Add(Vector2.zero);
                    C.Add(last);
                }
            }

            for (int i = 0; i < count - 1; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a0 = start + i * ring + j;
                    int b0 = a0 + ring;
                    tr.Add(a0); tr.Add(a0 + 1); tr.Add(b0);
                    tr.Add(a0 + 1); tr.Add(b0 + 1); tr.Add(b0);
                }
            }

            // ปิดปลายกิ่งเป็นกรวยเล็ก
            Vector3 lt = (bb.pts[count - 1] - bb.pts[count - 2]).normalized;
            int tip = V.Count;
            V.Add(bb.pts[count - 1] + lt * bb.radii[count - 1]);
            N.Add(lt);
            UV.Add(new Vector2(0.5f, bb.cum[count - 1] / circ));
            UV2.Add(Vector2.zero);
            C.Add(last);
            int lastRing = start + (count - 1) * ring;
            for (int j = 0; j < sides; j++)
            {
                tr.Add(lastRing + j); tr.Add(lastRing + j + 1); tr.Add(tip);
            }
        }

        /// <summary>แยก vertex ของสามเหลี่ยมใน list นี้ออกจากกัน ให้ได้เงาแบบเหลี่ยม (Stylized)</summary>
        static void Flatten(List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<Vector2> UV2, List<Color> C, List<int> tr)
        {
            var nv = new List<Vector3>(tr.Count);
            var nn = new List<Vector3>(tr.Count);
            var nu = new List<Vector2>(tr.Count);
            var nu2 = new List<Vector2>(tr.Count);
            var nc = new List<Color>(tr.Count);
            for (int i = 0; i < tr.Count; i += 3)
            {
                Vector3 a = V[tr[i]], b = V[tr[i + 1]], c = V[tr[i + 2]];
                Vector3 fn = Vector3.Cross(b - a, c - a);
                fn = fn.sqrMagnitude > 1e-12f ? fn.normalized : N[tr[i]];
                for (int k = 0; k < 3; k++)
                {
                    int idx = tr[i + k];
                    nv.Add(V[idx]);
                    nn.Add(fn);
                    nu.Add(UV[idx]);
                    nu2.Add(UV2[idx]);
                    nc.Add(C[idx]);
                }
            }

            V.Clear(); V.AddRange(nv);
            N.Clear(); N.AddRange(nn);
            UV.Clear(); UV.AddRange(nu);
            UV2.Clear(); UV2.AddRange(nu2);
            C.Clear(); C.AddRange(nc);
            for (int i = 0; i < tr.Count; i++) tr[i] = i;
        }

        /// <summary>
        /// ใบแบบแผ่น: 1–3 แผ่นขัดกันรอบแกนกลางใบ (มุมระหว่างแผ่นปรับได้ 90° = +, น้อยลง = X)
        /// แล้วสุ่มหมุนทั้งช่อ ไม่ให้ใบเรียงเป็นเส้นตรงจนดูออก
        /// </summary>
        static void Card(LeafInst l, Vector3 center, TreePreset p, List<Vector3> V, List<Vector3> N, List<Vector2> UV,
            List<Vector2> UV2, List<Color> C, List<int> tr)
        {
            Vector3 outward = l.pos - center;
            outward.y *= 0.5f;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : l.dir;
            Vector3 facing = (outward + l.dir * 0.8f).normalized;
            Vector3 yAxis;
            if (p.leafAttach)
            {
                // ใบชี้ออกจากกิ่ง (ไปทางปลายกิ่งเล็กน้อย) โคนใบจะได้ต่อกับกิ่งพอดี
                Vector3 side = Vector3.ProjectOnPlane(l.dir, l.tan);
                side = side.sqrMagnitude > 1e-6f ? side.normalized : TreeSpline.AnyPerpendicular(l.tan);
                yAxis = (l.tan * 0.55f + side * 0.85f + Vector3.up * 0.15f).normalized;
                facing = Vector3.ProjectOnPlane(facing, yAxis);
                if (facing.sqrMagnitude < 1e-6f) facing = TreeSpline.AnyPerpendicular(yAxis);
                facing.Normalize();
            }
            else yAxis = (l.tan + Vector3.up * 0.3f + l.dir * 0.5f).normalized;
            Vector3 xAxis = Vector3.Cross(yAxis, facing);
            xAxis = xAxis.sqrMagnitude > 1e-8f ? xAxis.normalized : TreeSpline.AnyPerpendicular(yAxis);
            facing = Vector3.Cross(xAxis, yAxis).normalized;

            // สุ่มหมุนทั้งช่อ: หันซ้าย-ขวา / ก้ม-เงย / เอียง
            float jy = (Hash01(l.key ^ 0x1B873593) * 2f - 1f) * p.leafYawJitter;
            float jp = (Hash01(l.key ^ 0x5A17E3C1) * 2f - 1f) * p.leafPitchJitter;
            float jr = (Hash01(l.key ^ 0x27D4EB2F) * 2f - 1f) * p.leafRollJitter;
            Quaternion jq = Quaternion.AngleAxis(jy, yAxis) * Quaternion.AngleAxis(jp, xAxis) * Quaternion.AngleAxis(jr, facing);
            yAxis = jq * yAxis;
            xAxis = jq * xAxis;
            facing = jq * facing;

            float s = l.size;
            Vector3 basePt = l.pos - yAxis * s * (p.leafAttach ? Mathf.Clamp(p.leafPivotV, 0f, 0.5f) : 0.15f);
            Vector3 sph = l.pos - center;
            var col = new Color(1f, l.phase, 1f, 1f);
            var hue = new Vector2(l.hue, 0f);

            int planes = Mathf.Clamp(p.leafPlanes, 1, 3);
            for (int i = 0; i < planes; i++)
            {
                float ang = 0f;
                if (i > 0)
                    ang = i * p.leafPlaneAngle + (Hash01(l.key ^ (0x3C6EF372 + i * 7919)) * 2f - 1f) * p.leafPlaneJitter;
                Quaternion pr = Quaternion.AngleAxis(ang, yAxis);
                Vector3 x = pr * xAxis;
                Vector3 f = pr * facing;

                Vector3 v0 = basePt - x * s * 0.5f;
                Vector3 v1 = basePt + x * s * 0.5f;
                Vector3 v2 = v1 + yAxis * s;
                Vector3 v3 = v0 + yAxis * s;
                Vector3 n = sph.sqrMagnitude > 1e-6f ? Vector3.Lerp(f, sph.normalized, 0.7f).normalized : f;

                int i0 = V.Count;
                V.Add(v0); V.Add(v1); V.Add(v2); V.Add(v3);
                N.Add(n); N.Add(n); N.Add(n); N.Add(n);
                UV.Add(new Vector2(0f, 0f)); UV.Add(new Vector2(1f, 0f)); UV.Add(new Vector2(1f, 1f)); UV.Add(new Vector2(0f, 1f));
                UV2.Add(hue); UV2.Add(hue); UV2.Add(hue); UV2.Add(hue);
                C.Add(col); C.Add(col); C.Add(col); C.Add(col);
                tr.Add(i0); tr.Add(i0 + 1); tr.Add(i0 + 2);
                tr.Add(i0); tr.Add(i0 + 2); tr.Add(i0 + 3);
            }
        }

        static readonly Vector3[] Ico = BuildIco();

        static readonly int[] IcoFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        static Vector3[] BuildIco()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
            return v;
        }

        static void Blob(LeafInst l, Vector3 center, float messiness, List<Vector3> V, List<Vector3> N, List<Vector2> UV,
            List<Vector2> UV2, List<Color> C, List<int> tr)
        {
            float R = l.size;
            Quaternion rot = Quaternion.LookRotation(l.dir, Vector3.up);
            int seed = Mathf.FloorToInt(l.hue * 100000f);
            var pts = new Vector3[Ico.Length];
            for (int i = 0; i < Ico.Length; i++)
            {
                float j = 1f + (Hash01(seed + i * 977) - 0.5f) * (0.2f + messiness * 0.3f);
                Vector3 lp = Ico[i] * R * j;
                lp.y *= 0.85f;
                pts[i] = l.pos + rot * lp;
            }

            var col = new Color(1f, l.phase, 1f, 1f);
            var hue = new Vector2(l.hue, 0f);
            for (int f = 0; f < IcoFaces.Length; f += 3)
            {
                Vector3 a = pts[IcoFaces[f]], b = pts[IcoFaces[f + 1]], c = pts[IcoFaces[f + 2]];
                Vector3 fn = Vector3.Cross(b - a, c - a);
                Vector3 mid = (a + b + c) / 3f;
                if (Vector3.Dot(fn, mid - l.pos) < 0f)
                {
                    Vector3 tmp = b; b = c; c = tmp;
                    fn = -fn;
                }
                fn = fn.normalized;
                int i0 = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                for (int k = 0; k < 3; k++)
                {
                    N.Add(fn);
                    C.Add(col);
                    UV2.Add(hue);
                }
                UV.Add(BlobUV(a, l.pos, R)); UV.Add(BlobUV(b, l.pos, R)); UV.Add(BlobUV(c, l.pos, R));
                tr.Add(i0); tr.Add(i0 + 1); tr.Add(i0 + 2);
            }
        }

        static Vector2 BlobUV(Vector3 p, Vector3 c, float r)
        {
            Vector3 d = (p - c) / Mathf.Max(0.0001f, r);
            return new Vector2(0.5f + d.x * 0.45f, 0.5f + d.y * 0.45f);
        }

        // ------------------------------------------------------------------ random

        public static float Hash01(int k)
        {
            unchecked
            {
                uint h = (uint)k * 2654435761u;
                h ^= h >> 16;
                h *= 0x85ebca6bu;
                h ^= h >> 13;
                h *= 0xc2b2ae35u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        public static Vector3 RandVec(System.Random r)
        {
            return new Vector3((float)(r.NextDouble() * 2.0 - 1.0), (float)(r.NextDouble() * 2.0 - 1.0),
                (float)(r.NextDouble() * 2.0 - 1.0));
        }

        public static int PickWeighted(List<WeightedMaterial> list, double r)
        {
            if (list == null || list.Count == 0) return 0;
            float total = 0f;
            for (int i = 0; i < list.Count; i++) total += Mathf.Max(0f, list[i].weight);
            if (total <= 0f) return Mathf.Clamp((int)(r * list.Count), 0, list.Count - 1);
            float x = (float)r * total;
            for (int i = 0; i < list.Count; i++)
            {
                x -= Mathf.Max(0f, list[i].weight);
                if (x <= 0f) return i;
            }
            return list.Count - 1;
        }
    }
}
