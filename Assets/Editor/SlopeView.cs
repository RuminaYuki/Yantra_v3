using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Yantra.TerrainTools
{
    // ระบายสีบนพื้นตรงที่ชันเกินกว่าผู้เล่นจะเดินขึ้นได้
    internal static class SlopeView
    {
        public static bool Enabled { get; private set; }
        public static float Limit = 45f;
        public static float WarnBand = 5f;
        public static bool SeeThrough;

        static Mesh _mesh;
        static Material _mat;
        static bool _dirty;
        static int _steepCells;

        public static int SteepCells { get { return _steepCells; } }

        public static void SetEnabled(bool on)
        {
            if (on == Enabled) return;
            Enabled = on;
            if (on)
            {
                SceneView.duringSceneGui += OnSceneGUI;
                Rebuild();
            }
            else
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                if (_mesh != null) Object.DestroyImmediate(_mesh);
                _mesh = null;
            }
            SceneView.RepaintAll();
        }

        public static void Rebuild()
        {
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            var red = new Color(0.95f, 0.2f, 0.15f, 0.5f);
            var yellow = new Color(1f, 0.8f, 0.15f, 0.35f);
            _steepCells = 0;

            foreach (Terrain t in Terrain.activeTerrains)
            {
                TerrainData td = t.terrainData;
                if (td == null) continue;

                Vector3 size = td.size;
                Vector3 origin = t.GetPosition();
                int cellsX = Mathf.Clamp(Mathf.CeilToInt(size.x / 1.5f), 16, 256);
                int cellsZ = Mathf.Clamp(Mathf.CeilToInt(size.z / 1.5f), 16, 256);

                for (int z = 0; z < cellsZ; z++)
                {
                    for (int x = 0; x < cellsX; x++)
                    {
                        float u = (x + 0.5f) / cellsX, v = (z + 0.5f) / cellsZ;
                        float steep = td.GetSteepness(u, v);
                        if (steep < Limit - WarnBand) continue;

                        Color c = steep >= Limit ? red : yellow;
                        if (steep >= Limit) _steepCells++;

                        int start = verts.Count;
                        AddCorner(verts, td, origin, size, (float)x / cellsX, (float)z / cellsZ);
                        AddCorner(verts, td, origin, size, (float)(x + 1) / cellsX, (float)z / cellsZ);
                        AddCorner(verts, td, origin, size, (float)(x + 1) / cellsX, (float)(z + 1) / cellsZ);
                        AddCorner(verts, td, origin, size, (float)x / cellsX, (float)(z + 1) / cellsZ);
                        colors.Add(c); colors.Add(c); colors.Add(c); colors.Add(c);
                        tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
                        tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
                    }
                }
            }

            if (_mesh == null)
                _mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateBounds();
            _dirty = false;
            SceneView.RepaintAll();
        }

        static void AddCorner(List<Vector3> verts, TerrainData td, Vector3 origin, Vector3 size, float u, float v)
        {
            float h = td.GetInterpolatedHeight(u, v);
            verts.Add(new Vector3(origin.x + u * size.x, origin.y + h + 0.15f, origin.z + v * size.z));
        }

        static Material Mat
        {
            get
            {
                if (_mat == null)
                {
                    _mat = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                    _mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    _mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    _mat.SetInt("_Cull", (int)CullMode.Off);
                    _mat.SetInt("_ZWrite", 0);
                }
                _mat.SetInt("_ZTest", (int)(SeeThrough ? CompareFunction.Always : CompareFunction.LessEqual));
                return _mat;
            }
        }

        static void OnSceneGUI(SceneView sv)
        {
            Event e = Event.current;

            // ปั้นพื้นเสร็จหนึ่งจังหวะ (ปล่อยเมาส์) ค่อยคำนวณใหม่
            if (e.type == EventType.MouseUp && Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Terrain>() != null)
            {
                _dirty = true;
                EditorApplication.delayCall += () => { if (Enabled && _dirty) Rebuild(); };
            }

            if (e.type == EventType.Repaint && _mesh != null)
            {
                Mat.SetPass(0);
                Graphics.DrawMeshNow(_mesh, Matrix4x4.identity);
            }

            Handles.BeginGUI();
            var rect = new Rect(10f, sv.position.height - 92f, 300f, 60f);
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 20f), "■ แดง = ชันเกิน " + Limit.ToString("0") + "° เดินขึ้นไม่ได้", RedLabel);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 30f, rect.width - 20f, 20f), "■ เหลือง = เกือบชันเกิน", YellowLabel);
            Handles.EndGUI();
        }

        static GUIStyle _red, _yellow;
        static GUIStyle RedLabel
        {
            get
            {
                if (_red == null) { _red = new GUIStyle(EditorStyles.boldLabel); _red.normal.textColor = new Color(1f, 0.45f, 0.4f); }
                return _red;
            }
        }
        static GUIStyle YellowLabel
        {
            get
            {
                if (_yellow == null) { _yellow = new GUIStyle(EditorStyles.boldLabel); _yellow.normal.textColor = new Color(1f, 0.82f, 0.3f); }
                return _yellow;
            }
        }
    }
}
