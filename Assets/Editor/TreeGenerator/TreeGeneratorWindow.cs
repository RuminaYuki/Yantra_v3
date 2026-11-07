using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SDFcl.Tool.TreeGenerator
{
    public class TreeGeneratorWindow : EditorWindow
    {
        static readonly string[] TabNames = { "โครงต้น", "กิ่งย่อย", "ใบ & วัสดุ", "Gallery & สุ่ม", "Bake & Export" };
        const int VariantCount = 6;

        [SerializeField] TreePreset working;
        [SerializeField] TreePreset source;
        [SerializeField] int tab;
        [SerializeField] bool unsaved;
        [SerializeField] bool showAdvanced;
        [SerializeField] int variantRound = 1;

        Vector2 galleryScroll, settingsScroll;
        SerializedObject so;
        TreeBuildResult result;
        Material[] previewMats;
        bool dirty = true;
        TreePreviewRenderer preview;
        TreeSceneEditor sceneEditor;
        readonly List<TreePreset> gallery = new List<TreePreset>();

        readonly List<TreePreset> variants = new List<TreePreset>();
        readonly List<Texture2D> variantThumbs = new List<Texture2D>();
        int variantSel = -1;
        TreeBuildResult variantResult;
        Material[] variantMats;

        GUIStyle header, mini, tileLabel, centerMini, bigButton, sectionBox, sectionTitle;
        bool sectionOpen;
        static readonly Color SectionColor = new Color(0.20f, 0.33f, 0.50f);

        public TreePreset Working { get { return working; } }
        public TreeBuildResult Result { get { return result; } }

        [MenuItem("Tools/SDFcl/Tree Generator")]
        public static void Open()
        {
            var w = GetWindow<TreeGeneratorWindow>();
            w.titleContent = new GUIContent("Tree Generator");
            w.minSize = new Vector2(1000f, 620f);
            w.Show();
        }

        // ================================================================== lifecycle

        void OnEnable()
        {
            titleContent = new GUIContent("Tree Generator");
            preview = new TreePreviewRenderer();
            sceneEditor = new TreeSceneEditor(this);
            Undo.undoRedoPerformed += OnUndoRedo;
            dirty = true;
            EditorApplication.delayCall += InitAfterLoad;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (sceneEditor != null) sceneEditor.End();
            if (preview != null) preview.Dispose();
            preview = null;
            DestroyResult();
            ClearVariants();
        }

        void OnDestroy()
        {
            if (working != null) DestroyImmediate(working);
        }

        void InitAfterLoad()
        {
            if (this == null) return;
            EnsureStarterPresets();
            RefreshGallery();
            if (working == null)
            {
                if (gallery.Count > 0) LoadPreset(gallery[0], false);
                else NewFromTemplate("Empty");
            }
            dirty = true;
            Repaint();
        }

        void OnUndoRedo()
        {
            if (working == null) return;
            if (so != null) so.Update();
            dirty = true;
            Rebuild();
            Repaint();
            SceneView.RepaintAll();
        }

        // ================================================================== paths

        string ToolRoot
        {
            get
            {
                string path = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                if (string.IsNullOrEmpty(path)) return "Assets/Editor/TreeGenerator";
                return Path.GetDirectoryName(path).Replace('\\', '/');
            }
        }

        string PresetFolder { get { return ToolRoot + "/Presets"; } }
        string ThumbFolder { get { return PresetFolder + "/Thumbnails"; } }

        // ================================================================== working copy

        static TreePreset CloneOf(TreePreset src)
        {
            var c = CreateInstance<TreePreset>();
            EditorUtility.CopySerialized(src, c);
            c.name = src.name;
            c.hideFlags = HideFlags.DontSave;
            return c;
        }

        void SetWorking(TreePreset w)
        {
            if (working != null && working != w) DestroyImmediate(working);
            working = w;
            working.hideFlags = HideFlags.DontSave;
            so = new SerializedObject(working);
            dirty = true;
            if (sceneEditor != null) sceneEditor.selBranch = sceneEditor.selKnot = sceneEditor.selTwig = -1;
        }

        void LoadPreset(TreePreset asset, bool confirm = true)
        {
            if (asset == null) return;
            if (confirm && !ConfirmDiscard()) return;
            SetWorking(CloneOf(asset));
            source = asset;
            unsaved = false;
            ClearVariants();
            Rebuild();
        }

        void NewFromTemplate(string kind)
        {
            var p = CreateInstance<TreePreset>();
            TreeTemplates.Apply(p, kind);
            p.name = TreeBaker.SafeName(p.displayName);
            SetWorking(p);
            source = null;
            unsaved = true;
            ClearVariants();
            Rebuild();
        }

        bool ConfirmDiscard()
        {
            return !unsaved || EditorUtility.DisplayDialog("Tree Generator",
                "ต้นที่เปิดอยู่มีการแก้ไขที่ยังไม่บันทึก จะทิ้งการแก้ไขนี้หรือไม่?", "ทิ้ง", "ยกเลิก");
        }

        /// <summary>เรียกทุกครั้งที่ข้อมูลต้นไม้ถูกแก้ (จากหน้าต่างหรือ Scene View)</summary>
        public void MarkEdited()
        {
            unsaved = true;
            dirty = true;
            if (so != null) so.Update();
            Rebuild();
            Repaint();
            SceneView.RepaintAll();
        }

        public void ForceRebuild()
        {
            dirty = true;
            Rebuild();
        }

        public void StopSceneEdit()
        {
            if (sceneEditor != null) sceneEditor.End();
            Repaint();
        }

        void Rebuild()
        {
            if (!dirty || working == null) return;
            dirty = false;
            DestroyResult();
            result = TreeBuilder.Build(working, TreeBuildOptions.ForLOD(0));
            result.mesh.hideFlags = HideFlags.HideAndDontSave;
            previewMats = TreeMaterialUtil.GetPreviewMaterials(working, result);
            if (sceneEditor != null) sceneEditor.UpdateMesh(result.mesh, previewMats);
        }

        void DestroyResult()
        {
            if (result != null && result.mesh != null) DestroyImmediate(result.mesh);
            result = null;
        }

        void Defer(Action a)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                try
                {
                    a();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    EditorUtility.DisplayDialog("Tree Generator", "เกิดข้อผิดพลาด: " + e.Message, "OK");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
                Repaint();
            };
        }

        // ================================================================== Gallery data

        void EnsureStarterPresets()
        {
            if (AssetDatabase.FindAssets("t:TreePreset").Length > 0) return;
            TreeBaker.EnsureFolder(PresetFolder);
            TreeBaker.EnsureFolder(ThumbFolder);
            try
            {
                for (int i = 0; i < TreeTemplates.Kinds.Length; i++)
                {
                    string kind = TreeTemplates.Kinds[i];
                    EditorUtility.DisplayProgressBar("Tree Generator", "สร้างพันธุ์ตั้งต้น: " + TreeTemplates.DisplayName(kind),
                        i / (float)TreeTemplates.Kinds.Length);
                    var p = CreateInstance<TreePreset>();
                    TreeTemplates.Apply(p, kind);
                    AssetDatabase.CreateAsset(p, PresetFolder + "/" + kind + ".asset");
                    SaveThumbnail(p, p);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        void RefreshGallery()
        {
            gallery.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:TreePreset"))
            {
                var p = AssetDatabase.LoadAssetAtPath<TreePreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null) gallery.Add(p);
            }
            gallery.Sort((a, b) =>
            {
                int c = string.Compare(a.category, b.category, StringComparison.Ordinal);
                return c != 0 ? c : string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
            });
        }

        void SaveThumbnail(TreePreset buildFrom, TreePreset asset)
        {
            if (preview == null) return;
            TreeBaker.EnsureFolder(ThumbFolder);
            TreeBuildResult r = TreeBuilder.Build(buildFrom, TreeBuildOptions.ForLOD(1));
            Texture2D tex = preview.RenderStatic(r.mesh, TreeMaterialUtil.GetPreviewMaterials(buildFrom, r), 256);
            DestroyImmediate(r.mesh);
            if (tex == null) return;
            byte[] png = tex.EncodeToPNG();
            DestroyImmediate(tex);

            string path = ThumbFolder + "/" + asset.name + "_thumb.png";
            File.WriteAllBytes(TreeBaker.FullPath(path), png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.mipmapEnabled = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            asset.thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            EditorUtility.SetDirty(asset);
        }

        void RetakeThumbnails()
        {
            RefreshGallery();
            for (int i = 0; i < gallery.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Tree Generator", "ถ่าย thumbnail: " + gallery[i].displayName, i / (float)gallery.Count);
                SaveThumbnail(gallery[i], gallery[i]);
            }
            AssetDatabase.SaveAssets();
            RefreshGallery();
        }

        void SaveAsNew()
        {
            TreeBaker.EnsureFolder(PresetFolder);
            var asset = CreateInstance<TreePreset>();
            EditorUtility.CopySerialized(working, asset);
            asset.hideFlags = HideFlags.None;
            string path = AssetDatabase.GenerateUniqueAssetPath(PresetFolder + "/" + TreeBaker.SafeName(working.displayName) + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            SaveThumbnail(working, asset);
            AssetDatabase.SaveAssets();
            source = asset;
            working.name = asset.name;
            unsaved = false;
            RefreshGallery();
            ShowNotification(new GUIContent("บันทึกลง Gallery แล้ว"));
        }

        void SaveOverwrite()
        {
            if (source == null)
            {
                SaveAsNew();
                return;
            }
            string keepName = source.name;
            EditorUtility.CopySerialized(working, source);
            source.name = keepName;
            source.hideFlags = HideFlags.None;
            SaveThumbnail(working, source);
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();
            unsaved = false;
            RefreshGallery();
            ShowNotification(new GUIContent("บันทึกทับแล้ว"));
        }

        // ================================================================== Variants

        void ClearVariants()
        {
            foreach (TreePreset v in variants)
                if (v != null) DestroyImmediate(v);
            variants.Clear();
            foreach (Texture2D t in variantThumbs)
                if (t != null) DestroyImmediate(t);
            variantThumbs.Clear();
            variantSel = -1;
            DestroyVariantResult();
        }

        void DestroyVariantResult()
        {
            if (variantResult != null && variantResult.mesh != null) DestroyImmediate(variantResult.mesh);
            variantResult = null;
            variantMats = null;
        }

        void GenerateVariants()
        {
            ClearVariants();
            int baseSeed = unchecked(working.seed * 7919 + variantRound * 104729);
            variantRound++;
            for (int i = 0; i < VariantCount; i++)
            {
                EditorUtility.DisplayProgressBar("Tree Generator", "สุ่มต้นที่ " + (i + 1), i / (float)VariantCount);
                TreePreset v = TreeVariation.CreateVariant(working, baseSeed + i, source);
                v.hideFlags = HideFlags.HideAndDontSave;
                variants.Add(v);
                TreeBuildResult r = TreeBuilder.Build(v, TreeBuildOptions.ForLOD(1));
                Texture2D tex = preview.RenderStatic(r.mesh, TreeMaterialUtil.GetPreviewMaterials(v, r), 160);
                DestroyImmediate(r.mesh);
                if (tex != null) tex.hideFlags = HideFlags.HideAndDontSave;
                variantThumbs.Add(tex);
            }
            EditorUtility.ClearProgressBar();
        }

        void SelectVariant(int i)
        {
            variantSel = i;
            DestroyVariantResult();
            if (i < 0 || i >= variants.Count) return;
            variantResult = TreeBuilder.Build(variants[i], TreeBuildOptions.ForLOD(0));
            variantResult.mesh.hideFlags = HideFlags.HideAndDontSave;
            variantMats = TreeMaterialUtil.GetPreviewMaterials(variants[i], variantResult);
        }

        void SaveSelectedVariant()
        {
            if (variantSel < 0 || variantSel >= variants.Count) return;
            bool link = working.variation.linkVariants;
            if (link && (source == null || unsaved))
            {
                if (!EditorUtility.DisplayDialog("ต้องบันทึกต้นแม่ก่อน",
                        "Variant ที่ผูกกับต้นแม่ต้องอ้างอิงต้นแม่ที่บันทึกแล้ว บันทึกต้นแม่ตอนนี้เลยไหม?", "บันทึก", "ยกเลิก"))
                    return;
                if (source == null) SaveAsNew();
                else SaveOverwrite();
            }

            TreePreset v = variants[variantSel];
            var asset = CreateInstance<TreePreset>();
            EditorUtility.CopySerialized(v, asset);
            asset.hideFlags = HideFlags.None;
            asset.displayName = working.displayName + " #" + (variantSel + 1);
            asset.sourcePreset = link ? source : null;
            asset.thumbnail = null;
            TreeBaker.EnsureFolder(PresetFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(PresetFolder + "/" + TreeBaker.SafeName(asset.displayName) + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            SaveThumbnail(asset, asset);
            AssetDatabase.SaveAssets();
            RefreshGallery();
            ShowNotification(new GUIContent("บันทึก Variant ลง Gallery แล้ว"));
        }

        void UseVariantAsParent()
        {
            if (variantSel < 0 || variantSel >= variants.Count) return;
            if (!ConfirmDiscard()) return;
            TreePreset c = CloneOf(variants[variantSel]);
            c.sourcePreset = null;
            c.variantSeed = 0;
            c.displayName = working.displayName + " (ใหม่)";
            SetWorking(c);
            source = null;
            unsaved = true;
            ClearVariants();
            Rebuild();
        }

        void UpdateLinked()
        {
            List<TreePreset> updated = TreeVariation.UpdateLinkedVariants(source);
            foreach (TreePreset u in updated) SaveThumbnail(u, u);
            AssetDatabase.SaveAssets();
            RefreshGallery();
            ShowNotification(new GUIContent("อัปเดต " + updated.Count + " Variant แล้ว"));
        }

        int LinkedCount()
        {
            if (source == null) return 0;
            int n = 0;
            foreach (TreePreset g in gallery)
                if (g != source && g.sourcePreset == source) n++;
            return n;
        }

        // ================================================================== Bake

        void BakeOne()
        {
            string path = TreeBaker.Bake(working, working.exportFolder, working.displayName);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            ShowNotification(new GUIContent("Bake แล้ว: " + Path.GetFileName(path)));
        }

        void BakeMany()
        {
            List<string> paths = TreeBaker.BakeBatch(working, working.exportFolder, working.displayName, working.variation.batchCount);
            if (paths.Count > 0)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]);
                if (prefab != null) EditorGUIUtility.PingObject(prefab);
            }
            ShowNotification(new GUIContent("Bake แล้ว " + paths.Count + " ต้น"));
        }

        int EstimateDrawCalls()
        {
            if (result == null) return 1;
            if (result.leafCount == 0) return 1;
            return 2 + TreeMaterialUtil.LeafShaderMismatch(working).Count;
        }

        // ================================================================== GUI

        void InitStyles()
        {
            if (header != null) return;
            header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            mini = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            tileLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            centerMini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            bigButton = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            sectionBox = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 0, 8),
                margin = new RectOffset(2, 2, 2, 2)
            };
            sectionTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
        }

        void OnGUI()
        {
            InitStyles();
            if (working == null)
            {
                GUILayout.Label("กำลังโหลด…");
                return;
            }
            if (so == null || so.targetObject != working) so = new SerializedObject(working);
            Rebuild();
            if (result == null) return;

            DrawToolbar();
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawGallery();
            DrawPreviewArea();
            DrawSettings();
            EditorGUILayout.EndHorizontal();
            DrawStatus();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int nt = GUILayout.Toolbar(tab, TabNames, EditorStyles.toolbarButton, GUILayout.Width(600f));
            if (nt != tab)
            {
                tab = nt;
                GUI.FocusControl(null);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label((source != null ? source.displayName : "ยังไม่บันทึก") + (unsaved ? "  *" : ""), EditorStyles.miniLabel);
            if (GUILayout.Button("Undo", EditorStyles.toolbarButton, GUILayout.Width(44f))) Undo.PerformUndo();
            if (GUILayout.Button("Redo", EditorStyles.toolbarButton, GUILayout.Width(44f))) Undo.PerformRedo();
            EditorGUILayout.EndHorizontal();
        }

        void DrawStatus()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Tris " + result.triangleCount.ToString("N0") + " (กิ่ง " + result.barkTriangles.ToString("N0") +
                            " / ใบ " + result.leafTriangles.ToString("N0") + ")  ·  ใบ " + result.leafCount.ToString("N0") + " ช่อ" +
                            "  ·  Draw calls หลัง Bake ≈ " + EstimateDrawCalls() + "  ·  กิ่งวาดเอง " + working.branches.Count +
                            "  ·  กิ่งอัตโนมัติ " + result.autoTwigCount, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(sceneEditor != null && sceneEditor.active ? "กำลังแก้ใน Scene View" : "Preview สด", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ Gallery

        void DrawGallery()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(214f));
            GUILayout.Label("Gallery พันธุ์ไม้", header);
            galleryScroll = EditorGUILayout.BeginScrollView(galleryScroll);
            string cat = null;
            int col = 0;
            for (int i = 0; i < gallery.Count; i++)
            {
                TreePreset g = gallery[i];
                if (g == null) continue;
                if (g.category != cat)
                {
                    if (col != 0) EditorGUILayout.EndHorizontal();
                    col = 0;
                    cat = g.category;
                    GUILayout.Label(string.IsNullOrEmpty(cat) ? "(ไม่มีหมวด)" : cat, EditorStyles.miniBoldLabel);
                }
                if (col == 0) EditorGUILayout.BeginHorizontal();
                DrawTile(g);
                col++;
                if (col == 2)
                {
                    EditorGUILayout.EndHorizontal();
                    col = 0;
                }
            }
            if (col != 0) EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ ต้นใหม่ (ลำต้นเปล่า)"))
                Defer(() =>
                {
                    if (ConfirmDiscard()) NewFromTemplate("Empty");
                });
            if (GUILayout.Button("รีเฟรช Gallery")) RefreshGallery();
            if (GUILayout.Button("ถ่าย Thumbnail ใหม่ทั้งหมด")) Defer(RetakeThumbnails);
            EditorGUILayout.EndVertical();
        }

        void DrawTile(TreePreset g)
        {
            Rect r = GUILayoutUtility.GetRect(98f, 112f, GUILayout.Width(98f), GUILayout.Height(112f));
            if (GUI.Button(r, GUIContent.none))
            {
                TreePreset target = g;
                Defer(() => LoadPreset(target));
            }
            if (Event.current.type == EventType.Repaint)
            {
                var img = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 26f);
                if (g.thumbnail != null) GUI.DrawTexture(img, g.thumbnail, ScaleMode.ScaleToFit);
                else GUI.Label(img, "ไม่มีรูป", centerMini);
                if (g == source) Outline(r, new Color(0.3f, 0.55f, 0.82f), 2f);
            }
            GUI.Label(new Rect(r.x + 2f, r.yMax - 20f, r.width - 4f, 18f), g.displayName + (g.sourcePreset != null ? " (V)" : ""), tileLabel);
        }

        static void Outline(Rect r, Color c, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        // ------------------------------------------------------------------ Preview

        void DrawPreviewArea()
        {
            Rect r = GUILayoutUtility.GetRect(200f, 200f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            bool showVariant = variantSel >= 0 && variantResult != null;
            Mesh mesh = showVariant ? variantResult.mesh : result.mesh;
            Material[] mats = showVariant ? variantMats : previewMats;

            var styleRect = new Rect(r.x + 10f, r.y + 10f, 190f, 22f);
            var seedRect = new Rect(r.xMax - 250f, r.y + 10f, 240f, 22f);
            if (preview.HandleInput(r, styleRect, seedRect)) Repaint();
            preview.Draw(r, mesh, mats);

            if (!showVariant)
            {
                int st = GUI.Toolbar(styleRect, (int)working.style, new[] { "Realistic", "Stylized" });
                if (st != (int)working.style)
                {
                    Undo.RecordObject(working, "เปลี่ยนสไตล์");
                    working.style = (TreeStyle)st;
                    working.leafMode = st == 1 ? LeafMode.Blob : LeafMode.Card;
                    MarkEdited();
                }

                GUI.Label(new Rect(seedRect.x, seedRect.y, 110f, 22f), "Seed " + working.seed, EditorStyles.whiteLabel);
                if (GUI.Button(new Rect(seedRect.x + 110f, seedRect.y, 130f, 22f), "สุ่มต้นใหม่"))
                {
                    Undo.RecordObject(working, "สุ่ม Seed");
                    working.seed = UnityEngine.Random.Range(1, 999999);
                    MarkEdited();
                }
            }
            else
            {
                GUI.Label(new Rect(r.x + 10f, r.y + 10f, 260f, 22f), "กำลังดู Variant #" + (variantSel + 1), EditorStyles.whiteLargeLabel);
                if (GUI.Button(new Rect(r.xMax - 160f, r.y + 10f, 150f, 22f), "กลับไปดูต้นแม่"))
                {
                    variantSel = -1;
                    DestroyVariantResult();
                }
            }

            GUI.Label(new Rect(r.x + 10f, r.yMax - 22f, r.width - 20f, 20f),
                "ลากเมาส์ = หมุน  ·  Scroll = ซูม  ·  ดับเบิลคลิก = รีเซ็ตมุม", EditorStyles.whiteMiniLabel);
        }

        // ------------------------------------------------------------------ Settings

        void DrawSettings()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(350f));
            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);
            so.Update();
            sectionOpen = false;
            switch (tab)
            {
                case 0: DrawShapeTab(); break;
                case 1: DrawTwigTab(); break;
                case 2: DrawMaterialTab(); break;
                case 3: DrawGalleryTab(); break;
                default: DrawBakeTab(); break;
            }
            CloseSection();
            if (so.ApplyModifiedProperties()) MarkEdited();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>เริ่มหมวดใหม่: ปิดกล่องของหมวดก่อนหน้า แล้วเปิดกล่องใหม่พร้อมแถบหัวข้อสี</summary>
        void Header(string text)
        {
            if (sectionOpen)
            {
                EditorGUILayout.EndVertical();
                GUILayout.Space(8f);
            }
            else GUILayout.Space(4f);

            EditorGUILayout.BeginVertical(sectionBox);
            sectionOpen = true;
            Rect r = GUILayoutUtility.GetRect(10f, 24f, GUILayout.ExpandWidth(true));
            r.xMin -= 8f;
            r.xMax += 8f;
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(r, SectionColor);
            GUI.Label(new Rect(r.x + 8f, r.y, r.width - 8f, r.height), text, sectionTitle);
            GUILayout.Space(6f);
        }

        /// <summary>หัวข้อย่อยในกล่องเดียวกัน: เส้นคั่นบาง + ตัวหนา</summary>
        void SubHeader(string text)
        {
            GUILayout.Space(6f);
            Rect line = GUILayoutUtility.GetRect(10f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(line, new Color(1f, 1f, 1f, 0.12f));
            GUILayout.Space(2f);
            GUILayout.Label(text, EditorStyles.boldLabel);
        }

        void CloseSection()
        {
            if (!sectionOpen) return;
            EditorGUILayout.EndVertical();
            sectionOpen = false;
        }

        void Slider(string prop, string label, float min, float max, string hint = null)
        {
            Slider(so.FindProperty(prop), label, min, max, hint);
        }

        void Slider(SerializedProperty sp, string label, float min, float max, string hint = null)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label);
            GUILayout.FlexibleSpace();
            if (hint != null) GUILayout.Label(hint, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Slider(sp, min, max, GUIContent.none);
        }

        int EnumToolbar(SerializedProperty sp, string[] labels)
        {
            EditorGUI.BeginChangeCheck();
            int v = GUILayout.Toolbar(sp.enumValueIndex, labels);
            if (EditorGUI.EndChangeCheck()) sp.enumValueIndex = v;
            return sp.enumValueIndex;
        }

        // ---------- แท็บ: โครงต้น

        void DrawShapeTab()
        {
            Header("ปรับทรงแบบง่าย");
            Slider("heightScale", "ความสูง / ขนาด", 0.3f, 3f);
            Slider("thickness", "ความอ้วนลำต้น", 0.3f, 3f);
            Slider("fullness", "ความพุ่ม", 0f, 1f, "โปร่ง ↔ ทึบ");
            Slider("messiness", "ความรก", 0f, 1f, "เป็นระเบียบ ↔ ยุ่ง");
            Slider("droop", "กิ่งห้อย", -1f, 1f, "ชี้ฟ้า ↔ ลู่ลง");
            Slider("age", "อายุ", 0f, 1f, "อ่อน ↔ แก่");

            Header("ความละเอียด (จำนวน poly)");
            GUILayout.Label("ตอนนี้ " + result.triangleCount.ToString("N0") + " tris · กิ่ง " + result.barkTriangles.ToString("N0") +
                            " · ใบ " + result.leafTriangles.ToString("N0"), EditorStyles.boldLabel);
            Slider("radialDetail", "ความกลมของกิ่ง", 0.2f, 2f, "เหลี่ยม ↔ กลม");
            EditorGUILayout.IntSlider(so.FindProperty("maxSides"), 3, 16, new GUIContent("ด้านสูงสุดต่อกิ่ง"));
            Slider("lengthDetail", "ความเนียนตามความยาว", 0.2f, 2f, "หยาบ ↔ เนียน");
            Slider("simplifyAngle", "ตัดจุดที่เส้นเกือบตรง (องศา)", 0f, 15f, "0 = ไม่ตัด");
            EditorGUILayout.HelpBox(
                "ลด poly ได้มากที่สุดจาก\n• แท็บกิ่งย่อย: ลด 'จำนวนต่อเมตร' หรือปิดชั้นสุดท้าย\n" +
                "• แท็บใบ: ใบ 1 แผ่นต่อช่อ / ลดความหนาแน่น / เพิ่มขนาดใบ\n• ตรงนี้: ลดความกลม + ด้านสูงสุด",
                MessageType.None);

            Header("โครง Spline");
            GUILayout.Label("กิ่งที่วาดเอง " + working.branches.Count + "   ·   กิ่งที่ระบบงอกให้ " + result.autoTwigCount);
            bool editing = sceneEditor.active;
            if (GUILayout.Button(editing ? "หยุดแก้ใน Scene View" : "แก้โครงใน Scene View", bigButton, GUILayout.Height(32f)))
            {
                if (editing) sceneEditor.End();
                else sceneEditor.Begin();
            }
            EditorGUILayout.HelpBox(
                "ใน Scene View\n• คลิกจุดสีขาวแล้วลากลูกศรเพื่อย้าย\n• โหมด 'เพิ่มจุด': คลิกเพื่อต่อปลายกิ่งที่เลือก\n" +
                "• โหมด 'แตกกิ่ง' หรือ Shift+คลิก: คลิกบนเส้นสีฟ้า\n• Ctrl+Scroll = ปรับรัศมี · Delete = ลบ",
                MessageType.None);

            GUILayout.Space(6f);
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced", true);
            if (showAdvanced)
            {
                EditorGUILayout.PropertyField(so.FindProperty("trunkRadius"), new GUIContent("รัศมีโคนลำต้น (m)"));
                Slider("tipRatio", "ความเรียวปลายกิ่ง", 0.02f, 1f, "เรียวมาก ↔ ไม่เรียว");
                Slider("childRadiusRatio", "ขนาดกิ่งลูกเทียบกิ่งแม่ (กฎดาวินชี)", 0.2f, 0.95f);
                Slider("trunkTwigStart", "กิ่งย่อยบนลำต้นเริ่มที่", 0f, 1f, "โคน ↔ ยอด");
                EditorGUILayout.PropertyField(so.FindProperty("seed"), new GUIContent("Seed"));
            }

            Header("เริ่มใหม่จากแม่แบบ");
            EditorGUILayout.BeginHorizontal();
            int c = 0;
            foreach (string kind in TreeTemplates.Kinds)
            {
                if (GUILayout.Button(TreeTemplates.DisplayName(kind), EditorStyles.miniButton))
                {
                    string k = kind;
                    Defer(() =>
                    {
                        if (ConfirmDiscard()) NewFromTemplate(k);
                    });
                }
                if (++c % 3 == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ---------- แท็บ: กิ่งย่อย

        void DrawTwigTab()
        {
            Header("กฎการงอกกิ่งย่อย");
            EditorGUILayout.HelpBox("ชั้นที่ 1 งอกจากกิ่งที่วาดเอง (รวมลำต้น) · ชั้นถัดไปงอกจากชั้นก่อนหน้า · ใบออกที่ชั้นสุดท้าย",
                MessageType.None);

            SerializedProperty levels = so.FindProperty("twigLevels");
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty e = levels.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                SerializedProperty en = e.FindPropertyRelative("enabled");
                EditorGUI.BeginChangeCheck();
                bool on = EditorGUILayout.ToggleLeft("ชั้นที่ " + (i + 1), en.boolValue, EditorStyles.boldLabel);
                if (EditorGUI.EndChangeCheck()) en.boolValue = on;
                GUILayout.FlexibleSpace();
                bool removed = i == levels.arraySize - 1 && i > 0 && GUILayout.Button("ลบชั้น", EditorStyles.miniButton);
                EditorGUILayout.EndHorizontal();
                if (removed)
                {
                    EditorGUILayout.EndVertical();
                    levels.DeleteArrayElementAtIndex(i);
                    break;
                }

                using (new EditorGUI.DisabledScope(!en.boolValue))
                {
                    Slider(e.FindPropertyRelative("perMeter"), "จำนวนต่อเมตร", 0f, 15f);

                    SerializedProperty st = e.FindPropertyRelative("startT");
                    SerializedProperty et = e.FindPropertyRelative("endT");
                    float a = st.floatValue, b = et.floatValue;
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("ช่วงที่งอก");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label((a * 100f).ToString("0") + "% – " + (b * 100f).ToString("0") + "%", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.MinMaxSlider(ref a, ref b, 0f, 1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        st.floatValue = a;
                        et.floatValue = b;
                    }

                    Slider(e.FindPropertyRelative("angle"), "มุมแตกกิ่ง (องศา)", 0f, 130f);
                    Slider(e.FindPropertyRelative("lengthRatio"), "ความยาว (เทียบกิ่งแม่)", 0.05f, 1.2f);
                    Slider(e.FindPropertyRelative("radiusRatio"), "ขนาด (เทียบกิ่งแม่)", 0.1f, 0.95f);
                    Slider(e.FindPropertyRelative("gravity"), "แรงโน้มถ่วง", -1f, 2f, "ชี้ขึ้น ↔ ห้อยลง");
                    Slider(e.FindPropertyRelative("curl"), "ปลายงอขึ้น", -0.5f, 0.8f);
                    Slider(e.FindPropertyRelative("taperLength"), "สั้นลงเมื่อใกล้ปลายกิ่งแม่", 0f, 1f);
                }
                EditorGUILayout.EndVertical();
            }

            if (levels.arraySize < 4 && GUILayout.Button("+ เพิ่มชั้นกิ่งย่อย"))
            {
                levels.arraySize++;
                SerializedProperty e = levels.GetArrayElementAtIndex(levels.arraySize - 1);
                e.FindPropertyRelative("enabled").boolValue = true;
                e.FindPropertyRelative("perMeter").floatValue = 4f;
            }

            if (working.suppressedTwigs.Count > 0)
                GUILayout.Label("กิ่งอัตโนมัติที่ถูกแปลงเป็น Spline แล้ว: " + working.suppressedTwigs.Count, mini);
        }

        // ---------- แท็บ: ใบ & วัสดุ

        void DrawMaterialTab()
        {
            Header("เปลือกลำต้น");
            GUILayout.Label("สุ่ม 1 แบบต่อต้น ใช้ทั้งต้น · ไม่ใส่ Material = ใช้วัสดุเริ่มต้นกับสีที่เลือก", mini);
            WeightedList(so.FindProperty("barkMaterials"), "เปลือก", working.barkColor);
            if (working.barkMaterials.Count == 0)
                EditorGUILayout.PropertyField(so.FindProperty("barkColor"), new GUIContent("สีเปลือก (ค่าเริ่มต้น)"));

            Header("รูปแบบใบ");
            EnumToolbar(so.FindProperty("leafMode"), new[] { "แผ่นช่อใบ (Card)", "ก้อนพุ่ม Low-poly" });
            Slider("leafDensity", "ความหนาแน่นใบ", 0f, 1f);
            Slider("leafSize", "ขนาดใบ (m)", 0.05f, 3f, "เล็ก ↔ ใหญ่");
            Slider("leafSizeJitter", "สุ่มขนาดใบ ±", 0f, 1f, "เท่ากันหมด ↔ ต่างกันมาก");
            if (working.leafMode == LeafMode.Card)
            {
                EditorGUILayout.PropertyField(so.FindProperty("leafAttach"), new GUIContent("ยึดโคนใบติดกิ่ง"));
                using (new EditorGUI.DisabledScope(!working.leafAttach))
                    Slider("leafPivotV", "ตำแหน่งโคนใบในรูป (จากขอบล่าง)", 0f, 0.5f, "ขอบล่าง ↔ กลางรูป");
                GUILayout.Label("รูปใบต้องมีก้านอยู่ตรงกลางขอบล่างของรูป ถ้าก้านในรูปอยู่สูงขึ้นมา ให้เลื่อน 'ตำแหน่งโคนใบในรูป' ให้ตรง", mini);
            }
            GUILayout.Label("ใบใหญ่ + ความหนาแน่นต่ำ = poly น้อยแต่พุ่มยังทึบ", mini);

            SerializedProperty ls = so.FindProperty("leafStartT");
            SerializedProperty le = so.FindProperty("leafEndT");
            float la = ls.floatValue, lb = le.floatValue;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("ช่วงที่ใบออก (ตามความยาวกิ่ง)");
            GUILayout.FlexibleSpace();
            GUILayout.Label((la * 100f).ToString("0") + "% – " + (lb * 100f).ToString("0") + "%", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.MinMaxSlider(ref la, ref lb, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                ls.floatValue = la;
                le.floatValue = lb;
            }

            if (working.leafMode == LeafMode.Card)
            {
                GUILayout.Space(4f);
                Header("แผ่นใบ (Card)");
                EditorGUILayout.IntSlider(so.FindProperty("leafPlanes"), 1, 3, new GUIContent("จำนวนแผ่นต่อช่อ"));
                using (new EditorGUI.DisabledScope(working.leafPlanes < 2))
                {
                    Slider("leafPlaneAngle", "มุมระหว่างแผ่น", 10f, 90f, "X ↔ +");
                    Slider("leafPlaneJitter", "สุ่มมุมแผ่นถัดไป ±°", 0f, 45f);
                }
                SubHeader("สุ่มหมุนทั้งช่อ (กันใบเรียงเป็นเส้นตรง)");
                Slider("leafYawJitter", "หันซ้าย-ขวา ±°", 0f, 180f);
                Slider("leafPitchJitter", "ก้ม-เงย ±°", 0f, 90f);
                Slider("leafRollJitter", "เอียง ±°", 0f, 90f);
            }

            Header("ใบไม้ (สุ่มทีละใบ)");
            GUILayout.Label("เปอร์เซ็นต์คำนวณจากน้ำหนัก รวมเป็น 100% อัตโนมัติ", mini);
            WeightedList(so.FindProperty("leafMaterials"), "ใบ", TreeMaterialUtil.DefaultLeafColor);

            SubHeader("การกระจายสี");
            EnumToolbar(so.FindProperty("leafDistribution"), new[] { "สุ่มกระจายทั่วต้น", "เป็นกลุ่มๆ ตามกิ่ง" });
            Slider("hueVariation", "สุ่มเฉดสีใบ", 0f, 1f, "น้อย ↔ มาก");
            GUILayout.Label("ค่าสุ่มเฉดสีเก็บไว้ใน UV2 ต้องใช้ shader ใบที่อ่านค่านี้ (ยังไม่มีในเวอร์ชันนี้)", mini);

            foreach (int k in TreeMaterialUtil.LeafShaderMismatch(working))
            {
                Shader s = TreeMaterialUtil.EffectiveLeafShader(working, k);
                EditorGUILayout.HelpBox("ใบ #" + (k + 1) + " ใช้ shader '" + (s != null ? s.name : "?") +
                                        "' ต่างจากใบแรก จึงรวม Atlas ไม่ได้ จะเพิ่ม 1 draw call", MessageType.Warning);
            }
        }

        void WeightedList(SerializedProperty list, string noun, Color defaultColor)
        {
            float total = 0f;
            for (int i = 0; i < list.arraySize; i++)
                total += Mathf.Max(0f, list.GetArrayElementAtIndex(i).FindPropertyRelative("weight").floatValue);

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                SerializedProperty mat = e.FindPropertyRelative("material");
                SerializedProperty colr = e.FindPropertyRelative("color");
                SerializedProperty w = e.FindPropertyRelative("weight");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(mat, GUIContent.none);
                if (mat.objectReferenceValue == null) EditorGUILayout.PropertyField(colr, GUIContent.none, GUILayout.Width(54f));
                bool remove = GUILayout.Button("×", GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();
                if (remove)
                {
                    EditorGUILayout.EndVertical();
                    list.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Slider(w, 0f, 100f, GUIContent.none);
                float pct = total > 0f ? Mathf.Max(0f, w.floatValue) / total * 100f : 100f / list.arraySize;
                GUILayout.Label(pct.ToString("0") + "%", GUILayout.Width(40f));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ เพิ่ม" + noun))
            {
                list.arraySize++;
                SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
                e.FindPropertyRelative("material").objectReferenceValue = null;
                e.FindPropertyRelative("color").colorValue = defaultColor;
                e.FindPropertyRelative("weight").floatValue = 50f;
            }
        }

        // ---------- แท็บ: Gallery & สุ่ม

        void DrawGalleryTab()
        {
            Header("บันทึกเป็น Gallery");
            EditorGUILayout.PropertyField(so.FindProperty("displayName"), new GUIContent("ชื่อ"));
            EditorGUILayout.PropertyField(so.FindProperty("category"), new GUIContent("หมวดใน Gallery"));
            if (source != null && GUILayout.Button("บันทึกทับ \"" + source.displayName + "\"")) Defer(SaveOverwrite);
            if (GUILayout.Button("บันทึกเป็นอันใหม่ลง Gallery", bigButton, GUILayout.Height(28f))) Defer(SaveAsNew);
            GUILayout.Label("เก็บทั้งเส้นที่วาดเอง ค่า slider วัสดุ และค่าการสุ่ม · thumbnail ถ่ายให้อัตโนมัติ", mini);

            Header("สุ่มจากต้นนี้");
            GUILayout.Label("ค่าทางขวา = ต่างจากต้นแม่แค่ไหน (0 = เหมือนเดิม)", mini);
            SerializedProperty v = so.FindProperty("variation");
            VarRow(v, "shapeOn", "shape", "เส้นที่วาดเอง (ขยับจุด)", 0f, 100f, "{0:0}%");
            VarRow(v, "twigsOn", "twigs", "กิ่งย่อย (100 = งอกใหม่หมด)", 0f, 100f, "{0:0}%");
            VarRow(v, "heightOn", "height", "ความสูง / ขนาด", 0f, 50f, "±{0:0}%");
            VarRow(v, "leanOn", "lean", "เอียงทั้งต้น", 0f, 20f, "±{0:0}°");
            VarRow(v, "leafOn", "leaf", "สัดส่วนสีใบ", 0f, 100f, "{0:0}%");
            VarRow(v, "barkOn", "bark", "เปลี่ยนเปลือก (โอกาส)", 0f, 100f, "{0:0}%");
            EditorGUILayout.PropertyField(v.FindPropertyRelative("keepSilhouette"), new GUIContent("คงรูปทรงพุ่มเดิมไว้"));
            EditorGUILayout.PropertyField(v.FindPropertyRelative("linkVariants"), new GUIContent("ผูกกับต้นแม่ (Variant)"));
            int locked = working.branches.Count(b => b.locked);
            GUILayout.Label("ล็อกไว้ " + locked + " กิ่ง (ไม่ถูกสุ่ม) · ล็อกเพิ่มได้ใน Scene View", mini);

            if (GUILayout.Button("สุ่มชุดใหม่ (" + VariantCount + " ต้น)", bigButton, GUILayout.Height(30f))) Defer(GenerateVariants);

            if (variants.Count > 0)
            {
                GUILayout.Space(4f);
                for (int i = 0; i < variants.Count; i++)
                {
                    if (i % 3 == 0) EditorGUILayout.BeginHorizontal();
                    Rect r = GUILayoutUtility.GetRect(104f, 120f, GUILayout.Width(104f), GUILayout.Height(120f));
                    if (GUI.Button(r, GUIContent.none))
                    {
                        int idx = i;
                        Defer(() => SelectVariant(idx));
                    }
                    if (Event.current.type == EventType.Repaint)
                    {
                        if (i < variantThumbs.Count && variantThumbs[i] != null)
                            GUI.DrawTexture(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), variantThumbs[i], ScaleMode.ScaleToFit);
                        if (i == variantSel) Outline(r, new Color(0.3f, 0.55f, 0.82f), 2f);
                    }
                    GUI.Label(new Rect(r.x + 5f, r.y + 3f, 40f, 16f), "#" + (i + 1), EditorStyles.whiteMiniLabel);
                    if (i % 3 == 2 || i == variants.Count - 1) EditorGUILayout.EndHorizontal();
                }

                if (variantSel >= 0 && variantSel < variants.Count)
                {
                    GUILayout.Label("เลือก Variant #" + (variantSel + 1) + " · Seed " + variants[variantSel].variantSeed, EditorStyles.boldLabel);
                    if (GUILayout.Button("บันทึกต้นนี้ลง Gallery")) Defer(SaveSelectedVariant);
                    if (GUILayout.Button("ใช้ต้นนี้เป็นต้นแม่ใหม่")) Defer(UseVariantAsParent);
                }
            }

            int linked = LinkedCount();
            if (linked > 0)
            {
                GUILayout.Space(6f);
                if (GUILayout.Button("อัปเดต Variant ที่ผูกไว้ (" + linked + " ต้น) ตามต้นแม่ที่บันทึกแล้ว")) Defer(UpdateLinked);
            }
        }

        void VarRow(SerializedProperty v, string onName, string valName, string label, float min, float max, string fmt)
        {
            SerializedProperty on = v.FindPropertyRelative(onName);
            SerializedProperty val = v.FindPropertyRelative(valName);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool b = EditorGUILayout.ToggleLeft(label, on.boolValue);
            if (EditorGUI.EndChangeCheck()) on.boolValue = b;
            GUILayout.FlexibleSpace();
            GUILayout.Label(string.Format(fmt, val.floatValue), EditorStyles.miniLabel, GUILayout.Width(44f));
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(!on.boolValue))
                EditorGUILayout.Slider(val, min, max, GUIContent.none);
        }

        // ---------- แท็บ: Bake & Export

        void DrawBakeTab()
        {
            Header("บันทึกไปที่");
            SerializedProperty folder = so.FindProperty("exportFolder");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(folder, GUIContent.none);
            if (GUILayout.Button("เลือก…", GUILayout.Width(60f)))
            {
                string abs = EditorUtility.OpenFolderPanel("เลือกโฟลเดอร์ปลายทาง", Application.dataPath, "");
                if (!string.IsNullOrEmpty(abs))
                {
                    string rel = TreeBaker.ToAssetPath(abs);
                    if (rel == null) EditorUtility.DisplayDialog("Tree Generator", "ต้องเลือกโฟลเดอร์ที่อยู่ใน Assets ของโปรเจกต์", "OK");
                    else folder.stringValue = rel;
                }
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
            string n = TreeBaker.SafeName(working.displayName);
            GUILayout.Label("จะได้: " + working.exportFolder + "/" + n + "/" + n + ".prefab + Mesh + Material + LeafAtlas.png", mini);
            if (!TreeBaker.IsAssetFolderPath(working.exportFolder))
                EditorGUILayout.HelpBox("โฟลเดอร์ต้องขึ้นต้นด้วย Assets", MessageType.Error);

            Header("ความเนียนรอยต่อกิ่ง");
            EnumToolbar(so.FindProperty("junction"), new[] { "เร็ว", "สวย" });
            GUILayout.Label("สวย = โคนกิ่งบานมากขึ้น + เกลี่ยแสงตรงรอยต่อ", mini);

            Header("ใส่ข้อมูลเพิ่ม");
            EditorGUILayout.PropertyField(so.FindProperty("windData"), new GUIContent("ข้อมูลลม (vertex color)"));
            EditorGUILayout.PropertyField(so.FindProperty("trunkCollider"), new GUIContent("Collider ลำต้น (Capsule)"));
            EditorGUILayout.PropertyField(so.FindProperty("lodShadowsOff"), new GUIContent("ปิดเงาที่ LOD2"));
            SerializedProperty atlas = so.FindProperty("atlasSize");
            EditorGUI.BeginChangeCheck();
            int atlasValue = EditorGUILayout.IntPopup("ขนาด Atlas ใบ", atlas.intValue,
                new[] { "512", "1024", "2048", "4096" }, new[] { 512, 1024, 2048, 4096 });
            if (EditorGUI.EndChangeCheck()) atlas.intValue = atlasValue;
            EditorGUILayout.IntSlider(so.FindProperty("atlasPadding"), 0, 32, new GUIContent("ระยะเว้นขอบ Atlas (px)"));

            Header("สรุปก่อน Bake");
            GUILayout.Label("LOD0: " + result.triangleCount.ToString("N0") + " tris · LOD1/LOD2 ลดกิ่งย่อยและใบลงอัตโนมัติ");
            GUILayout.Label("Mesh รวม 1 ชิ้นต่อ LOD · Draw calls ≈ " + EstimateDrawCalls() + " ต่อต้น");

            using (new EditorGUI.DisabledScope(!TreeBaker.IsAssetFolderPath(working.exportFolder)))
            {
                GUILayout.Space(6f);
                if (GUILayout.Button("Bake เป็น Prefab", bigButton, GUILayout.Height(40f))) Defer(BakeOne);

                Header("สร้างหลายต้นทีเดียว");
                EditorGUILayout.IntSlider(so.FindProperty("variation").FindPropertyRelative("batchCount"), 1, 30, new GUIContent("จำนวนต้น"));
                GUILayout.Label("ใช้ค่าจากแผง 'สุ่มจากต้นนี้' ในแท็บ Gallery & สุ่ม", mini);
                if (GUILayout.Button("Bake หลายต้น")) Defer(BakeMany);
            }
        }
    }
}
