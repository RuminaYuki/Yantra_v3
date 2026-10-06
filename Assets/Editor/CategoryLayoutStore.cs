using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CategoryInspector
{
    /// <summary>
    /// จัดการไฟล์หมวดแยกต่อ object
    /// - object ใน scene → CategoryLayouts/Scenes/&lt;ชื่อ scene&gt;/&lt;path ของ object&gt;.asset
    /// - prefab        → CategoryLayouts/Prefabs/&lt;ชื่อ prefab&gt;/&lt;path ภายใน prefab&gt;.asset
    /// ชื่อไฟล์มีไว้ให้คนอ่านเท่านั้น ตัวที่ใช้จับคู่จริงคือ data.key ข้างในไฟล์ (เปลี่ยนชื่อ object ทีหลังก็ยังหาเจอ)
    /// </summary>
    static class CategoryLayoutStore
    {
        public const string Root = "Assets/Editor/CategoryLayouts";
        const string LegacyAssetPath = "Assets/Editor/ComponentCategoryLayout.asset";
        const string ShowEmptyPref = "CategoryInspector.showEmptyCategories";

        static Dictionary<string, ObjectLayoutAsset> index;   // key → ไฟล์ (ล้างเมื่อมีไฟล์ในโฟลเดอร์เปลี่ยน)

        /// <summary>ตั้งค่าส่วนตัวของแต่ละคน เก็บใน EditorPrefs จะได้ไม่ต้องแก้ไฟล์ที่แชร์กัน</summary>
        public static bool ShowEmptyCategories
        {
            get => EditorPrefs.GetBool(ShowEmptyPref, true);
            set => EditorPrefs.SetBool(ShowEmptyPref, value);
        }

        public static void Invalidate() => index = null;

        public static IEnumerable<ObjectLayoutAsset> All
        {
            get { EnsureIndex(); return index.Values; }
        }

        public static ObjectLayoutAsset Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            EnsureIndex();
            if (!index.TryGetValue(key, out var a)) return null;
            if (a != null) return a;

            // ไฟล์ถูกลบไปแล้วแต่ index ยังไม่รู้ → สร้าง index ใหม่แล้วหาอีกรอบ
            Invalidate();
            EnsureIndex();
            return index.TryGetValue(key, out a) ? a : null;
        }

        /// <param name="group">โฟลเดอร์ย่อย เช่น "Scenes/Level1" หรือ "Prefabs/Player"</param>
        public static ObjectLayoutAsset Create(string key, string displayName, string group)
        {
            var existing = Get(key);
            if (existing != null) return existing;

            string folder = Root + "/" + (string.IsNullOrEmpty(group) ? "Other" : group);
            EnsureFolder(folder);

            var a = ScriptableObject.CreateInstance<ObjectLayoutAsset>();
            a.data = new ObjectLayout { key = key, displayName = displayName };
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + FileNameFor(displayName) + ".asset");
            AssetDatabase.CreateAsset(a, path);
            AssetDatabase.SaveAssetIfDirty(a);

            EnsureIndex();
            index[key] = a;
            return a;
        }

        public static void Delete(ObjectLayoutAsset a)
        {
            if (a == null) return;
            string path = AssetDatabase.GetAssetPath(a);
            if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
            Invalidate();
        }

        public static void Save(ObjectLayoutAsset a)
        {
            if (a == null) return;
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssetIfDirty(a);
        }

        static void EnsureIndex()
        {
            if (index != null) return;
            index = new Dictionary<string, ObjectLayoutAsset>();
            if (!AssetDatabase.IsValidFolder(Root)) return;

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(ObjectLayoutAsset), new[] { Root }))
            {
                var a = AssetDatabase.LoadAssetAtPath<ObjectLayoutAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (a == null || a.data == null || string.IsNullOrEmpty(a.data.key)) continue;
                if (!index.ContainsKey(a.data.key)) index[a.data.key] = a;   // ถ้ามีซ้ำ ใช้ตัวแรก
            }
        }

        static string FileNameFor(string displayName)
        {
            string s = string.IsNullOrEmpty(displayName) ? "Object" : displayName.Replace('/', '_');
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Trim();
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ------------------------------------------------------------------
        // ย้ายข้อมูลจากไฟล์รวมแบบเก่า (ComponentCategoryLayout.asset) มาเป็นไฟล์แยก
        // ------------------------------------------------------------------

        public static void MigrateLegacy()
        {
            var legacy = AssetDatabase.LoadAssetAtPath<ComponentCategoryLayout>(LegacyAssetPath);
            if (legacy == null) return;

            ShowEmptyCategories = legacy.showEmptyCategories;
            int moved = 0;
            foreach (var o in legacy.objects)
            {
                if (o == null || string.IsNullOrEmpty(o.key)) continue;
                if (o.categories.Count == 0 && o.assignments.Count == 0) continue;
                if (Get(o.key) != null) continue;

                var a = Create(o.key, o.displayName, LegacyGroup(o));
                a.data.categories = o.categories;
                a.data.assignments = o.assignments;
                Save(a);
                moved++;
            }

            AssetDatabase.DeleteAsset(LegacyAssetPath);
            Invalidate();
            Debug.Log($"[Category Inspector] Migrated {moved} object layout(s) from {LegacyAssetPath} to {Root}/");
        }

        static string LegacyGroup(ObjectLayout o)
        {
            if (o.key.StartsWith("prefab:"))
            {
                string guid = o.key.Substring("prefab:".Length).Split('/')[0];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                return "Prefabs/" + (string.IsNullOrEmpty(path) ? FirstSegment(o.displayName) : Path.GetFileNameWithoutExtension(path));
            }
            if (o.key.StartsWith("scene:") && GlobalObjectId.TryParse(o.key.Substring("scene:".Length), out var id))
            {
                string path = AssetDatabase.GUIDToAssetPath(id.assetGUID);
                if (!string.IsNullOrEmpty(path)) return "Scenes/" + Path.GetFileNameWithoutExtension(path);
            }
            return "Scenes/Untitled";
        }

        static string FirstSegment(string s) =>
            string.IsNullOrEmpty(s) ? "Unknown" : s.Split('/').First();

        /// <summary>มีไฟล์ในโฟลเดอร์หมวดถูกเพิ่ม/ลบ/ย้าย (เช่น git pull) → ล้าง index</summary>
        class Watcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(p => p.StartsWith(Root)))
                    Invalidate();
            }
        }
    }
}
