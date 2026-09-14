using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneCatalog", menuName = "Yantra/Scene Catalog")]
public class SceneCatalog : ScriptableObject
{
    [System.Serializable]
    public class NamedScene
    {
        [Tooltip("ชื่อเรียกที่ใช้ในโค้ด เช่น Level02, Boss, Tutorial\n" +
                 "ตั้งให้สื่อความหมาย ไม่ต้องตรงกับชื่อไฟล์ก็ได้")]
        public string key;

        [Tooltip("ลากไฟล์ scene จาก Project มาใส่")]
        public SceneReference scene;
    }

    [Header("Scene หลัก")]
    [Tooltip("หน้าเมนูหลัก")]
    [SerializeField] private SceneReference _mainMenu;

    [Tooltip("scene ที่กด New Game แล้วเข้า — ตัวจริงที่ส่งงาน")]
    [SerializeField] private SceneReference _gameplay;

    [Header("Scene ทดสอบ (ไม่กระทบปุ่ม New Game)")]
    [Tooltip("scene ที่ใช้ทดสอบระบบของตัวเอง\n" +
             "แยกช่องไว้เพื่อไม่ต้องสลับไปมา จะได้ไม่มีทางลืมสลับกลับก่อน commit\n" +
             "เว้นว่างได้ถ้าไม่ได้ใช้")]
    [SerializeField] private SceneReference _testGameplay;

    [Header("Scene อื่น ๆ")]
    [Tooltip("กด + เพื่อเพิ่ม scene ใหม่ได้เรื่อย ๆ โดยไม่ต้องแก้โค้ด\n" +
             "เรียกใช้: SceneLoader.Instance.LoadSceneByKey(\"ชื่อที่ตั้งไว้\")")]
    [SerializeField] private List<NamedScene> _additionalScenes = new();

    // ---------- Scene หลัก ----------

    public string MainMenu => _mainMenu.SceneName;
    public string Gameplay => _gameplay.SceneName;

    /// <summary>scene ทดสอบ — ถ้าไม่ได้ใส่ไว้จะคืนค่า scene จริงแทน</summary>
    public string TestGameplay => _testGameplay.IsValid ? _testGameplay.SceneName : _gameplay.SceneName;

    public bool HasTestScene => _testGameplay.IsValid;

    public bool IsMainMenu(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && sceneName == _mainMenu.SceneName;
    }

    // ---------- Scene อื่น ๆ ----------

    /// <summary>หาชื่อ scene จาก key ที่ตั้งไว้ในลิสต์</summary>
    public bool TryGetScene(string key, out string sceneName)
    {
        sceneName = null;
        if (string.IsNullOrEmpty(key)) return false;

        foreach (var entry in _additionalScenes)
        {
            if (entry == null) continue;
            if (entry.key != key) continue;
            if (!entry.scene.IsValid) continue;

            sceneName = entry.scene.SceneName;
            return true;
        }

        return false;
    }

    /// <summary>รายชื่อ key ทั้งหมด — ใช้ตอน debug ว่ามีอะไรให้เรียกบ้าง</summary>
    public IReadOnlyList<NamedScene> AdditionalScenes => _additionalScenes;

#if UNITY_EDITOR
    /// <summary>
    /// ตรวจให้ตั้งแต่ตอนแก้ใน Editor ไม่ต้องรอไปเจอตอนรัน
    /// </summary>
    private void OnValidate()
    {
        if (!_mainMenu.IsValid)
            Debug.LogWarning($"[{name}] ยังไม่ได้ใส่ scene เมนูหลัก", this);

        if (!_gameplay.IsValid)
            Debug.LogWarning($"[{name}] ยังไม่ได้ใส่ scene เกม", this);

        var seenKeys = new HashSet<string>();

        for (int i = 0; i < _additionalScenes.Count; i++)
        {
            var entry = _additionalScenes[i];
            if (entry == null) continue;

            if (string.IsNullOrEmpty(entry.key))
            {
                Debug.LogWarning($"[{name}] Scene อื่น ๆ ช่องที่ {i + 1} ยังไม่ได้ตั้งชื่อเรียก", this);
                continue;
            }

            if (!seenKeys.Add(entry.key))
                Debug.LogWarning($"[{name}] ชื่อเรียก '{entry.key}' ซ้ำกัน — ตัวแรกจะถูกใช้", this);

            if (!entry.scene.IsValid)
                Debug.LogWarning($"[{name}] '{entry.key}' ยังไม่ได้ใส่ไฟล์ scene", this);
        }
    }
#endif
}