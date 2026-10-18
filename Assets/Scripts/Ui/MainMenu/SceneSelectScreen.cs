using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yantra.UI;

// หน้าเลือก scene — ใช้สคริปต์นี้ได้ 2 หน้า
// SCENE SELECT (dev): Source = Build Settings → ทุก scene ที่อยู่ใน Build Profiles
// Load Game (โชว์): Source = Custom List → เฉพาะ scene ที่ลากใส่ ตั้งชื่อปุ่มเองได้
public class SceneSelectScreen : UIScreen
{
    public enum SceneSource
    {
        [Tooltip("ทุก scene ใน Build Profiles — หน้าเทสของ dev")]
        BuildSettings,

        [Tooltip("เฉพาะ scene ที่ลากใส่ในช่อง Scenes")]
        CustomList,
    }

    [Serializable]
    public class SceneEntry
    {
        [Tooltip("ชื่อบนปุ่ม — เว้นว่าง = ใช้ชื่อไฟล์ scene")]
        public string Label;

#if UNITY_EDITOR
        [Tooltip("ลากไฟล์ scene จาก Project มาใส่\nต้องอยู่ใน Build Profiles ด้วย ไม่งั้นตอน export โหลดไม่ได้")]
        public UnityEditor.SceneAsset Scene;
#endif

        // ไฟล์ scene ใช้ได้แค่ใน Editor — จดชื่อเก็บไว้ ตอน export จะได้มีชื่อติดไปด้วย
        [HideInInspector] public string SceneName;
    }

    [Header("Layout")]
    [Tooltip("ปุ่มต้นแบบ — ต้องปิด active ไว้ ระบบจะก๊อปเอง")]
    [SerializeField] private Button _buttonTemplate;

    [Tooltip("ที่วางปุ่ม ควรมี Vertical Layout Group")]
    [SerializeField] private Transform _buttonContainer;

    [Header("Source")]
    [SerializeField] private SceneSource _source = SceneSource.BuildSettings;

    [Tooltip("ใช้เมื่อ Source = Custom List — เรียงปุ่มตามลำดับนี้")]
    [SerializeField] private SceneEntry[] _scenes;

    [Tooltip("ใช้เมื่อ Source = Build Settings — ไม่แสดง scene ที่ชื่อขึ้นต้นด้วยคำเหล่านี้")]
    [SerializeField] private string[] _hidePrefixes = { "MainMenu" };

    [Header("Info")]
    [SerializeField] private TMP_Text _countText;

    private readonly List<Button> _spawnedButtons = new();
    private bool _built;

    protected override void Awake()
    {
        base.Awake();
        if (_buttonTemplate != null) _buttonTemplate.gameObject.SetActive(false);
    }

    protected override void OnOpening()
    {
        if (!_built) BuildButtons();
    }

    public void OnBackClicked()
    {
        UIManager.Instance.Back();
    }

    private void BuildButtons()
    {
        if (_buttonTemplate == null || _buttonContainer == null)
        {
            Debug.LogError($"[SceneSelectScreen] {name} ยังไม่ได้ใส่ Button Template หรือ Button Container", this);
            return;
        }

        ClearButtons();

        int shown = _source == SceneSource.CustomList ? BuildFromList() : BuildFromBuildSettings();

        if (_countText != null)
            _countText.text = $"{shown} scene";

        if (shown == 0)
            Debug.LogWarning($"[SceneSelectScreen] {name} ไม่มี scene ให้เลือก (Source = {_source}, Scenes = {(_scenes == null ? 0 : _scenes.Length)} แถว)", this);

        _built = true;
    }

    private int BuildFromBuildSettings()
    {
        int shown = 0;

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(path);

            if (ShouldHide(sceneName)) continue;

            CreateButton(sceneName, sceneName);
            shown++;
        }

        return shown;
    }

    private int BuildFromList()
    {
        if (_scenes == null || _scenes.Length == 0)
        {
            Debug.LogWarning($"[SceneSelectScreen] {name}: ลิสต์ Scenes ว่าง — ยังไม่ได้กด + หรือค่าที่ใส่หายไป (ใส่ตอน Play อยู่?)", this);
            return 0;
        }

        int shown = 0;

        for (int i = 0; i < _scenes.Length; i++)
        {
            SceneEntry entry = _scenes[i];

            if (entry == null || string.IsNullOrEmpty(entry.SceneName))
            {
                Debug.LogWarning($"[SceneSelectScreen] {name}: แถวที่ {i + 1} ไม่มีไฟล์ scene — ลากไฟล์ใส่ช่อง Scene", this);
                continue;
            }

            string sceneName = entry.SceneName;

            // กันปุ่มที่กดแล้วไม่ไปไหน — scene ที่ไม่อยู่ใน Build Profiles โหลดไม่ได้ตอน export
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[SceneSelectScreen] {name}: '{sceneName}' ไม่อยู่ใน Build Profiles — ข้ามปุ่มนี้", this);
                continue;
            }

            string label = string.IsNullOrWhiteSpace(entry.Label) ? sceneName : entry.Label;
            CreateButton(label, sceneName);
            shown++;
        }

        return shown;
    }

    private void CreateButton(string label, string sceneName)
    {
        var button = Instantiate(_buttonTemplate, _buttonContainer);
        button.gameObject.SetActive(true);
        button.gameObject.name = "Btn_" + sceneName;

        var text = button.GetComponentInChildren<TMP_Text>();
        if (text != null) text.text = label;

        // capture ค่าไว้ ไม่งั้นทุกปุ่มจะชี้ไป scene สุดท้าย
        string target = sceneName;
        button.onClick.AddListener(() => LoadScene(target));

        _spawnedButtons.Add(button);
    }

    private void LoadScene(string sceneName)
    {
        if (SceneLoader.Instance == null)
        {
            Debug.LogError("[SceneSelectScreen] ไม่พบ SceneLoader", this);
            return;
        }

        SceneLoader.Instance.LoadScene(sceneName);
    }

    private bool ShouldHide(string sceneName)
    {
        if (_hidePrefixes == null) return false;

        foreach (var prefix in _hidePrefixes)
        {
            if (string.IsNullOrEmpty(prefix)) continue;
            if (sceneName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void ClearButtons()
    {
        foreach (var button in _spawnedButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        _spawnedButtons.Clear();
    }

#if UNITY_EDITOR
    // จดชื่อ scene ตอนแก้ใน Inspector — ทำตรงนี้แทนตอนเซฟไฟล์
    // เพราะตอนเซฟ prefab Unity อาจไม่ยอมให้อ่านชื่อไฟล์ ชื่อเลยหายไปเงียบ ๆ
    private void OnValidate()
    {
        if (_scenes == null) return;

        foreach (SceneEntry entry in _scenes)
        {
            if (entry == null) continue;

            string sceneName = entry.Scene != null ? entry.Scene.name : string.Empty;
            if (entry.SceneName != sceneName)
                entry.SceneName = sceneName;
        }
    }
#endif
}