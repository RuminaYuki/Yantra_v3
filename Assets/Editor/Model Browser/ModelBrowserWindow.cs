using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ModelBrowser
{
    /// <summary>
    /// Model Browser: main-category tabs (top) · sub-category sidebar (left) · thumbnail grid.
    /// Grid: click = Placement Mode · double-click = open prefab · drag = drop into Scene / Hierarchy / another sub-category.
    /// </summary>
    public class ModelBrowserWindow : EditorWindow
    {
        enum SpecialTab { None, Favorites, Recent }

        [SerializeField] ModelBrowserDatabase db;
        [SerializeField] int mainIndex;
        [SerializeField] int subIndex = -1; // -1 = All
        [SerializeField] SpecialTab special;
        [SerializeField] float thumbSize = 96f;
        [SerializeField] string search = "";
        [SerializeField] bool editMode;
        [SerializeField] Transform parentTarget;

        VisualElement body, tabsRow, sidebarList, sourcesPanel, gridContent;
        ScrollView gridScroll;
        Label statusLabel, placingLabel;
        readonly Dictionary<string, Image> cardImages = new Dictionary<string, Image>();
        readonly Dictionary<string, VisualElement> cards = new Dictionary<string, VisualElement>();
        readonly Dictionary<SubCategory, List<string>> resolveCache = new Dictionary<SubCategory, List<string>>();
        int itemCount;
        bool refreshQueued;

        static bool Dark => EditorGUIUtility.isProSkin;
        static Color Panel => Dark ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.76f, 0.76f, 0.76f);
        static Color CardBg => Dark ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.86f, 0.86f, 0.86f);
        static Color CardHover => Dark ? new Color(0.32f, 0.32f, 0.32f) : new Color(0.93f, 0.93f, 0.93f);
        static readonly Color Accent = new Color(0.24f, 0.55f, 0.95f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.2f);

        [MenuItem("Tools/Model Browser")]
        public static void Open()
        {
            var w = GetWindow<ModelBrowserWindow>();
            w.titleContent = new GUIContent("Model Browser", EditorGUIUtility.IconContent("Prefab Icon").image);
            w.minSize = new Vector2(420, 260);
        }

        // ======================================================================
        // Lifecycle
        // ======================================================================

        void OnEnable()
        {
            if (db == null) db = ModelBrowserDatabase.FindExisting();
            ModelPlacementTool.Parent = parentTarget;
            ModelBrowserDatabase.Changed += QueueRefresh;
            ModelBrowserAssetWatcher.AssetsChanged += OnAssetsChanged;
            ModelThumbnailCache.Ready += OnThumbnailReady;
            ModelPlacementTool.Changed += OnPlacementChanged;
            ModelBrowserPrefs.RecentChanged += OnRecentChanged;
            Undo.undoRedoPerformed += QueueRefresh;
        }

        void OnDisable()
        {
            ModelBrowserDatabase.Changed -= QueueRefresh;
            ModelBrowserAssetWatcher.AssetsChanged -= OnAssetsChanged;
            ModelThumbnailCache.Ready -= OnThumbnailReady;
            ModelPlacementTool.Changed -= OnPlacementChanged;
            ModelBrowserPrefs.RecentChanged -= OnRecentChanged;
            Undo.undoRedoPerformed -= QueueRefresh;
        }

        void OnDestroy() => ModelPlacementTool.End();

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;

            root.Add(BuildToolbar());
            root.Add(BuildPlacementBar());

            tabsRow = new VisualElement();
            tabsRow.style.flexDirection = FlexDirection.Row;
            tabsRow.style.flexWrap = Wrap.Wrap;
            tabsRow.style.paddingLeft = tabsRow.style.paddingRight = 4;
            tabsRow.style.paddingTop = tabsRow.style.paddingBottom = 3;
            tabsRow.style.backgroundColor = Panel;
            tabsRow.style.borderBottomWidth = 1;
            tabsRow.style.borderBottomColor = Color.black * 0.4f;
            RegisterDrop(tabsRow, paths => CreateMainFromFolders(paths));
            root.Add(tabsRow);

            body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            root.Add(body);

            // Sidebar
            var sidebar = new ScrollView(ScrollViewMode.Vertical);
            sidebar.style.width = 150;
            sidebar.style.backgroundColor = Panel;
            sidebar.style.borderRightWidth = 1;
            sidebar.style.borderRightColor = Color.black * 0.4f;
            sidebarList = sidebar.contentContainer;
            sidebarList.style.minHeight = 200;
            RegisterDrop(sidebar, paths => CreateSubsFromPaths(paths));
            body.Add(sidebar);

            // Right side
            var right = new VisualElement();
            right.style.flexGrow = 1;
            right.style.flexDirection = FlexDirection.Column;
            body.Add(right);

            sourcesPanel = new VisualElement();
            sourcesPanel.style.paddingLeft = sourcesPanel.style.paddingRight = 6;
            sourcesPanel.style.paddingTop = sourcesPanel.style.paddingBottom = 4;
            sourcesPanel.style.backgroundColor = Panel;
            RegisterDrop(sourcesPanel, paths => { if (CurrentSub != null) AddPathsToSub(CurrentSub, paths); });
            right.Add(sourcesPanel);

            gridScroll = new ScrollView(ScrollViewMode.Vertical);
            gridScroll.style.flexGrow = 1;
            gridContent = gridScroll.contentContainer;
            gridContent.style.flexDirection = FlexDirection.Row;
            gridContent.style.flexWrap = Wrap.Wrap;
            gridContent.style.paddingLeft = gridContent.style.paddingTop = 4;
            right.Add(gridScroll);

            statusLabel = new Label();
            statusLabel.style.fontSize = 10;
            statusLabel.style.paddingLeft = 6;
            statusLabel.style.paddingBottom = 2;
            statusLabel.style.opacity = 0.7f;
            right.Add(statusLabel);

            root.schedule.Execute(UpdateStatus).Every(250);
            RebuildAll();
        }

        // ======================================================================
        // Toolbars
        // ======================================================================

        VisualElement BuildToolbar()
        {
            var bar = new Toolbar();

            var searchField = new ToolbarSearchField { value = search };
            searchField.style.flexGrow = 1;
            searchField.style.minWidth = 80;
            searchField.RegisterValueChangedCallback(e => { search = e.newValue; RebuildGrid(); });
            bar.Add(searchField);

            var sizeSlider = new Slider(48, 256) { value = thumbSize, tooltip = "Thumbnail size" };
            sizeSlider.style.width = 110;
            sizeSlider.RegisterValueChangedCallback(e => { thumbSize = e.newValue; ApplyCardSize(); });
            bar.Add(sizeSlider);

            var thumbBtn = new ToolbarButton { text = "Thumbnails ▾", tooltip = "Thumbnail camera / lighting settings" };
            thumbBtn.clicked += () => UnityEditor.PopupWindow.Show(thumbBtn.worldBound, new ThumbnailSettingsPopup(this));
            bar.Add(thumbBtn);

            var edit = new ToolbarToggle { text = "Edit", value = editMode, tooltip = "Edit categories and sources" };
            edit.RegisterValueChangedCallback(e => { editMode = e.newValue; RebuildAll(); });
            bar.Add(edit);

            var refresh = new ToolbarButton(() => { resolveCache.Clear(); RebuildAll(); }) { text = "Refresh" };
            bar.Add(refresh);
            return bar;
        }

        VisualElement BuildPlacementBar()
        {
            var bar = new Toolbar();

            var snap = new ToolbarToggle { text = "Snap", value = ModelBrowserPrefs.Snap, tooltip = "Snap X/Z to grid" };
            snap.RegisterValueChangedCallback(e => ModelBrowserPrefs.Snap = e.newValue);
            bar.Add(snap);

            bar.Add(SmallFloat("Grid", ModelBrowserPrefs.GridSize, v => ModelBrowserPrefs.GridSize = v, "Grid size (m)"));

            var snapY = new ToolbarToggle { text = "Y", value = ModelBrowserPrefs.SnapY, tooltip = "Also snap height to grid" };
            snapY.RegisterValueChangedCallback(e => ModelBrowserPrefs.SnapY = e.newValue);
            bar.Add(snapY);

            bar.Add(SmallFloat("Rot°", ModelBrowserPrefs.RotationStep, v => ModelBrowserPrefs.RotationStep = v, "Rotation step (R / Shift+R)"));

            var surface = new ToolbarToggle { text = "Surface", value = ModelBrowserPrefs.SurfaceMode, tooltip = "On: stick to objects under the mouse. Off: always use the ground plane." };
            surface.RegisterValueChangedCallback(e => ModelBrowserPrefs.SurfaceMode = e.newValue);
            bar.Add(surface);

            bar.Add(SmallFloat("Plane Y", ModelBrowserPrefs.PlaneHeight, v => ModelBrowserPrefs.PlaneHeight = v, "Height of the ground plane used when nothing is under the mouse"));

            var useParent = new ToolbarToggle { text = "Parent", value = ModelBrowserPrefs.UseParent, tooltip = "Place new objects under this parent" };
            useParent.RegisterValueChangedCallback(e => ModelBrowserPrefs.UseParent = e.newValue);
            bar.Add(useParent);

            var parentField = new ObjectField { objectType = typeof(Transform), allowSceneObjects = true, value = parentTarget };
            parentField.style.width = 130;
            parentField.RegisterValueChangedCallback(e =>
            {
                parentTarget = e.newValue as Transform;
                ModelPlacementTool.Parent = parentTarget;
                if (parentTarget) { ModelBrowserPrefs.UseParent = true; useParent.SetValueWithoutNotify(true); }
            });
            bar.Add(parentField);

            placingLabel = new Label();
            placingLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            placingLabel.style.marginLeft = 6;
            placingLabel.style.color = Accent;
            placingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            bar.Add(placingLabel);
            OnPlacementChanged();
            return bar;
        }

        static VisualElement SmallFloat(string label, float value, Action<float> set, string tooltip)
        {
            var row = new VisualElement { tooltip = tooltip };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginLeft = 4;
            var l = new Label(label);
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            l.style.marginRight = 2;
            row.Add(l);
            var f = new FloatField { value = value };
            f.style.width = 42;
            f.RegisterValueChangedCallback(e => set(e.newValue));
            row.Add(f);
            return row;
        }

        // ======================================================================
        // Rebuild
        // ======================================================================

        void QueueRefresh()
        {
            if (refreshQueued) return;
            refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                refreshQueued = false;
                if (this == null) return;
                resolveCache.Clear();
                RebuildAll();
            };
        }

        void OnAssetsChanged() => QueueRefresh();
        void OnRecentChanged() { if (special == SpecialTab.Recent) QueueRefresh(); }

        void RebuildAll()
        {
            if (tabsRow == null) return;
            ClampSelection();
            RebuildTabs();
            RebuildSidebar();
            RebuildSources();
            RebuildGrid();
        }

        void ClampSelection()
        {
            if (db == null) return;
            if (db.categories.Count == 0) { mainIndex = 0; subIndex = -1; return; }
            mainIndex = Mathf.Clamp(mainIndex, 0, db.categories.Count - 1);
            var subs = db.categories[mainIndex].subCategories;
            if (subIndex >= subs.Count) subIndex = subs.Count - 1;
        }

        MainCategory CurrentMain => db != null && special == SpecialTab.None && mainIndex < db.categories.Count ? db.categories[mainIndex] : null;
        SubCategory CurrentSub => CurrentMain != null && subIndex >= 0 && subIndex < CurrentMain.subCategories.Count ? CurrentMain.subCategories[subIndex] : null;

        // ---------------- Tabs ----------------

        void RebuildTabs()
        {
            tabsRow.Clear();
            if (db == null) return;

            for (int i = 0; i < db.categories.Count; i++)
            {
                int index = i;
                var cat = db.categories[i];
                bool selected = special == SpecialTab.None && mainIndex == i;

                if (editMode)
                {
                    var field = NameField(cat.name, v => cat.name = v);
                    field.style.width = 110;
                    var del = SmallButton("×", () => DeleteMain(index), "Delete category");
                    var group = Row(field, del);
                    group.style.marginRight = 8;
                    StyleTab(group, selected);
                    group.RegisterCallback<MouseDownEvent>(e => { if (e.button == 0) SelectMain(index); });
                    tabsRow.Add(group);
                }
                else
                {
                    var tab = new Label(cat.name);
                    StyleTab(tab, selected);
                    tab.RegisterCallback<MouseDownEvent>(e => { if (e.button == 0) SelectMain(index); });
                    tab.AddManipulator(new ContextualMenuManipulator(evt =>
                    {
                        evt.menu.AppendAction("Move Left", _ => MoveMain(index, -1), index > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                        evt.menu.AppendAction("Move Right", _ => MoveMain(index, 1), index < db.categories.Count - 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                        evt.menu.AppendAction("Rename (Edit mode)", _ => { editMode = true; CreateGUI(); });
                        evt.menu.AppendAction("Delete", _ => DeleteMain(index));
                    }));
                    tabsRow.Add(tab);
                }
            }

            if (editMode)
            {
                tabsRow.Add(SmallButton("+ Category", () =>
                {
                    Undo.RecordObject(db, "Add Category");
                    db.categories.Add(new MainCategory { name = "New Category" });
                    mainIndex = db.categories.Count - 1; subIndex = -1; special = SpecialTab.None;
                    db.Save();
                }, "Add main category"));
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            tabsRow.Add(spacer);

            tabsRow.Add(SpecialTabLabel("★ Favorites", SpecialTab.Favorites));
            tabsRow.Add(SpecialTabLabel("Recent", SpecialTab.Recent));

            if (db.categories.Count == 0 && !editMode)
            {
                var hint = new Label("ลากโฟลเดอร์แพ็กโมเดลจาก Project มาวางตรงนี้เพื่อสร้างหมวดใหญ่");
                hint.style.opacity = 0.6f;
                hint.style.unityTextAlign = TextAnchor.MiddleLeft;
                tabsRow.Insert(0, hint);
            }
        }

        Label SpecialTabLabel(string text, SpecialTab tab)
        {
            var l = new Label(text);
            StyleTab(l, special == tab);
            l.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button != 0) return;
                special = special == tab ? SpecialTab.None : tab;
                ModelThumbnailCache.CancelQueue();
                RebuildAll();
            });
            return l;
        }

        static void StyleTab(VisualElement ve, bool selected)
        {
            ve.style.paddingLeft = ve.style.paddingRight = 10;
            ve.style.paddingTop = ve.style.paddingBottom = 4;
            ve.style.marginRight = 2;
            ve.style.borderTopLeftRadius = ve.style.borderTopRightRadius = 4;
            ve.style.borderBottomLeftRadius = ve.style.borderBottomRightRadius = 4;
            ve.style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal;
            ve.style.fontSize = 13;
            ve.style.backgroundColor = selected ? Accent : Color.clear;
            if (selected) ve.style.color = Color.white;
        }

        void SelectMain(int index)
        {
            if (mainIndex == index && special == SpecialTab.None) return;
            mainIndex = index; subIndex = -1; special = SpecialTab.None;
            ModelThumbnailCache.CancelQueue();
            RebuildAll();
        }

        void MoveMain(int index, int dir)
        {
            int j = index + dir;
            if (j < 0 || j >= db.categories.Count) return;
            Undo.RecordObject(db, "Move Category");
            (db.categories[index], db.categories[j]) = (db.categories[j], db.categories[index]);
            if (mainIndex == index) mainIndex = j;
            db.Save();
        }

        void DeleteMain(int index)
        {
            var cat = db.categories[index];
            if (!EditorUtility.DisplayDialog("Delete category", $"ลบหมวด \"{cat.name}\" และหมวดย่อยทั้งหมด?\n(ไฟล์ asset จริงไม่ถูกลบ)", "Delete", "Cancel")) return;
            Undo.RecordObject(db, "Delete Category");
            db.categories.RemoveAt(index);
            db.Save();
        }

        // ---------------- Sidebar ----------------

        void RebuildSidebar()
        {
            sidebarList.Clear();
            if (db == null) { BuildCreateDatabaseUI(); return; }

            var main = CurrentMain;
            if (main == null)
            {
                if (special == SpecialTab.None && db.categories.Count == 0)
                    sidebarList.Add(Hint("ยังไม่มีหมวด\nลากโฟลเดอร์มาที่แถบด้านบน หรือกด Edit → + Category"));
                return;
            }

            sidebarList.Add(SidebarItem("All", -1, null));
            for (int i = 0; i < main.subCategories.Count; i++)
                sidebarList.Add(SidebarItem(main.subCategories[i].name, i, main.subCategories[i]));

            if (editMode)
            {
                var add = SmallButton("+ Sub-category", () =>
                {
                    Undo.RecordObject(db, "Add Sub-category");
                    main.subCategories.Add(new SubCategory { name = "New" });
                    subIndex = main.subCategories.Count - 1;
                    db.Save();
                }, "Add sub-category");
                add.style.marginTop = 6;
                sidebarList.Add(add);
            }

            var dropHint = Hint("ลากโฟลเดอร์ / prefab มาวาง\nบนหมวดย่อย หรือที่ว่างนี้");
            dropHint.style.marginTop = 10;
            sidebarList.Add(dropHint);
        }

        VisualElement SidebarItem(string name, int index, SubCategory sub)
        {
            bool selected = subIndex == index;
            VisualElement item;

            if (editMode && sub != null)
            {
                var field = NameField(name, v => sub.name = v);
                field.style.flexGrow = 1;
                var up = SmallButton("▲", () => MoveSub(index, -1), "Move up");
                var del = SmallButton("×", () => DeleteSub(index), "Delete sub-category");
                item = Row(field, up, del);
            }
            else
            {
                item = new Label(sub != null ? $"{name}  ({Resolve(sub).Count})" : name);
                if (sub != null)
                {
                    item.AddManipulator(new ContextualMenuManipulator(evt =>
                    {
                        evt.menu.AppendAction("Move Up", _ => MoveSub(index, -1));
                        evt.menu.AppendAction("Move Down", _ => MoveSub(index, 1));
                        evt.menu.AppendAction("Rename (Edit mode)", _ => { editMode = true; CreateGUI(); });
                        evt.menu.AppendAction("Delete", _ => DeleteSub(index));
                    }));
                }
            }

            item.style.paddingLeft = 8;
            item.style.paddingRight = 4;
            item.style.paddingTop = item.style.paddingBottom = 5;
            item.style.fontSize = 12;
            item.style.backgroundColor = selected ? Accent : Color.clear;
            if (selected) item.style.color = Color.white;
            item.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button != 0 || subIndex == index) return;
                subIndex = index;
                ModelThumbnailCache.CancelQueue();
                RebuildSidebar(); RebuildSources(); RebuildGrid();
            });

            if (sub != null) RegisterDrop(item, paths => AddPathsToSub(sub, paths));
            return item;
        }

        void MoveSub(int index, int dir)
        {
            var subs = CurrentMain.subCategories;
            int j = index + dir;
            if (j < 0 || j >= subs.Count) return;
            Undo.RecordObject(db, "Move Sub-category");
            (subs[index], subs[j]) = (subs[j], subs[index]);
            if (subIndex == index) subIndex = j;
            db.Save();
        }

        void DeleteSub(int index)
        {
            var subs = CurrentMain.subCategories;
            if (!EditorUtility.DisplayDialog("Delete sub-category", $"ลบหมวดย่อย \"{subs[index].name}\"?\n(ไฟล์ asset จริงไม่ถูกลบ)", "Delete", "Cancel")) return;
            Undo.RecordObject(db, "Delete Sub-category");
            subs.RemoveAt(index);
            if (subIndex >= subs.Count) subIndex = subs.Count - 1;
            db.Save();
        }

        void BuildCreateDatabaseUI()
        {
            var box = new HelpBox("ยังไม่มีไฟล์ฐานข้อมูล (ScriptableObject) สำหรับเก็บหมวดหมู่", HelpBoxMessageType.Info);
            sidebarList.Add(box);
            sidebarList.Add(new Button(CreateDatabase) { text = "Create Database" });
        }

        void CreateDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Model Browser Database", "ModelBrowserDatabase", "asset", "เลือกที่เก็บฐานข้อมูลหมวดหมู่");
            if (string.IsNullOrEmpty(path)) return;
            db = CreateInstance<ModelBrowserDatabase>();
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            RebuildAll();
        }

        // ---------------- Sources panel (edit mode) ----------------

        void RebuildSources()
        {
            sourcesPanel.Clear();
            var sub = CurrentSub;
            sourcesPanel.style.display = editMode && sub != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (!editMode || sub == null) return;

            var header = new Label($"แหล่งที่มาของ \"{sub.name}\"  —  ลากโฟลเดอร์ / prefab มาวางที่นี่");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 3;
            sourcesPanel.Add(header);

            if (sub.sources.Count == 0) sourcesPanel.Add(Hint("(ยังไม่มี)"));

            for (int i = 0; i < sub.sources.Count; i++)
            {
                int index = i;
                var src = sub.sources[i];
                string path = src.AssetPath;
                bool missing = string.IsNullOrEmpty(path);

                var icon = new Image { image = missing ? EditorGUIUtility.IconContent("console.warnicon.sml").image : AssetDatabase.GetCachedIcon(path) };
                icon.style.width = icon.style.height = 16;

                var label = new Label(missing ? "(missing asset)" : path);
                label.style.flexGrow = 1;
                label.style.flexShrink = 1;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.tooltip = path;
                label.RegisterCallback<MouseDownEvent>(e => { if (!missing) EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path)); });

                var row = Row(icon, label);
                if (!missing && src.IsFolder)
                {
                    int direct = db.CountInFolder(path, false), all = db.CountInFolder(path, true);
                    var toggle = new Toggle { value = src.includeSubfolders, tooltip = $"เฉพาะโฟลเดอร์นี้: {direct} · รวมโฟลเดอร์ย่อย: {all}" };
                    toggle.text = $"รวมโฟลเดอร์ย่อย ({(src.includeSubfolders ? all : direct)})";
                    toggle.RegisterValueChangedCallback(e =>
                    {
                        Undo.RecordObject(db, "Toggle Subfolders");
                        src.includeSubfolders = e.newValue;
                        db.Save();
                    });
                    row.Add(toggle);
                }
                row.Add(SmallButton("×", () =>
                {
                    Undo.RecordObject(db, "Remove Source");
                    sub.sources.RemoveAt(index);
                    db.Save();
                }, "Remove source (does not delete files)"));
                sourcesPanel.Add(row);
            }
        }

        // ======================================================================
        // Grid
        // ======================================================================

        List<string> Resolve(SubCategory sub)
        {
            if (resolveCache.TryGetValue(sub, out var list)) return list;
            list = sub.sources.SelectMany(s => db.Resolve(s)).Distinct().ToList();
            resolveCache[sub] = list;
            return list;
        }

        List<string> CurrentItems()
        {
            if (db == null) return new List<string>();
            IEnumerable<string> items;
            bool keepOrder = false;

            switch (special)
            {
                case SpecialTab.Favorites:
                    items = db.favorites.Select(AssetDatabase.GUIDToAssetPath);
                    break;
                case SpecialTab.Recent:
                    items = ModelBrowserPrefs.GetRecentGuids().Select(AssetDatabase.GUIDToAssetPath);
                    keepOrder = true;
                    break;
                default:
                    var main = CurrentMain;
                    if (main == null) return new List<string>();
                    var sub = CurrentSub;
                    items = sub != null ? Resolve(sub) : main.subCategories.SelectMany(Resolve).Distinct();
                    break;
            }

            items = items.Where(p => !string.IsNullOrEmpty(p) && ModelBrowserDatabase.IsPlaceableFile(p));
            if (!string.IsNullOrWhiteSpace(search))
                items = items.Where(p => Path.GetFileNameWithoutExtension(p).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            if (!keepOrder) items = items.OrderBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase);
            return items.ToList();
        }

        void RebuildGrid()
        {
            if (gridContent == null) return;
            Vector2 scroll = gridScroll.scrollOffset;
            gridScroll.schedule.Execute(() => gridScroll.scrollOffset = scroll);
            gridContent.Clear();
            cards.Clear();
            cardImages.Clear();

            var items = CurrentItems();
            itemCount = items.Count;
            foreach (var path in items) gridContent.Add(CreateCard(path));

            if (items.Count == 0 && db != null)
            {
                string msg = special == SpecialTab.Favorites ? "ยังไม่มีรายการโปรด — กด ★ บนการ์ด"
                    : special == SpecialTab.Recent ? "ยังไม่ได้วางอะไร"
                    : !string.IsNullOrWhiteSpace(search) ? "ไม่พบชื่อที่ค้นหา"
                    : "ว่าง — กด Edit แล้วลากโฟลเดอร์มาใส่หมวดย่อย";
                var h = Hint(msg);
                h.style.marginTop = 20;
                h.style.marginLeft = 10;
                gridContent.Add(h);
            }
        }

        VisualElement CreateCard(string path)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            string name = Path.GetFileNameWithoutExtension(path);
            bool isFav = db.favorites.Contains(guid);

            var card = new VisualElement { tooltip = path };
            card.style.marginRight = card.style.marginBottom = 4;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = 2;
            card.style.paddingBottom = 3;
            card.style.backgroundColor = CardBg;
            SetRadius(card, 4);
            SetBorder(card, 2, Color.clear);

            var img = new Image { scaleMode = ScaleMode.ScaleToFit };
            img.image = ModelThumbnailCache.Get(guid) ?? AssetDatabase.GetCachedIcon(path);
            SetRadius(img, 3);
            card.Add(img);

            var label = new Label(name);
            label.style.fontSize = 10;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginTop = 2;
            card.Add(label);

            var star = new Label("★") { tooltip = "Favorite" };
            star.style.position = Position.Absolute;
            star.style.right = 4;
            star.style.top = 2;
            star.style.fontSize = 15;
            star.style.color = isFav ? Gold : new Color(1, 1, 1, 0.35f);
            star.style.display = isFav ? DisplayStyle.Flex : DisplayStyle.None;
            star.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button != 0) return;
                ToggleFavorite(guid);
                e.StopPropagation();
            });
            card.Add(star);

            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                card.style.backgroundColor = CardHover;
                star.style.display = DisplayStyle.Flex;
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                card.style.backgroundColor = CardBg;
                star.style.display = db.favorites.Contains(guid) ? DisplayStyle.Flex : DisplayStyle.None;
            });

            // Click / double-click / drag
            bool pressed = false;
            Vector2 downPos = default;
            card.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button != 0) return;
                if (e.clickCount >= 2)
                {
                    pressed = false;
                    ModelPlacementTool.End();
                    OpenItem(path);
                }
                else
                {
                    pressed = true;
                    downPos = e.mousePosition;
                }
                e.StopPropagation();
            });
            card.RegisterCallback<MouseMoveEvent>(e =>
            {
                if (!pressed || (e.pressedButtons & 1) == 0) return;
                if ((e.mousePosition - downPos).sqrMagnitude > 36f) { pressed = false; StartDrag(path); }
            });
            card.RegisterCallback<MouseLeaveEvent>(e =>
            {
                if (pressed && (e.pressedButtons & 1) != 0) StartDrag(path);
                pressed = false;
            });
            card.RegisterCallback<MouseUpEvent>(e =>
            {
                if (e.button != 0 || !pressed) return;
                pressed = false;
                ModelPlacementTool.Toggle(path);
            });

            card.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Place in Scene (click)", _ => ModelPlacementTool.Begin(path));
                evt.menu.AppendAction(ModelBrowserDatabase.IsPrefabFile(path) ? "Open Prefab (double-click)" : "Select Model", _ => OpenItem(path));
                evt.menu.AppendAction("Ping in Project", _ => EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path)));
                evt.menu.AppendAction(db.favorites.Contains(guid) ? "Remove from Favorites" : "Add to Favorites", _ => ToggleFavorite(guid));
                evt.menu.AppendAction("Re-render Thumbnail", _ => ModelThumbnailCache.ForceRerender(guid));
            }));

            cards[guid] = card;
            cardImages[guid] = img;
            SizeCard(card, img);
            if (ModelPlacementTool.ActivePath == path) SetBorder(card, 2, Accent);
            return card;
        }

        void SizeCard(VisualElement card, Image img)
        {
            card.style.width = thumbSize + 4;
            img.style.width = img.style.height = thumbSize;
        }

        void ApplyCardSize()
        {
            foreach (var kv in cards)
                if (cardImages.TryGetValue(kv.Key, out var img)) SizeCard(kv.Value, img);
        }

        void StartDrag(string path)
        {
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!obj) return;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new Object[] { obj };
            DragAndDrop.paths = new[] { path };
            DragAndDrop.SetGenericData(ModelPlacementTool.DragKey, path);
            DragAndDrop.StartDrag(obj.name);
        }

        static void OpenItem(string path)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (!obj) return;
            if (ModelBrowserDatabase.IsPrefabFile(path)) AssetDatabase.OpenAsset(obj); // enters Prefab Mode
            else { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
        }

        void ToggleFavorite(string guid)
        {
            Undo.RecordObject(db, "Toggle Favorite");
            if (!db.favorites.Remove(guid)) db.favorites.Add(guid);
            db.Save();
        }

        void OnThumbnailReady(string guid)
        {
            if (cardImages.TryGetValue(guid, out var img)) img.image = ModelThumbnailCache.GetLoaded(guid);
        }

        void OnPlacementChanged()
        {
            if (placingLabel != null)
                placingLabel.text = ModelPlacementTool.IsActive ? "● Placing: " + Path.GetFileNameWithoutExtension(ModelPlacementTool.ActivePath) + "  (Esc to exit)" : "";
            foreach (var kv in cards)
            {
                bool active = ModelPlacementTool.IsActive && AssetDatabase.GUIDToAssetPath(kv.Key) == ModelPlacementTool.ActivePath;
                SetBorder(kv.Value, 2, active ? Accent : Color.clear);
            }
        }

        void UpdateStatus()
        {
            if (statusLabel == null) return;
            string s = $"{itemCount} items";
            if (ModelThumbnailCache.Pending > 0)
                s += $"   ·   Rendering thumbnails {ModelThumbnailCache.BatchDone}/{ModelThumbnailCache.BatchTotal}";
            statusLabel.text = s;
        }

        public void OnThumbnailSettingsChanged()
        {
            ModelThumbnailCache.CancelQueue();
            RebuildGrid();
        }

        // ======================================================================
        // Drag & drop INTO the window (folders / prefabs from Project, or cards)
        // ======================================================================

        static List<string> DraggedPaths() =>
            (DragAndDrop.paths ?? Array.Empty<string>())
            .Where(p => AssetDatabase.IsValidFolder(p) || ModelBrowserDatabase.IsPlaceableFile(p))
            .Distinct().ToList();

        void RegisterDrop(VisualElement ve, Action<List<string>> onDrop)
        {
            ve.RegisterCallback<DragUpdatedEvent>(e =>
            {
                if (db == null || DraggedPaths().Count == 0) return;
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                SetBorder(ve, 1, Accent);
                e.StopPropagation();
            });
            ve.RegisterCallback<DragLeaveEvent>(_ => SetBorder(ve, 0, Color.clear));
            ve.RegisterCallback<DragExitedEvent>(_ => SetBorder(ve, 0, Color.clear));
            ve.RegisterCallback<DragPerformEvent>(e =>
            {
                var paths = DraggedPaths();
                if (db == null || paths.Count == 0) return;
                DragAndDrop.AcceptDrag();
                SetBorder(ve, 0, Color.clear);
                e.StopPropagation();
                // Run after the drag finishes so modal dialogs are safe.
                EditorApplication.delayCall += () => { if (this != null) onDrop(paths); };
            });
        }

        /// <summary>Ask once: only this folder, or include all subfolders? null = cancelled.</summary>
        bool? AskIncludeSubfolders(List<string> folders, string title)
        {
            if (folders.Count == 0) return true;
            int direct = folders.Sum(f => db.CountInFolder(f, false));
            int all = folders.Sum(f => db.CountInFolder(f, true));
            string list = string.Join("\n", folders.Take(6)) + (folders.Count > 6 ? $"\n... (+{folders.Count - 6})" : "");
            int choice = EditorUtility.DisplayDialogComplex(title,
                $"{list}\n\nจะดึง prefab จากโฟลเดอร์แบบไหน?\n• เฉพาะโฟลเดอร์นี้: {direct} ชิ้น\n• รวมโฟลเดอร์ย่อยทั้งหมด: {all} ชิ้น",
                $"รวมโฟลเดอร์ย่อย ({all})", "ยกเลิก", $"เฉพาะโฟลเดอร์นี้ ({direct})");
            if (choice == 1) return null;
            return choice == 0;
        }

        void AddPathsToSub(SubCategory sub, List<string> paths)
        {
            var folders = paths.Where(AssetDatabase.IsValidFolder).ToList();
            bool? include = AskIncludeSubfolders(folders, $"เพิ่มเข้า \"{sub.name}\"");
            if (include == null) return;

            Undo.RecordObject(db, "Add Sources");
            foreach (var p in paths)
            {
                string guid = AssetDatabase.AssetPathToGUID(p);
                var existing = sub.sources.FirstOrDefault(s => s.guid == guid);
                bool isFolder = AssetDatabase.IsValidFolder(p);
                if (existing != null) { if (isFolder) existing.includeSubfolders = include.Value; continue; }
                sub.sources.Add(new SourceEntry { guid = guid, includeSubfolders = !isFolder || include.Value });
            }
            db.Save();
        }

        /// <summary>Dropped on the sidebar's empty area: one new sub-category per folder (prefabs go to "New").</summary>
        void CreateSubsFromPaths(List<string> paths)
        {
            var main = CurrentMain;
            if (main == null)
            {
                if (db.categories.Count == 0) { CreateMainFromFolders(paths); return; }
                EditorUtility.DisplayDialog("Model Browser", "เลือกหมวดใหญ่ (tab ด้านบน) ก่อน", "OK");
                return;
            }

            var folders = paths.Where(AssetDatabase.IsValidFolder).ToList();
            var files = paths.Where(p => !AssetDatabase.IsValidFolder(p)).ToList();
            bool? include = AskIncludeSubfolders(folders, $"สร้างหมวดย่อยใน \"{main.name}\"");
            if (include == null) return;

            Undo.RecordObject(db, "Create Sub-categories");
            foreach (var f in folders)
            {
                var sub = new SubCategory { name = Path.GetFileName(f) };
                sub.sources.Add(new SourceEntry { guid = AssetDatabase.AssetPathToGUID(f), includeSubfolders = include.Value });
                main.subCategories.Add(sub);
            }
            if (files.Count > 0)
            {
                var sub = new SubCategory { name = "New" };
                foreach (var f in files) sub.sources.Add(new SourceEntry { guid = AssetDatabase.AssetPathToGUID(f) });
                main.subCategories.Add(sub);
            }
            subIndex = main.subCategories.Count - 1;
            db.Save();
        }

        /// <summary>Dropped on the tab bar: a pack folder becomes a main category, its child folders become sub-categories.</summary>
        void CreateMainFromFolders(List<string> paths)
        {
            var folders = paths.Where(AssetDatabase.IsValidFolder).ToList();
            if (folders.Count == 0)
            {
                EditorUtility.DisplayDialog("Model Browser", "ลากเป็นโฟลเดอร์มาที่แถบนี้เพื่อสร้างหมวดใหญ่\n(prefab เดี่ยวให้ลากไปที่หมวดย่อยด้านซ้าย)", "OK");
                return;
            }

            Undo.RecordObject(db, "Create Categories");
            foreach (var folder in folders)
            {
                var children = AssetDatabase.GetSubFolders(folder).Where(c => db.CountInFolder(c, true) > 0).ToList();
                int choice = children.Count == 0 ? 2 : EditorUtility.DisplayDialogComplex($"สร้างหมวดใหญ่ \"{Path.GetFileName(folder)}\"",
                    $"{folder}\n\nพบโฟลเดอร์ลูกที่มีโมเดล {children.Count} โฟลเดอร์:\n{string.Join(", ", children.Take(8).Select(Path.GetFileName))}{(children.Count > 8 ? " ..." : "")}\n\nสร้างหมวดย่อยตามโฟลเดอร์ลูกเลยไหม?",
                    $"แยกหมวดย่อยตามโฟลเดอร์ ({children.Count})", "ยกเลิก", "หมวดย่อยเดียว (รวมทั้งหมด)");
                if (choice == 1) continue;

                var main = new MainCategory { name = Path.GetFileName(folder) };
                if (choice == 0)
                {
                    if (db.CountInFolder(folder, false) > 0)
                    {
                        var rootSub = new SubCategory { name = Path.GetFileName(folder) };
                        rootSub.sources.Add(new SourceEntry { guid = AssetDatabase.AssetPathToGUID(folder), includeSubfolders = false });
                        main.subCategories.Add(rootSub);
                    }
                    foreach (var c in children)
                    {
                        var sub = new SubCategory { name = Path.GetFileName(c) };
                        sub.sources.Add(new SourceEntry { guid = AssetDatabase.AssetPathToGUID(c), includeSubfolders = true });
                        main.subCategories.Add(sub);
                    }
                }
                else
                {
                    var sub = new SubCategory { name = "All" };
                    sub.sources.Add(new SourceEntry { guid = AssetDatabase.AssetPathToGUID(folder), includeSubfolders = true });
                    main.subCategories.Add(sub);
                }
                db.categories.Add(main);
                mainIndex = db.categories.Count - 1;
                subIndex = -1;
                special = SpecialTab.None;
            }
            db.Save();
        }

        // ======================================================================
        // Small UI helpers
        // ======================================================================

        TextField NameField(string value, Action<string> set)
        {
            var f = new TextField { value = value, isDelayed = true };
            f.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(db, "Rename");
                set(e.newValue);
                db.Save();
            });
            return f;
        }

        static Button SmallButton(string text, Action onClick, string tooltip)
        {
            var b = new Button(onClick) { text = text, tooltip = tooltip };
            b.style.paddingLeft = b.style.paddingRight = 4;
            b.style.marginLeft = 1;
            b.style.marginRight = 1;
            return b;
        }

        static VisualElement Row(params VisualElement[] children)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            foreach (var c in children) r.Add(c);
            return r;
        }

        static Label Hint(string text)
        {
            var l = new Label(text);
            l.style.opacity = 0.55f;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.fontSize = 11;
            l.style.paddingLeft = 8;
            l.style.paddingRight = 6;
            return l;
        }

        static void SetBorder(VisualElement ve, float width, Color color)
        {
            ve.style.borderLeftWidth = ve.style.borderRightWidth = ve.style.borderTopWidth = ve.style.borderBottomWidth = width;
            ve.style.borderLeftColor = ve.style.borderRightColor = ve.style.borderTopColor = ve.style.borderBottomColor = color;
        }

        static void SetRadius(VisualElement ve, float r)
        {
            ve.style.borderTopLeftRadius = ve.style.borderTopRightRadius = r;
            ve.style.borderBottomLeftRadius = ve.style.borderBottomRightRadius = r;
        }

        // ======================================================================
        // Thumbnail settings popup
        // ======================================================================

        class ThumbnailSettingsPopup : PopupWindowContent
        {
            readonly ModelBrowserWindow window;
            float yaw, pitch, light, ambient;
            Color bg;

            public ThumbnailSettingsPopup(ModelBrowserWindow w)
            {
                window = w;
                yaw = ModelBrowserPrefs.ThumbYaw;
                pitch = ModelBrowserPrefs.ThumbPitch;
                light = ModelBrowserPrefs.ThumbLight;
                ambient = ModelBrowserPrefs.ThumbAmbient;
                bg = ModelBrowserPrefs.ThumbBackground;
            }

            public override Vector2 GetWindowSize() => new Vector2(280, 230);

            public override void OnGUI(Rect rect)
            {
                EditorGUILayout.LabelField("Thumbnail Camera", EditorStyles.boldLabel);
                yaw = EditorGUILayout.Slider("Yaw", yaw, 0, 360);
                pitch = EditorGUILayout.Slider("Pitch", pitch, -10, 89);
                light = EditorGUILayout.Slider("Light", light, 0, 5);
                ambient = EditorGUILayout.Slider("Ambient", ambient, 0, 1);
                bg = EditorGUILayout.ColorField("Background", bg);

                if (window.db != null)
                {
                    EditorGUI.BeginChangeCheck();
                    bool fbx = EditorGUILayout.Toggle(new GUIContent("Include FBX/OBJ", "List model files that are not saved as .prefab"), window.db.includeModelFiles);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(window.db, "Include Model Files");
                        window.db.includeModelFiles = fbx;
                        window.resolveCache.Clear();
                        window.db.Save();
                    }
                }

                GUILayout.Space(6);
                if (GUILayout.Button("Apply (re-render changed)"))
                {
                    ModelBrowserPrefs.ThumbYaw = yaw;
                    ModelBrowserPrefs.ThumbPitch = pitch;
                    ModelBrowserPrefs.ThumbLight = light;
                    ModelBrowserPrefs.ThumbAmbient = ambient;
                    ModelBrowserPrefs.ThumbBackground = bg;
                    window.OnThumbnailSettingsChanged();
                }
                if (GUILayout.Button("Clear cache & re-render all"))
                {
                    ModelThumbnailCache.ClearAll();
                    window.OnThumbnailSettingsChanged();
                }
            }
        }
    }
}
