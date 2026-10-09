using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CategoryInspector
{
    /// <summary>
    /// หมวดหมู่หนึ่งหมวด
    /// เก็บเป็น flat list + parentId (แทนการซ้อนคลาสในตัวเอง) เพราะ Unity serialize คลาสที่อ้างถึงตัวเองซ้อนลึก ๆ ได้ไม่ดี
    /// </summary>
    [Serializable]
    public class CategoryNode
    {
        public string id = Guid.NewGuid().ToString("N");
        public string parentId = "";          // ว่าง = หมวดระดับบนสุด
        public string name = "New Category";
        public int order;                      // ลำดับเทียบกับหมวดพี่น้อง

        // ---- สไตล์ ----
        public Color headerColor = new Color(0.20f, 0.36f, 0.58f, 1f);
        public Color textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        public int fontSize = 13;
        public bool bold = true;
        public string builtinIcon = "Folder Icon";   // ชื่อไอคอน built-in ของ Unity (ว่าง = ไม่มี)
        public Texture2D customIcon;                 // ถ้าใส่ จะใช้แทน builtinIcon
    }

    /// <summary>จับคู่ "ชนิดของ component" กับหมวด (ภายใน object เดียว)</summary>
    [Serializable]
    public class TypeAssignment
    {
        public string typeName;
        public string categoryId;
        public int order;                      // ลำดับภายในหมวด
    }

    /// <summary>
    /// ชุดหมวดของ object หนึ่งตัว
    /// - prefab: ทุก instance ของ prefab เดียวกันใช้ชุดเดียวกัน (รวมตอนเปิด Prefab Mode)
    /// - object ธรรมดาใน scene: แยกเป็นของตัวเอง
    /// </summary>
    [Serializable]
    public class ObjectLayout
    {
        public string key;            // ตัวระบุ object (สร้างใน CategoryInspectorWindow.LayoutKey)
        public string displayName;    // ไว้ดูว่าเป็นของ object ไหน
        public List<CategoryNode> categories = new List<CategoryNode>();
        public List<TypeAssignment> assignments = new List<TypeAssignment>();

        public CategoryNode Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return categories.Find(c => c.id == id);
        }

        /// <summary>หมวดลูกโดยตรง เรียงตาม order (ถ้า parentId ว่าง จะรวมหมวดที่พ่อหายไปแล้วด้วย กันหมวดกำพร้า)</summary>
        public List<CategoryNode> ChildrenOf(string parentId)
        {
            string p = parentId ?? "";
            IEnumerable<CategoryNode> q = p.Length == 0
                ? categories.Where(c => string.IsNullOrEmpty(c.parentId) || Find(c.parentId) == null)
                : categories.Where(c => c.parentId == p);
            return q.OrderBy(c => c.order).ToList();
        }

        /// <summary>node เป็นตัว ancestorId เอง หรืออยู่ใต้มันหรือไม่</summary>
        public bool IsSelfOrDescendant(CategoryNode node, string ancestorId)
        {
            var cur = node;
            int guard = 0;
            while (cur != null && guard++ < 256)
            {
                if (cur.id == ancestorId) return true;
                cur = Find(cur.parentId);
            }
            return false;
        }

        public static string TypeKey(Type t) => t.FullName;

        /// <summary>
        /// key ของ component รายตัว = ชื่อ type + ลำดับในบรรดาตัวที่เป็น type เดียวกันบน object
        /// เช่น AudioSource ตัวแรก = "UnityEngine.AudioSource#0", ตัวที่สอง = "#1"
        /// </summary>
        public static string ComponentKey(Component c)
        {
            var t = c.GetType();
            int index = 0;
            foreach (var other in c.gameObject.GetComponents<Component>())
            {
                if (other == c) break;
                if (other != null && other.GetType() == t) index++;
            }
            return TypeKey(t) + "#" + index;
        }

        public TypeAssignment GetAssignment(Component c)
        {
            if (c == null) return null;
            string k = ComponentKey(c);
            var a = assignments.Find(x => x.typeName == k);

            // ข้อมูลรุ่นก่อนเก็บแค่ชื่อ type → ถือว่าเป็นของตัวแรก
            if (a == null && k.EndsWith("#0"))
            {
                string legacy = TypeKey(c.GetType());
                a = assignments.Find(x => x.typeName == legacy);
            }
            return a;
        }

        public string GetPath(CategoryNode node, string separator = " / ")
        {
            var parts = new List<string>();
            var cur = node;
            int guard = 0;
            while (cur != null && guard++ < 256)
            {
                parts.Insert(0, cur.name);
                cur = Find(cur.parentId);
            }
            return string.Join(separator, parts);
        }
    }

    /// <summary>
    /// [รุ่นเก่า] ไฟล์รวมหมวดของทุก object ไว้ไฟล์เดียว — เก็บคลาสไว้เพื่อย้ายข้อมูลเท่านั้น
    /// ตอนเปิด Category Inspector จะแยกเป็นไฟล์ต่อ object (ObjectLayoutAsset) แล้วลบไฟล์นี้ทิ้ง (ดู CategoryLayoutStore.MigrateLegacy)
    /// ชื่อไฟล์ .cs ต้องตรงกับชื่อคลาสนี้ ไม่อย่างนั้น Unity จะโหลด ScriptableObject ไม่ได้
    /// </summary>
    public class ComponentCategoryLayout : ScriptableObject
    {
        public List<ObjectLayout> objects = new List<ObjectLayout>();
        public bool showEmptyCategories = true;

        public ObjectLayout Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return objects.Find(o => o.key == key);
        }

        public ObjectLayout GetOrCreate(string key, string displayName)
        {
            var o = Get(key);
            if (o == null)
            {
                o = new ObjectLayout { key = key, displayName = displayName };
                objects.Add(o);
            }
            else if (!string.IsNullOrEmpty(displayName))
            {
                o.displayName = displayName;
            }
            return o;
        }
    }
}