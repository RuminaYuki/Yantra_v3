using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

// ต้องอยู่ในโฟลเดอร์ Editor
// ใช้ได้ทั้งตอนไม่ได้กด Play และตอน Play: ซ่อน Renderer ทีละกลุ่ม (แยกตาม shader) แล้วดูว่า error หยุดไหม
// ใช้ forceRenderingOff ซึ่งไม่ถูกเซฟลงฉาก ฉากจึงไม่เปลี่ยน
// นับ error 2 ทาง: จาก log callback และอ่านตัวเลขจากหน้า Console ตรงๆ
// (error จากการ์ดจอบางตัวไม่ผ่าน callback แต่ไปโผล่ใน Console)
public class RenderErrorFinder : EditorWindow
{
    private enum Phase { Idle, Baseline, Testing, AllOff, Done }

    private sealed class Group
    {
        public string Shader;
        public readonly List<Renderer> Renderers = new List<Renderer>();
        public int Errors = -1;
    }

    private string _match = "Texture index out of range";
    private float _settleSeconds = 0.3f;
    private float _measureSeconds = 1f;

    private static int _callbackCount;
    private static volatile string _matchCache = "";

    private Phase _phase = Phase.Idle;
    private readonly List<Group> _groups = new List<Group>();
    private readonly List<Renderer> _disabled = new List<Renderer>();
    private int _index;
    private double _stepStartTime;
    private double _measureStartTime;
    private int _callbackAtMeasure;
    private int _consoleAtMeasure;
    private bool _measuring;
    private string _status = "เปิดหน้า Scene ให้เห็นจุดที่ error ขึ้น แล้วกดเริ่มค้นหา";
    private Vector2 _scroll;

    private double _nextLiveRead;
    private int _liveConsole = -1;
    private int _liveRate = -1;
    private int _prevLiveConsole = -1;
    private bool _liveRepaint = true;

    [MenuItem("Tools/Debug/หาตัวการ Texture index out of range")]
    private static void Open() => GetWindow<RenderErrorFinder>("หาตัวการ DX11");

