using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yantra.TerrainTools
{
    public class TerrainToolsWindow : EditorWindow
    {
        enum Tab { Place, Grass, Check, Scene, Layer }

        static readonly GUIContent[] TabLabels =
        {
            new GUIContent(" วางของ", "แปรงวางของ / ทำให้ของติดพื้น / ต้นไม้ไม่ลอย"),
            new GUIContent(" หญ้า", "ลงหญ้าอัตโนมัติตามสีพื้น"),
            new GUIContent(" ตรวจงาน", "ตรวจก่อน push / ซ่อนต้นไม้ / หุ่นเทียบสเกล"),
            new GUIContent(" ฉาก", "ดูความชัน / วาดกำแพงล่องหน"),
            new GUIContent(" สีพื้น", "สร้าง Terrain Layer"),
        };

        [SerializeField] Tab _tab;
        [SerializeField] Vector2 _scroll;

        // วางของ
        [SerializeField] ScatterPreset _preset;
        [SerializeField] GroundSnap.AlignMode _align = GroundSnap.AlignMode.Upright;
        [SerializeField] float _follow = 1f;
        [SerializeField] float _snapSink = 0.05f;
        [SerializeField] bool _snapSlopeSink = true;
        [SerializeField] float _snapRadius = 0.4f;
        [SerializeField] bool _snapUseBottom = true;
        [SerializeField] bool _snapTerrainOnly = true;
        [SerializeField] float _treeSink = 0.05f;
        [SerializeField] bool _treeSlopeSink = true;
        [SerializeField] float _treeRadius = 0.4f;
        string _treeReport;

        // หญ้า
        [SerializeField] int _grassProto;
        [SerializeField] int _grassLayerMask = 1;
        [SerializeField] float _grassMinWeight = 0.5f;
        [SerializeField] float _grassMaxSlope = 30f;
        [SerializeField] float _grassStrength = 0.6f;
        [SerializeField] float _grassNoiseScale = 0.08f;
        [SerializeField] float _grassNoise = 0.5f;
        [SerializeField] bool _grassReplace = true;
        string _grassReport;

        // ตรวจงาน
        List<TerrainChecker.Issue> _issues;
        [SerializeField] float _dummyHeight = -1f;

        // สีพื้น
        [SerializeField] Texture2D _diffuse, _normal, _mask;
        [SerializeField] string _layerName = "";
        [SerializeField] Vector2 _tileSize = new Vector2(4f, 4f);
        [SerializeField] bool _addToSelected = true;

        static TerrainToolsWindow _open;

        static readonly GUIContent[] AlignOptions =
        {
            new GUIContent("คงมุมเดิม"),
            new GUIContent("ตั้งตรง (ต้นไม้ เสา)"),
            new GUIContent("เอียงตามพื้น (หิน)"),
        };

        [MenuItem("Tools/Yantra/Terrain Tools")]
        public static void Open()
        {
            var w = GetWindow<TerrainToolsWindow>();
            w.titleContent = new GUIContent("Terrain Tools");
            w.minSize = new Vector2(360f, 420f);
            w.Show();
        }

        public static void RepaintOpen()
        {
            if (_open != null) _open.Repaint();
        }

        void OnEnable()
        {
            _open = this;
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += Repaint;
            ScatterBrush.Preset = _preset;
            CharacterController cc = TTTerrain.FindPlayer();
            if (cc != null) SlopeView.Limit = cc.slopeLimit;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= Repaint;
            ScatterBrush.Active = false;
            WallDrawer.Active = false;
            SlopeView.SetEnabled(false);
            if (_open == this) _open = null;
        }

        void OnSceneGUI(SceneView sv)
        {
            if (_tab == Tab.Place) ScatterBrush.OnSceneGUI(sv);
            if (_tab == Tab.Scene) WallDrawer.OnSceneGUI(sv);
        }

        void OnGUI()
        {
            GUILayout.Label("Yantra Terrain Tools", TTGui.Title);

            EditorGUI.BeginChangeCheck();
            var tab = (Tab)GUILayout.Toolbar((int)_tab, TabLabels, GUILayout.Height(28f));
            if (EditorGUI.EndChangeCheck())
            {
                _tab = tab;
                ScatterBrush.Active = false;
                WallDrawer.Active = false;
                SceneView.RepaintAll();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(4f);
            switch (_tab)
            {
                case Tab.Place: DrawPlaceTab(); break;
                case Tab.Grass: DrawGrassTab(); break;
                case Tab.Check: DrawCheckTab(); break;
                case Tab.Scene: DrawSceneTab(); break;
                case Tab.Layer: DrawLayerTab(); break;
            }
            GUILayout.Space(10f);
            EditorGUILayout.EndScrollView();
        }

        // ================= วางของ =================

        void DrawPlaceTab()
        {
            TTGui.BeginSection("แปรงวางของ", "ปัดวางหิน พุ่มไม้ รั้ว หรือต้นไม้แบบ prefab ลงบนพื้น สุ่มขนาดและมุมให้เอง ของที่วางจะเก็บไว้ใต้ _Scatter ใน Hierarchy");

            EditorGUI.BeginChangeCheck();
            _preset = (ScatterPreset)EditorGUILayout.ObjectField(TTGui.C("ชุดแปรง", "ไฟล์ที่เก็บรายการของและค่าแปรง แชร์กันทั้งทีมได้"), _preset, typeof(ScatterPreset), false);
            if (EditorGUI.EndChangeCheck()) ScatterBrush.Preset = _preset;

            if (_preset == null)
            {
                TTGui.Note("ยังไม่มีชุดแปรง กดปุ่มด้านล่างเพื่อสร้าง");
                if (TTGui.BigButton("สร้างชุดแปรงใหม่", null, true)) CreatePreset();
                TTGui.EndSection();
                DrawSnapSection();
                DrawTreeSection();
                return;
            }

            DrawPresetItems();
            GUILayout.Space(6f);
            DrawPresetSettings();
            GUILayout.Space(8f);

            if (ScatterBrush.Active)
            {
                if (TTGui.DangerButton("ปิดแปรง (Esc)")) ScatterBrush.Active = false;
                TTGui.Note("<b>คลิก/ลาก</b> = วาง   <b>Shift + ลาก</b> = ลบ   <b>Ctrl + ลูกกลิ้ง</b> = ขนาดแปรง   <b>Alt</b> = หมุนกล้องตามปกติ");
            }
            else if (TTGui.BigButton("เปิดแปรง", "แล้วไปปัดในหน้าต่าง Scene", true))
            {
                ScatterBrush.Preset = _preset;
                ScatterBrush.Active = true;
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Focus();
            }
            TTGui.EndSection();

            DrawSnapSection();
            DrawTreeSection();
        }

        void CreatePreset()
        {
            string path = EditorUtility.SaveFilePanelInProject("สร้างชุดแปรง", "ชุดแปรง_ใหม่", "asset", "เลือกที่เก็บไฟล์ชุดแปรง");
            if (string.IsNullOrEmpty(path)) return;
            var p = CreateInstance<ScatterPreset>();
            AssetDatabase.CreateAsset(p, path);
            AssetDatabase.SaveAssets();
            _preset = p;
            ScatterBrush.Preset = p;
        }

        void DrawPresetItems()
        {
            GUILayout.Label("ของที่จะวาง", EditorStyles.boldLabel);
            for (int i = 0; i < _preset.items.Count; i++)
            {
                ScatterPreset.Item item = _preset.items[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                bool on = EditorGUILayout.Toggle(item.enabled, GUILayout.Width(18f));
                var prefab = (GameObject)EditorGUILayout.ObjectField(item.prefab, typeof(GameObject), false);
                GUILayout.Label(TTGui.C("โอกาส", "ยิ่งมาก ยิ่งออกบ่อย"), GUILayout.Width(38f));
                float weight = GUILayout.HorizontalSlider(item.weight, 0.05f, 1f, GUILayout.Width(70f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_preset, "แก้ชุดแปรง");
                    item.enabled = on;
                    item.prefab = prefab;
                    item.weight = weight;
                    EditorUtility.SetDirty(_preset);
                }
                if (GUILayout.Button("ลบ", GUILayout.Width(36f)))
                {
                    Undo.RecordObject(_preset, "ลบของออกจากชุดแปรง");
                    _preset.items.RemoveAt(i);
                    EditorUtility.SetDirty(_preset);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            Rect drop = GUILayoutUtility.GetRect(0f, 40f, GUILayout.ExpandWidth(true));
            GUI.Box(drop, "ลาก prefab จาก Project มาวางตรงนี้เพื่อเพิ่ม", EditorStyles.helpBox);
            HandleDrop(drop, objects =>
            {
                Undo.RecordObject(_preset, "เพิ่มของในชุดแปรง");
                foreach (Object o in objects)
                {
                    var go = o as GameObject;
                    if (go != null && PrefabUtility.IsPartOfPrefabAsset(go))
                        _preset.items.Add(new ScatterPreset.Item { prefab = go });
                }
                EditorUtility.SetDirty(_preset);
            });
        }

        void DrawPresetSettings()
        {
            var so = new SerializedObject(_preset);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("radius"), TTGui.C("ขนาดแปรง (m)"));
            EditorGUILayout.PropertyField(so.FindProperty("perStamp"), TTGui.C("จำนวนต่อครั้ง", "ปัดหนึ่งจังหวะวางกี่ชิ้น"));
            EditorGUILayout.PropertyField(so.FindProperty("spacing"), TTGui.C("ระยะต่อจังหวะ (m)", "ลากเมาส์ไกลเท่านี้ถึงจะวางชุดต่อไป"));
            EditorGUILayout.PropertyField(so.FindProperty("minDistance"), TTGui.C("ห่างกันอย่างน้อย (m)", "กันของซ้อนกัน 0 = ไม่เช็ค"));

            SerializedProperty scale = so.FindProperty("scale");
            Vector2 s = scale.vector2Value;
            float min = s.x, max = s.y;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.MinMaxSlider(TTGui.C("สุ่มขนาด", "ขนาดเล็กสุด–ใหญ่สุด เทียบกับ prefab"), ref min, ref max, 0.3f, 2.5f);
            GUILayout.Label(min.ToString("0.00") + "–" + max.ToString("0.00"), GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
            scale.vector2Value = new Vector2(min, max);

            EditorGUILayout.PropertyField(so.FindProperty("randomYaw"), TTGui.C("สุ่มหมุนรอบตัว"));
            EditorGUILayout.PropertyField(so.FindProperty("alignToGround"), TTGui.C("เอียงตามพื้น", "0 = ตั้งตรง (ต้นไม้) / 1 = เอียงตามพื้นเต็มที่ (หิน)"));
            EditorGUILayout.PropertyField(so.FindProperty("maxSlope"), TTGui.C("ห้ามวางบนพื้นชันเกิน (°)"));
            EditorGUILayout.PropertyField(so.FindProperty("sink"), TTGui.C("ฝังลงดิน (m)", "กดลงดินนิดหน่อย จะได้ไม่เห็นก้นลอย"));
            EditorGUILayout.PropertyField(so.FindProperty("slopeSink"), TTGui.C("ฝังเพิ่มตามความชัน", "บนเนินจะฝังลึกขึ้นเอง ไม่ให้เห็นโคนลอยฝั่งพื้นต่ำ"));
            if (so.FindProperty("slopeSink").boolValue)
                EditorGUILayout.PropertyField(so.FindProperty("baseRadius"), TTGui.C("ความกว้างโคน (m)"));
            EditorGUILayout.PropertyField(so.FindProperty("terrainOnly"), TTGui.C("วางบน Terrain เท่านั้น", "ไม่ติ๊ก = วางบนโมเดลอื่นที่มี Collider ได้ด้วย เช่น หิน สะพาน"));
            so.ApplyModifiedProperties();
        }

        void DrawSnapSection()
        {
            int count = Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable).Length;
            TTGui.BeginSection("ทำให้ของติดพื้น", "เลือกของในฉาก (หิน รั้ว ต้นไม้ที่ลากวางเอง) แล้วกดปุ่ม ของจะตกลงไปติดพื้นพอดี");

            _align = (GroundSnap.AlignMode)EditorGUILayout.Popup(TTGui.C("การหมุน"), (int)_align, AlignOptions);
            if (_align == GroundSnap.AlignMode.FollowGround)
                _follow = EditorGUILayout.Slider(TTGui.C("เอียงแค่ไหน"), _follow, 0f, 1f);
            _snapUseBottom = EditorGUILayout.Toggle(TTGui.C("ใช้ก้นโมเดล", "ติ๊ก = เอาส่วนล่างสุดของโมเดลแตะพื้น (ใช้ได้แม้จุดหมุนอยู่กลางโมเดล)"), _snapUseBottom);
            _snapSink = EditorGUILayout.FloatField(TTGui.C("ฝังลงดิน (m)"), _snapSink);
            _snapSlopeSink = EditorGUILayout.Toggle(TTGui.C("ฝังเพิ่มตามความชัน"), _snapSlopeSink);
            if (_snapSlopeSink) _snapRadius = EditorGUILayout.FloatField(TTGui.C("ความกว้างโคน (m)"), _snapRadius);
            _snapTerrainOnly = EditorGUILayout.Toggle(TTGui.C("ติดเฉพาะ Terrain", "ไม่ติ๊ก = ตกลงบนโมเดลอื่นที่มี Collider ได้ด้วย"), _snapTerrainOnly);

            GUI.enabled = count > 0;
            if (TTGui.BigButton(count > 0 ? "ติดพื้นของที่เลือก (" + count + " ชิ้น)" : "เลือกของในฉากก่อน", null, true))
            {
                int n = GroundSnap.SnapSelection(_align, _follow, _snapSink, _snapSlopeSink, _snapRadius, _snapUseBottom, _snapTerrainOnly);
                ShowNotification(new GUIContent("ติดพื้นแล้ว " + n + " ชิ้น"));
            }
            GUI.enabled = true;
            TTGui.EndSection();
        }

        void DrawTreeSection()
        {
            TTGui.BeginSection("ต้นไม้บน Terrain ไม่ลอย", "สำหรับต้นไม้ที่ปลูกด้วย Paint Trees ใช้หลังปั้นพื้นใหม่ หรือเจอต้นไม้ลอยบนเนิน");
            TTGui.Note(TTTerrain.TargetLabel());
            _treeSink = EditorGUILayout.FloatField(TTGui.C("ฝังลงดิน (m)"), _treeSink);
            _treeSlopeSink = EditorGUILayout.Toggle(TTGui.C("ฝังเพิ่มตามความชัน", "บนเนินจะฝังลึกขึ้นเอง ไม่ให้เห็นรากลอยฝั่งพื้นต่ำ"), _treeSlopeSink);
            if (_treeSlopeSink) _treeRadius = EditorGUILayout.FloatField(TTGui.C("ความกว้างโคน (m)"), _treeRadius);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("นับต้นไม้ลอย", GUILayout.Height(30f)))
            {
                int floating = 0;
                foreach (Terrain t in TTTerrain.SelectedOrAll()) floating += GroundSnap.CountFloating(t, 0.05f);
                _treeReport = floating == 0 ? "ไม่มีต้นไม้ลอย" : "เจอต้นไม้ลอย " + floating + " ต้น";
            }
            if (TTGui.BigButton("ทำให้ต้นไม้ทุกต้นติดพื้น", null, true))
            {
                int n = GroundSnap.FixTerrainTrees(TTTerrain.SelectedOrAll(), _treeSink, _treeSlopeSink, _treeRadius);
                _treeReport = "ปรับแล้ว " + n + " ต้น (กด Ctrl+Z ย้อนได้)";
            }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_treeReport)) EditorGUILayout.HelpBox(_treeReport, MessageType.Info);
            TTGui.EndSection();
        }

        // ================= หญ้า =================

        void DrawGrassTab()
        {
            Terrain[] targets = TTTerrain.SelectedOrAll();
            TTGui.BeginSection("ลงหญ้าอัตโนมัติ", "ลงหญ้าเฉพาะตรงที่ทาสีหญ้าไว้ เว้นที่ชันและทางเดิน ไม่ต้องเพ้นหญ้าเองทั้งแมพ");
            TTGui.Note(TTTerrain.TargetLabel());

            if (targets.Length == 0 || targets[0].terrainData == null)
            {
                EditorGUILayout.HelpBox("ไม่เจอ Terrain ในฉาก", MessageType.Warning);
                TTGui.EndSection();
                return;
            }

            TerrainData first = targets[0].terrainData;
            DetailPrototype[] protos = first.detailPrototypes;
            if (protos.Length == 0)
            {
                EditorGUILayout.HelpBox("Terrain ยังไม่มีหญ้าให้เลือก\nไปที่ Paint Details › Edit Details › Add Detail Mesh ก่อน", MessageType.Warning);
                TTGui.EndSection();
                return;
            }

            var protoNames = new string[protos.Length];
            var protoOptions = new GUIContent[protos.Length];
            for (int i = 0; i < protos.Length; i++)
            {
                protoNames[i] = TTTerrain.PrototypeName(protos[i]);
                protoOptions[i] = new GUIContent(protoNames[i]);
            }
            _grassProto = Mathf.Clamp(_grassProto, 0, protos.Length - 1);
            _grassProto = EditorGUILayout.Popup(TTGui.C("หญ้าตัวไหน"), _grassProto, protoOptions);
            if (!protos[_grassProto].usePrototypeMesh)
                EditorGUILayout.HelpBox("ตัวนี้เป็นหญ้าแบบรูปแบน HDRP ไม่แสดง ควรเปลี่ยนเป็น Detail Mesh", MessageType.Warning);

            TerrainLayer[] layers = first.terrainLayers;
            var layerNames = new string[layers.Length];
            for (int i = 0; i < layers.Length; i++) layerNames[i] = layers[i] != null ? layers[i].name : "(ว่าง)";
            _grassLayerMask = EditorGUILayout.MaskField(TTGui.C("ขึ้นบนสีพื้น", "เลือกได้หลายสี เช่น หญ้า กับ ดินปนหญ้า"), _grassLayerMask, layerNames);

            _grassMinWeight = EditorGUILayout.Slider(TTGui.C("สีพื้นต้องเข้มอย่างน้อย", "0.5 = ตรงที่สีนั้นทาไว้ครึ่งหนึ่งขึ้นไป"), _grassMinWeight, 0.05f, 1f);
            _grassMaxSlope = EditorGUILayout.Slider(TTGui.C("ไม่ขึ้นบนพื้นชันเกิน (°)"), _grassMaxSlope, 0f, 90f);
            _grassStrength = EditorGUILayout.Slider(TTGui.C("ความแน่น"), _grassStrength, 0.05f, 1f);
            _grassNoise = EditorGUILayout.Slider(TTGui.C("ความเป็นหย่อมๆ", "0 = เรียบเท่ากันหมด / 1 = เป็นกอ เป็นหย่อม ดูธรรมชาติ"), _grassNoise, 0f, 1f);
            if (_grassNoise > 0f) _grassNoiseScale = EditorGUILayout.Slider(TTGui.C("ขนาดหย่อม", "ค่าน้อย = หย่อมใหญ่"), _grassNoiseScale, 0.01f, 0.5f);
            _grassReplace = EditorGUILayout.Toggle(TTGui.C("ล้างหญ้าตัวนี้ของเดิมก่อน", "ไม่ติ๊ก = เติมเพิ่มจากที่เพ้นไว้"), _grassReplace);

            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (TTGui.BigButton("ลงหญ้า", null, true))
            {
                var settings = new AutoGrass.Settings
                {
                    prototype = protos[_grassProto],
                    layers = MaskToBools(_grassLayerMask, layers.Length),
                    minWeight = _grassMinWeight,
                    maxSlope = _grassMaxSlope,
                    strength = _grassStrength,
                    noiseScale = _grassNoiseScale,
                    noiseAmount = _grassNoise,
                    replace = _grassReplace
                };
                int n = AutoGrass.Run(targets, settings);
                _grassReport = "ลงหญ้าแล้ว " + n + " Terrain (กด Ctrl+Z ย้อนได้)";
            }
            if (GUILayout.Button("ล้างหญ้าตัวนี้", GUILayout.Height(34f), GUILayout.Width(110f))
                && EditorUtility.DisplayDialog("ล้างหญ้า", "ลบหญ้า “" + protoNames[_grassProto] + "” ออกทั้งหมด?", "ลบ", "ยกเลิก"))
            {
                int n = AutoGrass.Clear(targets, protos[_grassProto]);
                _grassReport = "ล้างแล้ว " + n + " Terrain";
            }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_grassReport)) EditorGUILayout.HelpBox(_grassReport, MessageType.Info);
            TTGui.EndSection();
        }

        static bool[] MaskToBools(int mask, int count)
        {
            var result = new bool[count];
            for (int i = 0; i < count; i++) result[i] = mask == -1 || (mask & (1 << i)) != 0;
            return result;
        }

        // ================= ตรวจงาน =================

        void DrawCheckTab()
        {
            TTGui.BeginSection("ตรวจก่อน push", "เช็คตามเช็คลิสต์ใน Terrain Bible ให้อัตโนมัติ ข้อไหนแก้ให้ได้จะมีปุ่มให้กด");
            if (TTGui.BigButton(_issues == null ? "ตรวจฉากนี้" : "ตรวจใหม่", null, true)) _issues = TerrainChecker.Run();

            if (_issues != null)
            {
                int errors = 0, warnings = 0;
                foreach (TerrainChecker.Issue i in _issues)
                {
                    if (i.level == TerrainChecker.Level.Error) errors++;
                    else if (i.level == TerrainChecker.Level.Warning) warnings++;
                }

                GUILayout.Space(6f);
                if (errors == 0 && warnings == 0)
                    EditorGUILayout.HelpBox("ผ่านหมด push ได้เลย อย่าลืมบอกในกลุ่มว่าเสร็จ", MessageType.Info);
                else
                    EditorGUILayout.HelpBox("ต้องแก้ " + errors + " ข้อ  ควรแก้ " + warnings + " ข้อ", errors > 0 ? MessageType.Error : MessageType.Warning);

                bool fixable = false;
                foreach (TerrainChecker.Issue issue in _issues)
                {
                    if (issue.fix != null && issue.level != TerrainChecker.Level.Info) fixable = true;
                    DrawIssue(issue);
                }

                if (fixable && GUILayout.Button("แก้ทุกข้อที่แก้ให้ได้", GUILayout.Height(28f)))
                {
                    foreach (TerrainChecker.Issue issue in _issues)
                        if (issue.fix != null && issue.level != TerrainChecker.Level.Info) issue.fix();
                    _issues = TerrainChecker.Run();
                }
            }
            TTGui.EndSection();

            TTGui.BeginSection("ซ่อนต้นไม้ชั่วคราว", "ต้นไม้บังตอนจัดของ ซ่อนได้โดยไม่ต้องลบ ลืมเปิดคืนก็ไม่เป็นไร ตอนเซฟระบบเปิดคืนในไฟล์ให้เอง");
            bool hidden = HideTrees.AnyHidden;
            if (hidden ? TTGui.DangerButton("เปิดต้นไม้คืน") : TTGui.BigButton("ซ่อนต้นไม้"))
                HideTrees.Toggle();
            TTGui.Note("ปุ่มลัด: <b>Shift + Alt + T</b> (เปลี่ยนได้ที่ Edit › Shortcuts › Yantra)");
            TTGui.EndSection();

            TTGui.BeginSection("หุ่นเทียบสเกล", "วางหุ่นสูงเท่าผู้เล่นไว้เทียบขนาดต้นไม้ บ้าน เนิน หุ่นนี้ไม่ติดไปในเกม และไม่ขวางตอนกด Play");
            if (_dummyHeight <= 0f) _dummyHeight = ScaleDummy.PlayerHeight();
            _dummyHeight = EditorGUILayout.FloatField(TTGui.C("ความสูง (m)", "อ่านจาก Character Controller ของผู้เล่นให้เอง"), _dummyHeight);
            EditorGUILayout.BeginHorizontal();
            if (TTGui.BigButton("วางหุ่นกลางจอ Scene", null, true)) ScaleDummy.Create(_dummyHeight);
            if (GUILayout.Button("ลบหุ่นทั้งหมด", GUILayout.Height(34f), GUILayout.Width(110f))) ScaleDummy.DeleteAll();
            EditorGUILayout.EndHorizontal();
            TTGui.EndSection();
        }

        void DrawIssue(TerrainChecker.Issue issue)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(TerrainChecker.Icon(issue.level), GUILayout.Width(20f), GUILayout.Height(20f));
            GUILayout.Label(issue.text, EditorStyles.wordWrappedLabel);
            if (issue.target != null && GUILayout.Button("เลือก", GUILayout.Width(44f)))
            {
                Selection.activeObject = issue.target;
                EditorGUIUtility.PingObject(issue.target);
            }
            if (issue.fix != null && GUILayout.Button(issue.fixLabel, GUILayout.Width(84f)))
            {
                issue.fix();
                _issues = TerrainChecker.Run();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        // ================= ฉาก =================

        void DrawSceneTab()
        {
            TTGui.BeginSection("ดูความชัน", "ระบายสีบนพื้นตรงที่ชันเกินกว่าผู้เล่นจะเดินขึ้นได้ ปั้นพื้นแล้วปล่อยเมาส์ สีจะอัปเดตเอง");
            EditorGUI.BeginChangeCheck();
            SlopeView.Limit = EditorGUILayout.Slider(TTGui.C("ชันเกินกี่องศาถึงเดินไม่ได้", "อ่านจาก Slope Limit ของผู้เล่นให้เอง"), SlopeView.Limit, 10f, 80f);
            SlopeView.SeeThrough = EditorGUILayout.Toggle(TTGui.C("มองทะลุ", "ถ้าเปิดแล้วไม่เห็นสี ให้ติ๊กอันนี้"), SlopeView.SeeThrough);
            if (EditorGUI.EndChangeCheck() && SlopeView.Enabled) SlopeView.Rebuild();

            if (SlopeView.Enabled)
            {
                EditorGUILayout.BeginHorizontal();
                if (TTGui.DangerButton("ปิดการดูความชัน")) SlopeView.SetEnabled(false);
                if (GUILayout.Button("อัปเดต", GUILayout.Height(34f), GUILayout.Width(80f))) SlopeView.Rebuild();
                EditorGUILayout.EndHorizontal();
                TTGui.Note("จุดที่ชันเกิน: " + SlopeView.SteepCells + " ช่อง (ช่องละประมาณ 1.5 m)");
            }
            else if (TTGui.BigButton("เปิดการดูความชัน", null, true))
            {
                SlopeView.SetEnabled(true);
            }
            TTGui.EndSection();

            TTGui.BeginSection("วาดกำแพงล่องหน", "คลิกจุดเรียงตามขอบแมพในหน้าต่าง Scene แล้วกดสร้าง ระบบจะทำกำแพงที่มองไม่เห็นให้ตลอดแนว");
            WallDrawer.Height = EditorGUILayout.FloatField(TTGui.C("ความสูงเหนือพื้น (m)"), WallDrawer.Height);
            WallDrawer.Thickness = EditorGUILayout.FloatField(TTGui.C("ความหนา (m)"), WallDrawer.Thickness);
            WallDrawer.CloseLoop = EditorGUILayout.Toggle(TTGui.C("ต่อจุดสุดท้ายกลับไปจุดแรก", "ล้อมเป็นวง"), WallDrawer.CloseLoop);
            WallDrawer.Layer = EditorGUILayout.LayerField(TTGui.C("Layer", "แนะนำ Ignore Raycast ผู้เล่นชนได้ แต่กระสุนกับปุ่ม E ไม่โดนกำแพง"), WallDrawer.Layer);

            if (WallDrawer.Active)
            {
                TTGui.Note("<b>คลิก</b> = เพิ่มจุด   <b>Backspace</b> = ลบจุดล่าสุด   <b>Enter</b> = สร้าง   <b>Esc</b> = เลิก");
                TTGui.Note("ตอนนี้มี " + WallDrawer.Points.Count + " จุด");
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = WallDrawer.Points.Count >= 2;
                if (TTGui.BigButton("สร้างกำแพง", null, true)) { WallDrawer.Build(); WallDrawer.Active = false; }
                GUI.enabled = true;
                if (GUILayout.Button("ยกเลิก", GUILayout.Height(34f), GUILayout.Width(80f))) { WallDrawer.Points.Clear(); WallDrawer.Active = false; }
                EditorGUILayout.EndHorizontal();
            }
            else if (TTGui.BigButton("เริ่มวาดกำแพง", null, true))
            {
                WallDrawer.Points.Clear();
                WallDrawer.Active = true;
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Focus();
            }
            TTGui.Note("กำแพงที่สร้างแล้วจะเห็นเป็นกรอบสีส้มตอนเปิดแท็บนี้ เก็บอยู่ใต้ Boundaries ใน Hierarchy");
            TTGui.EndSection();
        }

        // ================= สีพื้น =================

        void DrawLayerTab()
        {
            TTGui.BeginSection("สร้าง Terrain Layer", "ลากรูปมาวาง ระบบแยกให้เองว่ารูปไหนคือสี Normal หรือ Mask และตั้งค่ารูปให้ถูกต้อง");

            Rect drop = GUILayoutUtility.GetRect(0f, 56f, GUILayout.ExpandWidth(true));
            GUI.Box(drop, "ลากรูปทั้งชุด (สี / Normal / Mask) มาวางตรงนี้", EditorStyles.helpBox);
            HandleDrop(drop, objects => LayerMaker.AutoAssign(objects, ref _diffuse, ref _normal, ref _mask, ref _layerName));

            _diffuse = (Texture2D)EditorGUILayout.ObjectField(TTGui.C("สี (Diffuse)"), _diffuse, typeof(Texture2D), false);
            _normal = (Texture2D)EditorGUILayout.ObjectField(TTGui.C("Normal Map", "ระบบตั้ง Texture Type เป็น Normal map ให้"), _normal, typeof(Texture2D), false);
            _mask = (Texture2D)EditorGUILayout.ObjectField(TTGui.C("Mask Map", "R=Metallic G=AO B=Height A=Smoothness ระบบเอาติ๊ก sRGB ออกให้"), _mask, typeof(Texture2D), false);
            _layerName = EditorGUILayout.TextField(TTGui.C("ชื่อ Layer"), _layerName);
            _tileSize = EditorGUILayout.Vector2Field(TTGui.C("ขนาดลาย (m)", "รูป 1 แผ่นกว้างกี่เมตร ถ้าเห็นลายซ้ำให้เพิ่ม"), _tileSize);
            _addToSelected = EditorGUILayout.Toggle(TTGui.C("ใส่ลง Terrain ที่เลือกด้วย"), _addToSelected);

            Terrain selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Terrain>() : null;
            if (_addToSelected && selected != null && selected.terrainData != null)
            {
                int count = selected.terrainData.terrainLayers.Length;
                TTGui.Note("Terrain “" + selected.name + "” ใช้ไปแล้ว " + count + " จาก 8 Layer");
            }
            if (_mask == null)
                TTGui.Note("ไม่มี Mask Map ก็สร้างได้ แต่จะใช้ Height-based Blend ไม่ได้");

            GUI.enabled = _diffuse != null && !string.IsNullOrEmpty(_layerName);
            if (TTGui.BigButton("สร้าง Layer", null, true))
            {
                LayerMaker.Create(_diffuse, _normal, _mask, _layerName, _tileSize, _addToSelected ? selected : null);
                _diffuse = _normal = _mask = null;
                _layerName = "";
            }
            GUI.enabled = true;
            TTGui.EndSection();
        }

        // ================= ตัวช่วย =================

        static void HandleDrop(Rect area, System.Action<Object[]> onDrop)
        {
            Event e = Event.current;
            if (!area.Contains(e.mousePosition)) return;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                onDrop(DragAndDrop.objectReferences);
            }
            e.Use();
        }
    }
}
