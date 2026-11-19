using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// แถบ SP ตอนรวมเป็นเส้นเดียว — วาดทรงตามช่องจริง (เล็กไปใหญ่ก็ตามได้) พร้อมแสงวิ่งในตัว
// SkillPointBarUI สร้างและคุมเอง ไม่ต้องใส่เองใน Hierarchy
[RequireComponent(typeof(CanvasRenderer))]
public class SPWedgeBar : MaskableGraphic
{
    private const int Columns = 64;     // แบ่งแนวนอนกี่ช่วง — แสงวิ่งจะได้นุ่ม
    private const float Feather = 1f;   // ขอบนุ่มบนล่างกี่พิกเซล กันขอบหยัก

    // จุดคุมทรงเรียงซ้ายไปขวา: x = ตำแหน่งแนวนอน, y = ขอบล่าง, z = ขอบบน (พิกัดของตัวเอง)
    private readonly List<Vector3> _profile = new();
    private float _reveal = 1f;
    private float _shinePos = -1f;
    private float _shineWidth = 0.15f;
    private float _shineAlpha;
    private float _shineSlant;
    private float _outlineWidth;
    private Color _outlineColor = Color.clear;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    /// <param name="reveal">0–1 แถบโผล่ถึงไหน นับจากซ้าย</param>
    /// <param name="shinePos">กลางแสง 0–1 ตามความยาวแถบ (เลยขอบได้)</param>
    /// <param name="shineWidth">ความกว้างแสง เทียบความยาวแถบ</param>
    /// <param name="shineSlant">แสงเอียงแค่ไหน เทียบความยาวแถบ</param>
    public void SetState(List<Vector3> profile, float reveal, float shinePos, float shineWidth, float shineAlpha, float shineSlant)
    {
        reveal = Mathf.Clamp01(reveal);
        shineWidth = Mathf.Max(0.001f, shineWidth);

        // ไม่มีอะไรเปลี่ยน = ไม่ต้องสร้าง mesh ใหม่ (กรอบหลอดนิ่งเกือบตลอด)
        if (SameProfile(profile) && Mathf.Approximately(_reveal, reveal) && Mathf.Approximately(_shinePos, shinePos)
            && Mathf.Approximately(_shineWidth, shineWidth) && Mathf.Approximately(_shineAlpha, shineAlpha)
            && Mathf.Approximately(_shineSlant, shineSlant))
            return;

        _profile.Clear();
        _profile.AddRange(profile);
        _reveal = reveal;
        _shinePos = shinePos;
        _shineWidth = shineWidth;
        _shineAlpha = shineAlpha;
        _shineSlant = shineSlant;
        SetVerticesDirty();
    }

    /// <summary>เส้นขอบรอบนอก วาดใน mesh เดียวกันก่อนตัวหลอด — ขอบอยู่ข้างหลังเสมอ ไม่ต้องพึ่งลำดับใน Hierarchy</summary>
    public void SetOutline(float width, Color outlineColor)
    {
        width = Mathf.Max(0f, width);
        if (Mathf.Approximately(width, _outlineWidth) && outlineColor == _outlineColor) return;
        _outlineWidth = width;
        _outlineColor = outlineColor;
        SetVerticesDirty();
    }

    private bool SameProfile(List<Vector3> profile)
    {
        if (profile.Count != _profile.Count) return false;
        for (int i = 0; i < profile.Count; i++)
            if ((profile[i] - _profile[i]).sqrMagnitude > 0.0001f) return false;
        return true;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_profile.Count < 2 || _reveal <= 0.0001f) return;

        // ขอบก่อน แล้วตัวหลอดทับ — ใน mesh เดียวกันวาดตามลำดับเสมอ
        if (_outlineWidth > 0f && _outlineColor.a > 0f)
            DrawPass(vh, _outlineWidth, true);

        DrawPass(vh, 0f, false);
    }

    private void DrawPass(VertexHelper vh, float expand, bool outline)
    {
        float left = _profile[0].x;
        float length = _profile[^1].x - left;
        if (length <= 0.01f) return;

        float cut = left + length * _reveal;
        float from = left - expand;
        float to = cut + (_reveal >= 1f ? expand : 0f);
        int columns = Mathf.Max(1, Mathf.CeilToInt(Columns * _reveal));
        int segment = 0;
        int start = vh.currentVertCount;

        for (int i = 0; i <= columns; i++)
        {
            float x = Mathf.Lerp(from, to, (float)i / columns);
            Sample(Mathf.Clamp(x, left, _profile[^1].x), ref segment, out float bottom, out float top);
            bottom -= expand;
            top += expand;

            float t = (x - left) / length;
            Color topColor = outline ? _outlineColor : Shade(t - _shineSlant);
            Color bottomColor = outline ? _outlineColor : Shade(t + _shineSlant);

            AddVertex(vh, x, top + Feather, Clear(topColor));
            AddVertex(vh, x, top, topColor);
            AddVertex(vh, x, bottom, bottomColor);
            AddVertex(vh, x, bottom - Feather, Clear(bottomColor));
        }

        for (int i = 0; i < columns; i++)
        {
            int a = start + i * 4;
            int b = a + 4;
            for (int k = 0; k < 3; k++)
            {
                vh.AddTriangle(a + k, a + k + 1, b + k + 1);
                vh.AddTriangle(a + k, b + k + 1, b + k);
            }
        }
    }

    // ความสูงตรงจุด x — ต่อเส้นตรงระหว่างจุดคุม
    private void Sample(float x, ref int segment, out float bottom, out float top)
    {
        while (segment < _profile.Count - 2 && x > _profile[segment + 1].x)
            segment++;

        Vector3 a = _profile[segment];
        Vector3 b = _profile[segment + 1];
        float t = b.x - a.x > 0.0001f ? Mathf.Clamp01((x - a.x) / (b.x - a.x)) : 0f;
        bottom = Mathf.Lerp(a.y, b.y, t);
        top = Mathf.Lerp(a.z, b.z, t);
    }

    private Color Shade(float t)
    {
        Color c = color;
        if (_shineAlpha <= 0f) return c;

        float u = (t - _shinePos) / _shineWidth;
        if (u <= -1f || u >= 1f) return c;

        float k = 1f - u * u;
        k *= k * _shineAlpha;
        return new Color(Mathf.Lerp(c.r, 1f, k), Mathf.Lerp(c.g, 1f, k), Mathf.Lerp(c.b, 1f, k), c.a);
    }

    private static Color Clear(Color c)
    {
        c.a = 0f;
        return c;
    }

    private static void AddVertex(VertexHelper vh, float x, float y, Color c)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = new Vector3(x, y, 0f);
        vertex.color = c;
        vh.AddVert(vertex);
    }
}