using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// กรอกบทของโน้ตทั้ง 8 แผ่นให้ทีเดียว — เมนู Tools > Yantra > Fill Note Subtitles
// ต้องอยู่ในโฟลเดอร์ชื่อ Editor
//
// หาไฟล์ Subtitle Data ที่ชื่อขึ้นต้นด้วย Sub_Note_ แล้วเขียนทับให้เป็น 2 ประโยค:
//   ประโยค 1 = เสียงอ่านโน้ต (ไม่มี Speaker)      Text Key = note_xxx_01
//   ประโยค 2 = รินพูดหลังอ่าน (Speaker_Rin)      Text Key = note_xxx_02
// มีไฟล์เสียงชื่อตรง (note_xxx_01 หรือชื่อเก่า VO_Note_Xxx_01 / VO_Note_Xxx_Rin_01) = ใส่ให้เลย
// ยังไม่มี = เว้นไว้ ทีหลังใช้ Voice Lines ข้อ ④ จับคู่ได้
// กด Ctrl+Z ย้อนได้
public static class NoteSubtitleFiller
{
    private struct NoteDef
    {
        public string Match;    // ชื่อไฟล์หลัง Sub_Note_ ต้องขึ้นต้นด้วยคำนี้
        public string Key;      // Voice ID
        public string Legacy;   // ชื่อไฟล์เสียงแบบเก่า
        public string Note;     // ข้อความโน้ต (ซับอังกฤษ)
        public string Rin;      // รินพูด (ซับอังกฤษ)
    }

    private static readonly NoteDef[] Notes =
    {
        new NoteDef
        {
            Match = "shrine", Key = "note_shrine", Legacy = "VO_Note_Shrine",
            Note = "Those who travel along this path often stop to pay their respects at this shrine. " +
                   "They believe doing so will ensure that nothing, or 'something', follows them home.",
            Rin = "Better pay my respects. Better safe than sorry."
        },
        new NoteDef
        {
            Match = "kusala", Key = "note_kusala", Legacy = "VO_Note_Kusala",
            Note = "Every sorcerer sees the world differently from ordinary people. They can perceive what others cannot, " +
                   "and these things are known collectively as 'Kusala': the goodness born from good deeds.",
            Rin = "So what I'm seeing right in front of me... that's Kusala."
        },
        new NoteDef
        {
            Match = "tree", Key = "note_treeroots", Legacy = "VO_Note_TreeRoots",
            Note = "Ever since the incident in the village became headline news, no one has returned to this village. " +
                   "But those who know why the village was established still take turns coming here, keeping watch to prevent " +
                   "the power of 'them' from spreading any further.\n" +
                   "Especially the Nang Tani, whose power has been growing stronger with each passing day. " +
                   "They must keep watch, ensuring that its roots do not spread any farther.",
            Rin = "Nang Tani... so someone's still looking after those banana trees."
        },
        new NoteDef
        {
            Match = "diary", Key = "note_diary25", Legacy = "VO_Note_Diary25",
            Note = "Day 25/—/—\n" +
                   "Couldn't find any food again today. Is heaven playing tricks on us? This is supposed to be the season " +
                   "when the fruit ripens, yet there's nothing. The food we stored is running out. And what do the villagers " +
                   "blame? Ghosts and forest spirits, of all things.\n" +
                   "The elders can't think of anything else, even though it's plain to see. It was the flash flood two months " +
                   "ago that ruined almost the entire harvest.\n" +
                   "So what now? Mother has fallen ill, and the hospital is so far away. If this goes on...\n" +
                   "What am I supposed to do...",
            Rin = "I thought it was just a flash flood, nothing to do with ghosts... but in the end, ghosts are real."
        },
        new NoteDef
        {
            Match = "preta", Key = "note_preta", Legacy = "VO_Note_Preta",
            Note = "The hunger of a Preta stems from the karma and greed accumulated through its past deeds. " +
                   "One should make merit by pouring water in dedication to the deceased whenever the opportunity arises.",
            Rin = "I'll try pouring water. Maybe it'll work."
        },
        new NoteDef
        {
            Match = "letter", Key = "note_letter15", Legacy = "VO_Note_Letter15",
            Note = "Date: 15/–/–\n" +
                   "If anyone finds this note, I suppose you've made your way back here. Whatever your reason for coming, " +
                   "turn back. But if you're stubborn enough to keep going, then I can only pray that the sacred spirits protect you.\n" +
                   "You don't have to believe me, but Navin told me to 'look for the 5 unusual trees wrapped in three-colored " +
                   "cloth and seal them.' Believe it or not, it's up to you. Anyone who comes here is bound to die sooner or later anyway.\n" +
                   "That was everything I managed to learn from Navin before he disappeared.",
            Rin = "Wait... there should be five trees, each wrapped in three-colored cloth. I need to find them."
        },
        new NoteDef
        {
            Match = "bullet", Key = "note_bullets", Legacy = "VO_Note_Bullets",
            Note = "In the past, people would draw talismans on paper. The process was cumbersome and time-consuming, " +
                   "so they found a way around it by engraving special characters onto bullets instead. The enchantments " +
                   "engraved on these bullets only affect things imbued with magic. Against anything without magic, " +
                   "they are no different from ordinary bullets.",
            Rin = "It only works on them. That's all I need."
        },
        new NoteDef
        {
            Match = "roots", Key = "note_roots", Legacy = "VO_Note_Roots",
            Note = "These roots are manifestations of Nang Tani's influence. You must fulfill certain conditions to make " +
                   "them disappear. The 5 trees wrapped in three-colored cloth along the village crossroads may be connected to this.",
            Rin = "I have to deal with those trees first. Then these roots should disappear."
        },
    };

