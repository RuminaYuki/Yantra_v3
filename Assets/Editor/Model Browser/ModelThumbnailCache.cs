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

        // ---- GPU error tracking ----
        // DX11/D3D errors don't say which object caused them. We remember the prefab rendered most recently;
        // a GPU error logged during or shortly after its render is attributed to it, and that prefab
        // falls back to Unity's AssetPreview from then on (list kept in Library, cleared by "Clear cache").
        static readonly HashSet<string> problems = new HashSet<string>();
        static string lastRenderedGuid;
        static double lastRenderTime;
        const double ErrorAttributionWindow = 1.0; // seconds
        public static int ProblemCount => problems.Count;
        public static event Action ProblemsChanged;

        static string Folder => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "Library", "ModelBrowserThumbnails");
        static string ProblemsFile => Path.Combine(Folder, "_problems.txt");

        static ModelThumbnailCache()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
            Application.logMessageReceived += OnLog;
            LoadProblems();
        }

        public static bool IsProblem(string guid) => problems.Contains(guid);

        static bool IsGpuError(string msg) =>
            msg.IndexOf("Texture index out of range", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("d3d11", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("dx11", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("d3d12", StringComparison.OrdinalIgnoreCase) >= 0;

        static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Assert && type != LogType.Exception) return;
            if (lastRenderedGuid == null || message.StartsWith("[Model Browser]")) return;
            if (EditorApplication.timeSinceStartup - lastRenderTime > ErrorAttributionWindow) return;
            if (!IsGpuError(message)) return;

            string guid = lastRenderedGuid;
            lastRenderedGuid = null;
            if (!problems.Add(guid)) return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Debug.LogWarning($"[Model Browser] GPU error while rendering the thumbnail of \"{path}\" → that prefab now uses Unity's preview instead.\nError: {message}\nCheck the shader / material of this prefab (sampler count, Texture2DArray index, Decal/VFX).",
                AssetDatabase.LoadMainAssetAtPath(path));
            DeleteFiles(guid);
            if (loaded.TryGetValue(guid, out var t) && t != null) Object.DestroyImmediate(t);
            loaded.Remove(guid);
            loadedKey.Remove(guid);
            SaveProblems();
            ProblemsChanged?.Invoke();
        }

        static void LoadProblems()
        {
            problems.Clear();
            if (File.Exists(ProblemsFile))
                foreach (var line in File.ReadAllLines(ProblemsFile))
                    if (!string.IsNullOrWhiteSpace(line)) problems.Add(line.Trim());
        }

        static void SaveProblems()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(ProblemsFile, problems);
        }

        /// <summary>Returns the cached thumbnail, or null and queues a render.</summary>
        public static Texture2D Get(string guid)
        {
            if (!ModelBrowserPrefs.UseCustomThumbnails || problems.Contains(guid)) return null;
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
            if (problems.Remove(guid)) { SaveProblems(); ProblemsChanged?.Invoke(); }
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
            problems.Clear();
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            ProblemsChanged?.Invoke();
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
            // Rendering itself happens in ProcessQueue (called from a GUI Repaint). Here we only release
            // the preview scene a few seconds after the queue empties.
            if (queue.Count == 0 && pru != null && EditorApplication.timeSinceStartup - idleSince > 5)
            {
                pru.Cleanup();
                pru = null;
            }
        }

        static readonly List<string> readyBuffer = new List<string>();
        static bool processing;

        /// <summary>
        /// Renders queued thumbnails for up to <see cref="FrameBudgetMs"/>.
        /// MUST be called during a GUI Repaint event (e.g. from an IMGUIContainer) — rendering from
        /// EditorApplication.update happens outside the graphics frame and triggers
        /// "GUI Window tried to begin rendering..." / "device.IsInsideFrame()" errors.
        /// </summary>
        public static void ProcessQueue()
        {
            if (processing || queue.Count == 0) return;
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            processing = true;
            try
            {
                var sw = Stopwatch.StartNew();
                while (queue.Count > 0 && sw.Elapsed.TotalMilliseconds < FrameBudgetMs)
                {
                    string guid = queue.Dequeue();
                    queued.Remove(guid);
                    BatchDone++;
                    try { if (RenderOne(guid)) readyBuffer.Add(guid); }
                    catch (Exception e) { UnityEngine.Debug.LogWarning($"[Model Browser] Thumbnail failed for {AssetDatabase.GUIDToAssetPath(guid)}: {e.Message}"); }
                }
            }
            finally
            {
                processing = false;
                idleSince = EditorApplication.timeSinceStartup;
            }

            // Notify after the repaint finishes so the UI isn't modified mid-paint.
            if (readyBuffer.Count > 0)
            {
                var done = readyBuffer.ToArray();
                readyBuffer.Clear();
                EditorApplication.delayCall += () => { foreach (var g in done) Ready?.Invoke(g); };
            }
        }

        static bool RenderOne(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || problems.Contains(guid)) return false;
            string key = Key(guid);

            EnsureUtility();
            lastRenderedGuid = guid;
            lastRenderTime = EditorApplication.timeSinceStartup;
            // Instantiate directly into the utility's private preview scene (never touches the open scene).
            var inst = PrefabUtility.InstantiatePrefab(prefab, pru.camera.scene) as GameObject;
            if (inst == null) return false;
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
            // An error caught synchronously during Render() already flagged this prefab: discard its image.
            if (problems.Contains(guid))
            {
                DeleteFiles(guid);
                if (loaded.TryGetValue(guid, out var bad) && bad != null) Object.DestroyImmediate(bad);
                loaded.Remove(guid);
                loadedKey.Remove(guid);
                return false;
            }
            return true;
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
