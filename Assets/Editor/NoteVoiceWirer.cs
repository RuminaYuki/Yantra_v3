using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// ต่อเสียงอ่านให้โน้ตทุกแผ่นในฉากที่เปิดอยู่ — เมนู Tools > Yantra > Wire Note Voices
// ต้องอยู่ในโฟลเดอร์ชื่อ Editor
//
// ทุก GameObject ที่มี NotaAddition:
//   1. หา Sub_Note_... ที่ข้อความประโยคแรกตรงกับ Content ของโน้ต (ไม่ตรง = ลองเทียบจาก Title)
//   2. ใส่ Subtitle (มีอยู่แล้วใช้ตัวเดิม) แล้วใส่ Subtitle Data
//   3. Interactor Event > On Interact เพิ่ม Subtitle.Play() (มีแล้วไม่เพิ่มซ้ำ)
// กดซ้ำได้ไม่เบิ้ล / Ctrl+Z ย้อนได้ / ไม่เซฟฉากให้ — กด Ctrl+S เอง
public static class NoteVoiceWirer
{
    private const string Prefix = "Sub_Note_";

    [MenuItem("Tools/Yantra/Wire Note Voices (ต่อเสียงโน้ต)")]
    private static void Wire()
    {
        List<SubtitleDataSO> subs = LoadNoteSubtitles();
        if (subs.Count == 0)
        {
            EditorUtility.DisplayDialog("ต่อเสียงโน้ต", "ไม่เจอไฟล์ Subtitle Data ชื่อ Sub_Note_... เลย — กด Fill Note Subtitles ก่อน", "ตกลง");
            return;
        }

        List<MonoBehaviour> notes = FindNotesInOpenScenes();
        if (notes.Count == 0)
        {
            EditorUtility.DisplayDialog("ต่อเสียงโน้ต", "ไม่เจอโน้ต (NotaAddition) ในฉากที่เปิดอยู่", "ตกลง");
            return;
        }

        Undo.SetCurrentGroupName("ต่อเสียงโน้ต");
        int group = Undo.GetCurrentGroup();

        var done = new StringBuilder();
        var failed = new StringBuilder();
        int wired = 0;

        foreach (MonoBehaviour nota in notes)
        {
            GameObject go = nota.gameObject;
            ReadNote(nota, out string title, out string content);
            string label = $"{go.name} ({(string.IsNullOrWhiteSpace(title) ? "ไม่มี Title" : title.Trim())})";

            SubtitleDataSO data = FindSubtitle(subs, title, content);
            if (data == null)
            {
                failed.AppendLine($"✗ {label} — หาบทที่ตรงไม่เจอ");
                continue;
            }

            MonoBehaviour interactorEvent = FindComponent(go, "InteractorEvent");
            UnityEventBase onInteract = interactorEvent != null ? FindOnInteract(interactorEvent) : null;
            if (onInteract == null)
            {
                failed.AppendLine($"✗ {label} — ไม่มี Interactor Event / On Interact");
                continue;
            }

            Subtitle subtitle = go.GetComponent<Subtitle>();
            if (subtitle == null) subtitle = Undo.AddComponent<Subtitle>(go);

            var so = new SerializedObject(subtitle);
            SerializedProperty dataProp = so.FindProperty("_subtitleData");
            if (dataProp != null)
            {
                dataProp.objectReferenceValue = data;
                so.ApplyModifiedProperties();
            }

            bool added = false;
            if (!HasPlay(onInteract, subtitle))
            {
                Undo.RecordObject(interactorEvent, "ต่อเสียงโน้ต");
                UnityEventTools.AddVoidPersistentListener(onInteract, subtitle.Play);
                EditorUtility.SetDirty(interactorEvent);
                if (PrefabUtility.IsPartOfPrefabInstance(interactorEvent))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(interactorEvent);
                added = true;
            }

            EditorSceneManager.MarkSceneDirty(go.scene);
            wired++;
            done.AppendLine($"✓ {label} → {data.name}{(added ? "" : "  (ต่อไว้แล้ว)")}");
        }

        Undo.CollapseUndoOperations(group);

        string message = $"ต่อแล้ว {wired} / {notes.Count} แผ่น\n\n{done}";
        if (failed.Length > 0) message += $"\nยังไม่ได้:\n{failed}";
        message += "\nอย่าลืม Ctrl+S เซฟฉาก";

        Debug.Log("[NoteVoiceWirer]\n" + message);
        EditorUtility.DisplayDialog("ต่อเสียงโน้ต", message, "ตกลง");
    }

