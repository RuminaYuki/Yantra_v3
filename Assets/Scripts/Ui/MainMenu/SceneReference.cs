using UnityEngine;

[System.Serializable]
public struct SceneReference
{
#if UNITY_EDITOR
    [Tooltip("ลากไฟล์ scene จากหน้าต่าง Project มาใส่ตรงนี้")]
    [SerializeField] private UnityEditor.SceneAsset _sceneAsset;
#endif

    [SerializeField, HideInInspector] private string _sceneName;
    [SerializeField, HideInInspector] private string _scenePath;

    /// <summary>ชื่อ scene สำหรับส่งให้ SceneManager.LoadScene</summary>
    public string SceneName => _sceneName;

    /// <summary>พาธเต็มของไฟล์ — ใช้ตอน debug</summary>
    public string ScenePath => _scenePath;

    /// <summary>มีการใส่ scene ไว้จริงไหม</summary>
    public bool IsValid => !string.IsNullOrEmpty(_sceneName);

    public static implicit operator string(SceneReference reference) => reference._sceneName;

#if UNITY_EDITOR

    public bool SyncFromAsset()
    {
        string newPath = _sceneAsset != null
            ? UnityEditor.AssetDatabase.GetAssetPath(_sceneAsset)
            : string.Empty;

        string newName = string.IsNullOrEmpty(newPath)
            ? string.Empty
            : System.IO.Path.GetFileNameWithoutExtension(newPath);

        if (newPath == _scenePath && newName == _sceneName) return false;

        _scenePath = newPath;
        _sceneName = newName;
        return true;
    }
#endif
}