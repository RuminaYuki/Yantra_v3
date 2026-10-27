using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ModelBrowser
{
    /// <summary>
    /// Placement Mode: a ghost of the selected prefab follows the mouse in the Scene view.
    /// LMB place (repeatable) · R / Shift+R or Shift+Scroll rotate · Esc / right-click exit.
    /// Also handles drag & drop from the Model Browser into the Scene view (with snap + parent).
    /// </summary>
    [InitializeOnLoad]
    public static class ModelPlacementTool
    {
        public const string DragKey = "ModelBrowserItem";

        public static string ActivePath { get; private set; }
        public static bool IsActive => ActivePath != null;
        public static Transform Parent { get; set; }
        public static event Action Changed;

        static GameObject ghost;
        static string ghostPath;
        static Quaternion ghostRootRotation = Quaternion.identity;
        static float yaw;
        static bool hasPose;
        static Vector3 posePos;
        static Quaternion poseRot;
        static Vector2 rmbDownPos;
        static readonly PlacementShortcutContext shortcutContext = new PlacementShortcutContext();
        static bool contextRegistered;

        static ModelPlacementTool()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            AssemblyReloadEvents.beforeAssemblyReload += () => { End(); DestroyGhost(); };
            EditorSceneManager.sceneClosing += (s, r) => DestroyGhost();
            EditorApplication.playModeStateChanged += s => { End(); DestroyGhost(); };
        }

        public static void Begin(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            ActivePath = path;
            EnsureGhost(path);
            if (!contextRegistered) { ShortcutManager.RegisterContext(shortcutContext); contextRegistered = true; }
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        public static void End()
        {
            if (contextRegistered) { ShortcutManager.UnregisterContext(shortcutContext); contextRegistered = false; }
            if (!IsActive) return;
            ActivePath = null;
            hasPose = false;
            DestroyGhost();
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        public static void Toggle(string path)
        {
            if (ActivePath == path) End();
            else Begin(path);
        }

        // ---------------- Shortcuts (custom context beats the global W/E/R tool shortcuts) ----------------

        class PlacementShortcutContext : IShortcutContext
        {
            public bool active => IsActive && EditorWindow.focusedWindow is SceneView;
        }

        [Shortcut("Model Browser/Rotate Clockwise", typeof(PlacementShortcutContext), KeyCode.R)]
        static void RotateClockwise() => Rotate(1);

        [Shortcut("Model Browser/Rotate Counter-Clockwise", typeof(PlacementShortcutContext), KeyCode.R, ShortcutModifiers.Shift)]
        static void RotateCounterClockwise() => Rotate(-1);

        static void Rotate(int dir)
        {
            yaw = Mathf.Repeat(yaw + dir * ModelBrowserPrefs.RotationStep, 360f);
            if (hasPose)
            {
                poseRot = BaseRotation() * Quaternion.Euler(0f, yaw, 0f);
                ShowGhost(posePos, poseRot);
            }
            SceneView.RepaintAll();
        }

        // ---------------- Scene GUI ----------------

        static void OnSceneGUI(SceneView sv)
        {
            Event e = Event.current;
            if (HandleDrag(e)) return;
            if (!IsActive) return;

            int id = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.MouseMove:
                    // Focus the Scene view so R / Esc reach it without an extra click.
                    if (EditorWindow.focusedWindow != sv) sv.Focus();
                    UpdatePose(e.mousePosition);
                    sv.Repaint();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id) { UpdatePose(e.mousePosition); e.Use(); }
                    break;

                case EventType.MouseLeaveWindow:
                    if (ghost) ghost.SetActive(false);
                    hasPose = false;
                    break;

                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt)
                    {
                        UpdatePose(e.mousePosition);
                        if (hasPose) PlaceInstance(ActivePath, posePos, poseRot);
                        GUIUtility.hotControl = id;
                        e.Use();
                    }
                    else if (e.button == 1) rmbDownPos = e.mousePosition;
                    break;

                case EventType.MouseUp:
                    if (e.button == 0 && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    else if (e.button == 1 && (e.mousePosition - rmbDownPos).sqrMagnitude < 16f) { End(); e.Use(); }
                    break;

                case EventType.ContextClick:
                    End();
                    e.Use();
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape) { End(); e.Use(); }
                    break;

                case EventType.ScrollWheel:
                    if (e.shift)
                    {
                        float d = Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x) ? e.delta.y : e.delta.x;
                        if (d != 0f) Rotate(d > 0 ? 1 : -1);
                        e.Use();
                    }
                    break;

                case EventType.Repaint:
                    DrawGizmo();
                    DrawHud(e.mousePosition);
                    break;
            }
        }

        /// <summary>Drag & drop from the Model Browser window into the Scene view. Returns true if handled.</summary>
        static bool HandleDrag(Event e)
        {
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform && e.type != EventType.DragExited) return false;
            if (!(DragAndDrop.GetGenericData(DragKey) is string path)) return false;

            if (e.type == EventType.DragExited)
            {
                RestoreGhostAfterDrag();
                return true;
            }

            EnsureGhost(path);
            bool ok = ComputePose(e.mousePosition, out var pos, out var rot);
            if (ok) ShowGhost(pos, rot);
            DragAndDrop.visualMode = ok ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (e.type == EventType.DragPerform && ok)
            {
                DragAndDrop.AcceptDrag();
                var go = PlaceInstance(path, pos, rot);
                if (go) Selection.activeGameObject = go;
                RestoreGhostAfterDrag();
            }
            e.Use();
            return true;
        }

        static void RestoreGhostAfterDrag()
        {
            if (IsActive) { EnsureGhost(ActivePath); if (ghost) ghost.SetActive(false); hasPose = false; }
            else DestroyGhost();
        }

        // ---------------- Pose ----------------

        static void UpdatePose(Vector2 mouse)
        {
            hasPose = ComputePose(mouse, out posePos, out poseRot);
            if (hasPose) ShowGhost(posePos, poseRot);
            else if (ghost) ghost.SetActive(false);
        }

        static Transform EffectiveParent()
        {
            if (ModelBrowserPrefs.UseParent && Parent) return Parent;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null ? stage.prefabContentsRoot.transform : null;
        }

        static Quaternion BaseRotation()
        {
            var p = EffectiveParent();
            return p ? p.rotation : Quaternion.identity;
        }

        static bool ComputePose(Vector2 mouse, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = Quaternion.identity;
            bool hit = false;

            if (ModelBrowserPrefs.SurfaceMode)
            {
                // Hide the ghost so the raycast doesn't hit it.
                bool ghostWasActive = ghost && ghost.activeSelf;
                if (ghost) ghost.SetActive(false);
                hit = HandleUtility.PlaceObject(mouse, out pos, out _);
                if (ghost) ghost.SetActive(ghostWasActive);
            }

            if (!hit)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
                var plane = new Plane(Vector3.up, new Vector3(0f, ModelBrowserPrefs.PlaneHeight, 0f));
                if (!plane.Raycast(ray, out float d)) return false;
                pos = ray.GetPoint(d);
            }

            Transform parent = EffectiveParent();
            if (ModelBrowserPrefs.Snap)
            {
                float g = ModelBrowserPrefs.GridSize;
                Vector3 local = parent ? parent.InverseTransformPoint(pos) : pos;
                local.x = Mathf.Round(local.x / g) * g;
                local.z = Mathf.Round(local.z / g) * g;
                if (ModelBrowserPrefs.SnapY) local.y = Mathf.Round(local.y / g) * g;
                pos = parent ? parent.TransformPoint(local) : local;
            }

            rot = BaseRotation() * Quaternion.Euler(0f, yaw, 0f);
            return true;
        }

        // ---------------- Ghost ----------------

        static void EnsureGhost(string path)
        {
            if (ghost && ghostPath == path) return;
            DestroyGhost();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) return;

            ghost = Object.Instantiate(prefab);
            ghost.name = "[Model Browser Ghost]";
            // Strip scripts so the ghost can't run game logic.
            foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                try { Object.DestroyImmediate(mb); } catch { /* dependency order, ignore */ }
            }
            foreach (var t in ghost.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            ghostRootRotation = prefab.transform.localRotation;
            ghostPath = path;
            ghost.SetActive(false);
        }

        static void ShowGhost(Vector3 pos, Quaternion rot)
        {
            if (!ghost) return;
            ghost.transform.SetPositionAndRotation(pos, rot * ghostRootRotation);
            ghost.SetActive(true);
        }

        static void DestroyGhost()
        {
            if (ghost) Object.DestroyImmediate(ghost);
            ghost = null;
            ghostPath = null;
        }

        static void DrawGizmo()
        {
            if (!hasPose || !ghost || !ghost.activeSelf) return;
            var renderers = ghost.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                Handles.DrawWireCube(b.center, b.size);
            }
            Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
            float s = HandleUtility.GetHandleSize(posePos) * 0.15f;
            Handles.DrawWireDisc(posePos, Vector3.up, s);
            Handles.DrawLine(posePos, posePos + poseRot * Vector3.forward * s * 2f); // facing direction
        }

        static void DrawHud(Vector2 mouse)
        {
            Handles.BeginGUI();
            string name = System.IO.Path.GetFileNameWithoutExtension(ActivePath);
            string text = $"{name}   {yaw:0}°\nLMB place · R / Shift+R rotate · Esc / RMB exit";
            var content = new GUIContent(text);
            var style = EditorStyles.helpBox;
            Vector2 size = style.CalcSize(content);
            GUI.Label(new Rect(mouse.x + 18, mouse.y + 18, size.x + 8, size.y + 4), content, style);
            Handles.EndGUI();
        }

        // ---------------- Placing ----------------

        static GameObject PlaceInstance(string path, Vector3 pos, Quaternion rot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) return null;

            Transform parent = EffectiveParent();
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            Scene scene = parent ? parent.gameObject.scene : (stage != null ? stage.scene : SceneManager.GetActiveScene());

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (!go) return null;
            if (parent) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, rot * prefab.transform.localRotation);
            Undo.RegisterCreatedObjectUndo(go, "Place " + prefab.name);

            ModelBrowserPrefs.AddRecent(AssetDatabase.AssetPathToGUID(path));
            return go;
        }
    }
}