    private static List<SubtitleDataSO> LoadNoteSubtitles()
    {
        var subs = new List<SubtitleDataSO>();
        foreach (string guid in AssetDatabase.FindAssets("t:SubtitleDataSO"))
        {
            var data = AssetDatabase.LoadAssetAtPath<SubtitleDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null && data.Lines != null && data.Lines.Length > 0 &&
                data.name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                subs.Add(data);
        }
        return subs;
    }

    private static List<MonoBehaviour> FindNotesInOpenScenes()
    {
        var notes = new List<MonoBehaviour>();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && mb.GetType().Name == "NotaAddition")
                        notes.Add(mb);
        }
        return notes;
    }

    // อ่าน Title / Content ของ NotaAddition โดยไม่ต้องรู้ชื่อตัวแปรเป๊ะ ๆ
    private static void ReadNote(MonoBehaviour nota, out string title, out string content)
    {
        title = "";
        content = "";

        var so = new SerializedObject(nota);
        SerializedProperty p = so.GetIterator();
        while (p.Next(true))
        {
            if (p.propertyType != SerializedPropertyType.String) continue;

            string name = p.name.TrimStart('_', 'm').ToLowerInvariant();
            if (name == "title" && title.Length == 0) title = p.stringValue ?? "";
            else if (name.Contains("content") && content.Length == 0) content = p.stringValue ?? "";
        }
    }

    private static SubtitleDataSO FindSubtitle(List<SubtitleDataSO> subs, string title, string content)
    {
        // 1) เทียบข้อความ — ต้นข้อความโน้ตอยู่ในบทประโยคแรก
        string c = Normalize(content);
        if (c.Length > 0)
        {
            string probe = c.Length > 40 ? c.Substring(0, 40) : c;
            foreach (SubtitleDataSO data in subs)
                if (Normalize(data.Lines[0].DefaultText).Contains(probe))
                    return data;
        }

        // 2) เทียบ Title กับชื่อไฟล์ (Tree Roots → Sub_Note_TreeRoots)
        string t = Normalize(title).Replace(" ", "");
        if (t.Length > 0)
        {
            foreach (SubtitleDataSO data in subs)
            {
                string rest = data.name.Substring(Prefix.Length).ToLowerInvariant();
                if (rest.StartsWith(t) || t.StartsWith(rest))
                    return data;
            }
        }
        return null;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var sb = new StringBuilder(text.Length);
        bool space = false;
        foreach (char raw in text.ToLowerInvariant())
        {
            char c = raw == '’' || raw == '‘' ? '\'' : raw == '“' || raw == '”' ? '"' : raw;
            if (char.IsWhiteSpace(c))
            {
                space = sb.Length > 0;
                continue;
            }
            if (space) sb.Append(' ');
            space = false;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static MonoBehaviour FindComponent(GameObject go, string typeName)
    {
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == typeName)
                return mb;
        return null;
    }

    // ช่อง "On Interact" ใน InteractorEvent — หาจากชื่อตัวแปรที่มีคำว่า interact แต่ไม่ใช่ End
    private static UnityEventBase FindOnInteract(MonoBehaviour component)
    {
        UnityEventBase fallback = null;
        for (Type t = component.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!typeof(UnityEventBase).IsAssignableFrom(f.FieldType)) continue;

                string n = f.Name.ToLowerInvariant();
                if (n.Contains("end") || n.Contains("exit") || n.Contains("cancel")) continue;

                var evt = f.GetValue(component) as UnityEventBase;
                if (evt == null) continue;
                if (n.Contains("interact")) return evt;
                if (fallback == null) fallback = evt;
            }
        }
        return fallback;
    }

    private static bool HasPlay(UnityEventBase evt, Subtitle subtitle)
    {
        for (int i = 0; i < evt.GetPersistentEventCount(); i++)
            if (evt.GetPersistentTarget(i) == subtitle && evt.GetPersistentMethodName(i) == "Play")
                return true;
        return false;
    }
}
