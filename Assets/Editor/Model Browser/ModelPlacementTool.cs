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
        static Vector2 lastMouse;
        static bool poseDirty;           // set by shortcuts (no GUI context there); pose is recomputed on next Scene GUI event
        static float heightOffset;       // manual offset (E / Q, Ctrl+Scroll), kept between placements
        public static float HeightOffset
        {
            get => heightOffset;
            set { heightOffset = value; poseDirty = true; SceneView.RepaintAll(); }
        }
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

        [Shortcut("Model Browser/Raise", typeof(PlacementShortcutContext), KeyCode.E)]
        static void Raise() => ChangeHeight(1);

        [Shortcut("Model Browser/Lower", typeof(PlacementShortcutContext), KeyCode.Q)]
        static void Lower() => ChangeHeight(-1);

        [Shortcut("Model Browser/Reset Height Offset", typeof(PlacementShortcutContext), KeyCode.Q, ShortcutModifiers.Shift)]
        static void ResetHeight() { HeightOffset = 0f; Changed?.Invoke(); }

        static float HeightStepValue => ModelBrowserPrefs.SnapY ? ModelBrowserPrefs.GridSize : ModelBrowserPrefs.HeightStep;

        static void ChangeHeight(int dir)
        {
            HeightOffset = Mathf.Round((heightOffset + dir * HeightStepValue) * 1000f) / 1000f;
            Changed?.Invoke();
        }

        static void Rotate(int dir)
        {
            yaw = Mathf.Repeat(yaw + dir * ModelBrowserPrefs.RotationStep, 360f);
            poseDirty = true;
            SceneView.RepaintAll();
        }

        // ---------------- Scene GUI ----------------

        static void OnSceneGUI(SceneView sv)
        {
            Event e = Event.current;
            if (HandleDrag(e)) return;
            if (!IsActive) return;

            if (poseDirty && e.type != EventType.Used)
            {
                poseDirty = false;
                UpdatePose(lastMouse);
            }

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
                    if (e.shift || e.control)
                    {
                        float d = Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x) ? e.delta.y : e.delta.x;
                        if (d != 0f)
                        {
                            if (e.control) ChangeHeight(d > 0 ? -1 : 1); // scroll up = raise
                            else Rotate(d > 0 ? 1 : -1);
                        }
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
            lastMouse = mouse;
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

        /// <summary>Where the last pose got its height from (shown in the HUD).</summary>
        static string heightSource = "";
        static float lockedSurfaceY;
        static bool hasLockedY;

        static bool ComputePose(Vector2 mouse, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = BaseRotation() * Quaternion.Euler(0f, yaw, 0f);
            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            GameObject surfaceObj = null;
            bool hit = false;

            // The ghost lives on the Ignore Raycast layer and is excluded from picking, so it can't hit itself.
            {
                if (ModelBrowserPrefs.SurfaceMode)
                    hit = RaycastSurface(mouse, ray, out pos, out surfaceObj);

                if (!hit)
                {
                    var plane = new Plane(Vector3.up, new Vector3(0f, ModelBrowserPrefs.PlaneHeight, 0f));
                    if (!plane.Raycast(ray, out float d)) return false;
                    pos = ray.GetPoint(d);
                    heightSource = $"plane Y {ModelBrowserPrefs.PlaneHeight:0.##}";
                }

                Transform parent = EffectiveParent();
                if (ModelBrowserPrefs.Snap)
                {
                    float g = ModelBrowserPrefs.GridSize;
                    Vector3 local = parent ? parent.InverseTransformPoint(pos) : pos;
                    local.x = Mathf.Round(local.x / g) * g;
                    local.z = Mathf.Round(local.z / g) * g;
                    pos = parent ? parent.TransformPoint(local) : local;

                    // X/Z moved to the grid point → re-sample the ground height there (slopes, terrain, steps).
                    if (hit && ProbeDown(pos, surfaceObj, out float groundY)) pos.y = groundY;
                }
            }

            if (ModelBrowserPrefs.SnapY)
            {
                float g = ModelBrowserPrefs.GridSize;
                pos.y = Mathf.Round(pos.y / g) * g;
            }

            // Hold Shift = lock height: keep the last surface height while sliding over other objects.
            bool lockY = Event.current != null && Event.current.shift && hasLockedY;
            if (lockY) { pos.y = lockedSurfaceY; heightSource = "locked (Shift)"; }
            else { lockedSurfaceY = pos.y; hasLockedY = true; }

            // Lift so the bottom of the model sits on the surface (pivot not at the bottom).
            if (ModelBrowserPrefs.AlignBase && ghostLocalCorners.Count > 0)
            {
                Matrix4x4 m = Matrix4x4.TRS(pos, rot * ghostRootRotation, ghostRootScale);
                float minY = float.MaxValue;
                foreach (var c in ghostLocalCorners) minY = Mathf.Min(minY, m.MultiplyPoint3x4(c).y);
                pos.y += pos.y - minY;
            }

            pos.y += heightOffset;
            return true;
        }

        /// <summary>Closest hit from colliders (Physics) and from the mesh under the mouse (no collider needed).</summary>
        static bool RaycastSurface(Vector2 mouse, Ray ray, out Vector3 point, out GameObject surfaceObj)
        {
            point = default;
            surfaceObj = null;
            float best = float.MaxValue;

            var physics = PhysicsSceneForPlacement();
            Physics.SyncTransforms();
            if (physics.Raycast(ray.origin, ray.direction, out RaycastHit ph, 100000f, RayMask, QueryTriggerInteraction.Ignore))
            {
                best = ph.distance;
                point = ph.point;
                surfaceObj = ph.collider.gameObject;
                heightSource = "collider: " + ph.collider.name;
            }

            var picked = HandleUtility.PickGameObject(mouse, false, ghostObjects);
            if (picked && IsInMask(picked) && !IsGhost(picked) && RaycastObjectMeshes(ray, picked, out RaycastHit mh) && mh.distance < best)
            {
                best = mh.distance;
                point = mh.point;
                surfaceObj = picked;
                heightSource = "mesh: " + picked.name;
            }

            return best < float.MaxValue;
        }

        const int IgnoreRaycastLayer = 2;
        static int RayMask => ModelBrowserPrefs.SurfaceMask & ~(1 << IgnoreRaycastLayer);
        static bool IsInMask(GameObject go) => (RayMask & (1 << go.layer)) != 0;
        static bool IsGhost(GameObject go) => ghost && go.transform.IsChildOf(ghost.transform);
        static GameObject[] ghostObjects = new GameObject[0];

        /// <summary>Vertical ray down from slightly above the point, against colliders and the surface object's meshes.</summary>
        static bool ProbeDown(Vector3 at, GameObject surfaceObj, out float y)
        {
            y = at.y;
            const float above = 0.5f;
            var ray = new Ray(at + Vector3.up * above, Vector3.down);
            float best = float.MaxValue;

            if (PhysicsSceneForPlacement().Raycast(ray.origin, ray.direction, out RaycastHit ph, 1000f, RayMask, QueryTriggerInteraction.Ignore))
            {
                best = ph.distance;
                y = ph.point.y;
            }
            if (surfaceObj && RaycastObjectMeshes(ray, surfaceObj, out RaycastHit mh) && mh.distance < best)
            {
                best = mh.distance;
                y = mh.point.y;
            }
            return best < float.MaxValue;
        }

        static PhysicsScene PhysicsSceneForPlacement()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null ? stage.scene.GetPhysicsScene() : Physics.defaultPhysicsScene;
        }

        // HandleUtility.IntersectRayMesh is internal; it's what Unity's own scene picking uses (works without colliders).
        static readonly System.Reflection.MethodInfo intersectRayMesh = typeof(HandleUtility).GetMethod("IntersectRayMesh",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
            null, new[] { typeof(Ray), typeof(Mesh), typeof(Matrix4x4), typeof(RaycastHit).MakeByRefType() }, null);

        static bool RaycastObjectMeshes(Ray ray, GameObject go, out RaycastHit best)
        {
            best = default;
            if (intersectRayMesh == null) return false;
            bool found = false;
            RaycastHit closest = default;
            var args = new object[4];

            void Test(Mesh mesh, Matrix4x4 matrix)
            {
                if (!mesh) return;
                args[0] = ray; args[1] = mesh; args[2] = matrix; args[3] = null;
                if ((bool)intersectRayMesh.Invoke(null, args))
                {
                    var h = (RaycastHit)args[3];
                    if (!found || h.distance < closest.distance) { closest = h; found = true; }
                }
            }

            foreach (var mf in go.GetComponents<MeshFilter>()) Test(mf.sharedMesh, mf.transform.localToWorldMatrix);
            foreach (var smr in go.GetComponents<SkinnedMeshRenderer>()) Test(smr.sharedMesh, smr.transform.localToWorldMatrix);
            best = closest;
            return found;
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
            var all = ghost.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                t.gameObject.layer = IgnoreRaycastLayer; // never hit by our own raycasts
            }
            ghostObjects = System.Array.ConvertAll(all, t => t.gameObject);
            ghostRootRotation = prefab.transform.localRotation;
            ghostRootScale = prefab.transform.localScale;
            CacheLocalCorners();
            ghostPath = path;
            ghost.SetActive(false);
        }

        // Mesh bounds corners in the prefab root's local space (unscaled), used for "Base" alignment.
        static readonly System.Collections.Generic.List<Vector3> ghostLocalCorners = new System.Collections.Generic.List<Vector3>();
        static Vector3 ghostRootScale = Vector3.one;

        static void CacheLocalCorners()
        {
            ghostLocalCorners.Clear();
            if (!ghost) return;
            Matrix4x4 rootInv = Matrix4x4.TRS(ghost.transform.position, ghost.transform.rotation, ghost.transform.lossyScale).inverse;

            void Add(Mesh mesh, Transform t)
            {
                if (!mesh) return;
                Bounds b = mesh.bounds;
                Matrix4x4 m = rootInv * t.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    ghostLocalCorners.Add(m.MultiplyPoint3x4(c));
                }
            }

            foreach (var mf in ghost.GetComponentsInChildren<MeshFilter>(true)) Add(mf.sharedMesh, mf.transform);
            foreach (var smr in ghost.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Add(smr.sharedMesh, smr.transform);
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
            ghostObjects = new GameObject[0];
        }

        static void DrawGizmo()
        {
            if (!hasPose || !ghost || !ghost.activeSelf) return;
            var renderers = ghost.GetComponentsInChildren<Renderer>();
            if (ModelBrowserPrefs.ShowBounds && renderers.Length > 0)
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
            string offset = heightOffset == 0f ? "" : $"   offset {(heightOffset > 0 ? "+" : "")}{heightOffset:0.###} m";
            string text = $"{name}   {yaw:0}°   Y {posePos.y:0.##} ({heightSource}){offset}\n" +
                          "LMB place · hold Shift lock height · R / Shift+R rotate · E / Q up-down (Ctrl+Scroll) · Shift+Q reset · Esc / RMB exit";
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
