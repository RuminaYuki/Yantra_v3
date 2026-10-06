using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace CategoryInspector
{
    /// <summary>
    /// Inspector แยกที่จัด component เป็นหมวดซ้อนกันได้ไม่จำกัดชั้น
    /// เปิดจากเมนู Tools > Category Inspector
    /// </summary>
    public class CategoryInspectorWindow : EditorWindow
    {
        const string AssetFolder = "Assets/Editor";
        const string AssetPath = "Assets/Editor/ComponentCategoryLayout.asset";
        const string UncategorizedId = "__uncategorized__";
        const string PrefPrefix = "CategoryInspector.expanded.";

        // ไอคอน built-in ให้เลือก (ชื่อที่แสดง, ชื่อจริงใน Unity)
        static readonly (string label, string name)[] IconPresets =
        {
            ("(none)", ""),
            ("Folder", "Folder Icon"),
            ("Folder Open", "FolderOpened Icon"),
            ("GameObject", "GameObject Icon"),
            ("Prefab", "Prefab Icon"),
            ("Script", "cs Script Icon"),
            ("Rigidbody", "Rigidbody Icon"),
            ("Animator", "Animator Icon"),
            ("Audio", "AudioSource Icon"),
            ("Camera", "Camera Icon"),
            ("Light", "Light Icon"),
            ("Particles", "ParticleSystem Icon"),
            ("Info", "console.infoicon"),
            ("Warning", "console.warnicon"),
            ("Error", "console.erroricon"),
        };

        [SerializeField] bool locked;
        [SerializeField] UnityEngine.Object lockedObject;

        ComponentCategoryLayout layout;
        GameObject target;                 // GameObject ที่กำลังดู (null ถ้าเลือก asset)
        UnityEngine.Object assetTarget;    // asset ที่กำลังดู เช่น ScriptableObject, Material
        Editor headerEditor;               // ใช้วาดหัวแบบ Inspector ปกติ (เปิด/ปิด, ชื่อ, Tag, Layer, Static)
        IMGUIContainer headerContainer;
        string fallbackName;               // ชื่อ path ไว้หาชุดหมวดตอน Play
        string search = "";
        string editingCategoryId;     // หมวดที่เปิดแผงแก้สไตล์อยู่
        string lastSignature = "";
        string currentKey;            // ตัวระบุ object ปัจจุบัน (ใช้หาชุดหมวดของมัน)
        string currentName;

        // ชุดหมวดของ object ปัจจุบัน (ถ้ายังไม่เคยสร้าง จะได้ชุดว่างไว้อ่านอย่างเดียว)
        static readonly ObjectLayout Empty = new ObjectLayout();
        ObjectLayout Cur => Resolve() ?? Empty;

        /// <summary>
        /// หาชุดหมวดของ object ปัจจุบัน
        /// ตอน Play: object ที่ spawn จาก prefab (เช่น "Player(Clone)" จาก Netcode) จะไม่มีลิงก์กับ prefab แล้ว
        /// และ object ใน scene ก็ได้ id ใหม่ → ถ้าหาจาก key ไม่เจอ ให้หาจากชื่อ path แทน
        /// </summary>
        ObjectLayout Resolve()
        {
            if (layout == null || string.IsNullOrEmpty(currentKey)) return null;
            var o = layout.Get(currentKey);
            if (o != null || !EditorApplication.isPlayingOrWillChangePlaymode || string.IsNullOrEmpty(fallbackName)) return o;

            return layout.objects
                .Where(x => x.displayName == fallbackName)
                .OrderBy(x => x.key != null && x.key.StartsWith("prefab:") ? 0 : 1)   // ให้ prefab มาก่อน
                .FirstOrDefault();
        }

        // ใช้ก่อนแก้ไขทุกครั้ง: บันทึก Undo + สร้างชุดหมวดของ object นี้ถ้ายังไม่มี
        ObjectLayout Edit(string undoName)
        {
            Undo.RecordObject(layout, undoName);
            return Resolve() ?? layout.GetOrCreate(currentKey, currentName);
        }

        Label targetLabel;
        ScrollView scroll;
        VisualElement body;

        class SectionRefs
        {
            public VisualElement root, header, content, chevron;
            public Image icon;
            public Label title, badge;
        }

        // ------------------------------------------------------------------
        // เปิดหน้าต่าง / lifecycle
        // ------------------------------------------------------------------

        [MenuItem("Tools/Category Inspector")]
        public static void Open()
        {
            GetWindow<CategoryInspectorWindow>("Category Inspector").Show();
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += Rebuild;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= Rebuild;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (headerEditor != null) DestroyImmediate(headerEditor);
            headerEditor = null;
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode && state != PlayModeStateChange.EnteredEditMode) return;
            RefreshTarget();
            Rebuild();
        }

        void OnSelectionChange()
        {
            if (locked) return;
            editingCategoryId = null;
            selected.Clear();
            anchor = null;
            RefreshTarget();
            Rebuild();
        }

        // เรียก ~10 ครั้ง/วินาที: เช็กว่ามีการเพิ่ม/ลบ component หรือ object ถูกลบ
        void OnInspectorUpdate()
        {
            if (body == null) return;
            if (CurrentSignature() != lastSignature) Rebuild();
            headerContainer?.MarkDirtyRepaint();   // อัปเดตหัว (ชื่อ, เปิด/ปิด) ถ้าถูกแก้จากที่อื่น
        }

        void RefreshTarget()
        {
            var obj = locked ? lockedObject : Selection.activeObject;
            target = obj as GameObject;
            assetTarget = target == null ? obj : null;

            if (target != null)
            {
                currentKey = LayoutKey(target, out currentName);
                fallbackName = PathName(target);
            }
            else
            {
                currentKey = null;
                currentName = assetTarget != null ? assetTarget.name : null;
                fallbackName = null;
            }

            // editor สำหรับวาดหัว: สร้างใหม่เมื่อเปลี่ยน object
            UnityEngine.Object headerTarget = target != null ? (UnityEngine.Object)target : assetTarget;
            if (headerEditor == null || headerEditor.target != headerTarget)
            {
                if (headerEditor != null) DestroyImmediate(headerEditor);
                headerEditor = headerTarget != null ? Editor.CreateEditor(headerTarget) : null;
            }
        }

        string CurrentSignature()
        {
            if (target != null) return Signature(target);
            if (assetTarget != null) return "asset:" + ObjectId(assetTarget);
            return "null";
        }

        /// <summary>ชื่อ root (ตัด "(Clone)" ออก) + path ลงมาถึง object นี้ เช่น "Player/Model/Gun"</summary>
        static string PathName(GameObject go)
        {
            var root = go.transform.root;
            string rootName = root.name.Replace("(Clone)", "").Trim();
            string rel = RelPath(go.transform, root);
            return rootName + (rel == "/" ? "" : rel);
        }

        /// <summary>
        /// ตัวระบุ object สำหรับเก็บหมวด
        /// - ส่วนของ prefab (ทั้ง instance ใน scene, ตัว asset ใน Project และตอนเปิด Prefab Mode)
        ///   → GUID ของไฟล์ prefab + path ชื่อ object ภายใน prefab ทำให้ทุก instance ใช้ชุดเดียวกัน
        /// - object ธรรมดาใน scene → GlobalObjectId (ไม่เปลี่ยนแม้เปลี่ยนชื่อ)
        /// </summary>
        static string LayoutKey(GameObject go, out string displayName)
        {
            displayName = go.name;

            // 1) กำลังแก้ใน Prefab Mode
            var stage = PrefabStageUtility.GetPrefabStage(go);
            if (stage != null && stage.prefabContentsRoot != null)
            {
                string rel = RelPath(go.transform, stage.prefabContentsRoot.transform);
                displayName = System.IO.Path.GetFileNameWithoutExtension(stage.assetPath) + (rel == "/" ? "" : rel);
                return "prefab:" + AssetDatabase.AssetPathToGUID(stage.assetPath) + rel;
            }

            // 2) prefab asset ที่เลือกใน Project หรือ instance ของ prefab ใน scene
            var assetObj = EditorUtility.IsPersistent(go) ? go : PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (assetObj != null)
            {
                string path = AssetDatabase.GetAssetPath(assetObj);
                if (!string.IsNullOrEmpty(path))
                {
                    string rel = RelPath(assetObj.transform, assetObj.transform.root);
                    displayName = System.IO.Path.GetFileNameWithoutExtension(path) + (rel == "/" ? "" : rel);
                    return "prefab:" + AssetDatabase.AssetPathToGUID(path) + rel;
                }
            }

            // 3) object ธรรมดาใน scene (ต้องเซฟ scene ก่อน id ถึงจะคงที่)
            displayName = PathName(go);
            return "scene:" + GlobalObjectId.GetGlobalObjectIdSlow(go);
        }

        static string RelPath(Transform t, Transform root)
        {
            var parts = new List<string>();
            int guard = 0;
            while (t != null && t != root && guard++ < 256)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }
            return "/" + string.Join("/", parts);
        }

        static string Signature(GameObject go)
        {
            if (go == null) return "null";
            var sb = new System.Text.StringBuilder();
            sb.Append(ObjectId(go)).Append(':');
            foreach (var c in go.GetComponents<Component>())
                sb.Append(c == null ? "0" : ObjectId(c)).Append(',');
            return sb.ToString();
        }

        // Unity 6.2+ เปลี่ยนจาก GetInstanceID() เป็น GetEntityId()
        static string ObjectId(UnityEngine.Object o)
        {
#if UNITY_6000_2_OR_NEWER
            return o.GetEntityId().ToString();
#else
            return o.GetInstanceID().ToString();
#endif
        }

        void LoadLayout()
        {
            if (layout != null) return;
            var guids = AssetDatabase.FindAssets("t:" + nameof(ComponentCategoryLayout));
            if (guids.Length > 0)
            {
                layout = AssetDatabase.LoadAssetAtPath<ComponentCategoryLayout>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (layout != null) return;
            }
            if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets", "Editor");
            layout = CreateInstance<ComponentCategoryLayout>();
            AssetDatabase.CreateAsset(layout, AssetPath);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------
        // สร้าง UI หลัก
        // ------------------------------------------------------------------

        public void CreateGUI()
        {
            LoadLayout();
            var root = rootVisualElement;

            // แถวบน: ชื่อ object / Lock / + Category / เมนูจุดสามจุด
            var toolbar = new Toolbar();
            targetLabel = new Label();
            targetLabel.style.flexGrow = 1;
            targetLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            targetLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            targetLabel.style.paddingLeft = 4;
            targetLabel.style.overflow = Overflow.Hidden;
            targetLabel.tooltip = "Click to ping";
            targetLabel.AddManipulator(new Clickable(() =>
            {
                if (target) EditorGUIUtility.PingObject(target);
                else if (assetTarget) EditorGUIUtility.PingObject(assetTarget);
            }));
            toolbar.Add(targetLabel);

            var lockToggle = new ToolbarToggle { text = "Lock", value = locked };
            lockToggle.RegisterValueChangedCallback(e =>
            {
                locked = e.newValue;
                lockedObject = !locked ? null : (target != null ? (UnityEngine.Object)target : assetTarget);
                RefreshTarget();
                Rebuild();
            });
            toolbar.Add(lockToggle);

            toolbar.Add(new ToolbarButton(() => AddCategory("")) { text = "+ Category" });

            var menuBtn = new ToolbarButton(ShowWindowMenu) { tooltip = "Options" };
            menuBtn.style.justifyContent = Justify.Center;
            menuBtn.Add(MakeKebab(DefaultIconColor()));
            toolbar.Add(menuBtn);
            root.Add(toolbar);

            // แถวที่สอง: ช่องค้นหา (ค้นได้ทั้งชื่อ component และชื่อหมวด)
            var searchBar = new Toolbar();
            var searchField = new ToolbarSearchField();
            searchField.style.flexGrow = 1;
            searchField.style.width = StyleKeyword.Auto;
            searchField.RegisterValueChangedCallback(e => { search = e.newValue ?? ""; Rebuild(); });
            searchBar.Add(searchField);
            root.Add(searchBar);

            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            body = new VisualElement();
            body.style.paddingLeft = 4;
            body.style.paddingRight = 4;
            body.style.paddingBottom = 12;
            scroll.Add(body);
            root.Add(scroll);

            // ลากออกนอกหน้าต่างหรือยกเลิกการลาก → ล้างไฮไลต์
            root.RegisterCallback<DragExitedEvent>(_ => ClearDropHighlight());
            root.RegisterCallback<DragLeaveEvent>(e => { if (e.target == root) ClearDropHighlight(); });

            RefreshTarget();
            Rebuild();
        }

        void Rebuild()
        {
            if (body == null) return;
            LoadLayout();

            float scrollY = scroll.scrollOffset.y;
            dropHighlight = null;
            displayOrder.Clear();
            componentHeaders.Clear();
            body.Clear();
            lastSignature = CurrentSignature();
            targetLabel.text = target ? currentName : assetTarget ? assetTarget.name : "(nothing selected)";
            headerContainer = null;

            if (target == null && assetTarget == null)
            {
                body.Add(new HelpBox("Select a GameObject or an asset to inspect.", HelpBoxMessageType.Info));
                return;
            }

            // หัวแบบ Inspector ปกติ: GameObject = เปิด/ปิด, ชื่อ, Static, Tag, Layer, ปุ่ม Prefab
            //                       asset = ไอคอน, ชื่อ, ปุ่ม Open
            body.Add(BuildObjectHeader());

            // ScriptableObject หรือ asset อื่น: แสดง inspector เต็มเหมือนของปกติ (ไม่มีระบบหมวด)
            if (target == null)
            {
                var insp = new InspectorElement(assetTarget);
                insp.style.marginTop = 4;
                body.Add(insp);
                return;
            }

            if (Cur.categories.Count == 0)
                body.Add(new HelpBox("No categories yet. Click \"+ Category\", then use the dots menu on a component to move it in.", HelpBoxMessageType.None));

            // แยก component ลงถังตามหมวด
            var buckets = new Dictionary<string, List<Component>>();
            int missing = 0;
            foreach (var c in target.GetComponents<Component>())
            {
                if (c == null) { missing++; continue; }
                if (!MatchesSearch(c)) continue;
                var a = Cur.GetAssignment(c);
                string key = (a != null && Cur.Find(a.categoryId) != null) ? a.categoryId : UncategorizedId;
                if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<Component>();
                list.Add(c);
            }
            foreach (var key in buckets.Keys.ToList())
            {
                if (key == UncategorizedId) continue;
                buckets[key] = buckets[key].OrderBy(c => Cur.GetAssignment(c).order).ToList();
            }

            foreach (var node in Cur.ChildrenOf(""))
            {
                var el = BuildCategory(node, 0, buckets);
                if (el != null) body.Add(el);
            }

            if (buckets.TryGetValue(UncategorizedId, out var uncategorized) && uncategorized.Count > 0)
                body.Add(BuildUncategorized(uncategorized));

            if (missing > 0)
                body.Add(new HelpBox($"{missing} missing script(s) on this object.", HelpBoxMessageType.Warning));

            body.Add(MakeAddComponentButton(null, small: false));

            // ตัวที่ถูกลบหรือถูกกรองด้วยช่องค้นหาออกจากการเลือก
            selected.RemoveWhere(c => c == null || !componentHeaders.ContainsKey(c));

            scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0, scrollY));
        }

        VisualElement BuildObjectHeader()
        {
            headerContainer = new IMGUIContainer(() =>
            {
                if (headerEditor == null || headerEditor.target == null) return;
                headerEditor.DrawHeader();
            });
            headerContainer.style.marginLeft = -4;    // ให้ชิดขอบเหมือน Inspector ปกติ
            headerContainer.style.marginRight = -4;
            headerContainer.style.marginBottom = 4;
            return headerContainer;
        }

        bool MatchesSearch(Component c)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (ObjectNames.GetInspectorTitle(c).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var a = Cur.GetAssignment(c);
            var node = a != null ? Cur.Find(a.categoryId) : null;
            return node != null && Cur.GetPath(node).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        int CountInSubtree(CategoryNode node, Dictionary<string, List<Component>> buckets)
        {
            int n = buckets.TryGetValue(node.id, out var l) ? l.Count : 0;
            foreach (var child in Cur.ChildrenOf(node.id)) n += CountInSubtree(child, buckets);
            return n;
        }

        // ------------------------------------------------------------------
        // หมวด
        // ------------------------------------------------------------------

        VisualElement BuildCategory(CategoryNode node, int depth, Dictionary<string, List<Component>> buckets)
        {
            bool searching = !string.IsNullOrEmpty(search);
            int count = CountInSubtree(node, buckets);
            bool hide = count == 0 && (searching || !layout.showEmptyCategories);
            if (hide && editingCategoryId != node.id) return null;

            bool expanded = searching || GetExpanded(node.id);
            var refs = BuildSection(expanded, v => SetExpanded(node.id, v), () => ShowCategoryMenu(node));
            refs.root.style.marginTop = depth == 0 ? 6 : 3;
            MakeDropTarget(refs.root, node.id, null);
            refs.badge.text = count.ToString();
            ApplyCategoryStyle(node, refs);

            // ปุ่ม + เพิ่ม component ลงหมวดนี้ (โผล่เฉพาะตอนเอาเมาส์ชี้หัวหมวด จะได้ไม่รก)
            var plus = MakeAddComponentButton(node.id, small: true);
            plus.style.visibility = Visibility.Hidden;
            refs.header.Insert(refs.header.childCount - 1, plus);   // วางก่อนปุ่มจุดสามจุด
            refs.header.RegisterCallback<MouseEnterEvent>(_ => plus.style.visibility = Visibility.Visible);
            refs.header.RegisterCallback<MouseLeaveEvent>(_ => plus.style.visibility = Visibility.Hidden);

            if (editingCategoryId == node.id)
                refs.root.Insert(1, BuildStylePanel(node, refs));   // แทรกระหว่าง header กับเนื้อหา

            // component ของหมวดนี้ก่อน แล้วค่อยหมวดย่อย
            if (buckets.TryGetValue(node.id, out var comps))
                foreach (var c in comps) refs.content.Add(BuildComponent(c, node.id));

            foreach (var child in Cur.ChildrenOf(node.id))
            {
                var el = BuildCategory(child, depth + 1, buckets);
                if (el != null) refs.content.Add(el);
            }
            return refs.root;
        }

        VisualElement BuildUncategorized(List<Component> comps)
        {
            bool expanded = !string.IsNullOrEmpty(search) || GetExpanded(UncategorizedId);
            var r = BuildSection(expanded, v => SetExpanded(UncategorizedId, v), null);
            var fg = new Color(0.88f, 0.88f, 0.88f);
            r.root.style.marginTop = 10;
            MakeDropTarget(r.root, null, null);
            r.header.style.backgroundColor = new Color(0.32f, 0.32f, 0.32f, 0.85f);
            r.header.style.borderLeftColor = new Color(0.55f, 0.55f, 0.55f);
            r.title.text = "Uncategorized";
            r.title.style.color = fg;
            r.title.style.unityFontStyleAndWeight = FontStyle.Italic;
            r.badge.text = comps.Count.ToString();
            r.badge.style.color = fg;
            r.icon.style.display = DisplayStyle.None;
            SetChevronColor(r.chevron, fg);
            r.content.style.borderLeftColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            foreach (var c in comps) r.content.Add(BuildComponent(c, null));
            return r.root;
        }

        /// <summary>โครง header + เนื้อหาที่พับได้ ใช้ร่วมกันทุกหมวด</summary>
        SectionRefs BuildSection(bool expanded, Action<bool> onToggle, Action onMenu)
        {
            var r = new SectionRefs { root = new VisualElement(), header = new VisualElement() };

            var h = r.header.style;
            h.flexDirection = FlexDirection.Row;
            h.alignItems = Align.Center;
            h.paddingLeft = 4; h.paddingRight = 2; h.paddingTop = 3; h.paddingBottom = 3;
            h.borderLeftWidth = 4;
            SetRadius(r.header, 3);

            // พื้นที่กดเพื่อพับ/กาง (ไม่รวมปุ่มจุดสามจุด จะได้ไม่ชนกัน)
            var clickArea = new VisualElement();
            clickArea.style.flexDirection = FlexDirection.Row;
            clickArea.style.alignItems = Align.Center;
            clickArea.style.flexGrow = 1;
            clickArea.style.overflow = Overflow.Hidden;

            r.chevron = MakeChevron(expanded);
            r.icon = new Image { scaleMode = ScaleMode.ScaleToFit };
            r.icon.style.marginLeft = 2;
            r.icon.style.marginRight = 4;
            r.title = new Label();
            r.title.style.unityTextAlign = TextAnchor.MiddleLeft;
            r.title.style.flexShrink = 1;
            r.title.style.overflow = Overflow.Hidden;
            r.title.style.textOverflow = TextOverflow.Ellipsis;
            r.title.style.whiteSpace = WhiteSpace.NoWrap;
            clickArea.Add(r.chevron);
            clickArea.Add(r.icon);
            clickArea.Add(r.title);

            r.badge = new Label { tooltip = "Components in this category (including subcategories)" };
            r.badge.style.opacity = 0.7f;
            r.badge.style.fontSize = 10;
            r.badge.style.marginRight = 4;

            r.header.Add(clickArea);
            r.header.Add(r.badge);
            if (onMenu != null) r.header.Add(MakeKebabButton(onMenu, DefaultIconColor()));

            // เนื้อหา: มีเส้นซ้ายเป็นสีหมวด ให้เห็นการไหลจากหัวข้อแม่ลงไป
            r.content = new VisualElement();
            r.content.style.marginLeft = 6;
            r.content.style.paddingLeft = 6;
            r.content.style.paddingTop = 2;
            r.content.style.borderLeftWidth = 2;
            r.content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

            bool isOpen = expanded;
            clickArea.AddManipulator(new Clickable(() =>
            {
                isOpen = !isOpen;
                r.content.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;
                SetChevron(r.chevron, isOpen);
                onToggle?.Invoke(isOpen);
            }));

            r.root.Add(r.header);
            r.root.Add(r.content);
            return r;
        }

        void ApplyCategoryStyle(CategoryNode node, SectionRefs refs)
        {
            refs.header.style.backgroundColor = node.headerColor;
            refs.header.style.borderLeftColor = Lighten(node.headerColor, 0.3f);

            refs.title.text = node.name;
            refs.title.style.color = node.textColor;
            refs.title.style.fontSize = node.fontSize;
            refs.title.style.unityFontStyleAndWeight = node.bold ? FontStyle.Bold : FontStyle.Normal;
            refs.badge.style.color = node.textColor;
            SetChevronColor(refs.chevron, node.textColor);

            var kebab = refs.header.Q("kebab");
            if (kebab != null) foreach (var dot in kebab.Children()) dot.style.backgroundColor = node.textColor;

            var tex = GetIcon(node);
            refs.icon.image = tex;
            refs.icon.style.display = tex ? DisplayStyle.Flex : DisplayStyle.None;
            float size = node.fontSize + 3;
            refs.icon.style.width = size;
            refs.icon.style.height = size;

            var line = node.headerColor;
            line.a = 0.6f;
            refs.content.style.borderLeftColor = line;
        }

        /// <summary>แผงแก้สไตล์ จะโผล่ก็ต่อเมื่อกด "Edit Style..." จากเมนูจุดสามจุด</summary>
        VisualElement BuildStylePanel(CategoryNode node, SectionRefs refs)
        {
            var p = new VisualElement();
            p.style.backgroundColor = new Color(0, 0, 0, 0.2f);
            p.style.paddingLeft = 6; p.style.paddingRight = 6; p.style.paddingTop = 6; p.style.paddingBottom = 6;
            p.style.marginTop = 2; p.style.marginBottom = 4; p.style.marginLeft = 6;
            SetRadius(p, 3);

            void Changed(string undoName, Action apply)
            {
                Undo.RecordObject(layout, undoName);
                apply();
                EditorUtility.SetDirty(layout);
                ApplyCategoryStyle(node, refs);   // อัปเดตสดโดยไม่ต้อง rebuild ทั้งหน้า
            }

            var nameField = new TextField("Name") { value = node.name };
            nameField.RegisterValueChangedCallback(e => Changed("Rename Category", () => node.name = Sanitize(e.newValue)));

            var headerColor = new ColorField("Header Color") { value = node.headerColor, showAlpha = true };
            headerColor.RegisterValueChangedCallback(e => Changed("Category Color", () => node.headerColor = e.newValue));

            var textColor = new ColorField("Text Color") { value = node.textColor, showAlpha = false };
            textColor.RegisterValueChangedCallback(e => Changed("Category Text Color", () => node.textColor = e.newValue));

            var size = new SliderInt("Text Size", 9, 28) { value = node.fontSize, showInputField = true };
            size.RegisterValueChangedCallback(e => Changed("Category Text Size", () => node.fontSize = e.newValue));

            var bold = new Toggle("Bold") { value = node.bold };
            bold.RegisterValueChangedCallback(e => Changed("Category Bold", () => node.bold = e.newValue));

            var labels = IconPresets.Select(x => x.label).ToList();
            int idx = Array.FindIndex(IconPresets, x => x.name == (node.builtinIcon ?? ""));
            var icon = new DropdownField("Icon", labels, Mathf.Max(0, idx));
            icon.RegisterValueChangedCallback(e =>
            {
                int i = labels.IndexOf(e.newValue);
                if (i >= 0) Changed("Category Icon", () => node.builtinIcon = IconPresets[i].name);
            });

            var custom = new ObjectField("Custom Icon") { objectType = typeof(Texture2D), allowSceneObjects = false, value = node.customIcon };
            custom.tooltip = "Overrides the built-in icon";
            custom.RegisterValueChangedCallback(e => Changed("Category Icon", () => node.customIcon = e.newValue as Texture2D));

            var done = new Button(() => { editingCategoryId = null; Save(); Rebuild(); }) { text = "Done" };
            done.style.alignSelf = Align.FlexEnd;
            done.style.width = 70;
            done.style.marginTop = 4;

            p.Add(nameField);
            p.Add(headerColor);
            p.Add(textColor);
            p.Add(size);
            p.Add(bold);
            p.Add(icon);
            p.Add(custom);
            p.Add(done);

            nameField.schedule.Execute(() => nameField.Focus());
            return p;
        }

        // ------------------------------------------------------------------
        // Component
        // ------------------------------------------------------------------

        VisualElement BuildComponent(Component comp, string categoryId)
        {
            var root = new VisualElement();
            root.style.marginTop = 2;
            MakeDropTarget(root, categoryId, comp);   // ปล่อยบน component = แทรกไว้ก่อนตัวนี้

            bool expanded = InternalEditorUtility.GetIsInspectorExpanded(comp);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 4; header.style.paddingRight = 2; header.style.paddingTop = 2; header.style.paddingBottom = 2;
            SetRadius(header, 2);
            displayOrder.Add(comp);
            componentHeaders[comp] = header;
            ApplySelectionStyle(comp, header);

            var clickArea = new VisualElement();
            clickArea.style.flexDirection = FlexDirection.Row;
            clickArea.style.alignItems = Align.Center;
            clickArea.style.flexGrow = 1;
            clickArea.style.overflow = Overflow.Hidden;

            var chev = MakeChevron(expanded);
            SetChevronColor(chev, DefaultIconColor());

            var icon = new Image { image = EditorGUIUtility.ObjectContent(comp, comp.GetType()).image, scaleMode = ScaleMode.ScaleToFit };
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 4;

            var title = new Label(ObjectNames.GetInspectorTitle(comp));
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            title.style.flexShrink = 1;
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            title.style.whiteSpace = WhiteSpace.NoWrap;

            clickArea.Add(chev);
            clickArea.Add(icon);
            clickArea.Add(title);
            header.Add(clickArea);
            MakeDraggable(clickArea, comp);

            // ช่องติ๊ก enabled (เฉพาะ component ที่ปิดได้ เช่น MonoBehaviour, Collider, Renderer)
            if (EditorUtility.GetObjectEnabled(comp) != -1)
            {
                var toggle = new Toggle { value = EditorUtility.GetObjectEnabled(comp) == 1, tooltip = "Enabled" };
                toggle.style.marginRight = 2;
                toggle.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(comp, (e.newValue ? "Enable " : "Disable ") + comp.GetType().Name);
                    EditorUtility.SetObjectEnabled(comp, e.newValue);
                });
                // sync ถ้าไปเปิด/ปิดจาก Inspector ปกติหรือจากโค้ด
                toggle.schedule.Execute(() =>
                {
                    if (comp != null) toggle.SetValueWithoutNotify(EditorUtility.GetObjectEnabled(comp) == 1);
                }).Every(300);
                header.Add(toggle);
            }

            header.Add(MakeKebabButton(() => ShowComponentMenu(comp), DefaultIconColor()));

            // สร้าง inspector จริงเฉพาะตอนกางอยู่ (component ที่พับไว้ไม่กินแรง)
            var content = new VisualElement();
            content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (expanded) content.Add(new InspectorElement(comp));

            bool isOpen = expanded;
            var clickable = new Clickable(evt =>
            {
                // Shift = เลือกเป็นช่วง, Ctrl/Cmd = เลือกเพิ่มทีละตัว, คลิกธรรมดา = พับ/กาง
                var mods = Modifiers(evt);
                if ((mods & EventModifiers.Shift) != 0) { SelectRange(comp); return; }
                if ((mods & (EventModifiers.Control | EventModifiers.Command)) != 0) { ToggleSelect(comp); return; }
                ClearSelection();
                anchor = comp;

                isOpen = !isOpen;
                InternalEditorUtility.SetIsInspectorExpanded(comp, isOpen);   // sync กับ Inspector ปกติด้วย
                if (isOpen && content.childCount == 0) content.Add(new InspectorElement(comp));
                content.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;
                SetChevron(chev, isOpen);
            });

            // Clickable รับเฉพาะคลิกที่ไม่มี Shift/Ctrl ตามค่าเริ่มต้น ต้องเพิ่มให้รับคลิกพร้อมปุ่มเหล่านี้ด้วย
            foreach (var mod in new[] { EventModifiers.Shift, EventModifiers.Control, EventModifiers.Command })
                clickable.activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = mod });
            clickArea.AddManipulator(clickable);

            root.Add(header);
            root.Add(content);
            return root;
        }

        // ------------------------------------------------------------------
        // Add Component
        // ------------------------------------------------------------------

        /// <summary>
        /// ปุ่มเปิดรายการ component (ใช้ IMGUIContainer เพราะ dropdown ต้องเปิดจากใน GUI context ถึงจะวางตำแหน่งถูก)
        /// categoryId = null คือไม่ระบุหมวด
        /// </summary>
        IMGUIContainer MakeAddComponentButton(string categoryId, bool small)
        {
            IMGUIContainer c = null;
            c = new IMGUIContainer(() =>
            {
                var r = new Rect(0, 0, c.contentRect.width, c.contentRect.height);
                bool clicked = small
                    ? GUI.Button(r, new GUIContent(EditorGUIUtility.IconContent("Toolbar Plus").image, "Add component to this category"), "IconButton")
                    : GUI.Button(r, "Add Component");
                if (clicked) new ComponentDropdown(new AdvancedDropdownState(), t => AddComponentTo(t, categoryId)).Show(r);
            });

            if (small)
            {
                c.style.width = 18;
                c.style.height = 18;
                c.style.marginRight = 2;
            }
            else
            {
                c.style.width = 230;
                c.style.height = 24;
                c.style.alignSelf = Align.Center;
                c.style.marginTop = 12;
            }
            return c;
        }

        void AddComponentTo(Type type, string categoryId)
        {
            if (target == null) return;

            if (Attribute.IsDefined(type, typeof(DisallowMultipleComponent), true) && target.GetComponent(type) != null)
            {
                EditorUtility.DisplayDialog("Can't add component",
                    $"{ObjectNames.NicifyVariableName(type.Name)} is already on this object and doesn't allow duplicates.", "OK");
                return;
            }

            Component added;
            try { added = Undo.AddComponent(target, type); }
            catch (Exception e) { Debug.LogException(e); return; }
            if (added == null) return;

            InternalEditorUtility.SetIsInspectorExpanded(added, true);

            // ถ้า type นี้ยังไม่มีหมวด → ใส่หมวดที่กด + ให้เลย
            // ถ้า component ตัวนี้มีหมวดอยู่แล้ว จะไปโผล่ในหมวดเดิม
            if (!string.IsNullOrEmpty(categoryId) && Cur.Find(categoryId) != null && Cur.GetAssignment(added) == null)
                Assign(added, categoryId);
            else
                Rebuild();
        }

        /// <summary>รายการ component แบบค้นหาได้ (คล้ายปุ่ม Add Component ของ Unity)</summary>
        class ComponentDropdown : AdvancedDropdown
    {
        class TypeItem : AdvancedDropdownItem
        {
            public readonly Type type;
            public TypeItem(string name, Type t) : base(name) { type = t; }
        }

        static List<(string path, Type type)> cache;   // ล้างเองทุกครั้งที่ compile ใหม่
        readonly Action<Type> onPicked;

        public ComponentDropdown(AdvancedDropdownState state, Action<Type> onPicked) : base(state)
        {
            this.onPicked = onPicked;
            minimumSize = new Vector2(260, 380);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Add Component");
            var folders = new Dictionary<string, AdvancedDropdownItem>();
            foreach (var (path, type) in GetEntries())
            {
                int slash = path.LastIndexOf('/');
                var parent = slash < 0 ? root : GetFolder(root, folders, path.Substring(0, slash));
                parent.AddChild(new TypeItem(path.Substring(slash + 1), type)
                {
                    icon = AssetPreview.GetMiniTypeThumbnail(type) as Texture2D
                });
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is TypeItem t) onPicked?.Invoke(t.type);
        }

        static AdvancedDropdownItem GetFolder(AdvancedDropdownItem root, Dictionary<string, AdvancedDropdownItem> folders, string path)
        {
            if (folders.TryGetValue(path, out var f)) return f;
            int slash = path.LastIndexOf('/');
            var parent = slash < 0 ? root : GetFolder(root, folders, path.Substring(0, slash));
            f = new AdvancedDropdownItem(path.Substring(slash + 1));
            parent.AddChild(f);
            folders[path] = f;
            return f;
        }

        static List<(string path, Type type)> GetEntries()
        {
            if (cache != null) return cache;
            cache = new List<(string path, Type type)>();

            foreach (var t in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (t.IsAbstract || t.IsGenericTypeDefinition) continue;
                if (t == typeof(MonoBehaviour) || t == typeof(Behaviour)) continue;
                if (typeof(Transform).IsAssignableFrom(t)) continue;               // Transform เพิ่มเองไม่ได้
                if (Attribute.IsDefined(t, typeof(ObsoleteAttribute), false)) continue;

                string asm = t.Assembly.GetName().Name;
                if (asm.Contains("Editor")) continue;                               // สคริปต์ editor ใส่ object ไม่ได้
                if (asm.StartsWith("UnityEngine") && !t.IsVisible) continue;       // component ภายในของ Unity

                string path = MenuPathFor(t, asm);
                if (path != null) cache.Add((path, t));
            }

            cache.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.OrdinalIgnoreCase));
            return cache;
        }

        static string MenuPathFor(Type t, string asm)
        {
            // ถ้าสคริปต์ใส่ [AddComponentMenu("...")] ไว้ ใช้ path นั้น (ค่าว่าง = ตั้งใจซ่อน)
            var attr = (AddComponentMenu)Attribute.GetCustomAttribute(t, typeof(AddComponentMenu), false);
            if (attr != null)
                return string.IsNullOrEmpty(attr.componentMenu) ? null : attr.componentMenu;

            string nice = ObjectNames.NicifyVariableName(t.Name);

            if (asm.StartsWith("UnityEngine"))
            {
                string module = asm == "UnityEngine" || asm == "UnityEngine.CoreModule"
                    ? "Core"
                    : asm.Replace("UnityEngine.", "").Replace("Module", "");
                return "Unity/" + module + "/" + nice;
            }

            if (asm.StartsWith("Unity."))
                return "Packages/" + asm + "/" + nice;

            return string.IsNullOrEmpty(t.Namespace)
                ? "Scripts/" + nice
                : "Scripts/" + t.Namespace.Replace('.', '/') + "/" + nice;
        }
        }
 
        // ------------------------------------------------------------------
        // เมนูจุดสามจุด
        // ------------------------------------------------------------------
 
        void ShowWindowMenu()
        {
            var m = new GenericMenu();
            m.AddItem(new GUIContent("Expand All Categories"), false, () => SetAllExpanded(true));
            m.AddItem(new GUIContent("Collapse All Categories"), false, () => SetAllExpanded(false));
            m.AddItem(new GUIContent("Collapse All Components"), false, CollapseAllComponents);
            m.AddSeparator("");
            m.AddItem(new GUIContent("Show Empty Categories"), layout.showEmptyCategories, () =>
            {
                Undo.RecordObject(layout, "Toggle Empty Categories");
                layout.showEmptyCategories = !layout.showEmptyCategories;
                Save();
                Rebuild();
            });
            m.AddItem(new GUIContent("Ping Layout Asset"), false, () => EditorGUIUtility.PingObject(layout));

            m.AddSeparator("");
            if (target != null)
            {
                if (Cur.categories.Count > 0) m.AddItem(new GUIContent("Copy Categories"), false, CopyCategories);
                else m.AddDisabledItem(new GUIContent("Copy Categories"));

                if (HasCopiedCategories()) m.AddItem(new GUIContent("Paste Categories (Replace)"), false, PasteCategories);
                else m.AddDisabledItem(new GUIContent("Paste Categories (Replace)"));

                if (layout.Get(currentKey) != null) m.AddItem(new GUIContent("Clear Categories"), false, ClearCategories);
                else m.AddDisabledItem(new GUIContent("Clear Categories"));
            }
            m.ShowAsContext();
        }

        void ShowCategoryMenu(CategoryNode node)
        {
            var m = new GenericMenu();
            m.AddItem(new GUIContent("Edit Style..."), editingCategoryId == node.id, () =>
            {
                editingCategoryId = editingCategoryId == node.id ? null : node.id;
                Rebuild();
            });
            m.AddItem(new GUIContent("Add Subcategory"), false, () => AddCategory(node.id));
            m.AddSeparator("");
            m.AddItem(new GUIContent("Move Up"), false, () => MoveCategory(node, -1));
            m.AddItem(new GUIContent("Move Down"), false, () => MoveCategory(node, 1));

            if (!string.IsNullOrEmpty(node.parentId))
                m.AddItem(new GUIContent("Move Into/(Top Level)"), false, () => Reparent(node, ""));
            foreach (var other in AllInTreeOrder())
            {
                if (Cur.IsSelfOrDescendant(other, node.id)) continue;   // ห้ามย้ายเข้าไปใต้ตัวเอง
                if (other.id == node.parentId) continue;
                var otherId = other.id;
                m.AddItem(new GUIContent("Move Into/" + MenuPath(other)), false, () => Reparent(node, otherId));
            }

            m.AddSeparator("");
            m.AddItem(new GUIContent("Delete Category"), false, () => DeleteCategory(node));
            m.ShowAsContext();
        }

        void ShowComponentMenu(Component comp)
        {
            // ถ้ากดเมนูบนตัวที่ถูกเลือกไว้ คำสั่งจะใช้กับทุกตัวที่เลือก
            var comps = (selected.Contains(comp) && selected.Count > 1) ? SelectedInOrder() : new List<Component> { comp };
            bool multi = comps.Count > 1;
            string suffix = multi ? $" ({comps.Count} components)" : "";

            var a = Cur.GetAssignment(comp);
            bool anyAssigned = comps.Any(c => Cur.GetAssignment(c) != null);
            var m = new GenericMenu();

            var all = AllInTreeOrder();
            if (all.Count == 0) m.AddDisabledItem(new GUIContent("Move to Category" + suffix + "/(no categories yet)"));
            foreach (var node in all)
            {
                var id = node.id;
                bool current = !multi && a != null && a.categoryId == id;
                m.AddItem(new GUIContent("Move to Category" + suffix + "/" + MenuPath(node)), current, () => AssignMany(comps, id));
            }
            m.AddItem(new GUIContent("Move to New Category" + suffix), false, () =>
            {
                var id = AddCategory("", rebuild: false);
                AssignMany(comps, id);
            });
            if (anyAssigned) m.AddItem(new GUIContent("Remove from Category" + suffix), false, () => UnassignMany(comps));

            m.AddSeparator("");
            if (!multi && a != null)
            {
                m.AddItem(new GUIContent("Move Up"), false, () => MoveComponent(comp, -1));
                m.AddItem(new GUIContent("Move Down"), false, () => MoveComponent(comp, 1));
            }
            else
            {
                m.AddDisabledItem(new GUIContent("Move Up"));
                m.AddDisabledItem(new GUIContent("Move Down"));
            }

            m.AddSeparator("");
            if (!multi && comp is MonoBehaviour mb)
            {
                var script = MonoScript.FromMonoBehaviour(mb);
                if (script != null) m.AddItem(new GUIContent("Edit Script"), false, () => AssetDatabase.OpenAsset(script));
            }
            if (!multi) m.AddItem(new GUIContent("Ping Object"), false, () => EditorGUIUtility.PingObject(comp));
            if (selected.Count > 0) m.AddItem(new GUIContent("Clear Selection"), false, ClearSelection);
            m.ShowAsContext();
        }

        /// <summary>path สำหรับเมนู ถ้าหมวดมีลูก ต้องมีรายการ "(here)" เพราะเมนูย่อยกดเลือกตัวมันเองไม่ได้</summary>
        string MenuPath(CategoryNode node)
        {
            string path = Cur.GetPath(node, "/");
            if (Cur.ChildrenOf(node.id).Count > 0) path += "/(here)";
            return path;
        }

        List<CategoryNode> AllInTreeOrder()
        {
            var result = new List<CategoryNode>();
            void Walk(string parentId, int depth)
            {
                if (depth > 64) return;
                foreach (var n in Cur.ChildrenOf(parentId))
                {
                    result.Add(n);
                    Walk(n.id, depth + 1);
                }
            }
            Walk("", 0);
            return result;
        }

        // ------------------------------------------------------------------
        // แก้ไขข้อมูล (ทุกอย่าง Undo ได้)
        // ------------------------------------------------------------------

        string AddCategory(string parentId, bool rebuild = true)
        {
            if (target == null) return null;
            var o = Edit("Add Category");
            var parent = o.Find(parentId);
            var siblings = o.ChildrenOf(parentId);
            var n = new CategoryNode
            {
                parentId = parentId ?? "",
                order = siblings.Count == 0 ? 0 : siblings[siblings.Count - 1].order + 1,
            };

            // หมวดย่อยสืบสไตล์จากแม่: สีเข้มลง ตัวเล็กลงนิด ให้ลำดับชั้นดูออกเอง
            if (parent != null)
            {
                var c = parent.headerColor * 0.8f;
                c.a = 1f;
                n.headerColor = c;
                n.textColor = parent.textColor;
                n.fontSize = Mathf.Max(10, parent.fontSize - 1);
                n.bold = parent.bold;
                n.builtinIcon = parent.builtinIcon;
                n.customIcon = parent.customIcon;
                SetExpanded(parent.id, true);
            }

            o.categories.Add(n);
            editingCategoryId = n.id;   // เปิดแผงตั้งชื่อให้เลย
            Save();
            if (rebuild) Rebuild();
            return n.id;
        }

        void MoveCategory(CategoryNode node, int dir)
        {
            var o = Edit("Move Category");
            var siblings = o.ChildrenOf(node.parentId);
            int i = siblings.IndexOf(node), j = i + dir;
            if (i < 0 || j < 0 || j >= siblings.Count) return;
            siblings.RemoveAt(i);
            siblings.Insert(j, node);
            for (int k = 0; k < siblings.Count; k++) siblings[k].order = k;
            Save();
            Rebuild();
        }

        void Reparent(CategoryNode node, string newParentId)
        {
            var o = Edit("Move Category");
            var siblings = o.ChildrenOf(newParentId);
            node.parentId = newParentId ?? "";
            node.order = siblings.Count == 0 ? 0 : siblings[siblings.Count - 1].order + 1;
            if (!string.IsNullOrEmpty(newParentId)) SetExpanded(newParentId, true);
            Save();
            Rebuild();
        }

        void DeleteCategory(CategoryNode node)
        {
            if (!EditorUtility.DisplayDialog("Delete Category",
                    $"Delete \"{node.name}\"?\nIts subcategories and components will move up one level.",
                    "Delete", "Cancel")) return;

            var o = Edit("Delete Category");
            string parentId = node.parentId ?? "";
            foreach (var child in o.ChildrenOf(node.id)) child.parentId = parentId;

            if (string.IsNullOrEmpty(parentId))
                o.assignments.RemoveAll(a => a.categoryId == node.id);
            else
                foreach (var a in o.assignments)
                    if (a.categoryId == node.id) a.categoryId = parentId;

            o.categories.Remove(node);
            if (editingCategoryId == node.id) editingCategoryId = null;
            Save();
            Rebuild();
        }

        void Assign(Component comp, string categoryId, Component before = null) =>
            AssignMany(new List<Component> { comp }, categoryId, before);

        /// <summary>
        /// ย้ายหลาย component เข้าหมวดพร้อมกัน (Undo ครั้งเดียว)
        /// จับคู่เป็นรายตัว: AudioSource 2 ตัวบน object เดียวกันอยู่คนละหมวดได้
        /// before = แทรกไว้ก่อน component ตัวนี้, null = ต่อท้าย
        /// </summary>
        void AssignMany(IList<Component> comps, string categoryId, Component before = null)
        {
            if (target == null || string.IsNullOrEmpty(categoryId) || comps == null) return;
            var moving = comps.Where(c => c != null && c != before).Distinct().ToList();   // ปล่อยทับตัวเองไม่นับ
            if (moving.Count == 0) return;

            var o = Edit(moving.Count > 1 ? "Assign Categories" : "Assign Category");
            var movingAssignments = new List<TypeAssignment>();
            foreach (var c in moving)
            {
                var a = o.GetAssignment(c);
                if (a == null)
                {
                    a = new TypeAssignment { typeName = ObjectLayout.ComponentKey(c) };
                    o.assignments.Add(a);
                }
                a.categoryId = categoryId;
                if (!movingAssignments.Contains(a)) movingAssignments.Add(a);
            }

            // เรียงลำดับในหมวดใหม่ทั้งหมด แล้วแทรกกลุ่มที่ย้ายลงตำแหน่งที่ต้องการ (คงลำดับเดิมของกลุ่มไว้)
            var list = o.assignments
                .Where(x => !movingAssignments.Contains(x) && x.categoryId == categoryId)
                .OrderBy(x => x.order)
                .ToList();
            int index = list.Count;
            if (before != null)
            {
                int bi = list.IndexOf(o.GetAssignment(before));
                if (bi >= 0) index = bi;
            }
            list.InsertRange(index, movingAssignments);
            for (int k = 0; k < list.Count; k++) list[k].order = k;

            SetExpanded(categoryId, true);
            Save();
            Rebuild();
        }

        void UnassignMany(IList<Component> comps)
        {
            if (target == null || comps == null || comps.Count == 0) return;
            var o = Edit("Remove from Category");
            foreach (var c in comps)
            {
                if (c == null) continue;
                var a = o.GetAssignment(c);
                if (a != null) o.assignments.Remove(a);
            }
            Save();
            Rebuild();
        }

        // ------------------------------------------------------------------
        // เลือกหลายตัว (Shift = เป็นช่วง, Ctrl/Cmd = ทีละตัว)
        // ------------------------------------------------------------------

        readonly HashSet<Component> selected = new HashSet<Component>();
        Component anchor;                                                    // ตัวเริ่มของการเลือกแบบช่วง
        readonly List<Component> displayOrder = new List<Component>();      // ลำดับตามที่แสดงบนจอ
        readonly Dictionary<Component, VisualElement> componentHeaders = new Dictionary<Component, VisualElement>();

        static readonly Color SelectedColor = new Color(0.24f, 0.48f, 0.90f, 0.55f);

        static EventModifiers Modifiers(EventBase e)
        {
            if (e is IPointerEvent p) return p.modifiers;
            if (e is IMouseEvent m) return m.modifiers;
            return Event.current != null ? Event.current.modifiers : EventModifiers.None;
        }

        void SelectRange(Component comp)
        {
            int to = displayOrder.IndexOf(comp);
            int from = anchor != null ? displayOrder.IndexOf(anchor) : -1;
            if (from < 0) { anchor = comp; from = to; }
            if (to < 0) return;

            selected.Clear();
            for (int i = Mathf.Min(from, to); i <= Mathf.Max(from, to); i++) selected.Add(displayOrder[i]);
            RefreshSelectionStyles();
        }

        void ToggleSelect(Component comp)
        {
            if (!selected.Remove(comp)) selected.Add(comp);
            anchor = comp;
            RefreshSelectionStyles();
        }

        void ClearSelection()
        {
            if (selected.Count == 0) return;
            selected.Clear();
            RefreshSelectionStyles();
        }

        List<Component> SelectedInOrder() => displayOrder.Where(c => selected.Contains(c)).ToList();

        void RefreshSelectionStyles()
        {
            foreach (var kv in componentHeaders) ApplySelectionStyle(kv.Key, kv.Value);
        }

        void ApplySelectionStyle(Component comp, VisualElement header)
        {
            header.style.backgroundColor = selected.Contains(comp)
                ? SelectedColor
                : (EditorGUIUtility.isProSkin ? new Color(0, 0, 0, 0.18f) : new Color(0, 0, 0, 0.08f));
        }

        // ------------------------------------------------------------------
        // ลากวาง (Drag & Drop)
        // ------------------------------------------------------------------

        const string DragKey = "CategoryInspector.Components";
        static readonly Color DropColor = new Color(0.35f, 0.65f, 1f, 1f);
        VisualElement dropHighlight;   // เป้าที่กำลังไฮไลต์อยู่ (มีได้ทีละอันเดียว)

        /// <summary>
        /// กดค้างที่หัว component แล้วลาก จะเริ่ม drag ของ Unity (ลากไปใส่ field อื่นนอกหน้าต่างนี้ได้ด้วย)
        /// ถ้าตัวที่ลากถูกเลือกอยู่ จะลากทุกตัวที่เลือกไปด้วย
        /// </summary>
        void MakeDraggable(VisualElement handle, Component comp)
        {
            bool pressed = false;
            Vector2 start = default;

            handle.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button != 0) return;
                pressed = true;
                start = e.mousePosition;
            }, TrickleDown.TrickleDown);

            handle.RegisterCallback<MouseUpEvent>(_ => pressed = false, TrickleDown.TrickleDown);

            handle.RegisterCallback<MouseMoveEvent>(e =>
            {
                if (!pressed || comp == null) return;
                if ((e.mousePosition - start).sqrMagnitude < 36f) return;   // ขยับเกิน ~6px ถึงนับว่าลาก
                pressed = false;
                handle.ReleaseMouse();   // ปล่อยตัวคลิกพับ/กาง จะได้ไม่พับตอนปล่อยเมาส์

                // ลากตัวที่ไม่ได้เลือก → ลากตัวเดียว และล้างการเลือกเดิม
                if (!selected.Contains(comp)) ClearSelection();
                var comps = selected.Contains(comp) ? SelectedInOrder() : new List<Component> { comp };

                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = comps.Cast<UnityEngine.Object>().ToArray();
                DragAndDrop.SetGenericData(DragKey, comps);
                DragAndDrop.StartDrag(comps.Count > 1
                    ? $"{comps.Count} components"
                    : ObjectNames.GetInspectorTitle(comp));
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// ทำให้ element รับการปล่อย component ได้
        /// categoryId = null คือ Uncategorized, before = ใส่เมื่อเป้าเป็น component (แทรกก่อนตัวนั้น)
        /// </summary>
        void MakeDropTarget(VisualElement el, string categoryId, Component before)
        {
            bool isComponent = before != null;

            // เว้นขอบโปร่งใสไว้ก่อน จะได้ไม่กระตุกตอนไฮไลต์
            if (isComponent)
            {
                el.style.borderTopWidth = 2;
                el.style.borderTopColor = Color.clear;
            }
            else
            {
                el.style.borderTopWidth = 1; el.style.borderBottomWidth = 1;
                el.style.borderLeftWidth = 1; el.style.borderRightWidth = 1;
                SetBorderColor(el, Color.clear);
                SetRadius(el, 4);
            }

            el.RegisterCallback<DragUpdatedEvent>(e =>
            {
                if (GetDraggedComponents().Count == 0) return;
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                SetDropHighlight(el, isComponent);
                e.StopPropagation();   // ให้เป้าที่อยู่ในสุดได้ไปตัวเดียว
            });

            el.RegisterCallback<DragLeaveEvent>(_ => { if (dropHighlight == el) ClearDropHighlight(); });

            el.RegisterCallback<DragPerformEvent>(e =>
            {
                var comps = GetDraggedComponents();
                if (comps.Count == 0) return;
                DragAndDrop.AcceptDrag();
                ClearDropHighlight();
                e.StopPropagation();

                if (categoryId == null) UnassignMany(comps);
                else AssignMany(comps, categoryId, before);
            });
        }

        /// <summary>
        /// component ที่กำลังลาก: จากหน้าต่างนี้ หรือจากหัว component ใน Inspector ปกติก็ได้
        /// เอาเฉพาะของ object ที่กำลังดูอยู่
        /// </summary>
        List<Component> GetDraggedComponents()
        {
            var result = new List<Component>();
            if (target == null) return result;

            if (DragAndDrop.GetGenericData(DragKey) is List<Component> ours) result.AddRange(ours);
            else
                foreach (var o in DragAndDrop.objectReferences)
                    if (o is Component c) result.Add(c);

            result.RemoveAll(c => c == null || c.gameObject != target);
            return result;
        }

        void SetDropHighlight(VisualElement el, bool isComponent)
        {
            if (dropHighlight == el) return;
            ClearDropHighlight();
            dropHighlight = el;
            if (isComponent) el.style.borderTopColor = DropColor;   // เส้นบอกตำแหน่งแทรก
            else SetBorderColor(el, DropColor);                     // กรอบรอบหมวด
        }

        void ClearDropHighlight()
        {
            if (dropHighlight == null) return;
            SetBorderColor(dropHighlight, Color.clear);
            dropHighlight = null;
        }

        static void SetBorderColor(VisualElement e, Color c)
        {
            e.style.borderTopColor = c;
            e.style.borderBottomColor = c;
            e.style.borderLeftColor = c;
            e.style.borderRightColor = c;
        }

        void MoveComponent(Component comp, int dir)
        {
            if (target == null || comp == null) return;
            var o = Edit("Move Component");
            var a = o.GetAssignment(comp);
            if (a == null) return;

            // เรียงเฉพาะ component ที่อยู่บน object นี้ในหมวดเดียวกัน
            var list = target.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => o.GetAssignment(c))
                .Distinct()
                .Where(x => x != null && x.categoryId == a.categoryId)
                .OrderBy(x => x.order)
                .ToList();

            int i = list.IndexOf(a), j = i + dir;
            if (i < 0 || j < 0 || j >= list.Count) return;

            int baseOrder = list.Min(x => x.order);
            list.RemoveAt(i);
            list.Insert(j, a);
            for (int k = 0; k < list.Count; k++) list[k].order = baseOrder + k;
            Save();
            Rebuild();
        }

        // ---- คัดลอก / วาง / ล้าง ชุดหมวดของ object ----
        const string ClipboardPrefix = "CategoryInspectorLayout:";

        void CopyCategories()
        {
            EditorGUIUtility.systemCopyBuffer = ClipboardPrefix + JsonUtility.ToJson(Cur);
        }

        static bool HasCopiedCategories() =>
            (EditorGUIUtility.systemCopyBuffer ?? "").StartsWith(ClipboardPrefix);

        void PasteCategories()
        {
            if (target == null || !HasCopiedCategories()) return;
            var src = new ObjectLayout();
            JsonUtility.FromJsonOverwrite(EditorGUIUtility.systemCopyBuffer.Substring(ClipboardPrefix.Length), src);
            var o = Edit("Paste Categories");
            o.categories = src.categories;
            o.assignments = src.assignments;
            Save();
            Rebuild();
        }

        void ClearCategories()
        {
            var existing = layout.Get(currentKey);
            if (existing == null) return;
            if (!EditorUtility.DisplayDialog("Clear Categories",
                    $"Remove all categories from \"{currentName}\"?", "Clear", "Cancel")) return;
            Undo.RecordObject(layout, "Clear Categories");
            layout.objects.Remove(existing);
            editingCategoryId = null;
            Save();
            Rebuild();
        }

        void SetAllExpanded(bool value)
        {
            foreach (var c in Cur.categories) SetExpanded(c.id, value);
            SetExpanded(UncategorizedId, value);
            Rebuild();
        }

        void CollapseAllComponents()
        {
            if (target == null) return;
            foreach (var c in target.GetComponents<Component>())
                if (c != null) InternalEditorUtility.SetIsInspectorExpanded(c, false);
            Rebuild();
        }

        void Save()
        {
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssetIfDirty(layout);
        }

        // ------------------------------------------------------------------
        // ตัวช่วย
        // ------------------------------------------------------------------

        static bool GetExpanded(string id) => EditorPrefs.GetBool(PrefPrefix + id, true);
        static void SetExpanded(string id, bool value) => EditorPrefs.SetBool(PrefPrefix + id, value);

        static string Sanitize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Unnamed";
            return s.Replace('/', '-');   // "/" ใช้แบ่งเมนูย่อย เลยห้ามมีในชื่อ
        }

        static Texture GetIcon(CategoryNode node)
        {
            if (node.customIcon != null) return node.customIcon;
            if (string.IsNullOrEmpty(node.builtinIcon)) return null;
            return EditorGUIUtility.IconContent(node.builtinIcon).image;
        }

        static Color DefaultIconColor() =>
            EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.25f, 0.25f, 0.25f);

        static Color Lighten(Color c, float t)
        {
            var l = Color.Lerp(c, Color.white, t);
            l.a = 1f;
            return l;
        }

        static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        // ลูกศรพับ/กาง วาดจากขอบสี่เหลี่ยมหมุน 45° (ไม่พึ่งฟอนต์)
        static VisualElement MakeChevron(bool expanded)
        {
            var holder = new VisualElement();
            holder.style.width = 12;
            holder.style.height = 12;
            holder.style.marginRight = 3;
            holder.style.alignItems = Align.Center;
            holder.style.justifyContent = Justify.Center;

            var v = new VisualElement { name = "chev" };
            v.style.width = 5;
            v.style.height = 5;
            v.style.borderRightWidth = 1.5f;
            v.style.borderBottomWidth = 1.5f;
            holder.Add(v);

            SetChevron(holder, expanded);
            return holder;
        }

        static void SetChevron(VisualElement holder, bool expanded)
        {
            var v = holder.Q("chev");
            v.style.rotate = new Rotate(new Angle(expanded ? 45f : -45f, AngleUnit.Degree));
            v.style.marginTop = expanded ? -2 : 0;
            v.style.marginLeft = expanded ? 0 : -2;
        }

        static void SetChevronColor(VisualElement holder, Color c)
        {
            var v = holder.Q("chev");
            v.style.borderRightColor = c;
            v.style.borderBottomColor = c;
        }

        // จุดสามจุดแนวตั้ง วาดจากวงกลมเล็ก ๆ (ไม่พึ่งฟอนต์)
        static VisualElement MakeKebab(Color color)
        {
            var col = new VisualElement { name = "kebab", pickingMode = PickingMode.Ignore };
            col.style.flexDirection = FlexDirection.Column;
            col.style.alignItems = Align.Center;
            col.style.justifyContent = Justify.Center;
            col.style.width = 12;
            col.style.height = 16;
            for (int i = 0; i < 3; i++)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.style.width = 3;
                dot.style.height = 3;
                dot.style.marginTop = 1;
                dot.style.marginBottom = 1;
                dot.style.backgroundColor = color;
                SetRadius(dot, 1.5f);
                col.Add(dot);
            }
            return col;
        }

        static VisualElement MakeKebabButton(Action onClick, Color color)
        {
            var b = new Button(onClick) { tooltip = "Options" };
            b.style.width = 18;
            b.style.height = 18;
            b.style.marginLeft = 0; b.style.marginRight = 0; b.style.marginTop = 0; b.style.marginBottom = 0;
            b.style.paddingLeft = 0; b.style.paddingRight = 0; b.style.paddingTop = 0; b.style.paddingBottom = 0;
            b.style.borderLeftWidth = 0; b.style.borderRightWidth = 0; b.style.borderTopWidth = 0; b.style.borderBottomWidth = 0;
            b.style.backgroundColor = Color.clear;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            SetRadius(b, 3);
            b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = new Color(1, 1, 1, 0.15f));
            b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = Color.clear);
            b.Add(MakeKebab(color));
            return b;
        }
    }
}