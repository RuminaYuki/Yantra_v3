using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// รายการเสียงพากย์ทั้งเกม (เวอร์ชัน 2) — ดึงทุกประโยคจาก SubtitleDataSO ทุกไฟล์ในโปรเจกต์
//
//  ① ล็อก Voice ID    เติม Text Key ที่ยังว่าง = ชื่อไฟล์เสียงที่คนพากย์ต้องตั้ง (แทรก/สลับประโยคทีหลังก็ไม่เลื่อน)
//  ② Placeholder      ใส่เสียงชั่วคราวให้ทุกประโยคที่ยังไม่มีเสียง — แยกเสียงตามตัวละคร ฟังแล้วรู้ว่าใครพูด
//  ③ ส่งคนกำกับเสียง  Export ตาราง (CSV เปิดใน Excel / Google Sheets ได้) + คัดลอกสรุปไปวางในแชท
//  ④ ได้เสียงจริงแล้ว ตั้งชื่อไฟล์ตาม Voice ID วางในโฟลเดอร์เดียว กดจับคู่ — ใส่ให้ทุกประโยค แทน Placeholder
//
// ตรวจปัญหาให้ด้วย: Text Key ซ้ำ (เสียงจะใส่ผิดประโยค) / ไม่มี Text Key / ไม่มีข้อความ / ชื่อไฟล์เสียงมีภาษาไทย
//
// พากย์ไทย ซับอังกฤษ: ตาราง CSV มีช่อง "บทพากย์ (ไทย)" ว่างไว้ให้เติม — ส่งคนกำกับอัดตามช่องนั้น
// Export ทับไฟล์เดิม = บทไทย + หมายเหตุที่เติมไว้ไม่หาย (จับคู่ตาม Voice ID)
//
// ต้องอยู่ในโฟลเดอร์ชื่อ Editor — เมนู Tools > Yantra > Voice Lines
public class VoiceLineListWindow : EditorWindow
{
    private enum Status { Missing, Placeholder, Done }

    private enum Filter { All, NeedVoice, Missing, Placeholder, Done, Problems }

    private static readonly string[] FilterLabels =
    {
        "ทั้งหมด", "ยังไม่มีเสียงจริง", "ยังไม่มีเสียงเลย", "Placeholder", "มีเสียงจริงแล้ว", "มีปัญหา"
    };

    private class Line
    {
        public SubtitleDataSO Data;
        public int Index;
        public string VoiceId;
        public string Speaker;
        public Status Status;
        public float Seconds;
        public readonly List<string> Problems = new List<string>();
    }

    private class FileGroup
    {
        public SubtitleDataSO Data;
        public string Path;
        public string Folder;
        public bool Open = true;
        public readonly List<Line> Lines = new List<Line>();
    }

    private class SpeakerRow
    {
        public string Name;
        public int Total;
        public int NeedVoice;
        public float Seconds;
    }

    private const string PlaceholderPrefix = "VO_PH_";
    private const string DefaultPlaceholderFolder = "Assets/Audio/Voice/_Placeholder";
    private const string PrefPlaceholderFolder = "Yantra.VoiceLines.PlaceholderFolder.";
    private const string PrefVoiceFolder = "Yantra.VoiceLines.VoiceFolder.";
    private const string PrefCsvFolder = "Yantra.VoiceLines.CsvFolder.";
    private const string NoSpeaker = "(ไม่ระบุ / เสียงในหัว)";
    private const int WavRate = 22050;

    private readonly List<FileGroup> _files = new List<FileGroup>();
    private readonly List<Line> _lines = new List<Line>();
    private readonly List<SpeakerRow> _speakers = new List<SpeakerRow>();
    private readonly Dictionary<string, bool> _folderOpen = new Dictionary<string, bool>();
    private bool _busy;   // กำลังแก้ไฟล์ / สร้างเสียง — ห้ามดึงข้อมูลใหม่กลางคัน (Unity แจ้ง project change ระหว่างนั้น)

    private DefaultAsset _placeholderFolder;
    private DefaultAsset _voiceFolder;
    private Vector2 _scroll;
    private bool _showSpeakers = true;
    private bool _showSteps = true;
    private bool _exportBySpeaker;
    private Filter _filter = Filter.All;
    private int _speakerFilter;
    private string _search = "";
    private GUIStyle _rich;
    private GUIStyle _problem;

    [MenuItem("Tools/Yantra/Voice Lines (รายการเสียงพากย์)")]
    private static void Open()
    {
        var window = GetWindow<VoiceLineListWindow>("Voice Lines");
        window.minSize = new Vector2(560f, 460f);
    }

    private static string ProjectKey(string prefix) => prefix + Application.dataPath;

    private void OnEnable()
    {
        _placeholderFolder = LoadPref<DefaultAsset>(PrefPlaceholderFolder);
        _voiceFolder = LoadPref<DefaultAsset>(PrefVoiceFolder);
        Refresh();
    }

