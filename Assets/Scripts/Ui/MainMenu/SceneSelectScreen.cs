using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yantra.UI;

/// <summary>
/// หน้าเลือก scene สำหรับทดสอบ — สร้างปุ่มเองจาก Build Settings
///
/// ไม่ได้อ่านจาก SceneCatalog เพราะถ้าทุกคนเพิ่ม scene ตัวเองลงไฟล์เดียว
/// จะชนกันตอน merge บ่อยมาก ส่วน Build Settings ทุกคนต้องเพิ่มอยู่แล้ว
/// ใครเพิ่ม scene ปุ่มก็โผล่เอง ไม่ต้องแก้อะไรเพิ่ม
///
/// ⚠️ ต้องแปะ EditorOnlyScreen ด้วย ไม่งั้นคนเล่นจะเห็นหน้านี้ใน build
/// </summary>
public class SceneSelectScreen : UIScreen
{
    [Header("Layout")]
    [Tooltip("ปุ่มต้นแบบ — ต้องปิด active ไว้ ระบบจะก๊อปเอง")]
    [SerializeField] private Button _buttonTemplate;

    [Tooltip("ที่วางปุ่ม ควรมี Vertical Layout Group")]
    [SerializeField] private Transform _buttonContainer;

    [Header("Filter")]
    [Tooltip("ไม่แสดง scene ที่ชื่อขึ้นต้นด้วยคำเหล่านี้")]
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
        // สร้างครั้งเดียวพอ รายการ scene ไม่เปลี่ยนระหว่างเล่น
        if (!_built) BuildButtons();
    }

    public void OnBackClicked()
    {
        UIManager.Instance.Back();
    }

    // ---------- ภายใน ----------

    private void BuildButtons()
    {
        if (_buttonTemplate == null || _buttonContainer == null)
        {
            Debug.LogError("[SceneSelectScreen] ยังไม่ได้ใส่ Button Template หรือ Button Container", this);
            return;
        }

        ClearButtons();

        int total = SceneManager.sceneCountInBuildSettings;
        int shown = 0;

        for (int i = 0; i < total; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(path);

            if (ShouldHide(sceneName)) continue;

            CreateButton(sceneName);
            shown++;
        }

        if (_countText != null)
            _countText.text = $"{shown} scene";

        if (shown == 0)
            Debug.LogWarning("[SceneSelectScreen] ไม่มี scene ให้เลือก — เพิ่มใน Build Profiles หรือยัง", this);

        _built = true;
    }

    private void CreateButton(string sceneName)
    {
        var button = Instantiate(_buttonTemplate, _buttonContainer);
        button.gameObject.SetActive(true);
        button.gameObject.name = "Btn_" + sceneName;

        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = sceneName;

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
            if (sceneName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
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
}
