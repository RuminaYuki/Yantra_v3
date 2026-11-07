using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SDFcl.Tool.TreeGenerator
{
    /// <summary>Preview 3D ในหน้าต่าง (ลากหมุน / scroll ซูม) และถ่าย thumbnail</summary>
    public class TreePreviewRenderer : IDisposable
    {
        readonly PreviewRenderUtility pru;
        public float yaw = 30f;
        public float pitch = 8f;
        public float zoom = 1f;

        public TreePreviewRenderer()
        {
            pru = new PreviewRenderUtility();
            pru.camera.fieldOfView = 30f;
            pru.camera.nearClipPlane = 0.05f;
            pru.camera.farClipPlane = 2000f;
            pru.camera.cameraType = CameraType.Preview;
            pru.camera.allowHDR = true;
            pru.camera.clearFlags = CameraClearFlags.SolidColor;
            pru.camera.backgroundColor = Background;
            ApplyHdrpBackground(pru.camera, Background);
            if (pru.lights != null && pru.lights.Length > 0)
            {
                pru.lights[0].intensity = 1.2f;
                pru.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
                if (pru.lights.Length > 1)
                {
                    pru.lights[1].intensity = 0.6f;
                    pru.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 177f);
                }
            }
            pru.ambientColor = new Color(0.35f, 0.37f, 0.4f);
        }

        static readonly Color Background = new Color(0.17f, 0.185f, 0.2f, 1f);

        /// <summary>HDRP ไม่ใช้ Camera.backgroundColor แต่ใช้ HDAdditionalCameraData (ตั้งผ่าน reflection ไม่ต้องพึ่ง HDRP)</summary>
        static void ApplyHdrpBackground(Camera cam, Color color)
        {
            Type hdType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                hdType = asm.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData");
                if (hdType != null) break;
            }
            if (hdType == null) return;
            try
            {
                Component data = cam.GetComponent(hdType) ?? cam.gameObject.AddComponent(hdType);
                FieldInfo mode = hdType.GetField("clearColorMode", BindingFlags.Public | BindingFlags.Instance);
                if (mode != null) mode.SetValue(data, Enum.Parse(mode.FieldType, "Color"));
                FieldInfo col = hdType.GetField("backgroundColorHDR", BindingFlags.Public | BindingFlags.Instance);
                if (col != null) col.SetValue(data, color.linear);
            }
            catch (Exception)
            {
                // ใช้ค่าเริ่มต้นของ HDRP
            }
        }

        public void Dispose()
        {
            pru.Cleanup();
        }

        public void ResetView()
        {
            yaw = 30f;
            pitch = 8f;
            zoom = 1f;
        }

        /// <summary>คืน true ถ้ามุมกล้องเปลี่ยน (ต้อง Repaint)</summary>
        public bool HandleInput(Rect r, params Rect[] ignore)
        {
            int id = GUIUtility.GetControlID("TreeGenPreview".GetHashCode(), FocusType.Passive, r);
            Event e = Event.current;
            foreach (Rect ig in ignore)
                if (ig.Contains(e.mousePosition) && GUIUtility.hotControl != id) return false;

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (r.Contains(e.mousePosition) && e.button == 0)
                    {
                        if (e.clickCount == 2)
                        {
                            ResetView();
                            e.Use();
                            return true;
                        }
                        GUIUtility.hotControl = id;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        yaw += e.delta.x * 0.5f;
                        pitch = Mathf.Clamp(pitch + e.delta.y * 0.5f, -30f, 85f);
                        e.Use();
                        return true;
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
                case EventType.ScrollWheel:
                    if (r.Contains(e.mousePosition))
                    {
                        zoom = Mathf.Clamp(zoom * (1f + e.delta.y * 0.04f), 0.15f, 4f);
                        e.Use();
                        return true;
                    }
                    break;
            }
            return false;
        }

        public void Draw(Rect r, Mesh mesh, Material[] mats)
        {
            if (Event.current.type != EventType.Repaint || mesh == null || r.width < 2f || r.height < 2f) return;
            pru.BeginPreview(r, GUIStyle.none);
            RenderMesh(mesh, mats, yaw, pitch, zoom);
            pru.EndAndDrawPreview(r);
        }

        public Texture2D RenderStatic(Mesh mesh, Material[] mats, int size)
        {
            pru.BeginStaticPreview(new Rect(0, 0, size, size));
            if (mesh != null) RenderMesh(mesh, mats, 30f, 8f, 1f);
            return pru.EndStaticPreview();
        }

        void RenderMesh(Mesh mesh, Material[] mats, float y, float p, float z)
        {
            Bounds b = mesh.bounds;
            float radius = Mathf.Max(0.5f, b.extents.magnitude);
            float dist = radius / Mathf.Sin(pru.camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * z;
            Quaternion rot = Quaternion.Euler(p, y, 0f);
            pru.camera.transform.rotation = rot;
            pru.camera.transform.position = b.center - rot * Vector3.forward * dist;
            pru.camera.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
            pru.camera.farClipPlane = dist + radius * 4f;

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mats == null || mats.Length == 0) break;
                Material m = mats[Mathf.Min(s, mats.Length - 1)];
                if (m == null || mesh.GetIndexCount(s) == 0) continue;
                pru.DrawMesh(mesh, Matrix4x4.identity, m, s);
            }
            pru.Render(true, false);
        }
    }
}
