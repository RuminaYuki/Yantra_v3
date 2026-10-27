using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ModelBrowser
{
    /// <summary>
    /// Renders prefab thumbnails with our own camera (PreviewRenderUtility, works with HDRP),
    /// caches them as PNG in Library/ModelBrowserThumbnails, and renders a few per frame so the editor never freezes.
    /// A thumbnail is re-rendered automatically when the prefab's dependency hash or the thumbnail settings change.
    /// </summary>
    [InitializeOnLoad]
    public static class ModelThumbnailCache
    {
        public const int Size = 256;
        const double FrameBudgetMs = 12; // time spent rendering per editor update

        public static event Action<string> Ready; // guid
        public static int Pending => queue.Count;
        public static int BatchTotal { get; private set; }
        public static int BatchDone { get; private set; }

        static readonly Dictionary<string, Texture2D> loaded = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, string> loadedKey = new Dictionary<string, string>();
        static readonly Queue<string> queue = new Queue<string>();
        static readonly HashSet<string> queued = new HashSet<string>();
        static PreviewRenderUtility pru;
        static double idleSince;

        static string Folder => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "Library", "ModelBrowserThumbnails");

        static ModelThumbnailCache()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        /// <summary>Returns the cached thumbnail, or null and queues a render.</summary>
        public static Texture2D Get(string guid)
        {
            string key = Key(guid);
            if (key == null) return null;
            if (loaded.TryGetValue(guid, out var tex) && tex != null && loadedKey[guid] == key) return tex;

            string file = Path.Combine(Folder, key + ".png");
            if (File.Exists(file))
            {
                tex = LoadPng(file);
                if (tex != null) { Store(guid, key, tex); return tex; }
            }
            Enqueue(guid);
            return null;
        }

        /// <summary>Returns a thumbnail only if already in memory (no disk access, no queueing).</summary>
        public static Texture2D GetLoaded(string guid) => loaded.TryGetValue(guid, out var t) ? t : null;

        public static void ForceRerender(string guid)
        {
            DeleteFiles(guid);
            if (loaded.TryGetValue(guid, out var t) && t != null) Object.DestroyImmediate(t);
            loaded.Remove(guid);
            loadedKey.Remove(guid);
            Enqueue(guid);
        }

        public static void ClearAll()
        {
            CancelQueue();
            foreach (var t in loaded.Values) if (t != null) Object.DestroyImmediate(t);
            loaded.Clear();
            loadedKey.Clear();
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
        }

        /// <summary>Drop queued jobs (e.g. when switching tabs) so the new view renders first.</summary>
        public static void CancelQueue()
        {
            queue.Clear();
            queued.Clear();
            BatchTotal = BatchDone = 0;
        }

        static void Enqueue(string guid)
        {
            if (!queued.Add(guid)) return;
            if (queue.Count == 0) BatchTotal = BatchDone = 0;
            queue.Enqueue(guid);
            BatchTotal++;
        }

        static string Key(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            Hash128 h = AssetDatabase.GetAssetDependencyHash(path);
            return guid + "_" + h + "_" + SettingsHash();
        }

        static string SettingsHash()
        {
            string s = $"{Size}|{ModelBrowserPrefs.ThumbYaw}|{ModelBrowserPrefs.ThumbPitch}|{ModelBrowserPrefs.ThumbLight}|{ModelBrowserPrefs.ThumbAmbient}|{ColorUtility.ToHtmlStringRGBA(ModelBrowserPrefs.ThumbBackground)}";
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h.ToString("x8");
        }

        static void Update()
        {
            if (queue.Count == 0)
            {
                // Release the preview scene a few seconds after the queue empties.
                if (pru != null && EditorApplication.timeSinceStartup - idleSince > 5) { pru.Cleanup(); pru = null; }
                return;
            }

            var sw = Stopwatch.StartNew();
            while (queue.Count > 0 && sw.Elapsed.TotalMilliseconds < FrameBudgetMs)
            {
                string guid = queue.Dequeue();
                queued.Remove(guid);
                BatchDone++;
                try { RenderOne(guid); }
                catch (Exception e) { UnityEngine.Debug.LogWarning($"[Model Browser] Thumbnail failed for {AssetDatabase.GUIDToAssetPath(guid)}: {e.Message}"); }
            }
            idleSince = EditorApplication.timeSinceStartup;
        }

        static void RenderOne(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;
            string key = Key(guid);

            EnsureUtility();
            // Instantiate directly into the utility's private preview scene (never touches the open scene).
            var inst = PrefabUtility.InstantiatePrefab(prefab, pru.camera.scene) as GameObject;
            if (inst == null) return;
            try
            {
                foreach (var c in inst.GetComponentsInChildren<Camera>(true)) c.enabled = false;

                Bounds b = CalculateBounds(inst);
                var cam = pru.camera;
                float fov = 30f;
                cam.fieldOfView = fov;
                Quaternion rot = Quaternion.Euler(ModelBrowserPrefs.ThumbPitch, ModelBrowserPrefs.ThumbYaw, 0f);
                float radius = Mathf.Max(b.extents.magnitude, 0.01f);
                float dist = radius / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) * 1.02f;
                cam.transform.SetPositionAndRotation(b.center - rot * Vector3.forward * dist, rot);
                cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 1.5f);
                cam.farClipPlane = dist + radius * 1.5f + 1f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = ModelBrowserPrefs.ThumbBackground;
                ApplyHdrpBackground(cam, ModelBrowserPrefs.ThumbBackground);

                pru.lights[0].intensity = ModelBrowserPrefs.ThumbLight;
                pru.lights[0].transform.rotation = Quaternion.Euler(45f, ModelBrowserPrefs.ThumbYaw + 30f, 0f);
                pru.lights[1].intensity = ModelBrowserPrefs.ThumbLight * 0.5f;
                pru.lights[1].transform.rotation = Quaternion.Euler(340f, ModelBrowserPrefs.ThumbYaw + 200f, 0f);
                float a = ModelBrowserPrefs.ThumbAmbient;
                pru.ambientColor = new Color(a, a, a, 1f);

                pru.BeginStaticPreview(new Rect(0, 0, Size, Size));
                pru.Render(true, false);
                Texture2D tex = pru.EndStaticPreview();
                tex.hideFlags = HideFlags.HideAndDontSave;

                Directory.CreateDirectory(Folder);
                DeleteFiles(guid);
                File.WriteAllBytes(Path.Combine(Folder, key + ".png"), tex.EncodeToPNG());
                if (loaded.TryGetValue(guid, out var old) && old != null && old != tex) Object.DestroyImmediate(old);
                Store(guid, key, tex);
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
            Ready?.Invoke(guid);
        }

        static void EnsureUtility()
        {
            if (pru != null) return;
            pru = new PreviewRenderUtility();
            pru.camera.cameraType = CameraType.Preview;
            pru.camera.allowHDR = true;
        }

        static Bounds CalculateBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            bool has = false;
            Bounds b = new Bounds(go.transform.position, Vector3.one);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // HDRP ignores Camera.backgroundColor and uses HDAdditionalCameraData. Set it by reflection so
        // this tool has no hard dependency on the HDRP package (still works in URP/Built-in).
        static Type hdCameraType;
        static bool hdCameraSearched;
        static void ApplyHdrpBackground(Camera cam, Color color)
        {
            if (!hdCameraSearched)
            {
                hdCameraSearched = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    hdCameraType = asm.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData");
                    if (hdCameraType != null) break;
                }
            }
            if (hdCameraType == null) return;
            try
            {
                var data = cam.GetComponent(hdCameraType) ?? cam.gameObject.AddComponent(hdCameraType);
                var modeField = hdCameraType.GetField("clearColorMode", BindingFlags.Public | BindingFlags.Instance);
                if (modeField != null) modeField.SetValue(data, Enum.Parse(modeField.FieldType, "Color"));
                var colorField = hdCameraType.GetField("backgroundColorHDR", BindingFlags.Public | BindingFlags.Instance);
                if (colorField != null) colorField.SetValue(data, color.linear);
            }
            catch (Exception) { /* fall back to whatever HDRP does by default */ }
        }

        static Texture2D LoadPng(string file)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            if (tex.LoadImage(File.ReadAllBytes(file))) return tex;
            Object.DestroyImmediate(tex);
            return null;
        }

        static void Store(string guid, string key, Texture2D tex)
        {
            loaded[guid] = tex;
            loadedKey[guid] = key;
        }

        static void DeleteFiles(string guid)
        {
            if (!Directory.Exists(Folder)) return;
            foreach (var f in Directory.GetFiles(Folder, guid + "_*.png")) File.Delete(f);
        }

        static void Shutdown()
        {
            pru?.Cleanup();
            pru = null;
            foreach (var t in loaded.Values) if (t != null) Object.DestroyImmediate(t);
            loaded.Clear();
            loadedKey.Clear();
        }
    }
}
