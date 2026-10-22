using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// ต้องอยู่ในโฟลเดอร์ Editor
// หน้าเดียวรวมปุ่มปรับลุคที่ใช้บ่อย เขียนค่าลง Volume Profile ตรงๆ กด Ctrl+Z ย้อนได้
public class SceneLookPanel : EditorWindow
{
    private Volume _volume;
    private Light _moon;
    private Vector2 _scroll;

    private bool _showLight = true, _showColor = true, _showFog = true, _showGlow = true, _showEdge = true, _showWet = true, _showMoon = true;

    [MenuItem("Tools/Art/ปรับลุคฉาก")]
    private static void Open() => GetWindow<SceneLookPanel>("ปรับลุคฉาก");

    private void OnEnable()
    {
        Undo.undoRedoPerformed += Repaint;
        FindDefaults();
    }

    private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

    private void OnHierarchyChange()
    {
        if (_volume == null || _moon == null) FindDefaults();
        Repaint();
    }

    private void FindDefaults()
    {
        if (_volume == null)
        {
            Volume best = null;
            foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!v.isGlobal || v.sharedProfile == null) continue;
                if (best == null || v.priority > best.priority) best = v;
            }
            _volume = best;
        }

        if (_moon == null)
        {
            _moon = RenderSettings.sun;
            if (_moon == null)
            {
                foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (l.type == LightType.Directional && l.enabled) { _moon = l; break; }
            }
        }
    }

    private void OnGUI()
    {
        _volume = (Volume)EditorGUILayout.ObjectField("Global Volume", _volume, typeof(Volume), true);
        if (_volume == null || _volume.sharedProfile == null)
        {
            EditorGUILayout.HelpBox("ลาก Global Volume ของฉากมาใส่ช่องด้านบน", MessageType.Info);
            return;
        }

        VolumeProfile profile = _volume.sharedProfile;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("โปรไฟล์: " + profile.name, EditorStyles.miniLabel);
            if (GUILayout.Button("ดูไฟล์", GUILayout.Width(60))) EditorGUIUtility.PingObject(profile);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("บันทึกเป็นลุคใหม่ (สำเนา)", GUILayout.Height(24)))
                DuplicateProfile();
            if (GUILayout.Button("ใส่ลุคป่าหมอกกลางคืน", GUILayout.Height(24)))
                ApplyNightMistLook(profile);
        }
        EditorGUILayout.HelpBox("ปรับอะไรไปแล้วไม่ชอบ กด Ctrl+Z ย้อนได้", MessageType.None);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        _showLight = Section("ความสว่าง", _showLight);
        if (_showLight)
        {
            var ca = Get<ColorAdjustments>(profile);
            if (ca != null) Slider(ca, ca.postExposure, "ความสว่างรวม", "ลบ = มืดลง / บวก = สว่างขึ้น", -3f, 3f);
        }

        _showColor = Section("สีและอารมณ์ภาพ", _showColor);
        if (_showColor)
        {
            var ca = Get<ColorAdjustments>(profile);
            if (ca != null)
            {
                Slider(ca, ca.contrast, "ความคมของมืด-สว่าง", "มาก = มืดลึก สว่างจัด", -50f, 60f);
                Slider(ca, ca.saturation, "ความสดของสี", "ลบ = ซีดหม่น / บวก = สด", -100f, 50f);
                ColorField(ca, ca.colorFilter, "ฟิลเตอร์สีทั้งจอ", "ขาว = ไม่ย้อมสี");
            }
            var wb = Get<WhiteBalance>(profile);
            if (wb != null)
            {
                Slider(wb, wb.temperature, "เย็น ⟷ อุ่น", "ลบ = ฟ้าเย็น / บวก = ส้มอุ่น", -60f, 60f);
                Slider(wb, wb.tint, "เขียว ⟷ ม่วง", "ดึงโทนเขียวหรือม่วงแดง", -50f, 50f);
            }
            var st = Get<SplitToning>(profile);
            if (st != null)
            {
                ColorField(st, st.shadows, "สีในเงามืด", "สีที่ย้อมส่วนมืด เทา = ไม่ย้อม");
                ColorField(st, st.highlights, "สีในส่วนสว่าง", "สีที่ย้อมส่วนสว่าง เช่นแสงไฟ");
                Slider(st, st.balance, "เน้นเงา ⟷ เน้นสว่าง", "ลบ = สีเงากินพื้นที่มากกว่า", -100f, 100f);
            }
        }

        _showFog = Section("หมอก", _showFog);
        if (_showFog)
        {
            var fog = Get<Fog>(profile);
            if (fog != null)
            {
                Toggle(fog, fog.enabled, "เปิดหมอก", "");
                Slider(fog, fog.meanFreePath, "มองทะลุได้ไกล (เมตร)", "น้อย = หมอกหนา / มาก = หมอกบาง", 2f, 200f);
                ColorField(fog, fog.color, "สีหมอกระยะไกล", "สีที่ของไกลๆ จางเข้าไปหา");
                Slider(fog, fog.baseHeight, "ความสูงที่หมอกหนาสุด", "ต่ำกว่านี้หมอกหนาเต็มที่", -50f, 100f);
                Slider(fog, fog.maximumHeight, "ความสูงที่หมอกหมด", "หมอกจางลงเรื่อยๆ จนถึงระดับนี้", 0f, 300f);
                Toggle(fog, fog.enableVolumetricFog, "หมอกรับแสง (Volumetric)", "ปิด = แสงไฟไม่ฟุ้งในหมอก");
                ColorField(fog, fog.albedo, "สีหมอกตอนโดนแสง", "");
                Slider(fog, fog.globalLightProbeDimmer, "หมอกรับแสงฟ้า", "มาก = หมอกขาวสว่างทั้งฉาก", 0f, 1f);
                Slider(fog, fog.anisotropy, "แสงฟุ้งรอบโคมไฟ", "บวก = แสงฟุ้งเป็นรัศมีเวลามองเข้าหาไฟ", -1f, 1f);
            }
        }

        _showGlow = Section("แสงเรือง (เทียน โคมไฟ)", _showGlow);
        if (_showGlow)
        {
            var bloom = Get<Bloom>(profile);
            if (bloom != null)
            {
                Slider(bloom, bloom.intensity, "ความแรงแสงเรือง", "0 = ไม่เรืองเลย", 0f, 1f);
                Slider(bloom, bloom.threshold, "สว่างเท่าไหร่ถึงเรือง", "มาก = เรืองแค่ไฟจ้าๆ", 0f, 5f);
                Slider(bloom, bloom.scatter, "แสงเรืองกระจายกว้าง", "", 0f, 1f);
            }
        }

        _showEdge = Section("ขอบจอมืด", _showEdge);
        if (_showEdge)
        {
            var vig = Get<Vignette>(profile);
            if (vig != null)
            {
                Slider(vig, vig.intensity, "ความมืดขอบจอ", "", 0f, 1f);
                Slider(vig, vig.smoothness, "ความนุ่มขอบ", "", 0.01f, 1f);
                ColorField(vig, vig.color, "สีขอบจอ", "");
            }
        }

        _showWet = Section("ความเงาพื้นเปียก / แสงสะท้อน", _showWet);
        if (_showWet)
        {
            var ilc = Get<IndirectLightingController>(profile);
            if (ilc != null)
            {
                Slider(ilc, ilc.reflectionLightingMultiplier, "แสงสะท้อนบนผิว", "0 = หินเปียกไม่วาวเลย", 0f, 2f);
                Slider(ilc, ilc.indirectDiffuseLightingMultiplier, "แสงอ้อมรอบข้าง", "0 = ส่วนที่ไม่โดนแสงตรงจะดำสนิท", 0f, 2f);
            }
        }

        _showMoon = Section("แสงจันทร์ (Directional Light ในฉาก)", _showMoon);
        if (_showMoon) DrawMoon();

        EditorGUILayout.EndScrollView();
    }

    private void DrawMoon()
    {
        _moon = (Light)EditorGUILayout.ObjectField("ไฟ", _moon, typeof(Light), true);
        if (_moon == null) return;

        EditorGUI.BeginChangeCheck();
        Color color = EditorGUILayout.ColorField(new GUIContent("สีแสงจันทร์"), _moon.color, true, false, false);
        float intensity = EditorGUILayout.FloatField(new GUIContent("ความแรง (Lux)"), _moon.intensity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_moon, "ปรับแสงจันทร์");
            _moon.color = color;
            _moon.intensity = Mathf.Max(0f, intensity);
            MarkSceneDirty(_moon);
        }

        var hd = _moon.GetComponent<HDAdditionalLightData>();
        if (hd == null) return;

        EditorGUI.BeginChangeCheck();
        bool affects = EditorGUILayout.Toggle(new GUIContent("แสงจันทร์ส่องหมอก", "ปิด = หมอกไม่โดนแสงจันทร์เลย ไม่มีลำแสงลอดต้นไม้"), hd.affectsVolumetric);
        float dimmer = hd.volumetricDimmer;
        using (new EditorGUI.DisabledScope(!affects))
            dimmer = EditorGUILayout.Slider(new GUIContent("แสงจันทร์ในหมอก", "มาก = หมอกขาวโพลน"), dimmer, 0f, 2f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(hd, "ปรับแสงจันทร์ในหมอก");
            hd.affectsVolumetric = affects;
            hd.volumetricDimmer = dimmer;
            MarkSceneDirty(hd);
        }

        if (_moon.useColorTemperature)
            EditorGUILayout.HelpBox("ไฟนี้เปิด Color Temperature อยู่ สีจริงจะเป็นสีด้านบนผสมกับอุณหภูมิสี", MessageType.None);
    }

    // ---------- ตัวช่วยวาดช่อง ----------

    private static bool Section(string title, bool open)
    {
        EditorGUILayout.Space(6);
        return EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
    }

    private static GUIContent Label(VolumeParameter p, string label, string tip)
    {
        return new GUIContent(p.overrideState ? label : label + "  (ยังไม่ได้ใช้)", tip);
    }

    private static void Slider(VolumeComponent c, VolumeParameter<float> p, string label, string tip, float min, float max)
    {
        EditorGUI.BeginChangeCheck();
        float v = EditorGUILayout.Slider(Label(p, label, tip), p.value, min, max);
        if (EditorGUI.EndChangeCheck()) Set(c, p, v);
    }

    private static void ColorField(VolumeComponent c, ColorParameter p, string label, string tip)
    {
        EditorGUI.BeginChangeCheck();
        Color v = EditorGUILayout.ColorField(Label(p, label, tip), p.value, true, false, p.hdr);
        if (EditorGUI.EndChangeCheck()) Set(c, p, v);
    }

    private static void Toggle(VolumeComponent c, BoolParameter p, string label, string tip)
    {
        EditorGUI.BeginChangeCheck();
        bool v = EditorGUILayout.Toggle(Label(p, label, tip), p.value);
        if (EditorGUI.EndChangeCheck()) Set(c, p, v);
    }

    private static void Set<T>(VolumeComponent c, VolumeParameter<T> p, T value)
    {
        Undo.RecordObject(c, "ปรับลุค");
        p.Override(value);
        c.active = true;
        EditorUtility.SetDirty(c);
    }

    // หาเอฟเฟกต์ในโปรไฟล์ ถ้าไม่มีให้ปุ่มเพิ่ม
    private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component)) return component;

        if (GUILayout.Button("เพิ่ม " + typeof(T).Name + " เข้าโปรไฟล์"))
            return Add<T>(profile);
        return null;
    }

    private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(false);
        component.name = typeof(T).Name;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        if (EditorUtility.IsPersistent(profile))
            AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return component;
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        return profile.TryGet(out T component) ? component : Add<T>(profile);
    }

    private static void MarkSceneDirty(Component c)
    {
        EditorUtility.SetDirty(c);
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
    }

    // ---------- ปุ่มด้านบน ----------

    private void DuplicateProfile()
    {
        VolumeProfile profile = _volume.sharedProfile;
        string path = AssetDatabase.GetAssetPath(profile);
        if (string.IsNullOrEmpty(path)) return;

        string newPath = AssetDatabase.GenerateUniqueAssetPath(path.Replace(".asset", "_Look.asset"));
        if (!AssetDatabase.CopyAsset(path, newPath)) return;

        var copy = AssetDatabase.LoadAssetAtPath<VolumeProfile>(newPath);
        Undo.RecordObject(_volume, "สลับไปใช้ลุคใหม่");
        _volume.sharedProfile = copy;
        MarkSceneDirty(_volume);
        EditorGUIUtility.PingObject(copy);
    }

    private void ApplyNightMistLook(VolumeProfile profile)
    {
        if (!EditorUtility.DisplayDialog("ใส่ลุคป่าหมอกกลางคืน",
                "จะเปลี่ยนค่าในโปรไฟล์ " + profile.name + "\nแนะนำให้กด 'บันทึกเป็นลุคใหม่' ก่อน\n(กด Ctrl+Z ย้อนได้)",
                "ใส่เลย", "ยกเลิก"))
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("ใส่ลุคป่าหมอกกลางคืน");
        int undoGroup = Undo.GetCurrentGroup();

        var ca = GetOrAdd<ColorAdjustments>(profile);
        Set(ca, ca.postExposure, -0.3f);
        Set(ca, ca.contrast, 15f);
        Set(ca, ca.saturation, -25f);
        Set(ca, ca.colorFilter, Color.white);

        var wb = GetOrAdd<WhiteBalance>(profile);
        Set(wb, wb.temperature, -15f);
        Set(wb, wb.tint, 5f);

        var st = GetOrAdd<SplitToning>(profile);
        Set(st, st.shadows, new Color(0.33f, 0.42f, 0.50f));
        Set(st, st.highlights, new Color(0.95f, 0.62f, 0.30f));
        Set(st, st.balance, -15f);

        var fog = GetOrAdd<Fog>(profile);
        Set(fog, fog.enabled, true);
        Set(fog, fog.meanFreePath, 18f);
        Set(fog, fog.color, new Color(0.06f, 0.085f, 0.11f));
        Set(fog, fog.enableVolumetricFog, true);
        Set(fog, fog.albedo, new Color(0.72f, 0.80f, 0.88f));
        Set(fog, fog.globalLightProbeDimmer, 0.25f);
        Set(fog, fog.anisotropy, 0.35f);

        var bloom = GetOrAdd<Bloom>(profile);
        Set(bloom, bloom.intensity, 0.3f);
        Set(bloom, bloom.threshold, 1f);
        Set(bloom, bloom.scatter, 0.7f);

        var vig = GetOrAdd<Vignette>(profile);
        Set(vig, vig.intensity, 0.3f);
        Set(vig, vig.smoothness, 0.4f);
        Set(vig, vig.color, new Color(0.02f, 0.03f, 0.04f));

        var ilc = GetOrAdd<IndirectLightingController>(profile);
        Set(ilc, ilc.reflectionLightingMultiplier, 0.5f);

        if (_moon != null)
        {
            Undo.RecordObject(_moon, "ปรับแสงจันทร์");
            _moon.color = new Color(0.62f, 0.72f, 0.88f);
            MarkSceneDirty(_moon);

            var hd = _moon.GetComponent<HDAdditionalLightData>();
            if (hd != null)
            {
                Undo.RecordObject(hd, "ปรับแสงจันทร์ในหมอก");
                hd.affectsVolumetric = true;
                hd.volumetricDimmer = 0.25f;
                MarkSceneDirty(hd);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(profile);
        Repaint();
    }
}