    private const string Prefix = "sub_note_";

    [MenuItem("Tools/Yantra/Fill Note Subtitles (ใส่บทโน้ต)")]
    private static void Fill()
    {
        SpeakerSO rin = FindSpeaker();
        if (rin == null)
        {
            EditorUtility.DisplayDialog("ใส่บทโน้ต",
                "หา Speaker ของรินไม่เจอ (ชื่อไฟล์ Speaker_Rin หรือ Default Name = Rin)\n" +
                "สร้างจาก Create > YUKI Learning System > Subtitle > Speaker ก่อน", "ตกลง");
            return;
        }

        // ไฟล์ Subtitle Data ทั้งหมดที่ชื่อขึ้นต้นด้วย Sub_Note_
        var files = new List<SubtitleDataSO>();
        foreach (string guid in AssetDatabase.FindAssets("t:SubtitleDataSO"))
        {
            var data = AssetDatabase.LoadAssetAtPath<SubtitleDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null && data.name.ToLowerInvariant().StartsWith(Prefix)) files.Add(data);
        }

        var targets = new List<KeyValuePair<NoteDef, SubtitleDataSO>>();
        var missing = new List<string>();
        foreach (NoteDef def in Notes)
        {
            SubtitleDataSO data = Match(files, def);
            if (data != null) targets.Add(new KeyValuePair<NoteDef, SubtitleDataSO>(def, data));
            else missing.Add("Sub_Note_" + def.Key.Substring("note_".Length));
        }

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("ใส่บทโน้ต", "ไม่เจอไฟล์ Subtitle Data ที่ชื่อขึ้นต้นด้วย Sub_Note_ เลย", "ตกลง");
            return;
        }

        string list = "";
        foreach (var pair in targets) list += $"• {pair.Value.name}\n";
        string message = $"จะเขียนทับบทใน {targets.Count} ไฟล์นี้ (ของเดิมในไฟล์หายหมด / Ctrl+Z ย้อนได้):\n\n{list}";
        if (missing.Count > 0) message += $"\nหาไม่เจอ (ข้าม): {string.Join(", ", missing)}";

        if (!EditorUtility.DisplayDialog("ใส่บทโน้ต", message, "ใส่เลย", "ยกเลิก"))
            return;

        int voices = 0;
        foreach (var pair in targets)
        {
            NoteDef def = pair.Key;
            SubtitleDataSO data = pair.Value;

            AudioClip noteVoice = FindClip(def.Key + "_01", def.Legacy + "_01");
            AudioClip rinVoice = FindClip(def.Key + "_02", def.Legacy + "_Rin_01");
            if (noteVoice != null) voices++;
            if (rinVoice != null) voices++;

            Undo.RecordObject(data, "ใส่บทโน้ต");
            data.Lines = new[]
            {
                new SubtitleLine
                {
                    Speaker = null,                 // เสียงอ่านในหัว ซับไม่มีชื่อนำหน้า
                    TextKey = def.Key + "_01",
                    DefaultText = def.Note,
                    VoiceClip = noteVoice,
                    FallbackDuration = 0f           // 0 = เดาเวลาจากความยาวข้อความ
                },
                new SubtitleLine
                {
                    Speaker = rin,
                    TextKey = def.Key + "_02",
                    DefaultText = def.Rin,
                    VoiceClip = rinVoice,
                    FallbackDuration = 0f
                }
            };
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("ใส่บทโน้ต",
            $"ใส่บทแล้ว {targets.Count} ไฟล์ ({targets.Count * 2} ประโยค)\n" +
            $"เจอไฟล์เสียงชื่อตรง ใส่ให้แล้ว {voices} ไฟล์\n\n" +
            "ที่เหลือ: ตั้งชื่อไฟล์เสียงตาม Text Key (เช่น note_shrine_01) แล้วใช้ Voice Lines ข้อ ④ จับคู่", "ตกลง");
    }

    private static SubtitleDataSO Match(List<SubtitleDataSO> files, NoteDef def)
    {
        foreach (SubtitleDataSO data in files)
        {
            string rest = data.name.ToLowerInvariant().Substring(Prefix.Length);
            if (rest.StartsWith(def.Match)) return data;
        }
        return null;
    }

    private static SpeakerSO FindSpeaker()
    {
        SpeakerSO byDefaultName = null;
        foreach (string guid in AssetDatabase.FindAssets("t:SpeakerSO"))
        {
            var speaker = AssetDatabase.LoadAssetAtPath<SpeakerSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (speaker == null) continue;
            if (string.Equals(speaker.name, "Speaker_Rin", System.StringComparison.OrdinalIgnoreCase)) return speaker;
            if (byDefaultName == null && string.Equals(speaker.DefaultName, "Rin", System.StringComparison.OrdinalIgnoreCase))
                byDefaultName = speaker;
        }
        return byDefaultName;
    }

    private static AudioClip FindClip(params string[] names)
    {
        foreach (string name in names)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{name} t:AudioClip"))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null && string.Equals(clip.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return clip;
            }
        }
        return null;
    }
}