    private void OnFocus()
    {
        if (!_busy) Refresh();
    }

    private void OnProjectChange()
    {
        if (_busy) return;
        Refresh();
        Repaint();
    }

    private static T LoadPref<T>(string prefix) where T : Object
    {
        string guid = EditorPrefs.GetString(ProjectKey(prefix), "");
        if (string.IsNullOrEmpty(guid)) return null;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static void SavePref(string prefix, Object asset)
    {
        string guid = asset != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)) : "";
        EditorPrefs.SetString(ProjectKey(prefix), guid);
    }

    // ===================== เก็บข้อมูล =====================

    private void Refresh()
    {
        var openState = new Dictionary<string, bool>();
        foreach (FileGroup f in _files)
            openState[f.Path] = f.Open;

        _files.Clear();
        _lines.Clear();

        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:SubtitleDataSO"))
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(System.StringComparer.OrdinalIgnoreCase);

        foreach (string path in paths)
        {
            var data = AssetDatabase.LoadAssetAtPath<SubtitleDataSO>(path);
            if (data == null) continue;

            var file = new FileGroup { Data = data, Path = path, Folder = FolderLabel(path) };
            if (openState.TryGetValue(path, out bool open)) file.Open = open;
            _files.Add(file);

            if (data.Lines == null) continue;
            for (int i = 0; i < data.Lines.Length; i++)
            {
                SubtitleLine raw = data.Lines[i];
                var line = new Line
                {
                    Data = data,
                    Index = i,
                    VoiceId = VoiceId(data, i),
                    Speaker = SpeakerName(raw),
                    Status = GetStatus(raw.VoiceClip),
                    Seconds = EstimateSeconds(raw)
                };
                file.Lines.Add(line);
                _lines.Add(line);
            }
        }

        FindProblems();
        CountSpeakers();
    }

    // โฟลเดอร์ที่ไฟล์บทอยู่ นับจากโฟลเดอร์ Data — เช่น "01_Forest" (ไฟล์ที่อยู่ตรง Data เลย = "(ยังไม่จัดโฟลเดอร์)")
    private static string FolderLabel(string path)
    {
        string dir = Path.GetDirectoryName(path).Replace('\\', '/');
        int data = dir.LastIndexOf("/Data", System.StringComparison.OrdinalIgnoreCase);
        if (data >= 0)
        {
            string rest = dir.Substring(data + 5).TrimStart('/');
            return string.IsNullOrEmpty(rest) ? "(ยังไม่จัดโฟลเดอร์)" : rest;
        }
        return dir;
    }

    private void FindProblems()
    {
        var byKey = new Dictionary<string, List<Line>>(System.StringComparer.OrdinalIgnoreCase);
        foreach (Line line in _lines)
        {
            SubtitleLine raw = line.Data.Lines[line.Index];
            string key = raw.TextKey != null ? raw.TextKey.Trim() : "";

            if (key.Length == 0)
                line.Problems.Add("ยังไม่มี Text Key — กด ① ล็อก Voice ID");
            else
            {
                if (!byKey.TryGetValue(key, out List<Line> same))
                    byKey[key] = same = new List<Line>();
                same.Add(line);
            }

            if (string.IsNullOrWhiteSpace(raw.DefaultText))
                line.Problems.Add("ไม่มีข้อความ");

            if (!IsAsciiName(line.VoiceId))
                line.Problems.Add("Voice ID มีอักษรที่ไม่ใช่อังกฤษ/ตัวเลข — คนพากย์ตั้งชื่อไฟล์ยาก");
        }

        foreach (var pair in byKey)
        {
            if (pair.Value.Count < 2) continue;
            foreach (Line line in pair.Value)
            {
                var others = new List<string>();
                foreach (Line other in pair.Value)
                    if (other != line) others.Add($"{other.Data.name} ประโยค {other.Index + 1}");
                line.Problems.Add($"Text Key ซ้ำกับ {string.Join(", ", others)} — เสียงจะใส่ผิดประโยค");
            }
        }
    }

    private void CountSpeakers()
    {
        _speakers.Clear();
        var map = new Dictionary<string, SpeakerRow>();
        foreach (Line line in _lines)
        {
            if (!map.TryGetValue(line.Speaker, out SpeakerRow row))
            {
                row = new SpeakerRow { Name = line.Speaker };
                map[line.Speaker] = row;
                _speakers.Add(row);
            }
            row.Total++;
            row.Seconds += line.Seconds;
            if (line.Status != Status.Done) row.NeedVoice++;
        }
        _speakers.Sort((a, b) => b.Total.CompareTo(a.Total));
        if (_speakerFilter > _speakers.Count) _speakerFilter = 0;
    }

    // ไฟล์บทถูกลบ / จำนวนประโยคเปลี่ยนระหว่างเปิดหน้าต่าง — ดึงใหม่ก่อนวาด
    private bool IsStale()
    {
        int expected = 0;
        foreach (FileGroup f in _files)
        {
            if (f.Data == null) return true;
            if (f.Data.Lines != null) expected += f.Data.Lines.Length;
        }
        return expected != _lines.Count;
    }

    private static Status GetStatus(AudioClip clip)
    {
        if (clip == null) return Status.Missing;
        return IsPlaceholder(clip) ? Status.Placeholder : Status.Done;
    }

    private static bool IsPlaceholder(AudioClip clip) =>
        clip != null && clip.name.StartsWith(PlaceholderPrefix, System.StringComparison.OrdinalIgnoreCase);

    private static string SpeakerName(SubtitleLine line)
    {
        if (line.Speaker == null) return NoSpeaker;
        return string.IsNullOrEmpty(line.Speaker.DefaultName) ? line.Speaker.name : line.Speaker.DefaultName;
    }

    // ความยาวโดยประมาณ — มีเสียงจริง = ยาวตามเสียง / ไม่งั้นใช้ Fallback Duration / ไม่ได้ตั้ง = เดาจากความยาวข้อความ
    private static float EstimateSeconds(SubtitleLine line)
    {
        if (line.VoiceClip != null && !IsPlaceholder(line.VoiceClip)) return line.VoiceClip.length;
        if (line.FallbackDuration > 0f) return line.FallbackDuration;
        int chars = string.IsNullOrEmpty(line.DefaultText) ? 0 : line.DefaultText.Length;
        return Mathf.Max(1.2f, chars / 14f + 0.5f);
    }

    // ชื่อไฟล์ที่คนพากย์ต้องตั้ง — ใช้ Text Key ถ้ามี / ไม่มี = ชื่อที่ปุ่ม ① จะใส่ให้
    private static string VoiceId(SubtitleDataSO data, int index)
    {
        string key = data.Lines[index].TextKey;
        if (!string.IsNullOrWhiteSpace(key)) return key.Trim();
        return $"{KeyBase(data)}_{index + 1:00}";
    }

    // Sub_Forest_03_Blocked → forest_03_blocked / ชื่อไทยล้วน → line_ + รหัสสั้นจาก GUID (ไม่ซ้ำกันแน่นอน)
    private static string KeyBase(SubtitleDataSO data)
    {
        string name = data.name;
        if (name.StartsWith("Sub_", System.StringComparison.OrdinalIgnoreCase))
            name = name.Substring(4);

        var sb = new StringBuilder();
        foreach (char c in name.ToLowerInvariant())
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
            if (ok) sb.Append(c);
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
        }
        string result = sb.ToString().Trim('_');
        if (result.Length > 0) return result;

        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
        return "line_" + (guid.Length >= 6 ? guid.Substring(0, 6) : guid);
    }

    private static bool IsAsciiName(string value)
    {
        foreach (char c in value)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
            if (!ok) return false;
        }
        return value.Length > 0;
    }

    // ===================== หน้าจอ =====================

    private void OnGUI()
    {
        if (_rich == null)
        {
            _rich = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
            _problem = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true };
        }
        if (!_busy && IsStale()) Refresh();

        int done = 0, placeholder = 0, missing = 0, problems = 0, noKey = 0;
        float seconds = 0f;
        foreach (Line line in _lines)
        {
            if (line.Status == Status.Done) done++;
            else if (line.Status == Status.Placeholder) placeholder++;
            else missing++;
            if (line.Problems.Count > 0) problems++;
            if (string.IsNullOrWhiteSpace(line.Data.Lines[line.Index].TextKey)) noKey++;
            seconds += line.Seconds;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            $"ต้องมีเสียงพากย์ทั้งหมด <b>{_lines.Count}</b> ประโยค (จาก {_files.Count} ไฟล์ · ยาวรวมราว <b>{FormatTime(seconds)}</b>)\n" +
            $"<color=#7CD67C>มีเสียงจริง {done}</color>   ·   <color=#E8C35A>Placeholder {placeholder}</color>   ·   " +
            $"<color=#E86A5A>ยังไม่มีเสียงเลย {missing}</color>" +
            (problems > 0 ? $"   ·   <color=#FF8A65>มีปัญหา {problems}</color>" : ""), _rich);

        if (_files.Count == 0)
        {
            EditorGUILayout.HelpBox("ยังไม่มีไฟล์บทพูด (Subtitle Data) ในโปรเจกต์ — สร้างจาก Create > YUKI Learning System > Subtitle > Subtitle Data", MessageType.Info);
            return;
        }

        DrawSpeakers();
        DrawSteps(noKey, missing, placeholder);
        DrawFilters();
        DrawList();
    }

    private void DrawSpeakers()
    {
        _showSpeakers = EditorGUILayout.Foldout(_showSpeakers, "แยกตามตัวละคร", true);
        if (!_showSpeakers) return;

        EditorGUI.indentLevel++;
        foreach (SpeakerRow row in _speakers)
            EditorGUILayout.LabelField(row.Name,
                $"{row.Total} ประโยค   ·   ยังไม่มีเสียงจริง {row.NeedVoice}   ·   ราว {FormatTime(row.Seconds)}");
        EditorGUI.indentLevel--;
    }

    private void DrawSteps(int noKey, int missing, int placeholder)
    {
        EditorGUILayout.Space(4f);
        _showSteps = EditorGUILayout.Foldout(_showSteps, "ขั้นตอน", true);
        if (!_showSteps) return;

        // ① ----------------------------------------------------------
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("① ล็อก Voice ID", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("เติม Text Key ให้ประโยคที่ยังว่าง — Text Key = ชื่อไฟล์เสียงที่คนพากย์ต้องตั้ง", EditorStyles.wordWrappedMiniLabel);
        using (new EditorGUI.DisabledScope(noKey == 0))
        {
            if (GUILayout.Button(noKey == 0 ? "ทุกประโยคมี Text Key แล้ว ✔" : $"เติม Text Key ที่ยังว่าง ({noKey} ประโยค)", GUILayout.Height(22f)))
            {
                LockVoiceIds();
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.EndVertical();

        // ② ----------------------------------------------------------
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("② ใส่เสียง Placeholder", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("สร้างเสียง \"อึมๆ\" แยกตามตัวละคร (เสียงสูงต่ำไม่เท่ากัน ฟังแล้วรู้ว่าใครพูด) แล้วใส่ให้ทุกประโยคที่ยังไม่มีเสียง\n" +
                                   "ซับยังโชว์นานเท่า Fallback Duration เหมือนเดิม", EditorStyles.wordWrappedMiniLabel);
        EditorGUI.BeginChangeCheck();
        var phFolder = (DefaultAsset)EditorGUILayout.ObjectField("เก็บไฟล์ Placeholder ที่", _placeholderFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            _placeholderFolder = IsFolder(phFolder) ? phFolder : null;
            SavePref(PrefPlaceholderFolder, _placeholderFolder);
        }
        if (_placeholderFolder == null)
            EditorGUILayout.LabelField($"ว่าง = สร้างโฟลเดอร์ {DefaultPlaceholderFolder} ให้", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(missing == 0))
        {
            if (GUILayout.Button($"ใส่ Placeholder ({missing} ประโยค)", GUILayout.Height(22f)))
            {
                FillPlaceholders();
                GUIUtility.ExitGUI();
            }
        }
        using (new EditorGUI.DisabledScope(placeholder == 0))
        {
            if (GUILayout.Button($"เอา Placeholder ออก ({placeholder})", GUILayout.Height(22f)))
            {
                RemovePlaceholders();
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        // ③ ----------------------------------------------------------
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("③ ส่งให้คนกำกับเสียง", EditorStyles.boldLabel);
        _exportBySpeaker = EditorGUILayout.ToggleLeft("เรียงตารางตามตัวละคร (อัดทีละคนง่ายกว่า)", _exportBySpeaker);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Export ตาราง (CSV)", GUILayout.Height(22f)))
        {
            ExportCsv();
            GUIUtility.ExitGUI();   // หน้าต่างเลือกไฟล์ขัดจังหวะการวาด — จบเฟรมนี้เลย กัน error layout
        }
        if (GUILayout.Button("คัดลอกสรุปไปวางในแชท", GUILayout.Height(22f)))
        {
            EditorGUIUtility.systemCopyBuffer = BuildSummary();
            ShowNotification(new GUIContent("คัดลอกแล้ว — วางในแชทได้เลย"));
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        // ④ ----------------------------------------------------------
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("④ ได้เสียงจริงแล้ว", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("ตั้งชื่อไฟล์เสียงตาม Voice ID (เช่น forest_blocked_01.wav) วางรวมในโฟลเดอร์เดียว แล้วกดจับคู่ — ใส่แทน Placeholder ให้เอง",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUI.BeginChangeCheck();
        var voiceFolder = (DefaultAsset)EditorGUILayout.ObjectField("โฟลเดอร์เสียงจริง", _voiceFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            _voiceFolder = IsFolder(voiceFolder) ? voiceFolder : null;
            SavePref(PrefVoiceFolder, _voiceFolder);
        }
        using (new EditorGUI.DisabledScope(_voiceFolder == null))
        {
            if (GUILayout.Button("จับคู่เสียงจริงตาม Voice ID", GUILayout.Height(22f)))
            {
                MatchRealVoices();
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawFilters()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ทุกประโยค", EditorStyles.boldLabel, GUILayout.Width(70f));
        _filter = (Filter)EditorGUILayout.Popup((int)_filter, FilterLabels, GUILayout.Width(130f));

        var names = new string[_speakers.Count + 1];
        names[0] = "ทุกตัวละคร";
        for (int i = 0; i < _speakers.Count; i++) names[i + 1] = _speakers[i].Name;
        _speakerFilter = EditorGUILayout.Popup(_speakerFilter, names, GUILayout.Width(140f));

        _search = EditorGUILayout.TextField(_search);
        EditorGUILayout.EndHorizontal();
    }

    private bool PassFilter(Line line)
    {
        switch (_filter)
        {
            case Filter.NeedVoice: if (line.Status == Status.Done) return false; break;
            case Filter.Missing: if (line.Status != Status.Missing) return false; break;
            case Filter.Placeholder: if (line.Status != Status.Placeholder) return false; break;
            case Filter.Done: if (line.Status != Status.Done) return false; break;
            case Filter.Problems: if (line.Problems.Count == 0) return false; break;
        }

        if (_speakerFilter > 0 && _speakerFilter <= _speakers.Count && line.Speaker != _speakers[_speakerFilter - 1].Name)
            return false;

        if (!string.IsNullOrEmpty(_search))
        {
            string text = line.Data.Lines[line.Index].DefaultText ?? "";
            if (line.VoiceId.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                text.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                line.Data.name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }
        return true;
    }

    private void DrawList()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        string currentFolder = null;
        bool folderOpen = true;
        int shown = 0;

        foreach (FileGroup f in _files)
        {
            var visible = new List<Line>();
            foreach (Line line in f.Lines)
                if (PassFilter(line)) visible.Add(line);

            bool filtering = _filter != Filter.All || _speakerFilter > 0 || !string.IsNullOrEmpty(_search);
            if (filtering && visible.Count == 0) continue;

            // หัวโฟลเดอร์ (00_Intro / 01_Forest / ...)
            if (f.Folder != currentFolder)
            {
                currentFolder = f.Folder;
                if (!_folderOpen.TryGetValue(currentFolder, out folderOpen)) folderOpen = true;
                EditorGUILayout.Space(2f);
                folderOpen = EditorGUILayout.Foldout(folderOpen, $"โฟลเดอร์ {currentFolder}", true, EditorStyles.foldoutHeader);
                _folderOpen[currentFolder] = folderOpen;
            }
            if (!folderOpen) continue;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            int need = 0;
            foreach (Line line in f.Lines) if (line.Status != Status.Done) need++;
            f.Open = EditorGUILayout.Foldout(f.Open, $"{f.Data.name}  ({f.Lines.Count} ประโยค · ขาด {need})", true);
            if (GUILayout.Button("เปิด", GUILayout.Width(44f)))
            {
                Selection.activeObject = f.Data;
                EditorGUIUtility.PingObject(f.Data);
            }
            EditorGUILayout.EndHorizontal();

            if (f.Open)
            {
                EditorGUI.indentLevel++;
                foreach (Line line in visible)
                {
                    shown++;
                    DrawLine(line);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }

        if (shown == 0 && (_filter != Filter.All || _speakerFilter > 0 || !string.IsNullOrEmpty(_search)))
            EditorGUILayout.HelpBox("ไม่มีประโยคตรงกับที่เลือก", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void DrawLine(Line line)
    {
        SubtitleLine raw = line.Data.Lines[line.Index];
        string mark = line.Status == Status.Done ? "<color=#7CD67C>✔</color>"
            : line.Status == Status.Placeholder ? "<color=#E8C35A>●</color>"
            : "<color=#E86A5A>✗</color>";

        EditorGUILayout.LabelField(
            $"{mark} <b>{line.VoiceId}</b>   <color=#9FB4C8>{line.Speaker}</color>   ({line.Seconds:0.#} วิ)\n{raw.DefaultText}", _rich);

        foreach (string problem in line.Problems)
            EditorGUILayout.LabelField($"<color=#FF8A65>⚠ {problem}</color>", _problem);
    }

    private static bool IsFolder(DefaultAsset asset) =>
        asset != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(asset));

    private static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        return total >= 60 ? $"{total / 60} นาที {total % 60} วิ" : $"{total} วิ";
    }

    // ===================== ปุ่ม =====================

    private void LockVoiceIds()
    {
        // กันชนกับ Text Key ที่มีอยู่แล้ว
        var used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (Line line in _lines)
        {
            string key = line.Data.Lines[line.Index].TextKey;
            if (!string.IsNullOrWhiteSpace(key)) used.Add(key.Trim());
        }

        int changed = EditLines((data, i) =>
        {
            if (!string.IsNullOrWhiteSpace(data.Lines[i].TextKey)) return false;

            string key = VoiceId(data, i);
            string unique = key;
            for (char suffix = 'b'; used.Contains(unique) && suffix <= 'z'; suffix++)
                unique = key + suffix;

            used.Add(unique);
            data.Lines[i].TextKey = unique;
            return true;
        }, "ล็อก Voice ID");

        ShowNotification(new GUIContent($"เติม Text Key แล้ว {changed} ประโยค"));
    }

    private void FillPlaceholders()
    {
        string folder = PlaceholderFolderPath();
        if (folder == null) return;

        // 1) สร้างไฟล์เสียงให้ครบทุกตัวละครก่อน — ตอน import Unity แจ้ง project change ห้ามทำระหว่างวนแก้ไฟล์บท
        var speakers = new List<string>();
        foreach (Line line in _lines)
            if (line.Status == Status.Missing && !speakers.Contains(line.Speaker)) speakers.Add(line.Speaker);

        var clips = new Dictionary<string, AudioClip>();
        _busy = true;
        try
        {
            foreach (string speaker in speakers)
                clips[speaker] = GetOrCreatePlaceholder(folder, speaker);
        }
        finally
        {
            _busy = false;
        }

        // 2) ใส่ให้ทุกประโยคที่ยังไม่มีเสียง
        int changed = EditLines((data, i) =>
        {
            if (data.Lines[i].VoiceClip != null) return false;
            if (!clips.TryGetValue(SpeakerName(data.Lines[i]), out AudioClip clip) || clip == null) return false;

            data.Lines[i].VoiceClip = clip;
            return true;
        }, "ใส่ Placeholder เสียงพากย์");

        ShowNotification(new GUIContent($"ใส่ Placeholder แล้ว {changed} ประโยค ({clips.Count} เสียง)"));
    }

    private void RemovePlaceholders()
    {
        int changed = EditLines((data, i) =>
        {
            if (!IsPlaceholder(data.Lines[i].VoiceClip)) return false;
            data.Lines[i].VoiceClip = null;
            return true;
        }, "เอา Placeholder เสียงพากย์ออก");

        ShowNotification(new GUIContent($"เอาออกแล้ว {changed} ประโยค"));
    }

    private void MatchRealVoices()
    {
        string folder = AssetDatabase.GetAssetPath(_voiceFolder);
        var clips = new Dictionary<string, AudioClip>(System.StringComparer.OrdinalIgnoreCase);
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null && !IsPlaceholder(clip)) clips[clip.name.Trim()] = clip;
        }

        var matched = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        int changed = EditLines((data, i) =>
        {
            string id = VoiceId(data, i);
            if (!clips.TryGetValue(id, out AudioClip clip)) return false;
            matched.Add(id);
            if (data.Lines[i].VoiceClip == clip) return false;
            data.Lines[i].VoiceClip = clip;
            return true;
        }, "จับคู่เสียงพากย์จริง");

        // ไฟล์ที่ไม่ตรงกับประโยคไหนเลย = ส่วนใหญ่ชื่อพิมพ์ผิด
        var unmatched = new List<string>();
        foreach (var pair in clips)
            if (!matched.Contains(pair.Key)) unmatched.Add(pair.Key);

        if (unmatched.Count > 0)
            Debug.LogWarning($"[Voice Lines] ไฟล์เสียง {unmatched.Count} ไฟล์ไม่ตรงกับ Voice ID ไหนเลย (ชื่อพิมพ์ผิด?): {string.Join(", ", unmatched)}", _voiceFolder);

        ShowNotification(new GUIContent($"ใส่เสียงจริง {changed} ประโยค · ไฟล์ในโฟลเดอร์ {clips.Count}" +
                                         (unmatched.Count > 0 ? $" · ชื่อไม่ตรง {unmatched.Count} (ดู Console)" : "")));
    }

    // แก้ทีละประโยค + Ctrl+Z ย้อนได้ + เซฟไฟล์
    private int EditLines(System.Func<SubtitleDataSO, int, bool> edit, string undoName)
    {
        // วนจากสำเนา — เซฟไฟล์แล้ว Unity แจ้ง project change ระหว่างวน รายการจริงจะโดนดึงใหม่
        var files = new List<SubtitleDataSO>();
        foreach (FileGroup f in _files)
            if (f.Data != null && f.Data.Lines != null) files.Add(f.Data);

        int changed = 0;
        _busy = true;
        try
        {
            foreach (SubtitleDataSO data in files)
            {
                Undo.RecordObject(data, undoName);
                bool dirty = false;
                for (int i = 0; i < data.Lines.Length; i++)
                {
                    if (!edit(data, i)) continue;
                    dirty = true;
                    changed++;
                }
                if (dirty) EditorUtility.SetDirty(data);
            }

            if (changed > 0) AssetDatabase.SaveAssets();
        }
        finally
        {
            _busy = false;
        }

        Refresh();
        Repaint();
        return changed;
    }

    // ===================== เสียง Placeholder =====================

    private string PlaceholderFolderPath()
    {
        if (IsFolder(_placeholderFolder))
            return AssetDatabase.GetAssetPath(_placeholderFolder);

        // สร้างทีละชั้น Assets/Audio/Voice/_Placeholder
        string path = "Assets";
        foreach (string part in DefaultPlaceholderFolder.Substring("Assets/".Length).Split('/'))
        {
            string next = path + "/" + part;
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(path, part);
            path = next;
        }

        _placeholderFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        SavePref(PrefPlaceholderFolder, _placeholderFolder);
        return path;
    }

    private static AudioClip GetOrCreatePlaceholder(string folder, string speaker)
    {
        string fileName = PlaceholderPrefix + SafeFileName(speaker == NoSpeaker ? "Narration" : speaker);
        string path = $"{folder}/{fileName}.wav";

        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (existing != null) return existing;

        File.WriteAllBytes(path, BuildMumbleWav(speaker));
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static string SafeFileName(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
            sb.Append(ok ? c : '_');
        }
        string result = sb.ToString().Trim('_');
        // ชื่อไทยล้วน — ใช้เลขจากชื่อแทน ยังแยกตัวละครได้
        return result.Length > 0 ? result : "Speaker" + (StableHash(name) % 10000).ToString("0000");
    }

    // hash ที่ได้ค่าเดิมทุกครั้ง (string.GetHashCode เปลี่ยนได้ตามเครื่อง)
    private static uint StableHash(string text)
    {
        uint hash = 2166136261;
        foreach (char c in text)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }

    // เสียง "อึมๆ" 5 พยางค์ ~0.9 วิ — ระดับเสียงต่างกันตามตัวละคร ได้ยินแล้วรู้ว่ายังไม่ใช่เสียงจริง
    private static byte[] BuildMumbleWav(string speaker)
    {
        uint hash = StableHash(speaker);
        float basePitch = 150f + (hash % 1000) / 1000f * 170f;   // 150–320 Hz

        const int syllables = 5;
        const float syllableLength = 0.13f;
        const float step = 0.17f;
        int samples = (int)(WavRate * (step * (syllables - 1) + syllableLength + 0.05f));

        var pitches = new float[syllables];
        for (int s = 0; s < syllables; s++)
        {
            float wobble = ((hash >> (s * 5)) & 31) / 31f - 0.5f;   // ±0.5
            pitches[s] = basePitch * (1f + wobble * 0.3f);
        }
        pitches[syllables - 1] *= 0.85f;   // ท้ายประโยคเสียงลง เหมือนคนพูดจบ

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);            // PCM
            writer.Write((short)1);            // mono
            writer.Write(WavRate);
            writer.Write(WavRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(samples * 2);

            for (int n = 0; n < samples; n++)
            {
                float t = (float)n / WavRate;
                float value = 0f;
                for (int s = 0; s < syllables; s++)
                {
                    float local = t - s * step;
                    if (local < 0f || local > syllableLength) continue;

                    float env = Mathf.Sin(Mathf.PI * local / syllableLength);
                    float phase = 2f * Mathf.PI * pitches[s] * local;
                    // คล้ายสระ "อึม" — ฮาร์มอนิกไม่กี่ตัว ไม่แหลมหู
                    value += env * env * (Mathf.Sin(phase) + 0.45f * Mathf.Sin(phase * 2f) + 0.2f * Mathf.Sin(phase * 3f));
                }
                writer.Write((short)(Mathf.Clamp(value * 0.25f, -1f, 1f) * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }
    }

    // ===================== Export =====================

    private void ExportCsv()
    {
        string folder = EditorPrefs.GetString(ProjectKey(PrefCsvFolder), "");
        string path = EditorUtility.SaveFilePanel("Export รายการเสียงพากย์", folder, "VoiceLines.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;
        EditorPrefs.SetString(ProjectKey(PrefCsvFolder), Path.GetDirectoryName(path));

        // Export ทับไฟล์เดิม = เก็บบทไทย + หมายเหตุที่เติมไว้แล้ว (จับคู่ตาม Voice ID) ไม่ต้องพิมพ์ใหม่
        Dictionary<string, string[]> kept = ReadKeptColumns(path);
        if (kept == null) return;

        var fileOf = new Dictionary<SubtitleDataSO, FileGroup>();
        foreach (FileGroup f in _files) fileOf[f.Data] = f;

        var rows = new List<Line>(_lines);
        if (_exportBySpeaker)
        {
            var order = new Dictionary<Line, int>();
            for (int i = 0; i < rows.Count; i++) order[rows[i]] = i;
            rows.Sort((a, b) =>
            {
                int bySpeaker = string.Compare(a.Speaker, b.Speaker, System.StringComparison.OrdinalIgnoreCase);
                return bySpeaker != 0 ? bySpeaker : order[a].CompareTo(order[b]);
            });
        }

        var csv = new StringBuilder();
        // พากย์ไทย / ซับอังกฤษ — ช่อง "บทพากย์ (ไทย)" เว้นว่างให้คนเขียนบทเติม แล้วส่งให้คนกำกับอัดตามช่องนั้น
        csv.AppendLine("ลำดับ,Voice ID (ตั้งชื่อไฟล์เสียงตามนี้),ตัวละคร,บทพากย์ (ไทย),ซับ (อังกฤษ),ยาวประมาณ (วิ),ฉาก,ไฟล์บทพูด,ประโยคที่,สถานะ,ปัญหา,หมายเหตุคนกำกับ");

        int no = 0;
        foreach (Line line in rows)
        {
            SubtitleLine raw = line.Data.Lines[line.Index];
            FileGroup file = fileOf[line.Data];
            string status = line.Status == Status.Done ? "มีเสียงแล้ว" : line.Status == Status.Placeholder ? "Placeholder" : "ยังไม่มีเสียง";

            csv.Append(++no).Append(',')
               .Append(Csv(line.VoiceId)).Append(',')
               .Append(Csv(line.Speaker)).Append(',')
               .Append(Csv(KeptValue(kept, line.VoiceId, 0))).Append(',')   // บทพากย์ (ไทย) — เติมเอง
               .Append(Csv(raw.DefaultText)).Append(',')
               .Append(line.Seconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
               .Append(Csv(file.Folder)).Append(',')
               .Append(Csv(line.Data.name)).Append(',')
               .Append(line.Index + 1).Append(',')
               .Append(Csv(status)).Append(',')
               .Append(Csv(string.Join(" / ", line.Problems))).Append(',')
               .Append(Csv(KeptValue(kept, line.VoiceId, 1)))
               .AppendLine();
        }

        // BOM — ไม่งั้น Excel อ่านภาษาไทยเพี้ยน
        try
        {
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
        }
        catch (IOException)
        {
            EditorUtility.DisplayDialog("Export ไม่ได้", "ไฟล์นี้เปิดค้างอยู่ใน Excel หรือโปรแกรมอื่น — ปิดก่อนแล้วกดใหม่", "ตกลง");
            return;
        }

        EditorUtility.RevealInFinder(path);
        ShowNotification(new GUIContent($"Export แล้ว {no} ประโยค" + (kept.Count > 0 ? $" (เก็บบทไทยเดิมไว้ {kept.Count})" : "")));
    }

    private static string KeptValue(Dictionary<string, string[]> kept, string voiceId, int column) =>
        kept.TryGetValue(voiceId, out string[] values) ? values[column] : "";

    // อ่านไฟล์ที่จะ Export ทับ — Voice ID → [บทพากย์ (ไทย), หมายเหตุคนกำกับ] / ไม่มีไฟล์ = ว่าง / เปิดไม่ได้ = null (ยกเลิก)
    private static Dictionary<string, string[]> ReadKeptColumns(string path)
    {
        var kept = new Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return kept;

        string text;
        try
        {
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (IOException)
        {
            EditorUtility.DisplayDialog("Export ไม่ได้", "ไฟล์นี้เปิดค้างอยู่ใน Excel หรือโปรแกรมอื่น — ปิดก่อนแล้วกดใหม่", "ตกลง");
            return null;
        }

        List<List<string>> rows = ParseCsv(text);
        if (rows.Count < 2) return kept;

        int id = -1, thai = -1, note = -1;
        List<string> header = rows[0];
        for (int i = 0; i < header.Count; i++)
        {
            string h = header[i].Trim();
            if (h.StartsWith("Voice ID")) id = i;
            else if (h.StartsWith("บทพากย์")) thai = i;
            else if (h.StartsWith("หมายเหตุ")) note = i;
        }
        if (id < 0) return kept;

        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (id >= row.Count || string.IsNullOrWhiteSpace(row[id])) continue;

            string thaiText = thai >= 0 && thai < row.Count ? row[thai] : "";
            string noteText = note >= 0 && note < row.Count ? row[note] : "";
            if (thaiText.Length > 0 || noteText.Length > 0)
                kept[row[id].Trim()] = new[] { thaiText, noteText };
        }
        return kept;
    }

    // อ่าน CSV แบบมีเครื่องหมายคำพูด / ขึ้นบรรทัดใหม่ในช่องได้ (แบบที่ Excel / Google Sheets เซฟ)
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = false;
                }
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Length = 0; }
            else if (c == '\n') { row.Add(cell.ToString()); cell.Length = 0; rows.Add(row); row = new List<string>(); }
            else if (c != '\r' && c != '\uFEFF') cell.Append(c);
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        value = value.Replace("\r\n", "\n");
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private string BuildSummary()
    {
        int need = 0, problems = 0;
        float seconds = 0f;
        foreach (Line line in _lines)
        {
            if (line.Status != Status.Done) need++;
            if (line.Problems.Count > 0) problems++;
            seconds += line.Seconds;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"สรุปเสียงพากย์: ต้องมีทั้งหมด {_lines.Count} ประโยค (ยังไม่มีเสียงจริง {need}) ยาวรวมราว {FormatTime(seconds)}");
        foreach (SpeakerRow row in _speakers)
            sb.AppendLine($"- {row.Name}: {row.Total} ประโยค (ขาด {row.NeedVoice}) ราว {FormatTime(row.Seconds)}");
        if (problems > 0)
            sb.AppendLine($"* มี {problems} ประโยคที่ต้องแก้ก่อนอัด (ดูในตาราง ช่อง \"ปัญหา\")");
        return sb.ToString();
    }
}