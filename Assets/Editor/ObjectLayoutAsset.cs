using UnityEngine;

namespace CategoryInspector
{
    /// <summary>
    /// ไฟล์เก็บหมวดของ object หนึ่งตัว (หนึ่ง object = หนึ่งไฟล์ กันไฟล์ชนกันตอนหลายคนแก้พร้อมกัน)
    /// อยู่ใน Assets/Editor/CategoryLayouts/Scenes/&lt;ชื่อ scene&gt;/ หรือ Prefabs/&lt;ชื่อ prefab&gt;/
    /// ชื่อไฟล์ .cs ต้องตรงกับชื่อคลาสนี้ ไม่อย่างนั้น Unity จะโหลด ScriptableObject ไม่ได้
    /// </summary>
    public class ObjectLayoutAsset : ScriptableObject
    {
        public ObjectLayout data = new ObjectLayout();
    }
}