    private void OnEnable()
    {
        Application.logMessageReceivedThreaded += OnLog;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        Application.logMessageReceivedThreaded -= OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        RestoreDisabled();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        string match = _matchCache;
        if (!string.IsNullOrEmpty(match) && message != null && message.Contains(match))
            Interlocked.Increment(ref _callbackCount);
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingPlayMode && change != PlayModeStateChange.ExitingEditMode)
            return;
        RestoreDisabled();
        if (_phase != Phase.Done && _phase != Phase.Idle)
        {
            _phase = Phase.Idle;
            _status = "หยุดกลางคันเพราะเปลี่ยนโหมด Play";
        }
        Repaint();
    }

    private bool Running => _phase == Phase.Baseline || _phase == Phase.Testing || _phase == Phase.AllOff;

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ข้อความ error ที่จะนับ");
        _match = EditorGUILayout.TextField(_match);
        _settleSeconds = Mathf.Max(0.1f, EditorGUILayout.FloatField("รอหลังซ่อน (วินาที)", _settleSeconds));
        _measureSeconds = Mathf.Max(0.3f, EditorGUILayout.FloatField("นับ error (วินาที)", _measureSeconds));

        EditorGUILayout.Space();
        if (!ConsoleReader.Available)
            EditorGUILayout.HelpBox("อ่านหน้า Console ไม่ได้ ใช้แค่ log callback นับ", MessageType.Warning);
        else if (_liveConsole < 0)
            EditorGUILayout.HelpBox("ยังไม่เจอข้อความนี้ใน Console (ช่องค้นหาของ Console ต้องว่าง)", MessageType.None);
        else
            EditorGUILayout.HelpBox($"ใน Console ตอนนี้: {_liveConsole} ครั้ง  (เพิ่มขึ้นประมาณ {Mathf.Max(0, _liveRate)} ครั้ง/วินาที)", MessageType.None);

        _liveRepaint = EditorGUILayout.Toggle("สั่งหน้า Scene วาดใหม่ตลอด", _liveRepaint);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(Running))
        {
            if (GUILayout.Button("เริ่มค้นหา", GUILayout.Height(30)))
                Begin();
        }
        using (new EditorGUI.DisabledScope(!Running))
        {
            if (GUILayout.Button("หยุด"))
            {
                RestoreDisabled();
                _phase = Phase.Idle;
                _status = "หยุดแล้ว";
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(_status, MessageType.Info);

        if (_phase == Phase.Testing && _groups.Count > 0)
        {
            Rect rect = GUILayoutUtility.GetRect(18, 18, "TextField");
            EditorGUI.ProgressBar(rect, (float)_index / _groups.Count, $"{_index}/{_groups.Count}  {_groups[_index].Shader}");
        }

        DrawResults();
    }

    private void DrawResults()
    {
        var culprits = _groups.Where(g => g.Errors == 0).ToList();
        if (culprits.Count == 0)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("ซ่อนกลุ่มนี้แล้ว error หยุด = ตัวการ", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (Group group in culprits)
        {
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                int alive = group.Renderers.Count(r => r != null);
                EditorGUILayout.LabelField($"{group.Shader}  ({alive} ชิ้น)");
                if (GUILayout.Button("เลือกวัตถุ", GUILayout.Width(80)))
                {
                    Selection.objects = group.Renderers.Where(r => r != null).Select(r => (Object)r.gameObject).ToArray();
                    if (Selection.activeObject != null)
                        EditorGUIUtility.PingObject(Selection.activeObject);
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void Begin()
    {
        RestoreDisabled();
        _matchCache = _match;
        _groups.Clear();

        var byShader = new Dictionary<string, Group>();
        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy)
                continue;
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null)
                    continue;
                string key = material.shader.name;
                if (!byShader.TryGetValue(key, out Group group))
                {
                    group = new Group { Shader = key };
                    byShader.Add(key, group);
                }
                if (!group.Renderers.Contains(renderer))
                    group.Renderers.Add(renderer);
            }
        }

        // shader ที่ทีมทำเอง (Shader Graph) ลองก่อน มีโอกาสเป็นตัวการมากกว่า
        _groups.AddRange(byShader.Values
            .OrderByDescending(g => g.Shader.StartsWith("Shader Graphs"))
            .ThenBy(g => g.Shader));

        _phase = Phase.Baseline;
        _status = "กำลังนับ error ตอนยังไม่ได้ซ่อนอะไร...";
        StartStep();
    }

    private void StartStep()
    {
        _stepStartTime = EditorApplication.timeSinceStartup;
        _measuring = false;
    }

    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;

        if (!Running)
        {
            if (_liveRepaint)
                InternalEditorUtility.RepaintAllViews();
            UpdateLive(now);
            return;
        }

        // ไม่ได้กด Play หน้าจอไม่วาดใหม่เอง ต้องสั่งให้วาดทุกรอบ error ถึงจะขึ้นให้นับ
        InternalEditorUtility.RepaintAllViews();

        if (!_measuring)
        {
            if (now - _stepStartTime < _settleSeconds)
                return;
            _measuring = true;
            _measureStartTime = now;
            _callbackAtMeasure = Interlocked.CompareExchange(ref _callbackCount, 0, 0);
            _consoleAtMeasure = ConsoleReader.Count(_match);
            return;
        }

        if (now - _measureStartTime < _measureSeconds)
            return;

        int fromCallback = Interlocked.CompareExchange(ref _callbackCount, 0, 0) - _callbackAtMeasure;
        int consoleNow = ConsoleReader.Count(_match);
        int fromConsole = (_consoleAtMeasure >= 0 && consoleNow >= _consoleAtMeasure) ? consoleNow - _consoleAtMeasure : 0;
        OnStepDone(Mathf.Max(fromCallback, fromConsole));
        Repaint();
    }

    private void UpdateLive(double now)
    {
        if (now < _nextLiveRead)
            return;
        _nextLiveRead = now + 1.0;
        _matchCache = _match;
        int count = ConsoleReader.Count(_match);
        _liveRate = (_prevLiveConsole >= 0 && count >= _prevLiveConsole) ? count - _prevLiveConsole : -1;
        _prevLiveConsole = count;
        if (count != _liveConsole || _liveRate >= 0)
        {
            _liveConsole = count;
            Repaint();
        }
    }

    private void OnStepDone(int errors)
    {
        switch (_phase)
        {
            case Phase.Baseline:
                if (errors == 0)
                {
                    _phase = Phase.Idle;
                    _status = "ตอนนี้ error ไม่ได้ขึ้นเพิ่ม ดูบรรทัด \"ใน Console ตอนนี้\" ด้านบน ถ้าตัวเลขไม่วิ่ง แปลว่า error ไม่ได้เกิดตอนนี้ ลองหันกล้อง Scene ไปจุดอื่น หรือกด Play แล้วค้นหาตอนเล่นแทน";
                    return;
                }
                _phase = Phase.Testing;
                _index = 0;
                DisableGroup(_groups[0]);
                _status = $"เจอ error {errors} ครั้งใน {_measureSeconds:0.#} วินาที กำลังไล่ทีละ shader...";
                StartStep();
                break;

            case Phase.Testing:
                _groups[_index].Errors = errors;
                RestoreDisabled();
                _index++;
                if (_index < _groups.Count)
                {
                    DisableGroup(_groups[_index]);
                    StartStep();
                    return;
                }
                if (_groups.Any(g => g.Errors == 0))
                {
                    _phase = Phase.Done;
                    _status = "เสร็จแล้ว ดูรายชื่อด้านล่าง";
                    return;
                }
                _phase = Phase.AllOff;
                _status = "ซ่อนทีละกลุ่มแล้ว error ไม่หยุดสักกลุ่ม กำลังลองซ่อนทุกอย่างพร้อมกัน...";
                foreach (Group group in _groups)
                    DisableGroup(group);
                StartStep();
                break;

            case Phase.AllOff:
                RestoreDisabled();
                _phase = Phase.Done;
                _status = errors == 0
                    ? "ซ่อนทุกชิ้นแล้ว error หยุด แปลว่ามีหลาย shader เป็นตัวการร่วมกัน ส่งรูปหน้านี้มาให้ดู"
                    : "ซ่อนทุกชิ้นแล้ว error ยังขึ้น แปลว่าไม่ได้มาจากวัตถุในฉาก น่าจะมาจาก Volume (Fog, Post Process), การแสดงผล Debug ของหน้า Scene หรือ HDRP Asset";
                break;
        }
    }

    private void DisableGroup(Group group)
    {
        foreach (Renderer renderer in group.Renderers)
        {
            if (renderer == null || renderer.forceRenderingOff)
                continue;
            renderer.forceRenderingOff = true;
            _disabled.Add(renderer);
        }
    }

    private void RestoreDisabled()
    {
        foreach (Renderer renderer in _disabled)
            if (renderer != null)
                renderer.forceRenderingOff = false;
        _disabled.Clear();
    }

    // อ่านแถวในหน้า Console ผ่าน API ภายในของ Unity
    private static class ConsoleReader
    {
        private const int CollapseFlag = 1;
        private static readonly MethodInfo _start, _end, _get, _getCount;
        private static readonly PropertyInfo _flags;
        private static readonly FieldInfo _message;
        private static readonly Type _entryType;

        public static readonly bool Available;

        static ConsoleReader()
        {
            try
            {
                Type entries = FindType("UnityEditor.LogEntries");
                _entryType = FindType("UnityEditor.LogEntry");
                if (entries == null || _entryType == null)
                    return;
                const BindingFlags s = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                _start = entries.GetMethod("StartGettingEntries", s);
                _end = entries.GetMethod("EndGettingEntries", s);
                _get = entries.GetMethod("GetEntryInternal", s);
                _getCount = entries.GetMethod("GetEntryCount", s);
                _flags = entries.GetProperty("consoleFlags", s);
                _message = _entryType.GetField("message", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Available = _start != null && _end != null && _get != null && _message != null;
            }
            catch
            {
                Available = false;
            }
        }

        private static Type FindType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(name);
                if (type != null)
                    return type;
            }
            return null;
        }

        // คืนจำนวนครั้งรวมของข้อความนี้ใน Console (-1 = อ่านไม่ได้หรือไม่เจอ)
        public static int Count(string match)
        {
            if (!Available || string.IsNullOrEmpty(match))
                return -1;
            try
            {
                bool collapsed = _flags != null && _getCount != null && (((int)_flags.GetValue(null)) & CollapseFlag) != 0;
                object entry = Activator.CreateInstance(_entryType, true);
                var args = new object[2];
                var countArgs = new object[1];
                int total = 0;
                bool found = false;
                int rows = (int)_start.Invoke(null, null);
                try
                {
                    for (int i = 0; i < rows; i++)
                    {
                        args[0] = i;
                        args[1] = entry;
                        _get.Invoke(null, args);
                        if (!(_message.GetValue(entry) is string message) || !message.Contains(match))
                            continue;
                        found = true;
                        if (collapsed)
                        {
                            countArgs[0] = i;
                            total += (int)_getCount.Invoke(null, countArgs);
                        }
                        else
                        {
                            total++;
                        }
                    }
                }
                finally
                {
                    _end.Invoke(null, null);
                }
                return found ? total : -1;
            }
            catch
            {
                return -1;
            }
        }
    }
}