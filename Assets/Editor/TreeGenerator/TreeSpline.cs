using System.Collections.Generic;
using UnityEngine;

namespace SDFcl.Tool.TreeGenerator
{
    /// <summary>คณิตศาสตร์ของเส้น spline (Catmull-Rom ผ่านทุกจุด)</summary>
    public static class TreeSpline
    {
        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// สุ่มจุดตามเส้นโค้งที่ผ่าน pts ทุกจุด · outParam = ตำแหน่งในหน่วย "ลำดับ knot" (เช่น 1.5 = กลางระหว่าง knot 1 กับ 2)
        /// </summary>
        public static void Sample(IList<Vector3> pts, float step, List<Vector3> outPts, List<float> outParam)
        {
            outPts.Clear();
            outParam.Clear();
            int n = pts.Count;
            if (n == 0) return;
            if (n == 1)
            {
                outPts.Add(pts[0]);
                outParam.Add(0f);
                return;
            }

            step = Mathf.Max(0.01f, step);
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p1 = pts[i];
                Vector3 p2 = pts[i + 1];
                Vector3 p0 = i > 0 ? pts[i - 1] : p1 * 2f - p2;
                Vector3 p3 = i + 2 < n ? pts[i + 2] : p2 * 2f - p1;
                float len = Vector3.Distance(p1, p2);
                int seg = Mathf.Clamp(Mathf.CeilToInt(len / step), 1, 24);
                for (int s = 0; s < seg; s++)
                {
                    float t = s / (float)seg;
                    outPts.Add(CatmullRom(p0, p1, p2, p3, t));
                    outParam.Add(i + t);
                }
            }

            outPts.Add(pts[n - 1]);
            outParam.Add(n - 1);
        }

        public static Vector3 Bezier(Vector3 p0, Vector3 c0, Vector3 c1, Vector3 p1, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * c0 + 3f * u * t * t * c1 + t * t * t * p1;
        }

        /// <summary>
        /// ขาอัตโนมัติ (ระยะถึงปลายขาออก) ที่ทำให้ได้เส้นเดียวกับ Catmull-Rom ทุกประการ
        /// ปลายขาชี้ไปทางจุดถัดไป
        /// </summary>
        public static Vector3 AutoTangent(IList<Vector3> pts, int k)
        {
            int n = pts.Count;
            if (n < 2) return Vector3.zero;
            Vector3 m;
            if (k == 0) m = pts[1] - pts[0];
            else if (k == n - 1) m = pts[n - 1] - pts[n - 2];
            else m = (pts[k + 1] - pts[k - 1]) * 0.5f;
            return m / 3f;
        }

        /// <summary>สุ่มจุดตามเส้น Bezier ที่มีขาออก/ขาเข้าของทุกจุด · outParam เหมือน Sample</summary>
        public static void SampleBezier(IList<Vector3> pts, IList<Vector3> tanOut, IList<Vector3> tanIn, float step,
            List<Vector3> outPts, List<float> outParam)
        {
            outPts.Clear();
            outParam.Clear();
            int n = pts.Count;
            if (n == 0) return;
            if (n == 1)
            {
                outPts.Add(pts[0]);
                outParam.Add(0f);
                return;
            }

            step = Mathf.Max(0.01f, step);
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = pts[i];
                Vector3 p1 = pts[i + 1];
                Vector3 c0 = p0 + tanOut[i];
                Vector3 c1 = p1 + tanIn[i + 1];
                float len = (Vector3.Distance(p0, p1) + Vector3.Distance(p0, c0) + Vector3.Distance(c0, c1) + Vector3.Distance(c1, p1)) * 0.5f;
                int seg = Mathf.Clamp(Mathf.CeilToInt(len / step), 1, 32);
                for (int s = 0; s < seg; s++)
                {
                    float t = s / (float)seg;
                    outPts.Add(Bezier(p0, c0, c1, p1, t));
                    outParam.Add(i + t);
                }
            }

            outPts.Add(pts[n - 1]);
            outParam.Add(n - 1);
        }

        public static Vector3 AnyPerpendicular(Vector3 v)
        {
            Vector3 other = Mathf.Abs(v.y) < 0.99f ? Vector3.up : Vector3.right;
            return Vector3.Cross(v, other).normalized;
        }
    }
}
